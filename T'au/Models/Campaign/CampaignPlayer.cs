using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

public class CampaignPlayer
{
    [Key]
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
