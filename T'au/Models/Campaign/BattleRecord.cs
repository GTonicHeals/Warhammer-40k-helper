namespace Warhammer.Models.Campaign;

public class BattleRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime DatePlayed { get; set; } = DateTime.UtcNow;
    public string MissionName { get; set; } = "";
    public string WinnerPlayerId { get; set; } = "";
    public int Player1Vp { get; set; }
    public int Player2Vp { get; set; }
    public string? CapturedTerritoryId { get; set; }
    public string Notes { get; set; } = "";
}
