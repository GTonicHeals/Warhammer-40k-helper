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

public class MapGeneratorTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public MapGeneratorTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.AddTestAuthorization().SetAuthorized("gm").SetRoles(GmIdentity.Role);
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    private CampaignManagerService Fresh() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    [Fact]
    public async Task Choosing_a_biome_applies_its_sea_level_and_rivers()
    {
        var cut = _ctx.RenderComponent<MapGenerator>();
        await cut.WaitForElement("#mg-biome").ChangeAsync(new ChangeEventArgs { Value = "desert" });

        cut.Find("#mg-sea").GetAttribute("value").Should().Be("0.08");
        cut.Find("#mg-rivers").GetAttribute("value").Should().Be("2");
    }

    [Fact]
    public async Task Apply_saves_the_world_surface_controls()
    {
        var cut = _ctx.RenderComponent<MapGenerator>();
        await cut.WaitForElement("#mg-sea").InputAsync(new ChangeEventArgs { Value = "0.6" });
        await cut.Find("#mg-landmass").ChangeAsync(new ChangeEventArgs { Value = "archipelago" });
        await cut.Find("#mg-rivers").InputAsync(new ChangeEventArgs { Value = "7" });
        await cut.Find(".mg-actions .btn-warning").ClickAsync(new MouseEventArgs());

        var saved = await Fresh().LoadCampaignAsync();
        saved.MapSeaLevel.Should().Be(0.6);
        saved.MapLandmass.Should().Be("archipelago");
        saved.MapRivers.Should().Be(7);
    }

    [Fact]
    public void Noise_parameters_live_under_advanced()
    {
        var cut = _ctx.RenderComponent<MapGenerator>();
        cut.WaitForElement(".mg-advanced");

        var details = cut.Find(".mg-advanced");
        details.HasAttribute("open").Should().BeFalse();
        details.QuerySelectorAll("input[type=range]").Should().HaveCount(4);
        details.TextContent.Should().Contain("OCTAVES").And.Contain("LACUNARITY")
            .And.Contain("PERSISTENCE").And.Contain("SCALE");
    }

    [Fact]
    public async Task Legacy_biome_shows_as_agri()
    {
        var campaign = await Fresh().LoadCampaignAsync();
        campaign.MapBiome = "forge";
        campaign.MapLandmass = "garbage";
        await Fresh().SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<MapGenerator>();
        cut.WaitForElement("#mg-biome");

        cut.Find("#mg-biome").GetAttribute("value").Should().Be("agri");
        cut.Find("#mg-landmass").GetAttribute("value").Should().Be("continent");
    }

    public new void Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
