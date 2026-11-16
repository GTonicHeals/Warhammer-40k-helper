# Amend a Completed Battle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the GM change a Completed Battle's captured Territory and each unit's damage, with the map and Crusade Cards following.

**Architecture:** A new `PostBattleService.AmendAsync` validates every change before mutating, then applies the Territory capture/reveal and per-unit damage deltas and saves. A new page `battle/{id}/amend` edits a draft and calls it on Save. The War Room Engagement Log gets a pencil link on DONE rows.

**Tech Stack:** .NET / Blazor Server (InteractiveServer), EF Core SQLite, xUnit + FluentAssertions + bUnit.

**Spec:** `docs/superpowers/specs/2026-11-16-amend-battle-design.md`

## Global Constraints

- Only Completed Battles can be amended; Mustered ones keep using `battle/{id}/post`.
- Only the captured Territory and models/wounds lost change. Winner, score, XP, honours, scars, Out of Action, RP are never touched.
- A previously captured Territory is left as it is when the pick changes or is cleared.
- Damage edits apply only the difference to the card, so a Repair/Reinforce since the Battle is not undone.
- Validate everything before mutating anything (same rule as `CompleteAsync`).

## Review Focus

1. Card repaired since the Battle (card 0 lost, Battle recorded 2), GM amends to 3 → card becomes 1, not 3. (Task 1 test `Amend_damage_applies_only_the_difference`.)
2. Delta would push the card below 0 or above the unit's max → clamped, not rejected. (Task 1 test `Amend_damage_clamps_the_card`.)
3. Amending a draw to capture a Territory → rejected with "A Draw captures no territory." (Task 1 test `Amend_rejects_a_territory_on_a_draw`.)
4. Re-saving the page with no changes → nothing on the map or cards changes, no hidden Territory is revealed. (Task 1 test `Amend_with_no_changes_changes_nothing`.)
5. Unit removed from its Order (`RemovedFromOrder`) or card deleted → shown read-only on the page; the service rejects a damage entry for it. (Task 1 test `Amend_rejects_damage_for_a_removed_unit`; Task 2 test `Removed_units_are_read_only`.)

---

### Task 1: `PostBattleService.AmendAsync`

**Files:**
- Modify: `T'au/Services/Crusade/PostBattleService.cs`
- Test: `Warhammer.Tests/Crusade/PostBattleServiceTests.cs` (reuse `SeedAsync`, `AddHiddenNeighboursAsync`, `RevealedAsync`; complete the seeded Battle first with `CompleteAsync`)

**Interfaces:**
- Produces (in `PostBattleService.cs`, namespace `Warhammer.Services.Crusade`):
  - `public sealed record UnitDamageAmendment(string CardId, int ModelsLost, int WoundsLost);`
  - `public sealed record BattleAmendment(string? CapturedTerritoryId, IReadOnlyList<UnitDamageAmendment> Damage);`
  - `public static (int ModelsLost, int WoundsLost) RecordedDamage(BattleUnitResult r, CrusadeUnitCard card)` → `(r.ModelsLostAfter ?? card.ModelsLost, r.WoundsLostAfter ?? card.WoundsLost)`. The page's starting values.
  - `public async Task AmendAsync(string battleId, BattleAmendment amendment)`

**Behaviour of `AmendAsync`:**
- Throws `InvalidOperationException` when: Battle not found; Battle not Completed ("Only a completed Battle can be amended."); a Territory is picked on a draw ("A Draw captures no territory."); the Territory id is not a `Territory` in `MapNodes`; a `CardId` is not in `UnitResults`; the unit's card is missing or `RemovedFromOrder` ("{name} is no longer in its Order of Battle."); the new values fail `UnitStrength.InRange(card, …)` ("{name}: the damage entered is more than the unit can take.").
- Territory: only when `amendment.CapturedTerritoryId != battle.CapturedTerritoryId`. Set the record; if non-null, set `ControllingPlayerId = WinnerPlayerId` and reveal `TerritoryReveal.RevealedByCapture(...)`. The old Territory is not touched.
- Damage, per field (models and wounds separately), only when the new value differs from `RecordedDamage`: card value += (new − recorded), then `UnitStrength.Clamp(card)`; store new value in `ModelsLostAfter`/`WoundsLostAfter`. Orders are loaded from `Player1OrderId`/`Player2OrderId` via `CrusadeManagerService` (a missing order just means its cards are missing). Save only orders whose cards changed, then the campaign.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] Amend_sets_a_forgotten_territory_and_reveals_neighbours()
// SeedAsync(b => b.CapturedTerritoryId = null); AddHiddenNeighboursAsync(); CompleteAsync;
// AmendAsync(id, new("t1", [])) → t1.ControllingPlayerId == "p1"; battle.CapturedTerritoryId == "t1";
// RevealedAsync() == { t1:true, t2:true, t3:false }

