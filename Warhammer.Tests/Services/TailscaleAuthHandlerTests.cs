using System.Text.Encodings.Web;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Warhammer.Services.Auth;

namespace Warhammer.Tests.Services;

public class TailscaleAuthHandlerTests
{
    public static TheoryData<string?, string[]?, bool, bool> IsGmCases => new()
    {
        { "me@x.com", ["me@x.com"], false, true },
        { " ME@X.com ", ["me@x.com"], false, true },
        { "player@y.com", ["me@x.com"], false, false },
        { null, ["me@x.com"], false, false },
        { "", ["me@x.com"], false, false },
        { "me@x.com", null, false, false },
        { "me@x.com", [], false, false },
        { null, [], true, true },
    };

    [Theory]
    [MemberData(nameof(IsGmCases))]
    public void IsGm_matches_configured_logins_or_development(string? login, string[]? gmLogins, bool isDev, bool expected)
        => GmIdentity.IsGm(login, gmLogins, isDev).Should().Be(expected);

    private static async Task<AuthenticateResult> AuthenticateAsync(string environment, string? header, params string[] gmLogins)
    {
        var schemeOptions = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        schemeOptions.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        var gmOptions = new Mock<IOptionsMonitor<GmOptions>>();
        gmOptions.Setup(o => o.CurrentValue).Returns(new GmOptions { TailscaleLogins = gmLogins.ToList() });
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environment);

        var handler = new TailscaleAuthHandler(schemeOptions.Object, NullLoggerFactory.Instance, UrlEncoder.Default, gmOptions.Object, env.Object);
        var ctx = new DefaultHttpContext();
        if (header is not null) ctx.Request.Headers[GmIdentity.HeaderName] = header;
        await handler.InitializeAsync(new AuthenticationScheme(GmIdentity.Scheme, null, typeof(TailscaleAuthHandler)), ctx);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task Production_matching_header_yields_GM_principal()
    {
        var result = await AuthenticateAsync(Environments.Production, "me@x.com", "me@x.com");
        result.Succeeded.Should().BeTrue();
        result.Principal!.IsInRole(GmIdentity.Role).Should().BeTrue();
        result.Principal.Identity!.Name.Should().Be("me@x.com");
    }

    [Fact]
    public async Task Production_unknown_login_is_guest()
        => (await AuthenticateAsync(Environments.Production, "player@y.com", "me@x.com")).None.Should().BeTrue();

    [Fact]
    public async Task Production_without_header_is_guest()
        => (await AuthenticateAsync(Environments.Production, null, "me@x.com")).None.Should().BeTrue();

    [Fact]
    public async Task Development_without_header_is_GM()
    {
        var result = await AuthenticateAsync(Environments.Development, null);
        result.Succeeded.Should().BeTrue();
        result.Principal!.IsInRole(GmIdentity.Role).Should().BeTrue();
    }
}
