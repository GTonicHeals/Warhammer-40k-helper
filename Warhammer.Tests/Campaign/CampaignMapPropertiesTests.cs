using FluentAssertions;
using Warhammer.Models.Campaign;
// Alias to disambiguate the type Warhammer.Models.Campaign.Campaign from the
// enclosing test namespace Warhammer.Tests.Campaign, whose last segment shadows
// the type lookup. Using a distinct alias name (CampaignEntity) avoids the
// alias/namespace collision that a same-named alias would create.
using CampaignEntity = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Campaign;

// Pure-CLR invariants of the Campaign aggregate's map-config defaults.
// Persistence round-trips are covered by CampaignManagerServiceTests.
public class CampaignMapPropertiesTests
{
    [Fact]
    public void MapSeed_defaults_to_positive_nonzero_int()
    {
        new CampaignEntity().MapSeed.Should().BePositive();
    }

    [Fact]
    public void MapBiome_defaults_to_agri()
    {
        new CampaignEntity().MapBiome.Should().Be("agri");
    }

    [Fact]
    public void RegenerateMap_new_seed_is_positive_and_in_range()
    {
        var c = new CampaignEntity { MapSeed = 0 };
        c.MapSeed = Random.Shared.Next(1, 999_999);
        c.MapSeed.Should().BeInRange(1, 999_998);
    }

    [Fact]
    public void RegenerateMap_repeated_calls_produce_distinct_seeds()
    {
        var seeds = Enumerable.Range(0, 20)
            .Select(_ => Random.Shared.Next(1, 999_999))
            .ToList();

        seeds.Distinct().Should().HaveCountGreaterThan(1,
            "twenty independently generated seeds should not all be identical");
    }
}
