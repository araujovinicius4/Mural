using System.Windows;

namespace MuralInstitucional;

/// <summary>Independent ordinary top-level window: no Owner, no desktop parent and no Topmost.</summary>
public partial class RecoveryWindow : Window
{
    public event Action? ConfirmRequested;
    public event Action? RestoreRequested;
    public event Action? CloseMuralRequested;
    public RecoveryWindow() => InitializeComponent();
    public void UpdateStatus(bool confirmed, int seconds)
    {
        ConfirmButton.IsEnabled = !confirmed && seconds > 0;
        Explanation.Text = confirmed
            ? "Você confirmou que vê os cartões. Teste Win + D e as outras janelas. Este controle continua disponível na barra de tarefas para restaurar o mural."
            : $"Sem confirmação visual, o mural voltará ao modo convencional em {seconds} segundos. Se estiver invisível, use Desafixar agora.";
    }
    private void Confirm_Click(object sender, RoutedEventArgs e) => ConfirmRequested?.Invoke();
    private void Restore_Click(object sender, RoutedEventArgs e) => RestoreRequested?.Invoke();
    private void CloseMural_Click(object sender, RoutedEventArgs e) => CloseMuralRequested?.Invoke();
}
