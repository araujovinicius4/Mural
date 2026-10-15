using System.Windows;
using System.Windows.Controls;
using MuralInstitucional.Models;
using MuralInstitucional.Services;
using MuralInstitucional.ViewModels;

namespace MuralInstitucional;

public partial class CooperationDetailsWindow : Window
{
    private readonly Announcement announcement;
    private readonly BoardViewModel board;
    private Guid? selectedId;
    public CooperationDetailsWindow(Announcement announcement, BoardViewModel board)
    {
        InitializeComponent(); this.announcement = announcement; this.board = board;
        DataContext = announcement;
        Refresh();
    }
    private void Request_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (announcement is null) return;
        selectedId = (RequestInput.SelectedItem as CooperationRequest)?.Id;
        Render();
    }
    private CooperationRequest? Selected => announcement.CooperationRequests.FirstOrDefault(x => x.Id == selectedId);
    private void Refresh()
    {
        selectedId ??= announcement.CooperationRequests.LastOrDefault(x => x.Interest == announcement.CooperationInterest)?.Id
            ?? announcement.CooperationRequests.LastOrDefault()?.Id;
        var keepId = selectedId;
        RequestInput.ItemsSource = announcement.CooperationRequests.ToArray();
        selectedId = keepId;
        RequestInput.SelectedItem = announcement.CooperationRequests.FirstOrDefault(x => x.Id == keepId);
        Render();
    }
    private void Render()
    {
        FlowPanel.Visibility = announcement.CanCooperate ? Visibility.Visible : Visibility.Collapsed;
        ActionsPanel.Children.Clear();
        var request = Selected;
        StateText.Text = request?.StateLabel ?? "Sem solicitação. Registre interesse no cartão para iniciar.";
        RequesterCheck.IsChecked = request?.RequesterConsent == true;
        ResponsibleCheck.IsChecked = request?.ResponsibleConsent == true;
        RequesterCheck.IsEnabled = RequesterConsentButton.IsEnabled = request?.State == CooperationState.AwaitingConsent && !request.RequesterConsent;
        ResponsibleCheck.IsEnabled = ResponsibleConsentButton.IsEnabled = request?.State == CooperationState.AwaitingConsent && !request.ResponsibleConsent;
        IdentityText.Text = request?.IdentificationSimulated == true
            ? "Identificação fictícia consentida por ambos: Participante A e Responsável B. Nenhuma identidade real foi coletada."
            : "Identificação não simulada. O aceite não revela identidade; são necessários os dois consentimentos explícitos.";
        HistoryText.Text = request is null ? "" : string.Join(Environment.NewLine, request.History.Select(h =>
            $"{h.At.ToLocalTime():dd/MM/yyyy HH:mm:ss} — {CooperationRequest.Label(h.State)}{(h.Reason is null ? "" : ": " + h.Reason)}"));
        if (request is null || !announcement.CanCooperate) return;
        if (request.State is CooperationState.Completed or CooperationState.Refused or CooperationState.Cancelled)
        {
            var restart = new Button { Content = "Registrar nova solicitação local", Padding = new Thickness(10, 7, 10, 7),
                IsEnabled = announcement.CooperationInterest is null || announcement.CooperationInterest == request.Interest };
            restart.Click += (_, _) => {
                if (announcement.HasCooperationInterest) board.CancelCooperationInterest(announcement);
                if (board.RegisterCooperationInterest(announcement)) { selectedId = null; Refresh(); }
                NoticeText.Text = board.Notice;
            };
            ActionsPanel.Children.Add(restart);
        }
        foreach (var next in Enum.GetValues<CooperationState>().Where(x => CooperationInterestService.CanTransition(request.State, x)))
        {
            var label = next switch {
                CooperationState.AwaitingAcceptance => "Solicitante: encaminhar para análise",
                CooperationState.Accepted => "Responsável: aceitar solicitação",
                CooperationState.Refused => "Responsável: recusar solicitação",
                CooperationState.AwaitingConsent => "Abrir etapa de consentimentos",
                CooperationState.InProgress => "Iniciar cooperação",
                CooperationState.Completed => "Concluir cooperação", _ => "Cancelar cooperação" };
            var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 7, 10, 7),
                IsEnabled = next != CooperationState.InProgress || request.IdentificationSimulated };
            button.Click += (_, _) => {
                if (!board.TransitionCooperation(announcement, request.Id, next, ReasonInput.Text))
                    NoticeText.Text = "Transição indisponível. Confira o estado e os consentimentos.";
                else { ReasonInput.Clear(); NoticeText.Text = board.Notice; Refresh(); }
            };
            ActionsPanel.Children.Add(button);
        }
    }
    private void RecordConsent(bool requester)
    {
        if ((requester ? RequesterCheck.IsChecked : ResponsibleCheck.IsChecked) != true || Selected is not { } request) {
            NoticeText.Text = "Marque o consentimento explícito do papel simulado antes de registrar."; return;
        }
        NoticeText.Text = board.ConsentCooperation(announcement, request.Id, requester) ? board.Notice : "Consentimento indisponível neste estado.";
        Refresh();
    }
    private void RequesterConsent_Click(object sender, RoutedEventArgs e) => RecordConsent(true);
    private void ResponsibleConsent_Click(object sender, RoutedEventArgs e) => RecordConsent(false);
}
