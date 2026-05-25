using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

public class TerritoryNode
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public string  Name                { get; set; } = "";
    public string  Description         { get; set; } = "";
    public string  BuffEffect          { get; set; } = "";
    public string? ControllingPlayerId { get; set; }

    // Persisted as a JSON text column — see HasConversion in AppDbContext.OnModelCreating.
    public List<string> ConnectedNodeIds { get; set; } = new();

    public string SecretEventText { get; set; } = "";
    public bool   IsEventRevealed { get; set; }

    // Normalised map coordinates [0,1]. -1 = unplaced (auto-grid on first render).
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
}
