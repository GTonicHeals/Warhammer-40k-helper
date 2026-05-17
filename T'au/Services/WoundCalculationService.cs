namespace Warhammer.Services;

public record WoundCalcInput(
    string? HitRoll,          // e.g. "3+", "AUTO", or null
    int? WeaponStrength,
    int? WeaponAp,
    int? DefenderToughness,
    int? DefenderSave,
    bool PlusOneHit,
    bool PlusOneWound,
    bool BgntApplied,         // pre-evaluated: _bgntPenalty && CanApplyBgnt()
    bool MinusOneHit,
    bool MinusOneWound,
    bool ReduceAp,
    bool HasCover,
    int InvulnSave            // 0 = none, else 3/4/5/6
);

public record WoundCalcResult(
    int NetHitMod,
    int NetWoundMod,
    string? WoundRoll,
    string? EffectiveHitRoll,
    string? EffectiveWoundRoll,
    string? FinalSaveRoll
);

public static class WoundCalculationService
{
    public static WoundCalcResult Calculate(WoundCalcInput i)
    {
        // Net modifiers — 10th Ed: each is capped at [-1, +1]
        int netHit   = Math.Clamp((i.PlusOneHit   ? 1 : 0) - (i.MinusOneHit   ? 1 : 0) - (i.BgntApplied ? 1 : 0), -1, 1);
        int netWound = Math.Clamp((i.PlusOneWound  ? 1 : 0) - (i.MinusOneWound ? 1 : 0), -1, 1);

        // Effective AP (AP reduction makes it less severe; floor at 0)
        int effectiveAp = i.WeaponAp.HasValue
            ? (i.ReduceAp ? Math.Min(i.WeaponAp.Value + 1, 0) : i.WeaponAp.Value)
            : 0;

        // Base wound roll (S vs T table)
        string? woundRoll = null;
        if (i.WeaponStrength.HasValue && i.DefenderToughness.HasValue)
        {
            int s = i.WeaponStrength.Value, t = i.DefenderToughness.Value;
            woundRoll = s >= t * 2 ? "2+" :
                        s > t      ? "3+" :
                        s == t     ? "4+" :
                        s * 2 <= t ? "6+" :
                                     "5+";
        }

        // Effective hit roll (post-modifier)
        string? effectiveHitRoll;
        if (i.HitRoll is not null and not "AUTO")
        {
            int baseHit = ParseRollNum(i.HitRoll);
            int eff     = Math.Clamp(baseHit - netHit, 2, 7);
            effectiveHitRoll = eff <= 6 ? $"{eff}+" : "N/A";
        }
        else
        {
            effectiveHitRoll = i.HitRoll;
        }

        // Effective wound roll (post-modifier)
        string? effectiveWoundRoll = null;
        if (woundRoll is not null)
        {
            int baseWound = ParseRollNum(woundRoll);
            int eff       = Math.Clamp(baseWound - netWound, 2, 7);
            effectiveWoundRoll = eff <= 6 ? $"{eff}+" : "N/A";
        }

        // Final save: armour vs invuln, take the better
        string? finalSaveRoll = null;
        if (i.DefenderSave.HasValue && i.WeaponAp.HasValue)
        {
            int rawArmour      = i.DefenderSave.Value + Math.Abs(effectiveAp);
            int adjustedArmour = rawArmour - (i.HasCover ? 1 : 0);
            int armourSave     = Math.Max(adjustedArmour, i.DefenderSave.Value); // cover can't beat base save

            int finalSaveNum = i.InvulnSave > 0
                ? (armourSave <= 6 ? Math.Min(armourSave, i.InvulnSave) : i.InvulnSave)
                : armourSave;

            finalSaveRoll = finalSaveNum <= 6 ? $"{finalSaveNum}+" : "N/A";
        }
        else if (i.InvulnSave > 0 && i.WeaponAp.HasValue)
        {
            finalSaveRoll = $"{i.InvulnSave}+";
        }

        return new WoundCalcResult(netHit, netWound, woundRoll, effectiveHitRoll, effectiveWoundRoll, finalSaveRoll);
    }

    public static int ParseRollNum(string roll) =>
        int.TryParse(roll.TrimEnd('+'), out var n) ? n : 4;
}