[Fact] Amend_changing_the_territory_leaves_the_old_one_as_it_is()
// seed + a second Territory "t4"; Complete (t1 → p1); AmendAsync(id, new("t4", []))
// → t4 controller "p1", t1 controller still "p1", record == "t4"

[Fact] Amend_clearing_the_territory_only_changes_the_record()
// Complete; AmendAsync(id, new(null, [])) → record null, t1 still "p1"

[Fact] Amend_rejects_a_territory_on_a_draw()
// SeedAsync(b => { b.WinnerPlayerId = ""; b.CapturedTerritoryId = null; }); Complete;
// AmendAsync(id, new("t1", [])) throws "A Draw captures no territory."; t1 controller null

[Fact] Amend_rejects_a_mustered_battle()
// no Complete; throws "Only a completed Battle can be amended."

[Fact] Amend_damage_applies_only_the_difference()
// tweak: a1.ModelCount = 5; result a1 ModelsLostBefore = 0, ModelsLostAfter = 2; Complete (card 2);
// then set card a1.ModelsLost = 0 and save (a Repair); AmendAsync(id, new("t1", [new("a1", 3, 0)]))
// → card a1.ModelsLost == 1; battle a1 result ModelsLostAfter == 3

[Fact] Amend_damage_clamps_the_card()
// as above but amend to 0 → card 0 (not -2)

[Fact] Amend_rejects_damage_out_of_range()
// a1.ModelCount = 5; amend to 5 models lost → throws "...more than the unit can take."; card unchanged

[Fact] Amend_rejects_damage_for_a_removed_unit()
// Complete (b1 removed); AmendAsync(id, new("t1", [new("b1", 1, 0)])) throws "B1 is no longer in its Order of Battle."

[Fact] Amend_with_no_changes_changes_nothing()
// SeedAsync(b => b.CapturedTerritoryId = null); AddHiddenNeighbours; Complete;
// AmendAsync(id, new(null, [new("a1", 0, 0)])) → RevealedAsync() all false; a1 card 0/0
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "FullyQualifiedName~PostBattleServiceTests.Amend"`
Expected: build FAIL — `AmendAsync` not defined.

- [ ] **Step 3: Implement the records, `RecordedDamage` and `AmendAsync` in `PostBattleService.cs`** as described above. Validate in one pass collecting `(result, card, order, newModels, newWounds)`, then mutate.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Warhammer.Tests --filter "FullyQualifiedName~PostBattleServiceTests"`
Expected: all PASS (old tests too).

- [ ] **Step 5: Commit** — `feat: amend a completed battle's territory and damage`

---

### Task 2: Amend Battle page

**Files:**
- Create: `T'au/Components/Pages/Battle/AmendBattle.razor` (`@page "/battle/{Id}/amend"`, `@rendermode InteractiveServer`)
- Create: `T'au/Components/Pages/Battle/AmendBattle.razor.css` — copy the rules for `.pb-side-head`, `.pb-unit`, `.pb-unit-head`, `.pb-unit-name`, `.pb-unit-controls`, `.pb-stepper`, `.pb-damage-val`, `.pb-btn` from `PostBattle.razor.css` (CSS is scoped per component)
- Test: `Warhammer.Tests/Components/AmendBattleTests.cs` (setup copied from `PostBattleTests`: same services; seed then `PostBattleService.CompleteAsync`)

