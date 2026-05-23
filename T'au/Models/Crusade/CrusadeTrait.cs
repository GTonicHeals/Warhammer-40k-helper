using System.Text.Json.Serialization;

namespace Warhammer.Models.Crusade;

public static class CrusadeTraitTypes
{
    public const string BattleTrait = "Battle Trait";
    public const string WeaponModification = "Weapon Modification";
    public const string CrusadeRelic = "Crusade Relic";
    public const string BattleScar = "Battle Scar";

    public static readonly string[] HonourTypes =
        [BattleTrait, WeaponModification, CrusadeRelic];
}

public class CrusadeTrait
{
    // Top-level grouping: Battle Trait / Weapon Modification / Crusade Relic / Battle Scar.
    // For static traits, set by CrusadeTraitsService based on the source JSON array.
    [JsonPropertyName("type")] public string Type { get; set; } = "";

    // Sub-classifier from the data: relic tier (Artificer/Antiquity/Legendary),
    // battle-trait restriction (Character/Infantry/Mounted/Vehicle), or echo of the type.
    [JsonPropertyName("category")] public string Category { get; set; } = "";

    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("effect")] public string Effect { get; set; } = "";

    // Optional machine-readable encoding so the WoundCalculator can apply the trait
    // automatically. Multi-effect traits whose math doesn't fit one entry omit this
    // field; their text effect still surfaces in the tooltip/modal.
    [JsonPropertyName("machineModifiers")] public MachineModifier? Modifier { get; set; }

    // How the trait's modifier applies to weapons. Drives whether the Add-Honour
    // UI shows a "which weapon?" picker, and the WoundCalculator's per-weapon filter.
    //   "weapon"        — pick one weapon (any type); applies to that weapon only
    //   "weapon-ranged" — pick one ranged weapon
    //   "weapon-melee"  — pick one melee weapon
    //   "ranged"        — applies to every ranged weapon of the unit; no picker
    //   "melee"         — applies to every melee weapon of the unit; no picker
    //   null/omitted    — unit-wide (defender stat, ability, etc.); no weapon filter
    [JsonPropertyName("weaponScope")] public string? WeaponScope { get; set; }
}

// One stat or keyword tweak applied by a Battle Honour or Battle Scar.
// Stat names follow the calculator's vocabulary: S, AP, A, D, BS, WS (attacker side);
// T, Sv, Invuln, W, Move, OC (defender side).
// Use Value for an additive delta (Brutal: S +1). Use SetTo for an override that
// takes the better value (Artificer Armour: Sv → 2).
public class MachineModifier
{
    [JsonPropertyName("stat")] public string? Stat { get; set; }
    [JsonPropertyName("value")] public int? Value { get; set; }
    [JsonPropertyName("setTo")] public int? SetTo { get; set; }
    [JsonPropertyName("keyword")] public string? Keyword { get; set; }
}

public class CrusadeTraitsFile
{
    [JsonPropertyName("weaponModifications")] public List<CrusadeTrait> WeaponModifications { get; set; } = new();
    [JsonPropertyName("battleTraits")] public List<CrusadeTrait> BattleTraits { get; set; } = new();
    [JsonPropertyName("crusadeRelics")] public List<CrusadeTrait> CrusadeRelics { get; set; } = new();
    [JsonPropertyName("battleScars")] public List<CrusadeTrait> BattleScars { get; set; } = new();
}
