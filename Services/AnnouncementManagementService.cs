using System.IO;
using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

public sealed record AnnouncementDraft(string Title, string? Description, MediaKind Kind, string Path, string? Url, AnnouncementType? Type = null,
    AnnouncementDistribution? Distribution = null);

public sealed class AnnouncementManagementService(IAnnouncementRepository repository, ManagedMediaService media, bool isExample = false)
{
    public string? CleanupNotice { get; private set; }
    public static void Validate(AnnouncementDraft draft)
    {
        if (draft.Type is null || !Enum.IsDefined(draft.Type.Value)) throw new InvalidDataException("Tipo de anúncio obrigatório e válido.");
        if (draft.Distribution is not null && !AnnouncementDistribution.IsValid(draft.Distribution)) throw new InvalidDataException("Critérios de distribuição inválidos.");
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Trim().Length > 200) throw new InvalidDataException("Título obrigatório, até 200 caracteres.");
        if (draft.Description?.Length > 2000) throw new InvalidDataException("Descrição deve ter até 2.000 caracteres.");
        if (!string.IsNullOrWhiteSpace(draft.Url) && !ExternalLinkService.TryValidate(draft.Url.Trim(), out _))
            throw new InvalidDataException("URL deve ser HTTP/HTTPS válida e sem credenciais.");
        ManagedMediaService.Validate(draft.Path, draft.Kind);
    }
    public Announcement Save(IEnumerable<Announcement> existing, Announcement? original, AnnouncementDraft draft)
    {
        Validate(draft);
        string path = original is not null && original.MediaKind == draft.Kind
            && string.Equals(original.MediaPath, draft.Path, StringComparison.OrdinalIgnoreCase) && media.Owns(draft.Path)
            ? draft.Path : media.Import(draft.Path, draft.Kind);
        var result = new Announcement
        {
            Id = original?.Id ?? Guid.NewGuid(), IsExample = isExample, Title = draft.Title.Trim(), Description = draft.Description?.Trim(),
            ExchangeOriginId = original?.ExchangeOriginId, IsImported = original?.IsImported ?? false,
            Type = draft.Type!.Value, MediaKind = draft.Kind, MediaPath = path, ExternalUrl = string.IsNullOrWhiteSpace(draft.Url) ? null : draft.Url.Trim(),
            InitialLikes = original?.InitialLikes ?? 0, LikedLocally = original?.LikedLocally ?? false,
            CooperationInterest = original?.CooperationInterest, CooperationRequests = original?.CooperationRequests.ToList() ?? [],
            Distribution = draft.Distribution ?? original?.Distribution ?? new(),
            Comments = original?.Comments ?? [], PublishedAt = original?.PublishedAt ?? DateTimeOffset.Now
        };
        var items = existing.Where(x => x.IsExample == isExample && x.Id != result.Id).ToList(); items.Insert(0, result);
        try { repository.Save(items); }
        catch { if (path != original?.MediaPath) Cleanup(path, items.Where(x => x.Id != result.Id)); throw; }
        if (original is not null && original.MediaPath != path) Cleanup(original.MediaPath, existing.Where(x => x.Id != original.Id).Append(result));
        return result;
    }
    public void Delete(IEnumerable<Announcement> existing, Announcement item)
    {
        var remaining = existing.Where(x => x.IsExample == isExample && x.Id != item.Id).ToList();
        repository.Save(remaining);
        Cleanup(item.MediaPath, existing.Where(x => x.Id != item.Id));
    }
    private void Cleanup(string? path, IEnumerable<Announcement> remaining)
    {
        CleanupNotice = null;
        if (remaining.Any(x => string.Equals(x.MediaPath, path, StringComparison.OrdinalIgnoreCase))) return;
        try { media.DeleteOwned(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { CleanupNotice = "Dados salvos; mídia antiga não pôde ser removida: " + ex.Message; }
    }
}
