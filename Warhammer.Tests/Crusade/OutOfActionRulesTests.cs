using FluentAssertions;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;

namespace Warhammer.Tests.Crusade;

public class OutOfActionRulesTests
{
    private static CrusadeUnitCard Card(int honours, int scars)
    {
        var c = new CrusadeUnitCard();
        for (var i = 0; i < honours; i++) c.BattleHonours.Add(new AppliedTrait { Name = $"h{i}", Type = CrusadeTraitTypes.BattleTrait });
        for (var i = 0; i < scars; i++) c.BattleScars.Add(new AppliedTrait { Name = $"s{i}", Type = CrusadeTraitTypes.BattleScar });
        return c;
    }

    [Fact]
    public void Fresh_unit_can_take_scar_but_has_nothing_to_lose() =>
        OutOfActionRules.OptionsFor(new CrusadeUnitCard()).Should().Be(new OutOfActionOptions(true, false, false, false));

    [Fact]
    public void Unit_with_honour_may_choose_either() =>
        OutOfActionRules.OptionsFor(Card(honours: 1, scars: 0)).Should().Be(new OutOfActionOptions(true, true, false, false));

    [Fact]
    public void Three_scars_force_devastating_blow() =>
        OutOfActionRules.OptionsFor(Card(honours: 2, scars: 3)).Should().Be(new OutOfActionOptions(false, true, true, false));

    [Fact]
    public void Three_scars_and_no_honours_means_removal() =>
        OutOfActionRules.OptionsFor(Card(honours: 0, scars: 3)).Should().Be(new OutOfActionOptions(false, false, true, true));
}
