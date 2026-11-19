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

    // Table Mode: the line read aloud at the table, and a part players never see,
    // even once the note is public.
    public string Summary { get; set; } = "";
    public string Secret  { get; set; } = "";

    // The map location this note is about (a Node Id); null when it is about nothing on the map.
    public string? NodeId { get; set; }

    // Starred notes form the short list for tonight's session.
    public bool IsStarred { get; set; }

    // Jotted down mid-session with Quick note; filed out of the Inbox after the session.
    public bool IsInbox { get; set; }

    // When the note was last revealed to the players; null while it is GM-only.
    public DateTime? RevealedAt { get; set; }
}
