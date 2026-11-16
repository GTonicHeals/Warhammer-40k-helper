# Amend a Completed Battle — design (short)

## Why
The GM sometimes forgets to pick the captured Territory, or enters the wrong unit damage, during the
Post-battle Sequence. Today a Completed Battle can only be deleted; the map and cards can't be corrected
through the Battle itself.

## Goals
- Change a Completed Battle's captured Territory, and have the map follow.
- Change each unit's damage (models lost / wounds lost), and have the Crusade Cards follow.

## Non-goals
- Changing the winner, score, mission, notes, XP, honours, scars, Out of Action results or RP.
- Editing Mustered (unfinished) Battles — they already use the Post-battle page.

## Anti-goals
- Undoing a Repair or Reinforce bought since the Battle. Damage edits apply only the difference.
- Silently changing anything the GM didn't touch on the amend page.

## Approaches considered
1. **Separate "Amend battle" page** (picked) — pencil button on DONE rows in the War Room Engagement Log,
   opens `battle/{id}/amend`. Room for a long unit list; reuses the Post-battle damage steppers.
2. Inline edit inside the Engagement Log row — quick for territory, too cramped for a dozen units.
3. Reopen the Post-battle page in an edit mode — mostly locked fields (XP, honours, Out of Action);
   confusing about what actually changes.

## Behaviour
**Territory**
- Dropdown of Territories, same as the Post-battle one. Hidden when the Battle was a draw (no winner).
- Saving a new pick: the Battle's `CapturedTerritoryId` is updated, the new Territory's controller becomes
  the winner, and Territories revealed by that capture are revealed (`TerritoryReveal.RevealedByCapture`).
- A previously picked Territory is left as it is: the controller before the Battle was never stored, so it
  can't be restored. The page says so; the GM can fix it in the Campaign Editor.
- Clearing the pick only clears the Battle's record; no Territory changes.

**Unit damage**
- Each unit's starting value on the page = the Battle's stored "after" value, or the card's current value
  if none was entered.
- On save, the card's models/wounds lost change by (new − starting value), clamped to the unit's valid
  range (`UnitStrength`). The Battle stores the new value.
- Units whose card no longer exists, or that were removed from the Order of Battle, are shown read-only.
- Damage beyond what the unit can take is rejected before anything is saved (as Post-battle does today).

**Saving**
- Only fields the GM changed are applied. One Save button; nothing is applied until it is pressed.
- Logic lives in a service method (alongside `PostBattleService`), validated before anything is mutated.

## Left to the builder
- Exact layout/styling of the amend page (follow the Post-battle page).
- Test coverage: service tests for territory change, reveal, damage delta with an intervening repair,
  clamping; a component test for the page.
