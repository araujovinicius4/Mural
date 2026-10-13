using System.Diagnostics;

namespace MuralInstitucional.Services;

public static class ExternalLinkService
{
    public static bool TryValidate(string? address, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(address, UriKind.Absolute, out var candidate)
            || candidate.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(candidate.Host)
            || !string.IsNullOrEmpty(candidate.UserInfo)) return false;
        uri = candidate;
        return true;
    }
    public static void Open(string? address)
    {
        if (!TryValidate(address, out var uri)) throw new ArgumentException("Somente endereços HTTP/HTTPS válidos, sem credenciais, são aceitos.");
        Process.Start(new ProcessStartInfo(uri!.AbsoluteUri) { UseShellExecute = true });
    }
}
