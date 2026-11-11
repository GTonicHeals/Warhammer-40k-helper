using Warhammer.Services;

namespace Warhammer.Models.Calculator;

public enum Die { Hit, Wound, Save }
public enum ChipSide { Attacker, Defender }

// Off/On: manual toggle. Auto: derived from the datasheet and applied.
// Waiting: derived, but needs a Situation Toggle before it applies. Blocked: cannot apply.
public enum ChipState { Off, On, Auto, Waiting, Blocked }

// Key: torrent, heavy, lance, indirect, anti, stealth, bgnt, plusHit, plusWound,
//      minusHit, minusWound, reduceAp, cover, invuln
public record ModChip(Die Die, ChipSide Side, string Key, string Label, string Detail, ChipState State);

// Everything the datasheets say about one attack, after Crusade honour modifiers.
public record AttackFacts(
    string? Skill, int? Strength, int? Ap, bool IsRanged, IReadOnlyList<string> WeaponKeywords,
    IReadOnlyList<string> AttackerKeywords, IReadOnlyList<string> DefenderKeywords,
    bool DefenderHasStealth, int? DefenderToughness, int? DefenderSave, int? DefenderInvuln);

// Situation Toggles (facts the datasheet cannot know) and the manual modifiers.
public record Situation(bool Stationary, bool Charged, bool NotVisible, bool Cover, bool InEngagementRange,
    bool PlusHit, bool PlusWound, bool MinusHit, bool MinusWound, bool ReduceAp)
{
    public static readonly Situation None = new(false, false, false, false, false, false, false, false, false, false);
}

// AntiThreshold: when set, the wound roll needed is at most this value (Anti-X N+).
public record AttackResolution(WoundCalcInput Input, IReadOnlyList<ModChip> Chips, int? AntiThreshold);
