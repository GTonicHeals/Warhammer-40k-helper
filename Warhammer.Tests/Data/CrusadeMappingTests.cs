using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warhammer.Models.Crusade;
using Warhammer.Tests.TestInfrastructure;
using CampaignEntity = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Data;

public class CrusadeMappingTests : SqliteTestBase
{
    private async Task<int> SeedCampaignAsync()
    {
        await using var ctx = CreateContext();
        var c = new CampaignEntity { CampaignName = "C" };
        ctx.Campaigns.Add(c);
        await ctx.SaveChangesAsync();
        return c.Id;
    }

    [Fact]
    public async Task Order_round_trips_with_cards_and_json_columns()
    {
        var campaignId = await SeedCampaignAsync();
        var order = new OrderOfBattle
        {
            ArmyName = "Sept", CampaignId = campaignId, PlayerId = "p1",
            CustomTraits = { new CrusadeTrait { Name = "Custom", Type = CrusadeTraitTypes.CrusadeRelic } },
            Units =
            {
                new CrusadeUnitCard
                {
                    UnitName = "Breachers", Xp = 7,
                    BattleHonours = { new AppliedTrait { Name = "Brutal", Type = CrusadeTraitTypes.WeaponModification, WeaponName = "Pulse blaster" } }
                }
            }
        };
        await using (var ctx = CreateContext()) { ctx.OrdersOfBattle.Add(order); await ctx.SaveChangesAsync(); }
        await using (var ctx = CreateContext())
        {
            var loaded = await ctx.OrdersOfBattle.Include(o => o.Units).SingleAsync();
            loaded.CustomTraits.Should().ContainSingle(t => t.Name == "Custom");
            loaded.Units.Should().ContainSingle().Which.BattleHonours.Should().ContainSingle(h => h.WeaponName == "Pulse blaster");
            loaded.Units[0].CrusadePoints.Should().Be(1);
        }
    }

    [Fact]
    public async Task In_place_honour_mutation_is_persisted()
    {
        var campaignId = await SeedCampaignAsync();
        var order = new OrderOfBattle { CampaignId = campaignId, Units = { new CrusadeUnitCard { UnitName = "U" } } };
        await using (var ctx = CreateContext()) { ctx.OrdersOfBattle.Add(order); await ctx.SaveChangesAsync(); }
        await using (var ctx = CreateContext())
        {
            var card = await ctx.CrusadeUnitCards.SingleAsync();
            card.BattleScars.Add(new AppliedTrait { Name = "Scar", Type = CrusadeTraitTypes.BattleScar });
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = CreateContext())
            (await ctx.CrusadeUnitCards.SingleAsync()).BattleScars.Should().ContainSingle(s => s.Name == "Scar");
    }

    [Fact]
    public async Task Attrition_round_trips_through_the_crusade_service()
    {
        var campaigns = new Warhammer.Services.CampaignManagerService(CreateFactory(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Warhammer.Services.CampaignManagerService>.Instance);
        Warhammer.Services.CrusadeManagerService Svc() => new(CreateFactory(), campaigns,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Warhammer.Services.CrusadeManagerService>.Instance);

        var order = new OrderOfBattle
        {
            Units = { new CrusadeUnitCard { UnitName = "U", ModelCount = 10, WoundsPerModel = 2, ModelsLost = 3, WoundsLost = 1 } }
        };
        await Svc().SaveCrusadeAsync(order);

        var card = (await Svc().LoadAllCrusadesAsync()).Single().Units.Single();
        (card.ModelCount, card.WoundsPerModel, card.ModelsLost, card.WoundsLost).Should().Be((10, 2, 3, 1));

        order.Units[0].ModelsLost = 0;
        await Svc().SaveCrusadeAsync(order);
        (await Svc().LoadAllCrusadesAsync()).Single().Units.Single().ModelsLost.Should().Be(0);
    }
}
