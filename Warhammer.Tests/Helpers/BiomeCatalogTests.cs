using FluentAssertions;
using Warhammer.Helpers;

namespace Warhammer.Tests.Helpers;

public class BiomeCatalogTests
{
    [Fact]
    public void Has_the_nine_biomes_in_order() =>
        BiomeCatalog.All.Select(b => b.Key).Should().Equal("agri", "death", "ice", "ash", "desert", "jungle", "hive", "sept", "tomb");

    [Fact]
    public void Desert_defaults()
    {
        var d = BiomeCatalog.Get("desert");
        d.Label.Should().Be("Desert World");
        d.DefaultSeaLevel.Should().Be(0.08);
        d.DefaultRivers.Should().Be(2);
    }

    [Theory, InlineData(null), InlineData(""), InlineData("forge")]
    public void Unknown_key_falls_back_to_agri(string? key) => BiomeCatalog.Get(key).Key.Should().Be("agri");
}
