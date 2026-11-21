using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Campaign;
using Warhammer.Components.UI;
using Warhammer.Services.Auth;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CampaignEditorGmGateTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignEditorGmGateTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    private void AssertNoticeOnly(IRenderedComponent<CampaignEditor> cut)
    {
        cut.WaitForElement("#gm-only-notice");
        cut.FindAll("#ce-mode-play").Should().BeEmpty();
        cut.FindAll(".ce-form-panel").Should().BeEmpty();
        cut.FindComponents<UnsavedChangesGuard>().Should().BeEmpty("a guest has nothing to save");
    }

    [Fact]
    public void Guest_sees_the_notice_and_no_editor()
    {
        _ctx.AddTestAuthorization();
        AssertNoticeOnly(_ctx.RenderComponent<CampaignEditor>());
    }

    [Fact]
    public void Signed_in_non_GM_sees_the_notice()
    {
        _ctx.AddTestAuthorization().SetAuthorized("player");
        AssertNoticeOnly(_ctx.RenderComponent<CampaignEditor>());
    }

    [Fact]
    public void GM_sees_the_editor()
    {
        _ctx.AddTestAuthorization().SetAuthorized("gm").SetRoles(GmIdentity.Role);
        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement("#ce-mode-play");
        cut.FindAll("#gm-only-notice").Should().BeEmpty();
    }

    [Fact]
    public void No_auth_state_is_treated_as_guest()
        => AssertNoticeOnly(_ctx.RenderComponent<CampaignEditor>());

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
