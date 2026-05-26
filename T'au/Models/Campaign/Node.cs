using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

public abstract class Node
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public bool   IsPublic    { get; set; }

    // Persisted as JSON text — see HasConversion in AppDbContext.OnModelCreating.
    public List<string> ConnectedNodeIds { get; set; } = new();

    // Normalised map coordinates [0,1]. -1 = unplaced (auto-grid on first render).
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
}
