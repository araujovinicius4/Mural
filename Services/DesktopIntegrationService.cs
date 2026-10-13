using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace MuralInstitucional.Services;

public sealed record IntegrationResult(bool Success, string Message);

/// <summary>All HWND operations run on the UI thread. WorkerW is an undocumented shell detail.</summary>
public sealed partial class DesktopIntegrationService(Window window) : IDisposable
{
    private const int StyleIndex = -16, ExStyleIndex = -20;
    private const long Child = 0x40000000, Popup = 0x80000000, Caption = 0x00C00000,
        ThickFrame = 0x00040000, MinimizeBox = 0x00020000, MaximizeBox = 0x00010000,
        AppWindow = 0x00040000, ToolWindow = 0x00000080;
    private const uint PositionFlags = 0x0010 | 0x0020 | 0x0040 | 0x0004; // NOACTIVATE | FRAMECHANGED | SHOWWINDOW | NOZORDER
    private nint host;
    private uint hostProcess;
    private nint hostParent, iconView, iconHost;
    private SavedState? saved;
    private HwndSource? source;
    public event Action? UnpinRequested;
    public event Action? CloseRequested;
    public bool IsPinned { get; private set; }
    public bool IsVisuallyConfirmed { get; private set; }
    public bool PinAttempted { get; private set; }
    private long confirmationDeadline;
    public int ConfirmationSecondsRemaining => Math.Max(0, (int)Math.Ceiling((double)(confirmationDeadline - System.Diagnostics.Stopwatch.GetTimestamp()) / System.Diagnostics.Stopwatch.Frequency));
    public bool HasSavedState => saved is not null;
    public string LogPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MuralInstitucional", "Diagnostics", "desktop-integration.log");
    public string? LoggingError { get; private set; }
    private nint Handle => new WindowInteropHelper(window).Handle;
    public void PreventFurtherPinAttempts() => PinAttempted = true;

