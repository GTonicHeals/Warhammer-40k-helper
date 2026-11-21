namespace Warhammer.Services.Auth;

// Who counts as the Game Master. `tailscale serve` adds the visitor's Tailscale login as a request
// header; a login listed in Gm:TailscaleLogins is the GM. In Development everyone is GM, since
// Tailscale is not in front of `dotnet run`.
public static class GmIdentity
{
    public const string Scheme     = "Tailscale";
    public const string Role       = "GM";
    public const string HeaderName = "Tailscale-User-Login";

    public static bool IsGm(string? login, IEnumerable<string>? gmLogins, bool isDevelopment)
    {
        if (isDevelopment) return true;
        if (string.IsNullOrWhiteSpace(login) || gmLogins is null) return false;
        var trimmed = login.Trim();
        return gmLogins.Any(g => string.Equals(g?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class GmOptions
{
    public const string Section = "Gm";

    // Tailscale logins treated as GM. Set outside the repo (service environment variable
    // Gm__TailscaleLogins__0) so the real login is never committed.
    public List<string> TailscaleLogins { get; set; } = new();
}
