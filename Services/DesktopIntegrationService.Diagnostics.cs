using System.Runtime.InteropServices;

namespace MuralInstitucional.Services;

public sealed partial class DesktopIntegrationService
{
    public void Diagnose()
    {
        window.Dispatcher.VerifyAccess();
        Record($"DIAGNÓSTICO SOMENTE LEITURA OS={Environment.OSVersion}; pid={Environment.ProcessId}; log={LogPath}");
        LogSnapshot("JANELA CONVENCIONAL");
        try
        {
            host = FindDesktopHost();
            GetWindowThreadProcessId(host, out hostProcess);
            LogSnapshot("HOST CANDIDATO, SEM SETPARENT");
            var covering = FindCoveringShellLayer(RightArea());
            Record(covering is null
                ? "Nenhuma camada adicional de cobertura detectada. Isso NÃO comprova transparência de DefView nem visibilidade dos pixels WPF."
                : $"Possível camada de cobertura acima de WorkerW: {covering}. Não comprova opacidade por pixel, mas torna a integração não confiável.");
        }
        catch (Exception ex) { Record($"Diagnóstico do host: {ex.Message}"); }
        finally { host = 0; }
    }

    public void LogSnapshot(string stage)
    {
        Record($"--- {stage}; dispatcherAlive={window.Dispatcher.CheckAccess()}; visuallyConfirmed={IsVisuallyConfirmed} ---");
        DescribeWindow("Mural", Handle);
        if (host != 0)
        {
            DescribeWindow("WorkerW selecionado", host);
            DescribeWindow("Pai do WorkerW", GetParent(host));
            DescribeWindow("Camada de ícones", iconView);
            Record($"Host WM_NULL responde={Responds(host)} (timeout de 250ms); Mural IsHungAppWindow={IsHungAppWindow(Handle)}");
        }
        var progman = FindWindow("Progman", null);
        if (progman != 0)
        {
            DescribeWindow("Progman", progman);
            // Direct siblings in Z order reveal wallpaper layers, not just WorkerW/DefView.
            var index = 0;
            for (var child = GetWindow(progman, 5); child != 0 && index < 40; child = GetWindow(child, 2))
                DescribeWindow($"Progman filho z={index++}", child);
        }
        if (GetWindowRect(Handle, out var bounds))
        {
            foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
            {
                var point = new PointNative { X = bounds.Left + (int)((bounds.Right - bounds.Left) * fraction), Y = bounds.Top + (bounds.Bottom - bounds.Top) / 2 };
                var hit = WindowFromPoint(point);
                Record($"HitTest screen=({point.X},{point.Y}); HWND=0x{hit:X}; class={ClassName(hit)}; muralOrChild={hit == Handle || IsChild(Handle, hit)} (não é prova de visibilidade/oclusão)");
            }
        }
    }

    private void DescribeWindow(string label, nint hwnd)
    {
        if (hwnd == 0 || !IsWindow(hwnd)) { Record($"{label}: HWND=0x{hwnd:X} indisponível"); return; }
        GetWindowThreadProcessId(hwnd, out var pid);
        var hasRect = GetWindowRect(hwnd, out var rect);
        var hasClient = GetClientRect(hwnd, out var client);
        var clientOrigin = new PointNative();
        var originKnown = ClientToScreen(hwnd, ref clientOrigin);
        var style = GetWindowLongPtr(hwnd, StyleIndex).ToInt64();
        var exStyle = GetWindowLongPtr(hwnd, ExStyleIndex).ToInt64();
        var cloakedResult = DwmGetWindowAttribute(hwnd, 14, out var cloaked, sizeof(uint));
        var layered = GetLayeredWindowAttributes(hwnd, out var key, out var alpha, out var flags);
        Record($"{label}: HWND=0x{hwnd:X}; class={ClassName(hwnd)}; pid={pid}; parent=0x{GetParent(hwnd):X}; owner=0x{GetWindow(hwnd, 4):X}; style=0x{style:X}; exStyle=0x{exStyle:X}; visible={IsWindowVisible(hwnd)}; enabled={IsWindowEnabled(hwnd)}; minimized={IsIconic(hwnd)}; hung={IsHungAppWindow(hwnd)}; screen={(hasRect ? FormatRect(rect) : "erro")}; client={(hasClient ? FormatRect(client) : "erro")}; clientOriginScreen={(originKnown ? $"({clientOrigin.X},{clientOrigin.Y})" : "erro")}; DPI={GetDpiForWindow(hwnd)}; dpiContext=0x{GetWindowDpiAwarenessContext(hwnd):X}; DWM_cloaked_HR=0x{cloakedResult:X}; cloaked={cloaked}; layeredAttributesKnown={layered}; alpha={alpha}; colorKey=0x{key:X}; layeredFlags=0x{flags:X}");
    }

    private string? FindCoveringShellLayer(RectNative target)
    {
        if (hostParent == 0) return null;
        var count = 0;
        for (var sibling = GetWindow(hostParent, 5); sibling != 0 && sibling != host && count++ < 100; sibling = GetWindow(sibling, 2))
        {
            if (!IsWindowVisible(sibling) || IsCloaked(sibling)) continue;
            if (!GetWindowRect(sibling, out var rect) || !Intersects(rect, target)) continue;
            if (sibling == iconView)
            {
                // Icon surfaces are expected above wallpaper, but this observed 26100 configuration
                // covers our full area with a layered surface at global alpha 255 and no color key.
                // Do not assume per-pixel transparency; reject conservatively, without changing Explorer.
                if (!Covers(rect, target) || !GetLayeredWindowAttributes(sibling, out _, out var iconAlpha, out var iconFlags) ||
                    iconAlpha != 255 || (iconFlags & 2) == 0 || (iconFlags & 1) != 0) continue;
            }
            // A known global alpha of zero is transparent. Per-pixel alpha cannot be inferred here.
            if (GetLayeredWindowAttributes(sibling, out _, out var alpha, out var flags) && (flags & 2) != 0 && alpha == 0) continue;
            DescribeWindow("Possível cobertura acima do WorkerW", sibling);
            return $"{ClassName(sibling)} HWND=0x{sibling:X}, screen={FormatRect(rect)}";
        }
        return null;
    }

    private static bool Intersects(RectNative a, RectNative b) => a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
    private static bool Covers(RectNative a, RectNative b) => a.Left <= b.Left && a.Top <= b.Top && a.Right >= b.Right && a.Bottom >= b.Bottom;
    private static string FormatRect(RectNative rect) => $"({rect.Left},{rect.Top})-({rect.Right},{rect.Bottom}) [{rect.Right - rect.Left}x{rect.Bottom - rect.Top}]";
    private static bool IsCloaked(nint hwnd) => DwmGetWindowAttribute(hwnd, 14, out var value, sizeof(uint)) == 0 && value != 0;
    private static bool Responds(nint hwnd) => SendMessageTimeout(hwnd, 0, 0, 0, 0x0001 | 0x0002 | 0x0020, 250, out _) != 0;

    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsHungAppWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint hwnd, out RectNative rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint hwnd, ref PointNative point);
    [DllImport("user32.dll")] private static extern bool GetLayeredWindowAttributes(nint hwnd, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(PointNative point);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, uint attribute, out uint value, int size);
}
