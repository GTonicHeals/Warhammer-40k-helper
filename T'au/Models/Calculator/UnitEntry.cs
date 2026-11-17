using Warhammer.Models.Crusade;

namespace Warhammer.Models.Calculator;

// One pickable unit in the Wound Calculator: a roster Selection tagged with its roster seat
// (PlayerId, which drives the colour), the picker group it is listed under, its detachment
// and (when it comes from an Order of Battle) the Crusade Card.
public record UnitEntry(string Key, string Label, string PlayerId, UnitGroup Group, Selection Unit,
    string DetachmentName, CrusadeUnitCard? CrusadeData = null);

// A heading in the unit picker. Crusade armies get one group each (players first, then the
// Game Master's armies); plain roster files follow. Badge is the short tag on the datasheet band.
public record UnitGroup(string Id, string Name, string Badge, int Order, string? Tag = null);
