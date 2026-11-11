using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;
using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Crusade;

public class PostBattleServiceTests : SqliteTestBase
{
    private static AppliedTrait Trait(string name, string type = CrusadeTraitTypes.BattleTrait) => new() { Name = name, Type = type };

    private CampaignManagerService Campaigns() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);
    private CrusadeManagerService Crusades() => new(CreateFactory(), Campaigns(), NullLogger<CrusadeManagerService>.Instance);
    private PostBattleService Svc() => new(Campaigns(), Crusades(), NullLogger<PostBattleService>.Instance);

    // Seeds the scenario from the plan and returns the battle id. `tweak` edits the battle before saving.
    private async Task<string> SeedAsync(Action<BattleRecord, OrderOfBattle>? tweak = null)
    {
        var campaign = await Campaigns().LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab" });

        var a = new OrderOfBattle
        {
            Id = "A", ArmyName = "A", PlayerId = "p1",
            Units =
            {
                new CrusadeUnitCard { Id = "a1", UnitName = "A1", Xp = 14, BattleHonours = { Trait("Old") } },
                new CrusadeUnitCard { Id = "a2", UnitName = "A2", XpEligible = false }
            }
        };
        var b = new OrderOfBattle
        {
            Id = "B", ArmyName = "B", PlayerId = "p2",
            Units = { new CrusadeUnitCard { Id = "b1", UnitName = "B1",
                BattleScars = { Trait("s1", CrusadeTraitTypes.BattleScar), Trait("s2", CrusadeTraitTypes.BattleScar), Trait("s3", CrusadeTraitTypes.BattleScar) } } }
        };
        var crusades = Crusades();
        await crusades.SaveCrusadeAsync(a);
        await crusades.SaveCrusadeAsync(b);

        var battle = new BattleRecord
        {
            Status = BattleStatus.Mustered, PointsLimit = 1000, MissionName = "Take and Hold",
            Player1OrderId = "A", Player2OrderId = "B",
            WinnerPlayerId = "p1", CapturedTerritoryId = "t1",
            UnitResults =
            {
                new BattleUnitResult { CardId = "a1", PlayerId = "p1", EnemyUnitsDestroyed = 3, MarkedForGreatness = true, HonoursGained = { Trait("New") } },
                new BattleUnitResult { CardId = "a2", PlayerId = "p1", Destroyed = true, OutOfActionPassed = false,
                    Penalty = OutOfActionPenalty.BattleScar, ScarTaken = Trait("Scar", CrusadeTraitTypes.BattleScar) },
                new BattleUnitResult { CardId = "b1", PlayerId = "p2", Destroyed = true, OutOfActionPassed = false,
                    Penalty = OutOfActionPenalty.DevastatingBlow, RemovedFromOrder = true }
            }
        };
        tweak?.Invoke(battle, a);
        if (tweak is not null) await crusades.SaveCrusadeAsync(a);
        campaign.BattleHistory.Add(battle);
        await Campaigns().SaveCampaignAsync(campaign);
        return battle.Id;
    }

    [Fact]
    public async Task Complete_applies_xp_tallies_scars_removal_rp_and_map()
    {
        var battleId = await SeedAsync();

        await Svc().CompleteAsync(battleId);

        var orders = await Crusades().LoadAllCrusadesAsync();
        var a = orders.Single(o => o.PlayerId == "p1");
        var a1 = a.Units.Single(u => u.Id == "a1");
        a1.Xp.Should().Be(19);
        a1.BattlesPlayed.Should().Be(1);
        a1.BattlesSurvived.Should().Be(1);
        a1.UnitsDestroyed.Should().Be(3);
        a1.BattleHonours.Should().HaveCount(2);
        var a2 = a.Units.Single(u => u.Id == "a2");
        a2.Xp.Should().Be(0);
        a2.BattlesPlayed.Should().Be(1);
        a2.BattlesSurvived.Should().Be(0);
        a2.BattleScars.Should().ContainSingle();
        a.RequisitionPoints.Should().Be(6);

        var b = orders.Single(o => o.PlayerId == "p2");
        b.Units.Should().BeEmpty();
        b.RequisitionPoints.Should().Be(6);

        var campaign = await Campaigns().LoadCampaignAsync();
        var battle = campaign.BattleHistory.Single();
        battle.Status.Should().Be(BattleStatus.Completed);
        battle.UnitResults.Single(r => r.CardId == "a1").XpGained.Should().Be(5);
        campaign.Players.Single(p => p.Id == "p1").Wins.Should().Be(1);
        campaign.Players.Single(p => p.Id == "p2").Losses.Should().Be(1);
        ((Territory)campaign.MapNodes.Single()).ControllingPlayerId.Should().Be("p1");
    }

    [Fact]
    public async Task Complete_twice_throws()
    {
        var battleId = await SeedAsync();
        await Svc().CompleteAsync(battleId);

        await Svc().Invoking(s => s.CompleteAsync(battleId)).Should().ThrowAsync<InvalidOperationException>();

        (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p1").Units.Single(u => u.Id == "a1").Xp.Should().Be(19);
    }

    [Fact]
    public async Task Complete_rejects_wrong_honour_count_and_saves_nothing()
    {
        var battleId = await SeedAsync((battle, _) => battle.UnitResults[0].HonoursGained.Clear());

        await Svc().Invoking(s => s.CompleteAsync(battleId)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*A1*");

        (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p1").Units.Single(u => u.Id == "a1").Xp.Should().Be(14);
        (await Campaigns().LoadCampaignAsync()).BattleHistory.Single().Status.Should().Be(BattleStatus.Mustered);
    }

    [Fact]
    public async Task Complete_rejects_two_marked_for_greatness_on_one_side()
    {
        var battleId = await SeedAsync((battle, _) => battle.UnitResults[1].MarkedForGreatness = true);

        await Svc().Invoking(s => s.CompleteAsync(battleId)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Complete_rejects_scar_when_three_scars_force_devastating_blow()
    {
        var battleId = await SeedAsync((battle, _) =>
        {
            battle.UnitResults[2].Penalty = OutOfActionPenalty.BattleScar;
            battle.UnitResults[2].RemovedFromOrder = false;
            battle.UnitResults[2].ScarTaken = Trait("Fourth", CrusadeTraitTypes.BattleScar);
        });

        await Svc().Invoking(s => s.CompleteAsync(battleId)).Should().ThrowAsync<InvalidOperationException>().WithMessage("*B1*");
    }

    [Fact]
    public async Task Rp_caps_at_ten()
    {
        var battleId = await SeedAsync((_, a) => a.RequisitionPoints = 10);

        await Svc().CompleteAsync(battleId);

        (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p1").RequisitionPoints.Should().Be(10);
    }

    [Fact]
    public async Task Devastating_blow_removes_the_named_honour()
    {
        var battleId = await SeedAsync((battle, _) =>
        {
            var r = battle.UnitResults[0];
            r.Destroyed = true; r.OutOfActionPassed = false;
            r.Penalty = OutOfActionPenalty.DevastatingBlow; r.HonourLost = "Old";
        });

        await Svc().CompleteAsync(battleId);

        var a1 = (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p1").Units.Single(u => u.Id == "a1");
        a1.BattleHonours.Select(h => h.Name).Should().BeEquivalentTo(new[] { "New" });
        a1.BattlesSurvived.Should().Be(0);
    }
}
