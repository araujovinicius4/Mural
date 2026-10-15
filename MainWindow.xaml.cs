using System.Windows;
using System.Windows.Threading;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;
using MuralInstitucional.Controls;
using System.Windows.Controls;
using System.Windows.Media;

namespace MuralInstitucional;

public partial class MainWindow : Window
{
    public BoardViewModel Board { get; }
    private readonly DesktopIntegrationService desktop;
    private readonly DispatcherTimer monitor = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool runProbe = true;
    private RecoveryWindow? recovery;
    private bool closingRecovery;
    private bool loadedActionsStarted;

    public MainWindow()
    {
        InitializeComponent();
        var profileDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MuralInstitucional");
        Board = new BoardViewModel(new AnnouncementRepository(),
            new AnnouncementRepository(System.IO.Path.Combine(profileDirectory, "Publications"), "local-user"),
            new ManagedMediaService(System.IO.Path.Combine(profileDirectory, "Media")));
        DataContext = Board;
        desktop = new DesktopIntegrationService(this);
        desktop.UnpinRequested += () => Report(desktop.Unpin());
        desktop.CloseRequested += Close;
        SourceInitialized += (_, _) => Report(desktop.PositionRight());
        monitor.Tick += (_, _) =>
        {
            if (desktop.CheckHealth() is { } result) Report(result);
            recovery?.UpdateStatus(desktop.IsVisuallyConfirmed, desktop.ConfirmationSecondsRemaining);
        };
        Loaded += async (_, _) =>
        {
            if (loadedActionsStarted) return;
            loadedActionsStarted = true;
            // Native positioning can dispatch Loaded while SourceInitialized is still running.
            await Dispatcher.Yield(DispatcherPriority.Background);
            monitor.Start();
            if (runProbe && Environment.GetCommandLineArgs().Contains("--diagnose"))
            {
                desktop.Diagnose();
                Application.Current.Shutdown();
                return;
            }
            if (runProbe && Environment.GetCommandLineArgs().Contains("--probe"))
            {
                await Task.Delay(500);
                var pin = StartPin(Environment.GetCommandLineArgs().Contains("--probe-fail-after-parent"));
                var recoveryProbe = Environment.GetCommandLineArgs().Contains("--probe-recovery");
                await Task.Delay(recoveryProbe ? 22000 : 1500);
                var verified = desktop.VerifyPinned(out var reason);
                desktop.Record($"PROBE verificação tardia: {verified}; {reason}");
                var automaticallyRestored = pin.Success && !desktop.IsPinned && !desktop.HasSavedState;
                var unpin = desktop.Unpin();
                Report(unpin);
                desktop.Record($"PROBE RESULT pinProvisional={pin.Success}; structural={verified}; unpin={unpin.Success}; automaticallyRestored={automaticallyRestored}; visual=SEM CONFIRMAÇÃO; Win+D=não testado");
                await Task.Delay(500);
                // 3 = structural cycle only, never visual approval. 0 is reserved for the recovery test.
                Application.Current.Shutdown(recoveryProbe ? (automaticallyRestored ? 0 : 2) : (pin.Success && verified && unpin.Success ? 3 : 2));
            }
        };
        Closing += (_, _) =>
        {
            foreach (var card in CardControls()) card.StopMedia();
            Board.Save();
            monitor.Stop();
            CloseRecovery();
            desktop.Record(desktop.Unpin().Message);
            desktop.Dispose();
        };
    }

    private void Report(IntegrationResult result)
    {
        if (!result.Success && desktop.HasSavedState)
        {
            // If native restoration fails, replace only our own window with a clean WPF HWND.
            var replacement = new MainWindow { runProbe = false };
            replacement.desktop.PreventFurtherPinAttempts();
            replacement.Show();
            replacement.StatusText.Text = result.Message + " Uma nova janela convencional foi aberta por segurança.";
            desktop.Record("Fallback: nova janela convencional criada; fechando HWND anterior.");
            Close();
            return;
        }
        StatusText.Text = result.Message;
        DiagnosticPathText.Text = desktop.LoggingError is null
            ? $"Diagnóstico local: {desktop.LogPath}"
            : $"Não foi possível gravar o diagnóstico: {desktop.LoggingError}";
        PinButton.IsEnabled = false;
        UnpinButton.IsEnabled = desktop.HasSavedState;
        if (!desktop.IsPinned) CloseRecovery();
    }
    private IntegrationResult StartPin(bool simulateFailure = false)
    {
        // Recovery is preserved below; this stage blocks all experimental integration entry points.
        if (!DesktopPinningEnabled)
        {
            var disabled = new IntegrationResult(false, "Fixação não validada e desabilitada nesta etapa. Janela convencional preservada.");
            Report(disabled);
            return disabled;
        }
        if (desktop.PinAttempted)
        {
            var refused = new IntegrationResult(false, "Uma tentativa por execução. Não haverá nova integração com o Explorer.");
            Report(refused);
            return refused;
        }
        // Create the independent control before moving the mural into any shell layer.
        recovery = new RecoveryWindow();
        recovery.ConfirmRequested += () => Report(desktop.ConfirmVisible());
        recovery.RestoreRequested += () => Report(desktop.Unpin("Restauração solicitada pelo usuário."));
        recovery.CloseMuralRequested += Close;
        recovery.Closed += (_, _) =>
        {
            if (!closingRecovery)
            {
                recovery = null;
                Report(desktop.Unpin("O controle foi fechado; fixação cancelada."));
            }
        };
        recovery.Show();
        var result = desktop.Pin(simulateFailure);
        Report(result);
        recovery?.UpdateStatus(desktop.IsVisuallyConfirmed, desktop.ConfirmationSecondsRemaining);
        return result;
    }
    private void CloseRecovery()
    {
        closingRecovery = true;
        recovery?.Close();
        recovery = null;
        closingRecovery = false;
    }
    private void Pin_Click(object sender, RoutedEventArgs e) => StartPin();
    private void Unpin_Click(object sender, RoutedEventArgs e) => Report(desktop.Unpin());
    private void Position_Click(object sender, RoutedEventArgs e) => Report(desktop.PositionRight());
    // Confirmation belongs only to the menu action, never to Windows/session shutdown.
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Deseja encerrar o aplicativo?", "Encerrar aplicativo",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes)
            Close();
    }
    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private static bool DesktopPinningEnabled => false;
    private void NewAnnouncement_Click(object sender, RoutedEventArgs e) => new AnnouncementEditorWindow(Board) { Owner = this }.ShowDialog();
    private void Settings_Click(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private IEnumerable<AnnouncementCard> CardControls() => Descendants<AnnouncementCard>(Cards);
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is null) yield break;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private void PauseHiddenVideos()
    {
        foreach (var card in CardControls())
            if (WindowState == WindowState.Minimized) card.PauseMedia();
            else card.PauseIfOutside(BoardScroll);
    }
    private void Board_ScrollChanged(object sender, ScrollChangedEventArgs e) => PauseHiddenVideos();
    private void Board_SizeChanged(object sender, SizeChangedEventArgs e) => PauseHiddenVideos();
    protected override void OnStateChanged(EventArgs e) { base.OnStateChanged(e); PauseHiddenVideos(); }
}
