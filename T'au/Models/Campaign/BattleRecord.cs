using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Warhammer.Models.Campaign;

// Mustered: armies picked, battle not yet resolved. Completed: Post-battle Sequence applied (never twice).
public enum BattleStatus { Mustered, Completed }

public class BattleRecord
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public int CampaignId { get; set; }

    [ForeignKey(nameof(CampaignId))]
    public Campaign? Campaign { get; set; }

    public DateTime DatePlayed          { get; set; } = DateTime.UtcNow;
    public string   MissionName         { get; set; } = "";
    public string   WinnerPlayerId      { get; set; } = "";
    public int      Player1Vp           { get; set; }
    public int      Player2Vp           { get; set; }
    public string?  CapturedTerritoryId { get; set; }
    public string   Notes               { get; set; } = "";

    public BattleStatus Status          { get; set; }
    public int      PointsLimit         { get; set; }
    public string   Player1Id           { get; set; } = "p1";
    public string   Player2Id           { get; set; } = "p2";
    public string?  Player1OrderId      { get; set; }
    public string?  Player2OrderId      { get; set; }
    public List<BattleUnitResult> UnitResults { get; set; } = new();
}
