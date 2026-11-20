using Warhammer.Helpers;

namespace Warhammer.Models.Campaign;

/// <summary>Everything the JS World Surface builder needs; serialises camelCase over JS interop.</summary>
public sealed record WorldSurfaceSettings(
    int Seed, string Biome, double SeaLevel, string Landmass, int Rivers,
    double Ruggedness, double Features,
    int Octaves, double Lacunarity, double Persistence, double Scale,
    double OffsetX, double OffsetY)
{
    private static readonly string[] LandmassKeys = { "continent", "archipelago", "pangea", "random" };

    public static WorldSurfaceSettings From(Campaign c) => new(
        c.MapSeed,
        BiomeCatalog.Get(c.MapBiome).Key,
        c.MapSeaLevel,
        LandmassKeys.Contains(c.MapLandmass) ? c.MapLandmass : "continent",
        c.MapRivers,
        c.MapRuggedness,
        c.MapFeatures,
        c.MapOctaves,
        c.MapLacunarity,
        c.MapPersistence,
        c.MapScale,
        c.MapOffsetX,
        c.MapOffsetY);
}
