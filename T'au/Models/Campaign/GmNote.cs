using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

public class GmNote
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public string   Title     { get; set; } = "New Note";
    public string   Content   { get; set; } = "";
    public string   Tags      { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // X/Y >= 0 means this note is pinned to the map (currently also used as the
    // "MapNote" concept). Step 2+ may split this into a dedicated MapNote entity.
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;

    public bool IsPublic { get; set; }
}
