using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

// Local lifecycle only; delivery, consent and authenticated identity belong to future services.
public static class CooperationInterestService
{
    public static bool Register(Announcement announcement)
    {
        if (!announcement.CanCooperate || announcement.Id == Guid.Empty || announcement.HasCooperationInterest) return false;
        announcement.CooperationInterest = new LocalCooperationInterest {
            AnnouncementId = announcement.Id, RegisteredAt = DateTimeOffset.Now };
        return true;
    }

    public static bool Cancel(Announcement announcement)
    {
        if (!announcement.HasCooperationInterest) return false;
        announcement.CooperationInterest = null;
        return true;
    }

    public static bool IsValid(Announcement announcement) => announcement.CooperationInterest is not { } interest
        || interest.AnnouncementId == announcement.Id && interest.RegisteredAt != default
            && interest.Scope == LocalCooperationInterest.LocalScope;
}
