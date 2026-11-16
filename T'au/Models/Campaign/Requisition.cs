namespace Warhammer.Models.Campaign;

// What the app does for the buyer when a Requisition is bought. None = the Game Master applies it by hand.
public enum RequisitionKind { None, IncreaseSupplyLimit, RepairAll, ReinforceAll }

// One entry on the Campaign's Requisition list, edited in the Campaign Editor.
// Stored as JSON inside Campaign.Requisitions.
public class Requisition
{
    public string Id     { get; set; } = Guid.NewGuid().ToString();
    public string Name   { get; set; } = "";
    public int    Cost   { get; set; }
    public string Timing { get; set; } = "";
    public string Effect { get; set; } = "";
    public RequisitionKind Kind { get; set; }
    // Points added to the Supply Limit; used by IncreaseSupplyLimit only.
    public int    Amount { get; set; }
}

// One Requisition an Order of Battle bought. It keeps the name and the cost paid, not a link
// to the list entry, so editing or deleting the entry never rewrites history.
public class RequisitionPurchase
{
    public DateTime DateUtc { get; set; }
    public string   Name    { get; set; } = "";
    public int      Cost    { get; set; }
}

public static class RequisitionDefaults
{
    // The list a Campaign starts with: the Game Master's three house Requisitions, then the
    // core ones as text only (effects from crusade_rules.json). Fresh Recruits is replaced by Reinforce.
    public static List<Requisition> Create() =>
    [
        new()
        {
            Name = "Increase Supply Limit", Cost = 1, Timing = "At any time",
            Effect = "Increase the Supply Limit by 50 points.",
            Kind = RequisitionKind.IncreaseSupplyLimit, Amount = 50
        },
        new()
        {
            Name = "Repair", Cost = 1, Timing = "After a battle",
            Effect = "Restore every lost wound on every unit in the Order of Battle.",
            Kind = RequisitionKind.RepairAll
        },
        new()
        {
            Name = "Reinforce", Cost = 1, Timing = "After a battle",
            Effect = "Restore every lost model on every unit in the Order of Battle.",
            Kind = RequisitionKind.ReinforceAll
        },
        new()
        {
            Name = "Renowned Heroes", Cost = 1,
            Timing = "When adding a CHARACTER unit or when a CHARACTER gains a rank",
            Effect = "Give a CHARACTER one Enhancement. Replaces a Battle Honour if used upon promotion. Cannot be used on EPIC HEROES, units with existing Enhancements, or units with 'Disgraced' or 'Mark of Shame' Scars. "
                   + "(Core cost: 1 RP + 1 RP for each other Enhancement on the Order of Battle.)"
        },
        new()
        {
            Name = "Legendary Veterans", Cost = 3,
            Timing = "When a non-CHARACTER unit reaches 30 XP",
            Effect = "Uncaps XP limit, allows promotion above Battle-hardened, and increases maximum Battle Honours for that unit to 6."
        },
        new()
        {
            Name = "Rearm and Resupply", Cost = 1, Timing = "Before a battle",
            Effect = "Change any wargear options on a unit's datasheet. Replaced Crusade Relics or Weapon Modifications are lost."
        },
        new()
        {
            Name = "Repair and Recuperate", Cost = 1, Timing = "After a battle",
            Effect = "Remove one Battle Scar from a unit. Increases the unit's Crusade Points by 1. "
                   + "(Core cost: 1 RP + 1 RP per Battle Honour the unit has.)"
        }
    ];
}
