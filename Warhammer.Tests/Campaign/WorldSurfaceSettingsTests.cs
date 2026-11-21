using FluentAssertions;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class WorldSurfaceSettingsTests
{
    [Fact]
    public void From_copies_campaign_values()
    {
        var c = new Warhammer.Models.Campaign.Campaign { MapSeed = 7, MapBiome = "jungle", MapSeaLevel = 0.3, MapLandmass = "archipelago", MapRivers = 9 };
        var s = WorldSurfaceSettings.From(c);
        s.Seed.Should().Be(7);
        s.Biome.Should().Be("jungle");
        s.Landmass.Should().Be("archipelago");
        s.Rivers.Should().Be(9);
        s.SeaLevel.Should().Be(0.3);
    }

    [Fact]
    public void From_normalises_unknown_biome_and_landmass()
    {
        var s = WorldSurfaceSettings.From(new Warhammer.Models.Campaign.Campaign { MapBiome = "x", MapLandmass = "y" });
        s.Biome.Should().Be("agri");
        s.Landmass.Should().Be("continent");
    }

    [Fact]
    public void New_campaign_defaults()
    {
        var c = new Warhammer.Models.Campaign.Campaign();
        c.MapSeaLevel.Should().Be(0.35);
        c.MapLandmass.Should().Be("continent");
        c.MapRivers.Should().Be(4);
        c.MapRuggedness.Should().Be(0.5);
        c.MapFeatures.Should().Be(0.5);
    }
}
