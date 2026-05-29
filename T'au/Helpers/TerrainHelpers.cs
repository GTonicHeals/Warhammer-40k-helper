using Warhammer.Models.Campaign;

namespace Warhammer.Helpers;

public static class TerrainHelpers
{
    public static string Icon(TerrainType t) => t switch
    {
        TerrainType.Standard        => "bi-hexagon",
        TerrainType.Ruins           => "bi-building-slash",
        TerrainType.HiveCity        => "bi-buildings-fill",
        TerrainType.ForgeWorld      => "bi-gear-wide-connected",
        TerrainType.Plain           => "bi-align-bottom",
        TerrainType.Forest          => "bi-tree-fill",
        TerrainType.City            => "bi-building-fill",
        TerrainType.PowerPlant      => "bi-lightning-charge-fill",
        TerrainType.WeaponStockpile => "bi-box-seam-fill",
        TerrainType.MilitaryCamp    => "bi-flag-fill",
        TerrainType.OrbitalDefence  => "bi-rocket-fill",
        _                           => "bi-question-circle"
    };

    public static string Label(TerrainType t) => t switch
    {
        TerrainType.Standard        => "Standard",
        TerrainType.Ruins           => "Ruins",
        TerrainType.HiveCity        => "Hive City",
        TerrainType.ForgeWorld      => "Forge World",
        TerrainType.Plain           => "Plain",
        TerrainType.Forest          => "Forest",
        TerrainType.City            => "City",
        TerrainType.PowerPlant      => "Power Plant",
        TerrainType.WeaponStockpile => "Stockpile",
        TerrainType.MilitaryCamp    => "Mil. Camp",
        TerrainType.OrbitalDefence  => "Orbital Defence",
        _                           => t.ToString()
    };
}
