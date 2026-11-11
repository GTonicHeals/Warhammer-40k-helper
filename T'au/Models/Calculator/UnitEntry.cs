using Warhammer.Models.Crusade;

namespace Warhammer.Models.Calculator;

// One pickable unit in the Wound Calculator: a roster Selection tagged with its player,
// detachment and (when the player has an Order of Battle) the matching Crusade Card.
public record UnitEntry(string Key, string Label, string PlayerId, int PlayerIndex, Selection Unit,
    string DetachmentName, CrusadeUnitCard? CrusadeData = null);