**Interfaces:**
- Consumes: `PostBattleService.AmendAsync`, `BattleAmendment`, `UnitDamageAmendment`, `PostBattleService.RecordedDamage` (Task 1); `UnitStrength.MaxModelsLost/MaxWoundsLost/Describe`.

**Page content:**
- Title "Amend battle"; subtitle: mission · players · date.
- Messages (with "Back to the War Room." link): not found → "This Battle was not found."; Mustered → "This Battle hasn't been completed yet." plus link `battle/{id}/post`.
- Territory `<select id="territory">` (None + all Territories, hidden ones suffixed " (hidden)"), labelled "Territory captured by {winner}". On a draw, no select; show `#ab-territory-draw` "A Draw captures no territory."
- When the Battle already had a captured Territory and the selection differs, show `#ab-old-territory`: "{old name} stays with its current controller. Change it in the Campaign Editor if needed."
- Per side, per unit: the same stepper markup/data attributes as PostBattle's damage step (`data-models-minus/plus`, `data-models-lost`, `data-wounds-minus/plus`, `data-wounds-lost`), starting at `RecordedDamage`, bounded by 0 and `UnitStrength` max. Units with a missing card or `RemovedFromOrder` render `data-damage-locked="{cardId}"` with the text "No longer in the Order of Battle" and no buttons.
- `#ab-save` builds a `BattleAmendment` (all editable units' current values) and calls `AmendAsync`; on success show `#ab-saved` "Battle amended." and reload the page state from storage; on `InvalidOperationException` show `#ab-error` with the message.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] Picking_a_territory_and_saving_captures_it()
// battle completed with CapturedTerritoryId null; change #territory to "t1"; click #ab-save
// → #ab-saved shown; t1 controller "p1"

[Fact] Changing_damage_and_saving_updates_the_card()
// a1.ModelCount = 5; click [data-models-plus='a1'] twice; save → card a1.ModelsLost == 2

[Fact] Changing_an_existing_territory_warns_about_the_old_one()
// completed with t1; add t4; select t4 → #ab-old-territory contains "Hab"

[Fact] A_draw_has_no_territory_picker()
// winner "" → no #territory; #ab-territory-draw present

[Fact] Removed_units_are_read_only()
// b1 removed by Complete → [data-damage-locked='b1'] present; no [data-models-plus='b1']

[Fact] A_mustered_battle_points_to_the_post_battle_page()
// not completed → markup contains "hasn't been completed" and href "battle/{id}/post"
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "FullyQualifiedName~AmendBattleTests"`
Expected: build FAIL — `AmendBattle` not defined.

- [ ] **Step 3: Implement `AmendBattle.razor` and its CSS** as described.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Warhammer.Tests --filter "FullyQualifiedName~AmendBattleTests"`
Expected: all PASS.

- [ ] **Step 5: Commit** — `feat: Amend battle page`

---

### Task 3: Pencil link in the War Room Engagement Log

**Files:**
- Modify: `T'au/Components/Pages/Campaign/WarRoom.razor:261-267` (last cell of each battle row)
- Test: `Warhammer.Tests/Components/WarRoomTests.cs`

**Interfaces:**
- Consumes: route `battle/{id}/amend` (Task 2).

- [ ] **Step 1: Write the failing test**

```csharp
[Fact] Completed_battles_link_to_the_amend_page()
// RenderWithBattlesAsync(); cut.Find("#wr-amend-c1").GetAttribute("href").Should().Be("battle/c1/amend");
// cut.FindAll("#wr-amend-m1").Should().BeEmpty();
```

- [ ] **Step 2: Run it to verify it fails** — `dotnet test Warhammer.Tests --filter "FullyQualifiedName~WarRoomTests"` → FAIL (no element).

- [ ] **Step 3: Add, before the delete button, for Completed rows only:** `<a id="wr-amend-{b.Id}" class="wr-del-btn" href="battle/{b.Id}/amend" title="Amend this entry" aria-label="Amend this entry"><i class="bi bi-pencil"></i></a>`

- [ ] **Step 4: Run the full suite** — `dotnet test Warhammer.Tests` → all PASS.

- [ ] **Step 5: Commit** — `feat: War Room links completed battles to the amend page`
