using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Campaign;
using Warhammer.Services.Auth;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CampaignEditorBiomeTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignEditorBiomeTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.AddTestAuthorization().SetAuthorized("gm").SetRoles(GmIdentity.Role);
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    private CampaignManagerService Fresh() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    [Fact]
    public void Editor_biome_dropdown_lists_all_nine()
    {
        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement(".ce-biome-select");

        cut.FindAll(".ce-biome-select option").Should().HaveCount(9);
    }

    [Fact]
    public async Task Editor_biome_change_keeps_sea_level_and_rivers()
    {
        var campaign = await Fresh().LoadCampaignAsync();
        campaign.MapSeaLevel = 0.61;
        campaign.MapRivers = 11;
        await Fresh().SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<CampaignEditor>();
        await cut.WaitForElement(".ce-biome-select").ChangeAsync(new ChangeEventArgs { Value = "desert" });
        await cut.Find("#campaign-save").ClickAsync(new MouseEventArgs());

        var saved = await Fresh().LoadCampaignAsync();
        saved.MapBiome.Should().Be("desert");
        saved.MapSeaLevel.Should().Be(0.61);
        saved.MapRivers.Should().Be(11);
    }

    public new void Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
