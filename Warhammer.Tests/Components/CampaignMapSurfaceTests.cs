using Bunit;
using FluentAssertions;
using Warhammer.Components.UI;
using Warhammer.Models.Campaign;
using TestContext = Bunit.TestContext;
using CampaignModel = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Components;

public class CampaignMapSurfaceTests : IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignMapSurfaceTests() => _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

    private IRenderedComponent<CampaignMap> Render(WorldSurfaceSettings surface) =>
        _ctx.RenderComponent<CampaignMap>(p => p.Add(c => c.Surface, surface));

    private IReadOnlyList<JSRuntimeInvocation> Draws() =>
        _ctx.JSInterop.Invocations.Where(i => i.Identifier == "topoMapInterop.drawById").ToList();

    [Fact]
    public void Draws_the_surface_it_is_given()
    {
        var surface = WorldSurfaceSettings.From(new CampaignModel { MapSeed = 9, MapBiome = "tomb" });

        Render(surface);

        var draws = Draws();
        draws.Should().HaveCount(1);
        draws[0].Arguments[0].Should().Be("campaign-topo-canvas");
        draws[0].Arguments[1].Should().Be(surface);
    }

    [Fact]
    public void Redraws_only_when_the_surface_changes()
    {
        var surface = WorldSurfaceSettings.From(new CampaignModel { MapSeed = 9, MapBiome = "tomb" });
        var cut = Render(surface);

        cut.SetParametersAndRender(p => p.Add(c => c.Surface, surface with { }));
        Draws().Should().HaveCount(1);

        cut.SetParametersAndRender(p => p.Add(c => c.Surface, surface with { Rivers = surface.Rivers + 1 }));
        Draws().Should().HaveCount(2);
    }

    public void Dispose() => _ctx.Dispose();
}
