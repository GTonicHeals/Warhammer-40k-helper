namespace Warhammer.Models.Campaign;

public class CampaignEvent : Node
{
    public string  SecretEventText  { get; set; } = "";
    public bool    IsEventRevealed  { get; set; }
    public string? AttachedToNodeId { get; set; }
}
