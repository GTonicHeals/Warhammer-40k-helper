namespace Warhammer.Models.Campaign;

// One row of the Campaigns page: enough to choose a campaign without loading its graph.
public record CampaignSummary(
    int       Id,
    string    Name,
    int       PlayerCount,
    int       BattleCount,
    DateTime? LastOpenedUtc,
    bool      IsCurrent);
