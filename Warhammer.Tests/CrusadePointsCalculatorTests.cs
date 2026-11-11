using FluentAssertions;
using Warhammer.Models.Crusade;

namespace Warhammer.Tests;

public class CrusadePointsCalculatorTests
{
    private static AppliedTrait Honour(string type, string category = "") => new() { Name = "x", Type = type, Category = category };

    [Fact]
    public void Battle_trait_costs_one() =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.BattleTrait), isTitanic: false).Should().Be(1);

    [Fact]
    public void Battle_trait_on_titanic_costs_two() =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.BattleTrait), isTitanic: true).Should().Be(2);

    [Theory]
    [InlineData("Artificer", 1)]
    [InlineData("Antiquity", 2)]
    [InlineData("Legendary", 3)]
    public void Relic_costs_by_tier(string tier, int cost) =>
        CrusadePointsCalculator.CostOf(Honour(CrusadeTraitTypes.CrusadeRelic, tier), isTitanic: false).Should().Be(cost);

    [Fact]
    public void Card_total_is_honours_minus_scars()
    {
        var card = new CrusadeUnitCard
        {
            BattleHonours = { Honour(CrusadeTraitTypes.BattleTrait), Honour(CrusadeTraitTypes.CrusadeRelic, "Antiquity") },
            BattleScars   = { Honour(CrusadeTraitTypes.BattleScar) }
        };
        CrusadePointsCalculator.For(card).Should().Be(2);
        card.CrusadePoints.Should().Be(2);
    }
}
