namespace Warhammer.Helpers;

public sealed record LandmassInfo(string Key, string Label);

/// <summary>The Landmass shapes a World Surface can take. Unknown keys fall back to continent.</summary>
public static class LandmassCatalog
{
    public static IReadOnlyList<LandmassInfo> All { get; } = new LandmassInfo[]
    {
        new("continent",   "Continent"),
        new("archipelago", "Archipelago"),
        new("pangea",      "Pangea"),
        new("random",      "Random"),
    };

    public static LandmassInfo Get(string? key) =>
        All.FirstOrDefault(l => l.Key == key) ?? All[0];
}
