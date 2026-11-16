using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Campaign;
using Warhammer.Models.Campaign;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CampaignEditorHiddenTerritoryTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignEditorHiddenTerritoryTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    private CampaignManagerService Fresh() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    private async Task<IRenderedComponent<CampaignEditor>> RenderWithHiddenAsync(params string[] ids)
    {
        var campaign = await Fresh().LoadCampaignAsync();
        foreach (var id in ids)
            campaign.MapNodes.Add(new Territory { Id = id, Name = $"Hab {id}", X = 0.5, Y = 0.5 });
        await Fresh().SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement($".map-node[data-node-id='{ids[0]}']");
        return cut;
    }

    private static Task Click(IRenderedComponent<CampaignEditor> cut, string selector) =>
        cut.Find(selector).ClickAsync(new MouseEventArgs());

    private async Task<Dictionary<string, bool>> SavedAsync() =>
        (await Fresh().LoadCampaignAsync()).MapNodes.OfType<Territory>().ToDictionary(t => t.Id, t => t.IsPublic);

    [Fact]
    public async Task Toggle_reveals_a_hidden_territory()
    {
        var cut = await RenderWithHiddenAsync("t1");

        await Click(cut, ".map-node[data-node-id='t1']");
        cut.Find("#ce-visibility").TextContent.Should().Contain("Hidden");
        await Click(cut, "#ce-visibility");
        cut.Find("#ce-visibility").TextContent.Should().Contain("Revealed");
        await Click(cut, "#campaign-save");

        (await SavedAsync())["t1"].Should().BeTrue();
    }

    [Fact]
    public async Task Reveal_all_reveals_every_territory()
    {
        var cut = await RenderWithHiddenAsync("t1", "t2");

        await Click(cut, "#ce-reveal-all");
        await Click(cut, "#campaign-save");

        (await SavedAsync()).Values.Should().AllBeEquivalentTo(true);
        cut.Find("#ce-reveal-all").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Hidden_territories_are_marked_on_the_editor_map()
    {
        var cut = await RenderWithHiddenAsync("t1");

        var node = cut.Find(".map-node[data-node-id='t1']");
        node.ClassList.Should().Contain("node-hidden");
        node.QuerySelector(".map-node-label")!.TextContent.Should().StartWith("\U0001f512");
    }

    public new void Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
