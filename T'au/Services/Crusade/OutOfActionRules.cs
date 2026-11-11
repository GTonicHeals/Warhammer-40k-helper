using Warhammer.Models.Crusade;

namespace Warhammer.Services.Crusade;

public record OutOfActionOptions(bool CanTakeScar, bool CanTakeDevastatingBlow, bool MustTakeDevastatingBlow, bool WillBeRemoved);

// What a destroyed unit that failed its Out of Action test may suffer.
//   Battle Scar      — only while it has fewer than three scars
//   Devastating Blow — lose one Battle Honour; only possible when it has one
//   Three scars      — Devastating Blow is mandatory; with no honour left the unit is struck off
public static class OutOfActionRules
{
    public const int MaxScars = 3;

    public static OutOfActionOptions OptionsFor(CrusadeUnitCard card)
    {
        var atMaxScars = card.BattleScars.Count >= MaxScars;
        var hasHonour  = card.BattleHonours.Count > 0;
        return new OutOfActionOptions(
            CanTakeScar:             !atMaxScars,
            CanTakeDevastatingBlow:  hasHonour,
            MustTakeDevastatingBlow: atMaxScars,
            WillBeRemoved:           atMaxScars && !hasHonour);
    }
}
