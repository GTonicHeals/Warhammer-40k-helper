using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warhammer.Models.Campaign;
using Warhammer.Tests.TestInfrastructure;
// Alias because the enclosing namespace Warhammer.Tests.Campaign shadows the
// type Warhammer.Models.Campaign.Campaign.
using CampaignEntity = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Campaign;

// Verifies the SQLite-specific JSON column conversion for TerritoryNode.ConnectedNodeIds.
// Postgres mapped List<string> -> text[] natively; SQLite has no array type, so we serialise
// to a JSON text column via HasConversion in AppDbContext. These tests guard that workaround
// against regression — without them, a broken converter would fail silently in production.
public class TerritoryNodeTests : SqliteTestBase
{
    [Fact]
    public async Task ConnectedNodeIds_roundtrips_through_json_conversion()
    {
        const string nodeId = "hub";
        var campaign = new CampaignEntity
        {
            MapNodes =
            {
                new TerritoryNode
                {
                    Id               = nodeId,
                    Name             = "Hub",
                    ConnectedNodeIds = { "NodeA", "NodeB", "NodeC" }
                }
            }
        };

        await using (var ctx = CreateContext())
        {
            ctx.Campaigns.Add(campaign);
            await ctx.SaveChangesAsync();
        }

        // Read in a separate context so the assertion can't be satisfied by a cached
        // tracked instance — ChangeTracker.Clear on the same context would also work,
        // but a fresh context is the stronger guarantee.
        await using (var ctx = CreateContext())
        {
            ctx.ChangeTracker.Clear();

            var loaded = await ctx.TerritoryNodes.SingleAsync(n => n.Id == nodeId);

            loaded.ConnectedNodeIds.Should().HaveCount(3);
            loaded.ConnectedNodeIds.Should().BeEquivalentTo(new[] { "NodeA", "NodeB", "NodeC" });
        }
    }

    [Fact]
    public async Task ConnectedNodeIds_mutation_persists_through_value_comparer()
    {
        const string nodeId = "hub";
        var campaign = new CampaignEntity
        {
            MapNodes =
            {
                new TerritoryNode
                {
                    Id               = nodeId,
                    Name             = "Hub",
                    ConnectedNodeIds = { "Alpha", "Beta" }
                }
            }
        };

        await using (var ctx = CreateContext())
        {
            ctx.Campaigns.Add(campaign);
            await ctx.SaveChangesAsync();
        }

        // Mutate the list in place — this is the case a missing ValueComparer would
        // silently miss. EF would treat the reference as unchanged and skip the UPDATE.
        await using (var ctx = CreateContext())
        {
            var node = await ctx.TerritoryNodes.SingleAsync(n => n.Id == nodeId);
            node.ConnectedNodeIds.Add("Gamma");
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext())
        {
            var reloaded = await ctx.TerritoryNodes.AsNoTracking().SingleAsync(n => n.Id == nodeId);
            reloaded.ConnectedNodeIds.Should().BeEquivalentTo(new[] { "Alpha", "Beta", "Gamma" });
        }
    }
}
