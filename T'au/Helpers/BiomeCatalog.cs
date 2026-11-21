namespace Warhammer.Helpers;

public sealed record BiomeInfo(string Key, string Label, double DefaultSeaLevel, int DefaultRivers);

/// <summary>The Biomes a World Surface can take. Unknown keys fall back to agri.</summary>
public static class BiomeCatalog
{
    public static IReadOnlyList<BiomeInfo> All { get; } = new BiomeInfo[]
    {
        new("agri",   "Agri World",   0.40, 5),
        new("death",  "Death World",  0.30, 4),
        new("ice",    "Ice World",    0.45, 3),
        new("ash",    "Ash Wastes",   0.20, 3),
        new("desert", "Desert World", 0.08, 2),
        new("jungle", "Jungle World", 0.30, 9),
        new("hive",   "Hive World",   0.25, 2),
        new("sept",   "Sept World",   0.35, 4),
        new("tomb",   "Tomb World",   0.15, 0),
    };

    public static BiomeInfo Get(string? key) =>
        All.FirstOrDefault(b => b.Key == key) ?? All[0];
}
