using System.Text.Json;
using Warhammer.Models.Calculator;

namespace Warhammer.Services;

public class WoundCalcStateService
{
    public string? AttackerFilter { get; set; }
    public string? DefenderFilter { get; set; }
    public string? AttackerUnitKey { get; set; }
    public string? DefenderUnitKey { get; set; }
    public string? WeaponName { get; set; }

    // Situation Toggles and manual modifiers, and the invulnerable save in force for the defender.
    public Situation? Situation { get; set; }
    public int? Invuln { get; set; }

    public bool IsEmpty => AttackerUnitKey is null && DefenderUnitKey is null
                           && AttackerFilter is null && DefenderFilter is null;

    public void Clear()
    {
        AttackerFilter = null;
        DefenderFilter = null;
        AttackerUnitKey = null;
        DefenderUnitKey = null;
        WeaponName = null;
        Situation = null;
        Invuln = null;
    }

    // Browser-storage copy, so the selection survives a reload or a dropped circuit.
    private record Snapshot(string? AttackerFilter, string? DefenderFilter, string? AttackerUnitKey,
        string? DefenderUnitKey, string? WeaponName, Situation? Situation, int? Invuln);

    public string ToJson() => JsonSerializer.Serialize(new Snapshot(
        AttackerFilter, DefenderFilter, AttackerUnitKey, DefenderUnitKey, WeaponName, Situation, Invuln));

    public bool TryLoadJson(string json)
    {
        try
        {
            var snap = JsonSerializer.Deserialize<Snapshot>(json);
            if (snap is null) return false;
            AttackerFilter = snap.AttackerFilter;
            DefenderFilter = snap.DefenderFilter;
            AttackerUnitKey = snap.AttackerUnitKey;
            DefenderUnitKey = snap.DefenderUnitKey;
            WeaponName = snap.WeaponName;
            Situation = snap.Situation;
            Invuln = snap.Invuln;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
