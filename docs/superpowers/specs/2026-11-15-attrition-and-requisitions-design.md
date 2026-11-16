# Attrition and Requisitions: Design

Agreed 2026-11-15. Vocabulary is in `GLOSSARY.md`. Builds on `2026-11-10-crusade-campaign-and-10th-rules-spec.md` (Muster and the Post-battle Sequence already exist).

Lines marked **(call)** were decided by the agent, not the Game Master, and are open to correction.

## Intent

The Game Master wants attrition to matter across a Crusade Campaign. Today every unit starts each Battle fresh, as the 10th edition Crusade rules say. This is a house rule: models killed and wounds lost in one Battle stay lost in the next, until the player pays Requisition Points to undo them. Requisitions become the lever that makes this a decision, so the Game Master also needs to control what Requisitions exist and what they cost, and to hand out Requisition Points by judgement after each Battle.

## Goals

1. After a Battle, each Crusade Card remembers how many models it has lost and how many wounds its damaged model is missing.
2. That damage is visible wherever the unit is picked or reviewed: Muster and the Order of Battle page.
3. The Campaign has a Requisition list the Game Master edits in the Campaign Editor.
4. An Order of Battle can buy a Requisition: Requisition Points are deducted, any built-in effect is applied, and the purchase is logged.
5. The Game Master types the Requisition Points each side earns from a Battle.

## Non-goals

- Tracking each model individually (per-model wounds, which sergeant died).
- Feeding damage in from the Roster page's in-memory wound counter or the Wound Calculator.
- Reducing a unit's points when it is under strength. **(call)** It costs full points at Muster.
- Automatic healing between Battles.
- Agendas, faction Requisitions, automating the text-only core Requisitions.

## Anti-goals

- Slowing the Post-battle Sequence down. Entering damage must be a couple of taps per damaged unit and zero taps for an untouched one.
- Losing or resetting live data in `T'au/campaign.db`.
- The app refusing something the Game Master wants to do. Damage numbers and Requisition Points stay hand-editable everywhere.

## Constraints

- .NET 10, Blazor Interactive Server, EF Core 10 with SQLite; tests in `Warhammer.Tests` (xUnit, FluentAssertions, bUnit, Moq).
- `T'au/campaign.db` is tracked in git and holds live data. Schema changes go through EF migrations only.
- Existing bUnit tests select by `data-*` attributes and element ids on Muster and Post-battle; keep those stable and add new ones in the same style.
- The Game Master is the only user. Tablet landscape first.
- Requisition Points never exceed `OrderOfBattle.RequisitionPointsMax` (10).

## Approaches considered

1. **Two numbers per Crusade Card (chosen).** The card stores models lost and wounds lost; size and wounds-per-model come from the roster. Quick to enter, enough for Repair and Reinforce to act on.
2. **Per-model tracking.** Accurate for mixed units, but far more tapping after every Battle and a much larger card model. More than "attrition should matter" needs.
3. **Free-text condition note.** Trivial, but nothing can be shown at Muster in a usable form and Requisitions could not clear it.

The Game Master picked 1.

For the Requisition list there was one sensible shape (campaign-owned, editable, with optional built-in effects). A hard-coded list fails the "I can edit it" requirement; text-only entries would leave the Game Master repairing cards by hand.

## Part 1: Attrition

### Data

`CrusadeUnitCard` gains four integers, all default 0, via one migration:

| Field | Meaning |
|---|---|
| `ModelCount` | Models in the unit at full strength. 0 = unknown. |
| `WoundsPerModel` | Wounds characteristic of the unit's main profile. 0 = unknown. |
| `ModelsLost` | Models currently dead. |
| `WoundsLost` | Wounds currently missing from the one damaged surviving model. |

**(call)** `ModelCount` and `WoundsPerModel` are stored on the card rather than looked up live, so Muster and the Order of Battle page do not need a roster load and a card still works when its roster file changes. They are filled from the player's roster when a card is added, back-filled for existing cards the first time the Order of Battle is opened with a matching datasheet, and are editable on the card.

Limits when the maxima are known: `ModelsLost` 0 to `ModelCount − 1`, `WoundsLost` 0 to `WoundsPerModel − 1`. **(call)** A unit can never be reduced to zero models between Battles; losing the last model is what Destroyed means. When a maximum is unknown (0) the field accepts any non-negative number.

A derived `IsDamaged` (`ModelsLost > 0 || WoundsLost > 0`) and a display string such as `7/10 models · 2 wounds lost` belong next to the card model so every page words it the same way.

`BattleUnitResult` gains `ModelsLostAfter` and `WoundsLostAfter` (nullable ints): the unit's condition at the end of this Battle, as entered. Null means not entered, which leaves the card unchanged.

### Post-battle Sequence

A new step, **Damage**, sits after Casualties. Steps become: Result, Casualties, Damage, Tallies, Out of Action, Rank-ups, Review.

- Every unit in both Crusade Armies is listed, grouped by side like the Casualties step.
- Each row shows the unit name, its condition coming into the Battle, and two steppers: models lost and wounds lost. They start at the card's current values, so an untouched unit needs no input.
- Steppers stop at the limits above when known.
- **(call)** Destroyed units are listed too, flagged as destroyed, with the same steppers. The Game Master sets what strength they return at; no automatic rule. The Out of Action step text that says a passing unit "returns unharmed" is reworded to match.
- The step is never skipped and is always valid.
- Review shows each unit's damage change where there is one (for example `10 → 7 models`).

