using Warhammer.Helpers;

namespace Warhammer.Models.Crusade;

// Attrition on a Crusade Card: the limits, the wording and the roster lookup, in one place
// so every page agrees. A size of 0 means unknown: no upper limit is enforced.
public static class UnitStrength
{
    // A unit never drops to zero models between Battles; losing the last one is being destroyed.
    public static int? MaxModelsLost(CrusadeUnitCard c) => c.ModelCount <= 0 ? null : c.ModelCount - 1;

    // The damaged model is still alive, so it is missing fewer wounds than it has.
    public static int? MaxWoundsLost(CrusadeUnitCard c) => c.WoundsPerModel <= 0 ? null : c.WoundsPerModel - 1;

    public static bool InRange(CrusadeUnitCard c, int modelsLost, int woundsLost) =>
        modelsLost >= 0 && woundsLost >= 0
        && (MaxModelsLost(c) is not { } maxModels || modelsLost <= maxModels)
        && (MaxWoundsLost(c) is not { } maxWounds || woundsLost <= maxWounds);

    public static void Clamp(CrusadeUnitCard c)
    {
        c.ModelCount     = Math.Max(0, c.ModelCount);
        c.WoundsPerModel = Math.Max(0, c.WoundsPerModel);
        c.ModelsLost = Math.Clamp(c.ModelsLost, 0, MaxModelsLost(c) ?? int.MaxValue);
        c.WoundsLost = Math.Clamp(c.WoundsLost, 0, MaxWoundsLost(c) ?? int.MaxValue);
    }

    public static string Describe(CrusadeUnitCard c) => Describe(c, c.ModelsLost, c.WoundsLost);

    // "" when undamaged, otherwise e.g. "7/10 models · 1 wound lost".
    public static string Describe(CrusadeUnitCard c, int modelsLost, int woundsLost)
    {
        var parts = new List<string>(2);
        if (modelsLost > 0)
            parts.Add(c.ModelCount > 0
                ? $"{Math.Max(0, c.ModelCount - modelsLost)}/{c.ModelCount} models"
                : $"{modelsLost} {(modelsLost == 1 ? "model" : "models")} lost");
        if (woundsLost > 0)
            parts.Add($"{woundsLost} {(woundsLost == 1 ? "wound" : "wounds")} lost");
        return string.Join(" · ", parts);
    }

    // Unit size and wounds per model from a roster selection; 0 for whatever cannot be told.
    // Wounds come from the largest group of models (the first one on a tie).
    public static (int ModelCount, int WoundsPerModel) FromRoster(Selection? unit)
    {
        if (unit is null) return (0, 0);
        var groups = unit.Type == "model" ? [unit] : ModelGroups(unit).ToList();
        if (groups.Count == 0) return (0, 0);

        var count = groups.Sum(g => Math.Max(0, g.Number));
        var largest = groups.MaxBy(g => g.Number)!;
        var w = RosterHelper.GetChar(RosterHelper.GetAllProfilesRecursive(largest, "Unit").FirstOrDefault(), "W");
        return (count, int.TryParse(w, out var wounds) && wounds > 0 ? wounds : 0);
    }

    private static IEnumerable<Selection> ModelGroups(Selection s) =>
        s.Selections.SelectMany(c => c.Type == "model" ? [c] : ModelGroups(c));
}
