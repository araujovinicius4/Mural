namespace MuralInstitucional.Models;

// One optional local manifestation per announcement. No user or machine identity is collected.
public sealed record LocalCooperationInterest
{
    public const string LocalScope = "local-demonstrative-profile";
    public Guid AnnouncementId { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }
    public string Scope { get; init; } = LocalScope;
}
