using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Battle;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class AmendBattleTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public AmendBattleTests()
    {
        var factory = CreateFactory();
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new PostBattleService(sp.GetRequiredService<CampaignManagerService>(),
            sp.GetRequiredService<CrusadeManagerService>(), NullLogger<PostBattleService>.Instance));
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        Dispose();
    }

    private CampaignManagerService Campaigns => _ctx.Services.GetRequiredService<CampaignManagerService>();
    private CrusadeManagerService Crusades => _ctx.Services.GetRequiredService<CrusadeManagerService>();
    private PostBattleService PostBattle => _ctx.Services.GetRequiredService<PostBattleService>();

    // p1 (A: a1, 5 models) beats p2 (B: b1, destroyed and removed). Completed unless `complete` is false.
    private async Task<string> SeedAsync(string winner = "p1", string? captured = null, bool complete = true)
    {
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab" });
        campaign.MapNodes.Add(new Territory { Id = "t4", Name = "Spire" });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "A1", XpEligible = false, ModelCount = 5 } } });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "B", ArmyName = "Chapter", PlayerId = "p2",
            Units = { new CrusadeUnitCard { Id = "b1", UnitName = "B1", XpEligible = false } } });

        var battle = new BattleRecord
        {
            Status = BattleStatus.Mustered, PointsLimit = 1000, MissionName = "Take and Hold",
            Player1OrderId = "A", Player2OrderId = "B", WinnerPlayerId = winner, CapturedTerritoryId = captured,
            UnitResults =
            {
                new BattleUnitResult { CardId = "a1", PlayerId = "p1", UnitName = "A1" },
                new BattleUnitResult { CardId = "b1", PlayerId = "p2", UnitName = "B1", Destroyed = true, OutOfActionPassed = true }
            }
        };
        campaign.BattleHistory.Add(battle);
        await Campaigns.SaveCampaignAsync(campaign);

        if (complete)
        {
            await PostBattle.CompleteAsync(battle.Id);
            // b1 is removed from its Order, as a failed Out of Action with no honours would do.
            var updated = await Campaigns.LoadCampaignAsync();
            updated.BattleHistory.Single().UnitResults.Single(r => r.CardId == "b1").RemovedFromOrder = true;
            await Campaigns.SaveCampaignAsync(updated);
        }
        return battle.Id;
    }

    private IRenderedComponent<AmendBattle> Render(string id) =>
        _ctx.RenderComponent<AmendBattle>(p => p.Add(x => x.Id, id));

    private static Task Click(IRenderedComponent<AmendBattle> cut, string selector) =>
        cut.Find(selector).ClickAsync(new MouseEventArgs());

    private async Task<string?> ControllerAsync(string id) =>
        (await Campaigns.LoadCampaignAsync()).MapNodes.OfType<Territory>().Single(t => t.Id == id).ControllingPlayerId;

    [Fact]
    public async Task Picking_a_territory_and_saving_captures_it()
    {
        var id = await SeedAsync();
        var cut = Render(id);
        cut.WaitForElement("#ab-save");

        await cut.Find("#territory").ChangeAsync(new ChangeEventArgs { Value = "t1" });
        await Click(cut, "#ab-save");

        cut.WaitForElement("#ab-saved");
        (await ControllerAsync("t1")).Should().Be("p1");
    }

    [Fact]
    public async Task Changing_damage_and_saving_updates_the_card()
    {
        var id = await SeedAsync();
        var cut = Render(id);
        cut.WaitForElement("#ab-save");

        await Click(cut, "[data-models-plus='a1']");
        await Click(cut, "[data-models-plus='a1']");
        cut.Find("[data-models-lost='a1']").TextContent.Should().Be("2");
        await Click(cut, "#ab-save");

        cut.WaitForElement("#ab-saved");
        (await Crusades.LoadAllCrusadesAsync()).Single(o => o.Id == "A").Units.Single().ModelsLost.Should().Be(2);
    }

    [Fact]
    public async Task Changing_an_existing_territory_warns_about_the_old_one()
    {
        var id = await SeedAsync(captured: "t1");
        var cut = Render(id);
        cut.WaitForElement("#ab-save");
        cut.FindAll("#ab-old-territory").Should().BeEmpty();

        await cut.Find("#territory").ChangeAsync(new ChangeEventArgs { Value = "t4" });

        cut.Find("#ab-old-territory").TextContent.Should().Contain("Hab");
    }

    [Fact]
    public async Task A_draw_has_no_territory_picker()
    {
        var id = await SeedAsync(winner: "");
        var cut = Render(id);
        cut.WaitForElement("#ab-save");

        cut.FindAll("#territory").Should().BeEmpty();
        cut.Find("#ab-territory-draw").TextContent.Should().Contain("A Draw captures no territory.");
    }

    [Fact]
    public async Task Removed_units_are_read_only()
    {
        var id = await SeedAsync();
        var cut = Render(id);
        cut.WaitForElement("#ab-save");

        cut.Find("[data-damage-locked='b1']").TextContent.Should().Contain("No longer in the Order of Battle");
        cut.FindAll("[data-models-plus='b1']").Should().BeEmpty();
    }

    [Fact]
    public async Task A_mustered_battle_points_to_the_post_battle_page()
    {
        var id = await SeedAsync(complete: false);
        var cut = Render(id);

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("hasn't been completed"));
        cut.Find($"a[href='battle/{id}/post']").Should().NotBeNull();
    }
}
