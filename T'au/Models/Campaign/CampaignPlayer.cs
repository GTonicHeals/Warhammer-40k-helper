using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

public class CampaignPlayer
{
    // The seat ("p1", "p2") — unique within a campaign, not across campaigns. The key is
    // (CampaignId, Id); see AppDbContext.
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public string Name           { get; set; } = "";
    public int    VictoryPoints  { get; set; }
    public int    CampaignPoints { get; set; }
    public int    Wins           { get; set; }
    public int    Losses         { get; set; }
}
