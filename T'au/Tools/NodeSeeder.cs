using Warhammer.Models.Campaign;
using Warhammer.Services;

namespace Warhammer.Tools;

// Dev-only seeder. Inserts a curated batch of Territory and CampaignEvent nodes
// against the active campaign.
// Idempotent on Name — existing nodes are skipped, so running twice is harmless.
//
// Invoked via: dotnet run --project T'au -- seed nodes
public sealed class NodeSeeder(
    CampaignManagerService          campaigns,
    ILogger<NodeSeeder>             logger)
{
    public async Task<(int Added, int Skipped, int CampaignId)> SeedAsync(CancellationToken ct = default)
    {
        var campaign = await campaigns.LoadCampaignAsync();

        var existingNames = campaign.MapNodes
            .Select(n => n.Name)
            .ToHashSet(StringComparer.Ordinal);

        int added = 0, skipped = 0;
        foreach (var node in DefaultNodes())
        {
            if (existingNames.Contains(node.Name)) { skipped++; continue; }
            campaign.MapNodes.Add(node);
            added++;
        }

        if (added > 0)
            await campaigns.SaveCampaignAsync(campaign);

        logger.LogInformation(
            "NodeSeeder finished: added {Added}, skipped {Skipped} for Campaign Id={CampaignId}",
            added, skipped, campaign.Id);

        return (added, skipped, campaign.Id);
    }

    private static IEnumerable<Node> DefaultNodes() =>
    [
        // ── Territories ────────────────────────────────────────────────────────
        //new Territory
        //{
        //    Name              = "Manufactorium Secundus",
        //    Description       = "A sprawling forge complex, half-buried under centuries of slag and promethium runoff.",
        //    BuffEffect        = "Controlling player gains +1 Reinforcement Point per battle fought here.",
        //    VictoryPointValue = 2,
        //    TerrainType       = TerrainType.ForgeWorld,
        //    X = 0.18, Y = 0.22
        //}
    ];
}
