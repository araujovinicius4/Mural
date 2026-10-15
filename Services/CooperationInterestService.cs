using MuralInstitucional.Models;

namespace MuralInstitucional.Services;

// Both roles are simulated in one local profile; this is not an authorization boundary.
public static class CooperationInterestService
{
    public static bool Register(Announcement announcement)
    {
        if (!announcement.CanCooperate || announcement.Id == Guid.Empty || announcement.HasCooperationInterest || announcement.CooperationRequests.Count >= 500) return false;
        var registeredAt = DateTimeOffset.Now;
        var latest = announcement.CooperationRequests.Select(x => x.Interest.RegisteredAt).DefaultIfEmpty().Max();
        if (registeredAt <= latest) registeredAt = latest.AddTicks(1);
        announcement.CooperationInterest = new LocalCooperationInterest {
            AnnouncementId = announcement.Id, RegisteredAt = registeredAt };
        Migrate(announcement);
        announcement.CooperationChanged();
        return true;
    }

    public static bool Cancel(Announcement announcement)
    {
        if (!announcement.CanCooperate || !announcement.HasCooperationInterest) return false;
        Migrate(announcement);
        var request = announcement.CooperationRequests.Last(x => x.Interest == announcement.CooperationInterest);
        if (request.State is not (CooperationState.Completed or CooperationState.Refused or CooperationState.Cancelled))
            Replace(announcement, request, request with { State = CooperationState.Cancelled,
                History = [.. request.History, new(CooperationState.Cancelled, DateTimeOffset.Now)] });
        announcement.CooperationInterest = null;
        return true;
    }

    public static void Migrate(Announcement announcement)
    {
        if (announcement.CooperationInterest is { } interest && !announcement.CooperationRequests.Any(x => x.Interest == interest))
            announcement.CooperationRequests.Add(new CooperationRequest { Id = announcement.CooperationRequests.Count == 0 ? announcement.Id : Guid.NewGuid(),
                Interest = interest, History = [new(CooperationState.InterestRegistered, interest.RegisteredAt)] });
    }
    public static bool CanTransition(CooperationState from, CooperationState to) =>
        to == CooperationState.Cancelled && from is not (CooperationState.Completed or CooperationState.Refused or CooperationState.Cancelled)
        || (from, to) is (CooperationState.InterestRegistered, CooperationState.AwaitingAcceptance)
        or (CooperationState.AwaitingAcceptance, CooperationState.Accepted)
        or (CooperationState.AwaitingAcceptance, CooperationState.Refused)
        or (CooperationState.Accepted, CooperationState.AwaitingConsent)
        or (CooperationState.AwaitingConsent, CooperationState.InProgress)
        or (CooperationState.InProgress, CooperationState.Completed);

    public static bool Transition(Announcement announcement, Guid id, CooperationState next, string? reason = null)
    {
        var request = announcement.CooperationRequests.SingleOrDefault(x => x.Id == id);
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (!announcement.CanCooperate || request is null || request.History.Count >= 1000 || !CanTransition(request.State, next) || reason?.Length > 500
            || next == CooperationState.InProgress && !request.IdentificationSimulated) return false;
        Replace(announcement, request, request with { State = next,
            History = [.. request.History, new(next, DateTimeOffset.Now, reason)] });
        return true;
    }
    public static bool Consent(Announcement announcement, Guid id, bool requester)
    {
        var request = announcement.CooperationRequests.SingleOrDefault(x => x.Id == id);
        if (!announcement.CanCooperate || request?.State != CooperationState.AwaitingConsent
            || (requester ? request.RequesterConsent : request.ResponsibleConsent)) return false;
        Replace(announcement, request, request with { RequesterConsent = requester || request.RequesterConsent,
            ResponsibleConsent = !requester || request.ResponsibleConsent,
            History = [.. request.History, new(request.State, DateTimeOffset.Now,
                requester ? "Consentimento explícito: solicitante simulado" : "Consentimento explícito: responsável simulado",
                requester ? "requester" : "responsible")] });
        return true;
    }
    private static void Replace(Announcement announcement, CooperationRequest old, CooperationRequest updated)
    {
        announcement.CooperationRequests[announcement.CooperationRequests.IndexOf(old)] = updated;
        announcement.CooperationChanged();
    }
    private static bool ValidInterest(Announcement announcement, LocalCooperationInterest? interest) => interest is not null
        && interest.AnnouncementId == announcement.Id && interest.RegisteredAt != default && interest.Scope == LocalCooperationInterest.LocalScope;
    public static bool IsValid(Announcement announcement) =>
        (announcement.CooperationInterest is null || ValidInterest(announcement, announcement.CooperationInterest))
        && announcement.CooperationRequests is { Count: <= 500 } requests
        && requests.Select(x => x?.Id).Distinct().Count() == requests.Count
        && requests.All(x => x is not null && x.Id != Guid.Empty && ValidInterest(announcement, x.Interest)
            && Enum.IsDefined(x.State) && x.History is { Count: > 0 and <= 1000 }
            && x.History.All(h => h is not null && Enum.IsDefined(h.State) && h.At != default && h.Reason?.Length is not > 500
                && (h.ConsentRole is null || h.State == CooperationState.AwaitingConsent && h.ConsentRole is "requester" or "responsible"))
            && x.History[0].State == CooperationState.InterestRegistered && x.History[0].At == x.Interest.RegisteredAt
            && x.History[^1].State == x.State
            && x.History.Zip(x.History.Skip(1)).All(pair => pair.First.At <= pair.Second.At &&
                (pair.Second.ConsentRole is null ? CanTransition(pair.First.State, pair.Second.State) :
                    pair.First.State == CooperationState.AwaitingConsent && pair.Second.State == pair.First.State))
            && x.History.Count(h => h.ConsentRole == "requester") == (x.RequesterConsent ? 1 : 0)
            && x.History.Count(h => h.ConsentRole == "responsible") == (x.ResponsibleConsent ? 1 : 0)
            && (x.State is not (CooperationState.InProgress or CooperationState.Completed) || x.IdentificationSimulated)
            && (!(x.RequesterConsent || x.ResponsibleConsent) || x.History.Any(h => h.State == CooperationState.AwaitingConsent)));
}
