using System.Text.Json.Serialization;

namespace Warhammer.Models.Crusade;

public class OrderOfBattle
{
    public const int RequisitionPointsMax = 10;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ArmyName { get; set; } = "";
    public string Faction { get; set; } = "";
    public string DetachmentName { get; set; } = "";
    public string PlayerId { get; set; } = "";   // FK to RosterLoadService player IDs (p1, p2, ...); empty = legacy crusade
    public int SupplyLimit { get; set; } = 1000;
    public int RequisitionPoints { get; set; } = 5;
    public List<CrusadeUnitCard> Units { get; set; } = new();
    public List<CrusadeTrait> CustomTraits { get; set; } = new(); // user-added relics & traits scoped to this crusade
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public int SupplyUsed => Units.Sum(u => u.TotalPoints);

    [JsonIgnore]
    public int SupplyRemaining => SupplyLimit - SupplyUsed;
}
