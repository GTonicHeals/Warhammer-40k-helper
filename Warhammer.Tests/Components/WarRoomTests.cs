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

public class WarRoomTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public WarRoomTests()
    {
        var factory = CreateFactory();
        _ctx.Services.AddSingleton(new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
    }

    private CampaignManagerService Campaigns => _ctx.Services.GetRequiredService<CampaignManagerService>();

    // No territories are seeded, so the page skips the JS-backed CampaignMap.
    private async Task<IRenderedComponent<WarRoom>> RenderWithBattlesAsync()
    {
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.BattleHistory.Add(new BattleRecord { Id = "m1", MissionName = "Unfinished", Status = BattleStatus.Mustered });
        campaign.BattleHistory.Add(new BattleRecord { Id = "c1", MissionName = "Finished",   Status = BattleStatus.Completed, WinnerPlayerId = "p1" });
        await Campaigns.SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<WarRoom>();
        cut.WaitForAssertion(() => cut.FindAll(".wr-table tbody tr.wr-battle-row").Count.Should().Be(2));
        return cut;
    }

    private static Task Click(IRenderedComponent<WarRoom> cut, string selector) =>
        cut.Find(selector).ClickAsync(new MouseEventArgs());

    [Fact]
    public async Task Deleting_a_battle_asks_for_confirmation_then_removes_it()
    {
        var cut = await RenderWithBattlesAsync();

        await Click(cut, "#wr-del-m1");
        cut.FindAll(".wr-table tbody tr.wr-battle-row").Count.Should().Be(2, "nothing is deleted before confirming");

        await Click(cut, "#wr-del-yes-m1");

        cut.FindAll(".wr-table tbody tr.wr-battle-row").Count.Should().Be(1);
        cut.Markup.Should().NotContain("Unfinished").And.Contain("Finished");
        (await Campaigns.LoadCampaignAsync()).BattleHistory.Should().ContainSingle().Which.Id.Should().Be("c1");
    }

    [Fact]
    public async Task Completed_battles_link_to_the_amend_page()
    {
        var cut = await RenderWithBattlesAsync();

        cut.Find("#wr-amend-c1").GetAttribute("href").Should().Be("battle/c1/amend");
        cut.FindAll("#wr-amend-m1").Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelling_the_confirmation_keeps_the_battle()
    {
        var cut = await RenderWithBattlesAsync();

        await Click(cut, "#wr-del-m1");
        await Click(cut, "#wr-del-no-m1");

        cut.FindAll("#wr-del-yes-m1").Should().BeEmpty();
        (await Campaigns.LoadCampaignAsync()).BattleHistory.Should().HaveCount(2);
    }

    [Fact]
    public async Task Confirming_a_completed_battle_warns_that_its_results_are_kept()
    {
        var cut = await RenderWithBattlesAsync();

        await Click(cut, "#wr-del-m1");
        cut.FindAll(".wr-del-note").Should().BeEmpty("a Mustered battle applied nothing");
        await Click(cut, "#wr-del-no-m1");

        await Click(cut, "#wr-del-c1");
        cut.Find(".wr-del-note").TextContent.Should().Contain("kept");
    }

    [Fact]
    public async Task Hidden_territories_are_not_shown_counted_or_named()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab", IsPublic = true, ControllingPlayerId = "p1", X = 0.2, Y = 0.2,
                                              ConnectedNodeIds = { "t2" } });
        campaign.MapNodes.Add(new Territory { Id = "t2", Name = "Secret Vault", ControllingPlayerId = "p1", X = 0.6, Y = 0.6,
                                              ConnectedNodeIds = { "t1" } });
        await Campaigns.SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<WarRoom>();
        cut.WaitForElement(".map-node[data-node-id='t1']");

        cut.FindAll(".map-node[data-node-id='t2']").Should().BeEmpty();
        cut.FindAll(".map-edge").Should().BeEmpty();
        await cut.Find(".map-node[data-node-id='t1']").ClickAsync(new MouseEventArgs());
        cut.Markup.Should().NotContain("Secret Vault");
        cut.Find(".wr-strip-p1").GetAttribute("style").Should().Contain("width:100%");
    }

    [Fact]
    public async Task With_every_territory_hidden_the_map_still_shows_public_notes()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Secret Vault", X = 0.6, Y = 0.6 });
        campaign.GmNotebook.Add(new GmNote { Id = "n1", Title = "Landing Zone", IsPublic = true, X = 0.3, Y = 0.3 });
        await Campaigns.SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<WarRoom>();
        cut.WaitForElement(".map-note-pin[data-node-id='n1']");

        cut.FindAll(".wr-no-nodes").Should().BeEmpty();
        cut.Markup.Should().NotContain("Secret Vault");
    }

    [Fact]
    public async Task Revealing_events_keeps_changes_made_after_the_page_loaded()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab", IsPublic = true, X = 0.2, Y = 0.2 });
        campaign.MapNodes.Add(new Territory { Id = "t2", Name = "Ridge", X = 0.6, Y = 0.6 });
        campaign.MapNodes.Add(new CampaignEvent { Id = "e1", Name = "Ambush", AttachedToNodeId = "t1" });
        await Campaigns.SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<WarRoom>();
        cut.WaitForElement(".map-node[data-node-id='t1']");

        // A battle completed elsewhere while the War Room was open.
        var elsewhere = await Campaigns.LoadCampaignAsync();
        var t1 = elsewhere.MapNodes.OfType<Territory>().Single(t => t.Id == "t1");
        t1.ControllingPlayerId = "p2";
        elsewhere.MapNodes.OfType<Territory>().Single(t => t.Id == "t2").IsPublic = true;
        await Campaigns.SaveCampaignAsync(elsewhere);

        await cut.Find(".map-node[data-node-id='t1']").ClickAsync(new MouseEventArgs());

        var saved = (await Campaigns.LoadCampaignAsync()).MapNodes;
        saved.OfType<CampaignEvent>().Single().IsEventRevealed.Should().BeTrue();
        saved.OfType<Territory>().Single(t => t.Id == "t1").ControllingPlayerId.Should().Be("p2");
        saved.OfType<Territory>().Single(t => t.Id == "t2").IsPublic.Should().BeTrue();
    }

    public new void Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
