using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;

namespace Warhammer.Services.Crusade;

// Crusade XP awarded by one Battle (10th edition core Crusade rules).
//   Battle Experience   +1 to every unit in the Crusade Army
//   Dealers of Death    +1 each time the unit's cumulative enemy-units-destroyed tally passes a multiple of 3
//   Marked for Greatness +3 to one unit per side
// EPIC HERO, FORTIFICATION and SWARM units (XpEligible == false) gain nothing.
public static class XpRules
{
    public const int BattleExperience   = 1;
    public const int MarkedForGreatness = 3;
    public const int DealersOfDeathStep = 3;

    public static int DealersOfDeathXp(int tallyBefore, int tallyAfter) =>
        Math.Max(0, tallyAfter / DealersOfDeathStep - tallyBefore / DealersOfDeathStep);

    public static int XpFor(BattleUnitResult r, CrusadeUnitCard card)
    {
        if (!card.XpEligible) return 0;
        return BattleExperience
             + DealersOfDeathXp(card.UnitsDestroyed, card.UnitsDestroyed + r.EnemyUnitsDestroyed)
             + (r.MarkedForGreatness ? MarkedForGreatness : 0);
    }

    // Each rank crossed owes one Battle Honour.
    public static int RanksGained(int xpBefore, int xpAfter, bool isCharacter, bool legendaryVeteransUsed) =>
        Math.Max(0, (int)RankCalculator.RankFor(xpAfter, isCharacter, legendaryVeteransUsed)
                  - (int)RankCalculator.RankFor(xpBefore, isCharacter, legendaryVeteransUsed));
}
