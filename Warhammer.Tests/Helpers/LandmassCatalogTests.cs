using FluentAssertions;
using Warhammer.Helpers;

namespace Warhammer.Tests.Helpers;

public class LandmassCatalogTests
{
    [Fact]
    public void Lists_the_four_landmasses_in_order()
    {
        LandmassCatalog.All.Select(l => l.Key).Should().Equal("continent", "archipelago", "pangea", "random");
        LandmassCatalog.All.Select(l => l.Label).Should().Equal("Continent", "Archipelago", "Pangea", "Random");
    }

    [Fact]
    public void Unknown_key_falls_back_to_continent() =>
        LandmassCatalog.Get("nope").Key.Should().Be("continent");
}
