namespace Warhammer.Models.Campaign;

public class CampaignPlayer
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int VictoryPoints { get; set; }
    public int CampaignPoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}