    public void Record(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} pid={Environment.ProcessId} {message}{Environment.NewLine}");
            LoggingError = null;
        }
        catch (IOException ex) { LoggingError = ex.Message; }
        catch (UnauthorizedAccessException ex) { LoggingError = ex.Message; }
    }

    public IntegrationResult PositionRight()
    {
        try
        {
            window.Dispatcher.VerifyAccess();
            if (!IsPinned) window.WindowState = WindowState.Normal;
            Move(RightArea(), IsPinned ? host : 0);
            return Result(true, "Janela posicionada nos 60% à direita da área útil do monitor.");
        }
        catch (Exception ex) { return Result(false, $"Não foi possível posicionar: {ex.Message}"); }
    }

    public IntegrationResult Pin(bool simulateFailureAfterParent = false)
    {
        window.Dispatcher.VerifyAccess();
        if (IsPinned) return Result(false, "Há uma tentativa provisória em andamento. Confirme a imagem ou restaure a janela.");
        if (PinAttempted) return Result(false, "A tentativa desta execução já foi concluída. O modo convencional foi preservado; não haverá nova integração com o Explorer.");
        if (saved is not null) return Result(false, "Desafixe para concluir a recuperação antes de tentar novamente.");
        PinAttempted = true;
        try
        {
            LogSnapshot("ANTES DA FIXAÇÃO");
            EnsureRecoveryHotkeys();
            host = FindDesktopHost();
            GetWindowThreadProcessId(host, out hostProcess);
            LogSnapshot("HOST SELECIONADO");
            var target = RightArea(); // Capture screen coordinates before changing parent/monitor semantics.
            if (FindCoveringShellLayer(target) is { } covering)
                throw new InvalidOperationException($"WorkerW está atrás de uma possível camada de cobertura ({covering}). Fixação visual não confiável; janela convencional preservada.");
            if (!IsWindowEnabled(host))
                throw new InvalidOperationException("WorkerW está desabilitado para interação. Fixação não confiável; janela convencional preservada.");
            // Cross-process SetParent can reset DPI awareness. Refuse incompatible contexts.
            if (!AreDpiAwarenessContextsEqual(GetWindowDpiAwarenessContext(Handle), GetWindowDpiAwarenessContext(host)))
                throw new InvalidOperationException("Contextos DPI diferentes entre WPF e Explorer. Fixação recusada para preservar o processo.");
            window.WindowState = WindowState.Normal;
            Check(GetWindowRect(Handle, out var bounds), "GetWindowRect");
            saved = new(GetStyle(StyleIndex), GetStyle(ExStyleIndex), bounds);
            Record($"Snapshot salvo: style=0x{saved.Style:X}; exStyle=0x{saved.ExStyle:X}; screen={FormatRect(bounds)}; target={FormatRect(target)}");
            SetStyle(StyleIndex, (saved.Style & ~(Popup | Caption | ThickFrame | MinimizeBox | MaximizeBox)) | Child);
            SetStyle(ExStyleIndex, (saved.ExStyle & ~AppWindow) | ToolWindow);
            LogSnapshot("ESTILOS CHILD ANTES DE SETPARENT");
            ChangeParent(host);
            LogSnapshot("APÓS SETPARENT, ANTES DE SETWINDOWPOS");
            Move(target, host);
            LogSnapshot("APÓS SETWINDOWPOS");
            if (simulateFailureAfterParent)
                throw new InvalidOperationException("Falha simulada após SetParent para testar rollback.");
            if (!VerifyPinned(out var reason)) throw new InvalidOperationException(reason);
            IsPinned = true;
            IsVisuallyConfirmed = false;
            confirmationDeadline = System.Diagnostics.Stopwatch.GetTimestamp() + 20 * System.Diagnostics.Stopwatch.Frequency;
            return Result(true, "Fixação provisória, sem validação visual. Confirme que vê os cartões no controle separado em até 20 segundos; caso contrário, a janela será restaurada automaticamente.");
        }
        catch (Exception ex)
        {
            var recovery = Unpin();
            return Result(false, $"Fixação falhou: {ex.Message} {recovery.Message}");
        }
    }

    public IntegrationResult Unpin(string? reason = null)
    {
        window.Dispatcher.VerifyAccess();
        IsPinned = false;
        IsVisuallyConfirmed = false;
        if (saved is null) { ReleaseRecoveryHotkeys(); return Result(true, "Modo convencional."); }
        try
        {
            LogSnapshot("ANTES DA RECUPERAÇÃO");
            ChangeParent(0);
            SetStyle(StyleIndex, saved.Style);
            SetStyle(ExStyleIndex, saved.ExStyle);
            Move(saved.Bounds, 0);
            if (GetParent(Handle) != 0 || GetStyle(StyleIndex) != saved.Style || GetStyle(ExStyleIndex) != saved.ExStyle)
                throw new InvalidOperationException("Parentesco ou estilos não foram restaurados.");
            saved = null;
            LogSnapshot("RECUPERAÇÃO CONVENCIONAL VERIFICADA");
            host = 0;
            ReleaseRecoveryHotkeys();
            return Result(true, $"{reason} Modo convencional restaurado; mover, redimensionar e minimizar disponíveis.".Trim());
        }
        catch (Exception ex)
        {
            // Keep snapshot for retry. Closing remains available in every state.
            return Result(false, $"{reason} Recuperação incompleta: {ex.Message} Tente Desafixar novamente ou use Fechar.".Trim());
        }
    }

    public bool VerifyPinned(out string reason)
    {
        reason = "";
        if (!IsWindow(Handle) || !IsWindow(host)) reason = "HWND do mural ou do host indisponível.";
        else if (!IsWindow(iconView) || GetParent(iconView) != iconHost || GetParent(host) != hostParent ||
            !IsBelow(host, hostParent == iconHost ? iconView : iconHost))
            reason = "A hierarquia do desktop ou a ordem atrás dos ícones mudou.";
        else if (GetParent(Handle) != host) reason = "O mural não é filho do host selecionado.";
        else if ((GetStyle(StyleIndex) & Child) == 0 || (GetStyle(ExStyleIndex) & 0x8) != 0)
            reason = "Estilos incompatíveis (WS_CHILD/Topmost).";
        else if (!IsWindowVisible(Handle) || !IsWindowVisible(host)) reason = "Mural ou host não visível.";
        else if (!IsWindowEnabled(host)) reason = "Host desabilitado para interação.";
        else if (GetWindowRect(Handle, out var current) && FindCoveringShellLayer(current) is { } cover)
            reason = $"Possível camada de cobertura apareceu: {cover}.";
        else if (IsIconic(Handle) || IsHungAppWindow(host)) reason = "Janela minimizada ou host sem resposta.";
        else if (IsCloaked(Handle) || IsCloaked(host)) reason = "Mural ou host ocultado pelo compositor (DWM cloaked).";
        else
        {
            GetWindowThreadProcessId(host, out var pid);
            if (pid != hostProcess || hostProcess == 0) reason = "Processo do host mudou.";
            else if (!GetWindowRect(Handle, out var rect) || !GetWindowRect(host, out var parent) ||
                rect.Right <= rect.Left || rect.Bottom <= rect.Top || rect.Left < parent.Left ||
                rect.Top < parent.Top || rect.Right > parent.Right || rect.Bottom > parent.Bottom)
                reason = "Geometria fora da área do host.";
        }
        return reason.Length == 0;
    }

    public IntegrationResult ConfirmVisible()
    {
        if (!IsPinned || ConfirmationSecondsRemaining == 0 || !VerifyPinned(out _))
            return Unpin("Confirmação fora do prazo ou estrutura indisponível.");
        IsVisuallyConfirmed = true;
        return Result(true, "Usuário confirmou que vê os cartões. Win + D, acesso aos ícones e cobertura por outros aplicativos ainda precisam de teste manual.");
    }

    public IntegrationResult? CheckHealth()
    {
        if (!IsPinned) return null;
        if (!VerifyPinned(out var reason))
        {
            LogSnapshot("INTEGRIDADE PERDIDA");
            return Unpin($"Fixação não confiável: {reason}");
        }
        if (!IsVisuallyConfirmed && ConfirmationSecondsRemaining == 0)
            return Unpin("A visibilidade dos cartões não foi confirmada. A integração visual não foi validada.");
        return null;
    }

    private nint FindDesktopHost()
    {
        var progman = FindWindow("Progman", null);
        var shell = GetShellWindow();
        Record($"Descoberta: Progman=0x{progman:X}; GetShellWindow=0x{shell:X}; OS={Environment.OSVersion}; sessão={System.Diagnostics.Process.GetCurrentProcess().SessionId}");
        if (progman == 0 || progman != shell)
            throw new InvalidOperationException("Progman não corresponde ao shell ativo.");
        GetWindowThreadProcessId(progman, out var shellPid);
        Record($"Ambiente: {Environment.OSVersion}; processo={Environment.ProcessId}; sessão={System.Diagnostics.Process.GetCurrentProcess().SessionId}; Progman=0x{progman:X}; shellPID={shellPid}");
        // Read-only discovery: never ask Explorer to create/show/reorder any windows.

        nint candidate = 0;
        EnumWindows((top, _) =>
        {
            var icons = FindWindowEx(top, 0, "SHELLDLL_DefView", null);
            if (icons != 0 || ClassName(top) is "Progman" or "WorkerW")
                Record($"Top-level {ClassName(top)} 0x{top:X}; DefView=0x{icons:X}; visible={IsWindowVisible(top)}");
            if (icons == 0) return true;
            GetWindowThreadProcessId(top, out var ownerPid);
            if (ownerPid != shellPid) return true;
            // Windows 11 can host wallpaper WorkerW as a child under Progman.
            var childWorker = FindWindowEx(top, 0, "WorkerW", null);
            Record($"Child WorkerW=0x{childWorker:X}; visible={IsWindowVisible(childWorker)}; abaixoDosIcones={IsBelow(childWorker, icons)}");
            if (Eligible(childWorker, shellPid) && IsBelow(childWorker, icons)) candidate = childWorker;
            // Classic topology: separate WorkerW behind the icon host.
            if (candidate == 0)
            {
                var siblingWorker = FindWindowEx(0, top, "WorkerW", null);
                if (Eligible(siblingWorker, shellPid)) candidate = siblingWorker;
            }
            if (candidate != 0)
            {
                iconView = icons;
                iconHost = top;
                hostParent = GetParent(candidate);
            }
            return candidate == 0;
        }, 0);
        if (candidate == 0)
            throw new InvalidOperationException("Nenhum WorkerW visível e seguro atrás dos ícones foi encontrado. Não será usado Progman diretamente nem Topmost.");
        Record($"Host candidato: WorkerW 0x{candidate:X}; parent=0x{GetParent(candidate):X}");
        return candidate;
    }

    private void EnsureRecoveryHotkeys()
    {
        source = HwndSource.FromHwnd(Handle) ?? throw new InvalidOperationException("HwndSource indisponível.");
        // Standard user-session hotkeys provide an exit even when the shell intercepts mouse input.
        const uint modifiers = 0x4000 | 1 | 2 | 4; // NOREPEAT | ALT | CONTROL | SHIFT
        if (!RegisterHotKey(Handle, 0x4D12, modifiers, 0x7B))
        {
            source = null;
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Atalho de recuperação Ctrl+Alt+Shift+F12 indisponível.");
        }
        if (!RegisterHotKey(Handle, 0x4D11, modifiers, 0x7A))
        {
            var error = Marshal.GetLastPInvokeError();
            UnregisterHotKey(Handle, 0x4D12);
            source = null;
            throw new Win32Exception(error, "Atalho de fechamento Ctrl+Alt+Shift+F11 indisponível.");
        }
        source.AddHook(HotkeyHook);
        Record("Atalhos de recuperação registrados: Ctrl+Alt+Shift+F12 (desafixar), Ctrl+Alt+Shift+F11 (fechar).");
    }

    private nint HotkeyHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam is 0x4D12 or 0x4D11)
        {
            handled = true;
            // Defer until the native message finishes; recovery may replace/close this HWND.
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (wParam == 0x4D12) UnpinRequested?.Invoke();
                else CloseRequested?.Invoke();
            }));
        }
        return 0;
    }

    private void ReleaseRecoveryHotkeys()
    {
        if (source is null) return;
        UnregisterHotKey(Handle, 0x4D12);
        UnregisterHotKey(Handle, 0x4D11);
        source.RemoveHook(HotkeyHook);
        source = null;
    }
    public void Dispose() => ReleaseRecoveryHotkeys();

    private static bool IsBelow(nint candidate, nint icons)
    {
        for (var next = GetWindow(icons, 2); next != 0; next = GetWindow(next, 2))
            if (next == candidate) return true;
        return false;
    }
    private static bool Eligible(nint candidate, uint shellPid)
    {
        if (candidate == 0 || !IsWindowVisible(candidate) || FindWindowEx(candidate, 0, "SHELLDLL_DefView", null) != 0) return false;
        GetWindowThreadProcessId(candidate, out var pid);
        return pid == shellPid;
    }

    private RectNative RightArea()
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        Check(GetMonitorInfo(MonitorFromWindow(Handle, 2), ref info), "GetMonitorInfo");
        var area = info.Work;
        area.Left = area.Right - (int)Math.Round((area.Right - area.Left) * 0.60);
        return area;
    }
    private void Move(RectNative target, nint parent)
    {
        var point = new PointNative { X = target.Left, Y = target.Top };
        if (parent != 0) Check(ScreenToClient(parent, ref point), "ScreenToClient");
        Marshal.SetLastPInvokeError(0);
        var moved = SetWindowPos(Handle, 0, point.X, point.Y, target.Right - target.Left, target.Bottom - target.Top, PositionFlags);
        var error = Marshal.GetLastPInvokeError();
        Record($"SetWindowPos: parent=0x{parent:X}; requestedScreen={FormatRect(target)}; parentClient=({point.X},{point.Y}); flags=0x{PositionFlags:X}; result={moved}; error={error}");
        if (!moved) throw new Win32Exception(error, "SetWindowPos");
        Check(GetWindowRect(Handle, out var actual), "GetWindowRect");
        Record($"SetWindowPos: actualScreen={FormatRect(actual)}");
        if (Math.Abs(actual.Left - target.Left) > 2 || Math.Abs(actual.Top - target.Top) > 2 ||
            Math.Abs(actual.Right - target.Right) > 2 || Math.Abs(actual.Bottom - target.Bottom) > 2)
            throw new InvalidOperationException("O Windows não aplicou a geometria solicitada.");
    }
    private long GetStyle(int index) => GetWindowLongPtr(Handle, index).ToInt64();
    private void SetStyle(int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(Handle, index, new nint(value));
        var error = Marshal.GetLastPInvokeError();
        Record($"SetWindowLongPtr: index={index}; requested=0x{value:X}; previous=0x{previous:X}; actual=0x{GetStyle(index):X}; error={error}");
        if (previous == 0 && error != 0) throw new Win32Exception(error);
    }
    private void ChangeParent(nint parent)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetParent(Handle, parent);
        var error = Marshal.GetLastPInvokeError();
        Record($"SetParent: requested=0x{parent:X}; previous=0x{previous:X}; actual=0x{GetParent(Handle):X}; error={error}");
        if (previous == 0 && error != 0) throw new Win32Exception(error);
        // While WS_CHILD is still set, SetParent(NULL) makes GetParent return the desktop HWND.
        // Unpin clears WS_CHILD next and then verifies GetParent == NULL for the top-level window.
        var actual = GetParent(Handle);
        if (actual != parent && !(parent == 0 && actual == GetDesktopWindow()))
            throw new InvalidOperationException("SetParent não confirmou o parentesco solicitado.");
    }
    private IntegrationResult Result(bool success, string message) { Record($"success={success}; {message}"); return new(success, message); }
    private static void Check(bool success, string api) { if (!success) throw new Win32Exception(Marshal.GetLastPInvokeError(), api); }
    private static string ClassName(nint hwnd) { var text = new StringBuilder(256); GetClassName(hwnd, text, text.Capacity); return text.ToString(); }
    private sealed record SavedState(long Style, long ExStyle, RectNative Bounds);
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public RectNative Monitor, Work; public uint Flags; }
    private delegate bool EnumCallback(nint hwnd, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string className, string? title);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern nint GetDesktopWindow();
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll")] private static extern nint GetParent(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetParent(nint hwnd, nint parent);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)] private static extern nint GetLong64(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetLong32(nint hwnd, int index);
    private static nint GetWindowLongPtr(nint hwnd, int index) => IntPtr.Size == 8 ? GetLong64(hwnd, index) : new nint(GetLong32(hwnd, index));
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] private static extern nint SetLong64(nint hwnd, int index, nint value);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetLong32(nint hwnd, int index, int value);
    private static nint SetWindowLongPtr(nint hwnd, int index, nint value) => IntPtr.Size == 8 ? SetLong64(hwnd, index, value) : new nint(SetLong32(hwnd, index, unchecked((int)value.ToInt64())));
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(nint hwnd, out RectNative rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool ScreenToClient(nint hwnd, ref PointNative point);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)] private static extern nint SendMessageTimeout(nint hwnd, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll")] private static extern nint GetWindowDpiAwarenessContext(nint hwnd);
    [DllImport("user32.dll")] private static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