`PostBattleService.Apply` writes `ModelsLostAfter` / `WoundsLostAfter` to the card when they are set. Validation rejects values outside the limits, in line with how it already validates everything before mutating.

### Muster

Each unit row shows a damage badge when the card is damaged. Selection and points are unchanged.

### Order of Battle page (`CrusadeDetail`)

Each card shows its strength and lets the Game Master edit `ModelsLost`, `WoundsLost`, `ModelCount` and `WoundsPerModel` directly, saved like the other inline card fields.

## Part 2: Requisitions

### The Requisition list

Owned by the Campaign, stored as a JSON column (`Campaign.Requisitions`) using the existing `HasJsonConversion` pattern. Each entry:

| Field | Notes |
|---|---|
| `Id` | Guid string |
| `Name` | |
| `Cost` | Requisition Points, whole number ≥ 0 |
| `Timing` | Free text ("At any time", "After a battle") |
| `Effect` | Free text shown to the Game Master |
| `Kind` | `None`, `IncreaseSupplyLimit`, `RepairAll`, `ReinforceAll` |
| `Amount` | Used by `IncreaseSupplyLimit` only (points added) |

Built-in effects, applied to the buying Order of Battle:

- `IncreaseSupplyLimit`: `SupplyLimit += Amount`.
- `RepairAll`: `WoundsLost = 0` on every Crusade Card.
- `ReinforceAll`: `ModelsLost = 0` on every Crusade Card.
- `None`: nothing; the Game Master applies the effect by hand.

Starting list, as the Game Master specified for the first three:

| Name | Cost | Kind |
|---|---|---|
| Increase Supply Limit | 1 | `IncreaseSupplyLimit`, Amount 50 |
| Repair | 1 | `RepairAll` |
| Reinforce | 1 | `ReinforceAll` |
| Renowned Heroes | 1 | `None` |
| Legendary Veterans | 3 | `None` |
| Rearm and Resupply | 1 | `None` |
| Repair and Recuperate | 1 | `None` |

**(call)** The four core entries are carried over as text-only with the effect text from `crusade_rules.json`; their variable costs are noted in the effect text and handled by the editable cost at purchase (below). Core "Fresh Recruits" is dropped because Reinforce replaces it. The existing campaign gets this list once, the first time it is loaded after the change (a seeded flag, so a list the Game Master empties stays empty); a "Restore defaults" button in the editor re-adds any missing defaults.

### Campaign Editor

A "Requisitions" panel, opened from the header the way GM Notes is, lists the entries with add, edit and delete. Editing covers every field above; `Amount` shows only for `IncreaseSupplyLimit`. Changes go through the editor's existing dirty-state and Save.

### Buying

On the Order of Battle page, next to the Requisition Points counter, a "Requisitions" control lists the Campaign's entries with cost and effect. Buying one:

1. Confirms, showing the cost. **(call)** The cost is editable in the confirmation, pre-filled from the list, to cover the core variable-cost entries.
2. Is refused when the Order of Battle has fewer Requisition Points than the cost.
3. Deducts the points, applies the built-in effect, appends to the purchase log, saves.

For `RepairAll` and `ReinforceAll` the confirmation states how many units will be affected; **(call)** buying is still allowed when that is zero, with a warning.

The purchase log is a JSON column on `OrderOfBattle` (`RequisitionLog`): date, Requisition name, cost paid. It is shown newest-first on the Order of Battle page. **(call)** Entries are a record only; there is no undo. The existing +/− Requisition Points buttons stay for corrections.

The buy logic (afford check, deduction, effect, log) lives in a service with unit tests, not in the page.

### Requisition Points from a Battle

The automatic +1 per side is removed. `BattleRecord` gains `Player1RpAwarded` and `Player2RpAwarded`. **(call)** They are entered in the Result step beside Victory Points, default 1, range 0 to 10. On completion each Order of Battle gains its award, capped at 10. Review and the completion summary show the award instead of the fixed +1. **(call)** The migration gives both columns a default of 1, so a Battle that is already Mustered but not completed keeps today's outcome unless the Game Master changes it.

## Glossary changes

- Add **Attrition**: models and wounds a Crusade Card has lost and carries into its next Battle. A house rule.
- Add **Requisition**: an entry on the Campaign's Requisition list that an Order of Battle buys with Requisition Points.
- **Post-battle Sequence**: replace "gain a Requisition Point" with recording damage and the Game Master awarding Requisition Points.
- **Out of Action Test**: "A pass restores the unit" becomes "A pass spares the unit a penalty".

## Order of work

Two plans, in this order:

1. Attrition: card fields and migration, Damage step, service changes, Muster badge, Order of Battle page editing.
2. Requisitions: list model and migration with seed, Campaign Editor panel, buy service and UI, purchase log, Game Master-awarded Requisition Points.

Plan 2's Repair and Reinforce depend on plan 1's fields.

## Left to the builder

- How `ModelCount` and `WoundsPerModel` are derived from a roster `Selection` (the Roster page's `GetMaxWoundsStr` shows the wounds lookup; model count needs working out from the selection tree). When it cannot be derived confidently, leave 0.
- Layout and styling of the Damage step, badges, editor panel and buy control, following the existing `pb-*` classes and the Crusade page look.
- Where the display helper and the buy service live, and their names.
- Test coverage in the style of `PostBattleServiceTests`, `PostBattleTests` and `MusterTests`; existing tests that assert the +1 Requisition Point or the six step titles need updating.
