using Warhammer.Models.Crusade;

namespace Warhammer.Models.Campaign;

public enum OutOfActionPenalty { None, BattleScar, DevastatingBlow }

// One unit of a Crusade Army in one Battle: mustered with CardId/Points, then filled in
// by the Post-battle Sequence. Stored as JSON inside BattleRecord.UnitResults.
public class BattleUnitResult
{
    public string CardId   { get; set; } = "";
    public string PlayerId { get; set; } = "";
    public string UnitName { get; set; } = "";
    public int    Points   { get; set; }

    public bool Destroyed           { get; set; }
    public int  EnemyUnitsDestroyed { get; set; }
    public bool MarkedForGreatness  { get; set; }

    // Out of Action test, typed in after the roll at the table. null = not taken.
    public bool?              OutOfActionPassed { get; set; }
    public OutOfActionPenalty Penalty           { get; set; }
    public AppliedTrait?      ScarTaken         { get; set; }
    public string?            HonourLost        { get; set; }

    public List<AppliedTrait> HonoursGained    { get; set; } = new();
    public int                XpGained         { get; set; }
    public bool               RemovedFromOrder { get; set; }

    // Attrition: the unit's condition at the end of this Battle. null = not entered, card unchanged.
    public int? ModelsLostAfter { get; set; }
    public int? WoundsLostAfter { get; set; }
}
