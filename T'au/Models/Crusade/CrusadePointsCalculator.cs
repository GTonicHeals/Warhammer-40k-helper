namespace Warhammer.Models.Crusade;

// Crusade Points are derived from a card's honours and scars, never typed.
//   Crusade Relic: Artificer +1, Antiquity +2, Legendary +3 (by tier in Category)
//   Any other Battle Honour: +1, or +2 when the unit is TITANIC
//   Battle Scar: −1 each
public static class CrusadePointsCalculator
{
    public static int CostOf(AppliedTrait t, bool isTitanic) => CostOf(t.Type, t.Category, isTitanic);

    public static int CostOf(string type, string? category, bool isTitanic)
    {
        if (!string.Equals(type, CrusadeTraitTypes.CrusadeRelic, StringComparison.OrdinalIgnoreCase))
            return isTitanic ? 2 : 1;

        var cat = category ?? "";
        if (cat.Contains("Legendary", StringComparison.OrdinalIgnoreCase)) return 3;
        if (cat.Contains("Antiquity", StringComparison.OrdinalIgnoreCase)) return 2;
        return 1; // Artificer or unrecognised
    }

    public static int For(CrusadeUnitCard card) =>
        card.BattleHonours.Sum(h => CostOf(h, card.IsTitanic)) - card.BattleScars.Count;
}
