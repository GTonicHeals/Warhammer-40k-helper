using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Warhammer.Services.Auth;

// Turns the Tailscale login header into a GM principal. Anyone else gets no identity (a guest).
// The header is trusted because the app only listens on localhost behind `tailscale serve`.
public sealed class TailscaleAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IOptionsMonitor<GmOptions> _gm;
    private readonly IHostEnvironment _env;

    public TailscaleAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<GmOptions> gm,
        IHostEnvironment env)
        : base(options, logger, encoder)
    {
        _gm  = gm;
        _env = env;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var login = Request.Headers[GmIdentity.HeaderName].FirstOrDefault()?.Trim();
        if (!GmIdentity.IsGm(login, _gm.CurrentValue.TailscaleLogins, _env.IsDevelopment()))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, string.IsNullOrEmpty(login) ? "dev-gm" : login),
                new Claim(ClaimTypes.Role, GmIdentity.Role),
            ],
            GmIdentity.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), GmIdentity.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
