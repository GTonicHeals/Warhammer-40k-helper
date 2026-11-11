using FluentAssertions;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;

namespace Warhammer.Tests.Crusade;

public class XpRulesTests
{
    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(2, 4, 1)]
    [InlineData(0, 6, 2)]
    [InlineData(3, 3, 0)]
    public void Dealers_of_death_counts_every_third(int before, int after, int xp) =>
        XpRules.DealersOfDeathXp(before, after).Should().Be(xp);

    [Fact]
    public void XpFor_sums_experience_tally_and_mark()
    {
        var card = new CrusadeUnitCard { UnitsDestroyed = 2 };
        var r = new BattleUnitResult { EnemyUnitsDestroyed = 1, MarkedForGreatness = true };
        XpRules.XpFor(r, card).Should().Be(1 + 1 + 3);
    }

    [Fact]
    public void XpFor_is_zero_for_ineligible_unit() =>
        XpRules.XpFor(new BattleUnitResult { MarkedForGreatness = true }, new CrusadeUnitCard { XpEligible = false }).Should().Be(0);

    [Theory]
    [InlineData(14, 19, 1)]
    [InlineData(5, 16, 2)]
    [InlineData(0, 5, 0)]
    [InlineData(30, 35, 0)]
    public void Ranks_gained_for_non_character(int before, int after, int ranks) =>
        XpRules.RanksGained(before, after, isCharacter: false, legendaryVeteransUsed: false).Should().Be(ranks);

    [Fact]
    public void Ranks_gained_character_reaches_heroic() =>
        XpRules.RanksGained(30, 31, isCharacter: true, legendaryVeteransUsed: false).Should().Be(1);
}
