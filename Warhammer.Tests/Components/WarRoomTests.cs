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

    public new void Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
