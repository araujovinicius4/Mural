using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MuralInstitucional.Models;

public enum MediaKind { Image, Video }

public sealed class Announcement : INotifyPropertyChanged
{
    public Guid Id { get; set; }
    public Guid? ExchangeOriginId { get; set; }
    public bool IsImported { get; set; }
    [JsonIgnore] public bool IsExample { get; set; } = true;
    [JsonIgnore] public string OriginLabel => IsExample ? "EXEMPLO DEMONSTRATIVO" : IsImported ? "RECEBIDO VIA ZIP • ORIGEM NÃO AUTENTICADA" : "CADASTRADO NESTE PERFIL";
    // Legacy JSON without Type defaults to Comunicado and is migrated on save.
    public AnnouncementType Type { get; set; } = AnnouncementType.Comunicado;
    [JsonIgnore] public string TypeLabel => AnnouncementTypes.Name(Type);
    [JsonIgnore] public string TypeColor => AnnouncementTypes.Color(Type);
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public MediaKind MediaKind { get; set; }
    private string? mediaPath;
    public string? MediaPath { get => mediaPath; set { mediaPath = value; Changed(); } }
    public string? ExternalUrl { get; set; }
    public int InitialLikes { get; set; }
    private bool liked;
    public bool LikedLocally
    {
        get => liked;
        set { liked = value; Changed(); Changed(nameof(LikeCount)); Changed(nameof(LikeLabel)); }
    }
    private LocalCooperationInterest? cooperationInterest;
    public LocalCooperationInterest? CooperationInterest
    {
        get => cooperationInterest;
        set { cooperationInterest = value; Changed(); CooperationChanged(); }
    }
    public List<CooperationRequest> CooperationRequests { get; set; } = [];
    public AnnouncementDistribution Distribution { get; set; } = new();
    [JsonIgnore] public string CooperationStatus => !CanCooperate ? "" :
        CooperationRequests.LastOrDefault(x => x.Interest == CooperationInterest)?.StateLabel
        ?? CooperationRequests.LastOrDefault()?.StateLabel ?? "Sem solicitação";
    [JsonIgnore] public string CooperationButtonLabel => CooperationRequests.LastOrDefault(x => x.Interest == CooperationInterest)?.State
        is null or CooperationState.InterestRegistered ? "✓ Interesse registrado" : "Acompanhar cooperação";
    [JsonIgnore] public string CooperationButtonToolTip => CooperationRequests.LastOrDefault(x => x.Interest == CooperationInterest)?.State
        is null or CooperationState.InterestRegistered ? "Clique para cancelar seu interesse" : "Abrir detalhes do fluxo local";
    public void CooperationChanged() { Changed(nameof(CooperationStatus)); Changed(nameof(HasCooperationInterest));
        Changed(nameof(CooperationButtonLabel)); Changed(nameof(CooperationButtonToolTip)); }
    [JsonIgnore] public bool HasCooperationInterest => CooperationInterest is not null;
    [JsonIgnore] public bool CanCooperate => Type is AnnouncementType.Demanda or AnnouncementType.Oferta;
    public DateTimeOffset PublishedAt { get; set; }
    public ObservableCollection<DemoComment> Comments { get; set; } = [];
    [JsonIgnore] public int LikeCount => InitialLikes + (LikedLocally ? 1 : 0);
    [JsonIgnore] public string LikeLabel => $"{(LikedLocally ? "♥" : "♡")} {LikeCount}";
    [JsonIgnore] public string CommentLabel => $"Comentários · {Comments.Count}";
    [JsonIgnore] public bool HasExternalUrl => !string.IsNullOrWhiteSpace(ExternalUrl);
    public void CommentsChanged() => Changed(nameof(CommentLabel));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class DemoComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnnouncementId { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class DemoDocument
{
    public int Version { get; set; } = 1;
    public string DataKind { get; set; } = "local-demo";
    public List<Announcement> Announcements { get; set; } = [];
}
