using System.Text.Json.Serialization;

namespace MuralInstitucional.Models;

[JsonConverter(typeof(JsonStringEnumConverter<CooperationState>))]
public enum CooperationState { InterestRegistered, AwaitingAcceptance, Accepted, Refused, AwaitingConsent, InProgress, Completed, Cancelled }
public sealed record CooperationEvent(CooperationState State, DateTimeOffset At, string? Reason = null, string? ConsentRole = null);
public sealed record CooperationRequest
{
    public Guid Id { get; init; }
    public LocalCooperationInterest Interest { get; init; } = new();
    public CooperationState State { get; init; }
    public bool RequesterConsent { get; init; }
    public bool ResponsibleConsent { get; init; }
    public List<CooperationEvent> History { get; init; } = [];
    [JsonIgnore] public bool IdentificationSimulated => RequesterConsent && ResponsibleConsent;
    [JsonIgnore] public string StateLabel => Label(State);
    public static string Label(CooperationState state) => state switch {
        CooperationState.InterestRegistered => "Interesse registrado",
        CooperationState.AwaitingAcceptance => "Aguardando aceite",
        CooperationState.Accepted => "Solicitação aceita",
        CooperationState.Refused => "Solicitação recusada",
        CooperationState.AwaitingConsent => "Aguardando consentimento para identificação",
        CooperationState.InProgress => "Cooperação em andamento",
        CooperationState.Completed => "Cooperação concluída",
        CooperationState.Cancelled => "Cooperação cancelada", _ => "Estado inválido" };
}
