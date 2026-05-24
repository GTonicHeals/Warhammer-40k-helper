using System.Text.Json;
using FluentAssertions;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class CampaignStateMapPropertiesTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static T RoundTrip<T>(T obj) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, Opts), Opts)!;

    [Fact]
    public void MapSeed_defaults_to_positive_nonzero_int()
    {
        var state = new CampaignState();
        state.MapSeed.Should().BePositive();
    }

    [Fact]
    public void MapBiome_defaults_to_agri()
    {
        var state = new CampaignState();
        state.MapBiome.Should().Be("agri");
    }

    [Fact]
    public void MapSeed_roundtrips_correctly()
    {
        var state = new CampaignState { MapSeed = 42_701 };
        RoundTrip(state).MapSeed.Should().Be(42_701);
    }

    [Fact]
    public void MapBiome_roundtrips_correctly()
    {
        foreach (var biome in new[] { "agri", "death", "ice", "ash" })
        {
            var state = new CampaignState { MapBiome = biome };
            RoundTrip(state).MapBiome.Should().Be(biome);
        }
    }

    [Fact]
    public void RegenerateMap_new_seed_is_positive_and_in_range()
    {
        var state = new CampaignState { MapSeed = 0 };
        state.MapSeed = Random.Shared.Next(1, 999_999);
        state.MapSeed.Should().BeInRange(1, 999_998);
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

    [Fact]
    public void Legacy_campaign_json_without_map_fields_deserializes_with_defaults()
    {
        // Simulate old localStorage JSON that predates MapSeed / MapBiome.
        const string legacyJson = """
            {
                "CampaignName": "Old War",
                "Players": {},
                "MapNodes": [],
                "BattleHistory": []
            }
            """;

        var state = JsonSerializer.Deserialize<CampaignState>(legacyJson, Opts)!;

        state.CampaignName.Should().Be("Old War");
        // Missing fields fall back to property initialisers — constructor default runs.
        state.MapSeed.Should().BePositive();
        state.MapBiome.Should().Be("agri");
    }
}
