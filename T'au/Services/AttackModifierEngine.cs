using System.Text.RegularExpressions;
using Warhammer.Models.Calculator;

namespace Warhammer.Services;

// Turns datasheet facts and Situation Toggles into the calculator input plus the chips
// the page shows. All 10th edition modifier rules live here; the page never re-derives them.
//   Torrent        hit is automatic
//   Heavy          +1 hit, ranged, when the attacker remained stationary
//   Indirect Fire  -1 hit and the target has cover, ranged, when the target is not visible
//   Stealth        -1 hit against ranged attacks
//   Big Guns Never Tire  -1 hit for a VEHICLE/MONSTER firing a non-Pistol ranged weapon in Engagement Range
//   Lance          +1 wound, melee, when the bearer charged this turn
//   Anti-X N+      wound roll capped at N+ when the defender has keyword X (whole keyword)
//   Ignores Cover  cover cannot apply; melee never gets cover
//   Invulnerable save from the datasheet
public static partial class AttackModifierEngine
{
    [GeneratedRegex(@"^Anti-(.+?)\s+(\d)\+$", RegexOptions.IgnoreCase)]
    private static partial Regex AntiPattern();

    public static AttackResolution Resolve(AttackFacts f, Situation s)
    {
        bool Has(string kw) => f.WeaponKeywords.Any(k => k.StartsWith(kw, StringComparison.OrdinalIgnoreCase));
        bool AttackerIs(string kw) => f.AttackerKeywords.Any(k => string.Equals(k, kw, StringComparison.OrdinalIgnoreCase));

        var hit = new List<ModChip>();
        var wound = new List<ModChip>();
        var save = new List<ModChip>();

        bool plusHit = s.PlusHit, minusHit = s.MinusHit, plusWound = s.PlusWound, bgnt = false, cover = false;

        // Hit
        var torrent = Has("Torrent");
        if (torrent)
            hit.Add(new(Die.Hit, ChipSide.Attacker, "torrent", "Torrent", "Hits automatically", ChipState.Auto));

        if (f.IsRanged && Has("Heavy"))
        {
            hit.Add(new(Die.Hit, ChipSide.Attacker, "heavy", "Heavy +1", "+1 to hit if the attacker remained stationary",
                s.Stationary ? ChipState.Auto : ChipState.Waiting));
            plusHit |= s.Stationary;
        }

        if (f.IsRanged && Has("Indirect Fire"))
        {
            hit.Add(new(Die.Hit, ChipSide.Attacker, "indirect", "Indirect −1", "−1 to hit and the target has cover if it is not visible",
                s.NotVisible ? ChipState.Auto : ChipState.Waiting));
            minusHit |= s.NotVisible;
            cover |= s.NotVisible;
        }

        if (f.IsRanged && f.DefenderHasStealth)
        {
            hit.Add(new(Die.Hit, ChipSide.Defender, "stealth", "Stealth −1", "−1 to hit against ranged attacks", ChipState.Auto));
            minusHit = true;
        }

        if (f.IsRanged && !Has("Pistol") && (AttackerIs("Vehicle") || AttackerIs("Monster")))
        {
            hit.Add(new(Die.Hit, ChipSide.Attacker, "bgnt", "Big Guns −1", "−1 to hit while within Engagement Range",
                s.InEngagementRange ? ChipState.Auto : ChipState.Waiting));
            bgnt = s.InEngagementRange;
        }

        hit.Add(Manual(Die.Hit, ChipSide.Attacker, "plusHit", "+1 Hit", s.PlusHit));
        hit.Add(Manual(Die.Hit, ChipSide.Defender, "minusHit", "−1 Hit", s.MinusHit));

        // Wound
        if (!f.IsRanged && Has("Lance"))
        {
            wound.Add(new(Die.Wound, ChipSide.Attacker, "lance", "Lance +1", "+1 to wound if the bearer charged this turn",
                s.Charged ? ChipState.Auto : ChipState.Waiting));
            plusWound |= s.Charged;
        }

        int? anti = null;
        foreach (var kw in f.WeaponKeywords)
        {
            var m = AntiPattern().Match(kw.Trim());
            if (!m.Success) continue;
            var target = m.Groups[1].Value.Trim();
            if (!f.DefenderKeywords.Any(d => string.Equals(d.Trim(), target, StringComparison.OrdinalIgnoreCase))) continue;
            var n = int.Parse(m.Groups[2].Value);
            if (anti is null || n < anti)
            {
                anti = n;
                wound.RemoveAll(c => c.Key == "anti");
                wound.Add(new(Die.Wound, ChipSide.Attacker, "anti", $"Anti-{target} {n}+",
                    $"Wounds on {n}+ against {target.ToUpperInvariant()}", ChipState.Auto));
            }
        }

        wound.Add(Manual(Die.Wound, ChipSide.Attacker, "plusWound", "+1 Wound", s.PlusWound));
        wound.Add(Manual(Die.Wound, ChipSide.Defender, "minusWound", "−1 Wound", s.MinusWound));

        // Save
        var ignoresCover = Has("Ignores Cover");
        cover |= s.Cover;
        var effectiveAp = f.Ap is { } ap ? (s.ReduceAp ? Math.Min(ap + 1, 0) : ap) : 0;
        var coverState =
            !f.IsRanged || ignoresCover                ? ChipState.Blocked :
            f.DefenderSave is <= 3 && effectiveAp == 0 ? ChipState.Blocked :
            s.Cover                                    ? ChipState.On :
            cover                                      ? ChipState.Auto :
                                                         ChipState.Off;
        var coverDetail =
            !f.IsRanged                     ? "Cover only applies against ranged attacks" :
            ignoresCover                    ? "This weapon ignores cover" :
            coverState == ChipState.Blocked ? "No cover bonus for Sv 3+ or better against AP 0" :
                                              "+1 to the armour save against ranged attacks";
        save.Add(new(Die.Save, ChipSide.Defender, "cover", "Cover", coverDetail, coverState));
        save.Add(Manual(Die.Save, ChipSide.Defender, "reduceAp", "AP −1", s.ReduceAp));
        if (f.DefenderInvuln is > 0 and var inv)
            save.Add(new(Die.Save, ChipSide.Defender, "invuln", $"Invuln {inv}+", "Invulnerable save from the datasheet", ChipState.Auto));

        var input = new WoundCalcInput(
            HitRoll:           torrent ? "AUTO" : f.Skill,
            WeaponStrength:    f.Strength,
            WeaponAp:          f.Ap,
            DefenderToughness: f.DefenderToughness,
            DefenderSave:      f.DefenderSave,
            PlusOneHit:        plusHit,
            PlusOneWound:      plusWound,
            BgntApplied:       bgnt,
            MinusOneHit:       minusHit,
            MinusOneWound:     s.MinusWound,
            ReduceAp:          s.ReduceAp,
            HasCover:          f.IsRanged && cover,
            InvulnSave:        f.DefenderInvuln ?? 0,
            IsRanged:          f.IsRanged,
            IgnoresCover:      ignoresCover);

        return new AttackResolution(input, [.. hit, .. wound, .. save], anti);
    }

    private static ModChip Manual(Die die, ChipSide side, string key, string label, bool on) =>
        new(die, side, key, label, "Manual modifier", on ? ChipState.On : ChipState.Off);
}
