# Requisitions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Game Master edits the Campaign's Requisition list in the Campaign Editor, an Order of Battle buys from it with Requisition Points, and the Game Master awards Requisition Points after each Battle.

**Architecture:** The Requisition list is a JSON column on `Campaign`; the purchase log is a JSON column on `OrderOfBattle`; both use the existing `HasJsonConversion` pattern, so `SaveCampaignAsync` and `SaveCrusadeAsync` carry them unchanged. A pure static `RequisitionRules` owns the buy logic (afford check, deduction, built-in effect, log entry); pages call it and save through the existing services. `BattleRecord` gains the per-side award that replaces the fixed +1.

**Tech Stack:** .NET 10, Blazor Interactive Server, EF Core 10 SQLite, xUnit + FluentAssertions + bUnit.

**Spec:** `docs/superpowers/specs/2026-11-15-attrition-and-requisitions-design.md` (Part 2). Depends on plan `2026-11-15-01-attrition.md` being merged (`ModelsLost`, `WoundsLost` on `CrusadeUnitCard`).

## Global Constraints

- `T'au/campaign.db` holds live data: schema changes through EF migrations only.
- Requisition Points never exceed `OrderOfBattle.RequisitionPointsMax` (10) and never go below 0.
- Starting list, exact values: Increase Supply Limit (1, `IncreaseSupplyLimit`, Amount 50); Repair (1, `RepairAll`); Reinforce (1, `ReinforceAll`); Renowned Heroes (1, `None`); Legendary Veterans (3, `None`); Rearm and Resupply (1, `None`); Repair and Recuperate (1, `None`). No Fresh Recruits.
- Repair and Reinforce act on every Crusade Card of the buying Order of Battle.
- Purchases have no undo; the existing +/− Requisition Points buttons stay.
- The automatic +1 Requisition Point per Battle is removed; the award is typed, default 1, range 0 to 10.
- Use the glossary terms; write "Requisition Points" in prose and UI copy, not "RP".
- Test command: `dotnet test Warhammer.Tests --nologo` (add `--filter "<ClassName>"`). Migration command: `dotnet ef migrations add <Name> --project "T'au/Warhammer.csproj" --output-dir Data/Migrations`.
- Commit messages end with `Co-Authored-By: Claude Fable 5.2 <noreply@anthropic.com>`.

## Review Focus

- Buying with too few Requisition Points must change nothing: no deduction, no effect, no log entry (Task 2).
- A list the Game Master has emptied must stay empty after a reload; defaults are seeded once, not whenever the list is empty (Task 1).
- An award that would take an Order of Battle past 10 is capped, and the summary shows the capped figure (Task 3).
- Deleting or renaming a Requisition must not break or rewrite existing purchase-log entries (Task 2: the log stores the name, not the id).
- A cost typed as negative or blank at purchase must be refused, not treated as a refund (Tasks 2, 5).

---

### Task 1: Requisition list and purchase log storage

**Files:**
- Create: `T'au/Models/Campaign/Requisition.cs`, `T'au/Data/Migrations/<timestamp>_AddRequisitions.cs` (generated)
- Modify: `T'au/Models/Campaign/Campaign.cs`, `T'au/Models/Crusade/OrderOfBattle.cs`, `T'au/Data/AppDbContext.cs`, `T'au/Services/CampaignManagerService.cs`
- Test: `Warhammer.Tests/Campaign/CampaignManagerServiceTests.cs`, `Warhammer.Tests/Data/CrusadeMappingTests.cs`

**Interfaces:**
- Produces in `Requisition.cs` (namespace `Warhammer.Models.Campaign`):
  - `enum RequisitionKind { None, IncreaseSupplyLimit, RepairAll, ReinforceAll }`
  - `class Requisition { string Id = Guid.NewGuid().ToString(); string Name = ""; int Cost; string Timing = ""; string Effect = ""; RequisitionKind Kind; int Amount; }`
  - `class RequisitionPurchase { DateTime DateUtc; string Name = ""; int Cost; }`
  - `static class RequisitionDefaults { static List<Requisition> Create(); }` returning the seven entries from Global Constraints with fresh ids.
- Produces on `Campaign`: `List<Requisition> Requisitions = new()`, `bool RequisitionsSeeded`.
- Produces on `OrderOfBattle`: `List<RequisitionPurchase> RequisitionLog = new()`.

- [ ] **Step 1: Write failing tests**
  - `CampaignManagerServiceTests.Load_seeds_the_default_requisitions_once`: first `LoadCampaignAsync` returns 7 Requisitions; the first three are `("Increase Supply Limit", 1, IncreaseSupplyLimit, 50)`, `("Repair", 1, RepairAll, 0)`, `("Reinforce", 1, ReinforceAll, 0)`; "Legendary Veterans" costs 3; no entry is named "Fresh Recruits". Clear the list, `SaveCampaignAsync`, load with a fresh service → list is empty.
  - `CampaignManagerServiceTests.Requisition_edits_persist`: change one entry's `Cost` in place, save, reload → new cost (pins the JSON value comparer).
  - `CrusadeMappingTests.Requisition_log_round_trips`: add a `RequisitionPurchase { Name = "Repair", Cost = 1 }` to an Order of Battle, save, reload → one entry with that name and cost.

- [ ] **Step 2: Run to verify they fail** → build error.

- [ ] **Step 3: Implement.**
  - Map `Campaign.Requisitions` and `OrderOfBattle.RequisitionLog` with `HasJsonConversion()`.
  - In `LoadCampaignAsync`, after the campaign is loaded or created: when `!RequisitionsSeeded`, set `Requisitions = RequisitionDefaults.Create()`, `RequisitionsSeeded = true`, and persist it (the flag, not list emptiness, decides).
  - Effect text for the three built-ins: "Increase the Supply Limit by 50 points.", "Restore every lost wound on every unit in the Order of Battle.", "Restore every lost model on every unit in the Order of Battle." Timing "At any time" for the first, "After a battle" for the other two. The four text-only entries take `timing` and `effect` verbatim from `T'au/wwwroot/data/crusade_rules.json`; where the core cost varies, append the core cost wording in brackets to the effect, e.g. "(Core cost: 1 RP + 1 RP per Battle Honour the unit has.)".

- [ ] **Step 4: Generate the migration** `AddRequisitions` → adds `Requisitions` (TEXT, default `'[]'`) and `RequisitionsSeeded` (INTEGER, default 0) to `Campaigns`, and `RequisitionLog` (TEXT, default `'[]'`) to `OrdersOfBattle`. Edit the generated defaults if EF emits `''` for the TEXT columns.

- [ ] **Step 5: Run the suite** → all pass.

- [ ] **Step 6: Commit** — `feat: Campaign Requisition list and Order of Battle purchase log`

---

### Task 2: Buy rules

**Files:**
- Create: `T'au/Services/Crusade/RequisitionRules.cs`
- Test: `Warhammer.Tests/Crusade/RequisitionRulesTests.cs`

**Interfaces:**
- Consumes: `Requisition`, `RequisitionKind`, `RequisitionPurchase` (Task 1); `CrusadeUnitCard.ModelsLost/WoundsLost` (attrition plan).
- Produces `public static class RequisitionRules`:
  - `string? CannotBuy(OrderOfBattle order, int cost)`: `null` when allowed; otherwise the reason ("The cost cannot be negative." / "Not enough Requisition Points: needs {cost}, has {n}.").
  - `int UnitsAffected(OrderOfBattle order, Requisition req)`: cards with `WoundsLost > 0` for `RepairAll`, `ModelsLost > 0` for `ReinforceAll`, otherwise 0.
  - `void Buy(OrderOfBattle order, Requisition req, int cost, DateTime nowUtc)`: throws `InvalidOperationException(CannotBuy(...))` before mutating anything; otherwise deducts, applies the effect, appends `RequisitionPurchase { DateUtc = nowUtc, Name = req.Name, Cost = cost }`.

- [ ] **Step 1: Write failing tests** (plain unit tests, no database). Order fixture: `RequisitionPoints = 3`, `SupplyLimit = 1000`, cards `u1 { ModelsLost = 2, WoundsLost = 1 }`, `u2 { ModelsLost = 0, WoundsLost = 3 }`, `u3` undamaged.
  - `IncreaseSupplyLimit` Amount 50, cost 1 → `SupplyLimit == 1050`, `RequisitionPoints == 2`, log has one entry `("Increase Supply Limit", 1)` with the given time.
  - `RepairAll` → every `WoundsLost == 0`; `u1.ModelsLost` still 2. `UnitsAffected == 2`.
  - `ReinforceAll` → every `ModelsLost == 0`; `u2.WoundsLost` still 3. `UnitsAffected == 1`.
  - `None` → only points and log change.
  - Cost 4 with 3 points → throws; points, supply, cards and log all unchanged.
  - Cost −1 → throws, nothing changes. Cost 0 → allowed.
  - Paying a cost different from `req.Cost` logs the cost paid.

- [ ] **Step 2: Run to verify they fail.**

- [ ] **Step 3: Implement `RequisitionRules`.**

- [ ] **Step 4: Run the suite** → all pass.

- [ ] **Step 5: Commit** — `feat: Requisition buy rules`

---

### Task 3: Game Master awards Requisition Points

**Files:**
- Modify: `T'au/Models/Campaign/BattleRecord.cs`, `T'au/Services/Crusade/PostBattleService.cs`, `T'au/Components/Pages/Battle/PostBattle.razor`, `GLOSSARY.md`
- Create: `T'au/Data/Migrations/<timestamp>_AddBattleRequisitionAwards.cs` (generated)
- Test: `Warhammer.Tests/Crusade/PostBattleServiceTests.cs`, `Warhammer.Tests/Components/PostBattleTests.cs`

**Interfaces:**
- Produces on `BattleRecord`: `int Player1RpAwarded = 1`, `int Player2RpAwarded = 1`. Migration column default **1** so already-Mustered Battles keep today's outcome.
- Produces DOM hooks on the Result step: inputs `#rp1`, `#rp2`.

- [ ] **Step 1: Write failing tests**
  - Service `Complete_awards_the_requisition_points_entered`: tweak `Player1RpAwarded = 3, Player2RpAwarded = 0` → order A `RequisitionPoints == 8` (from 5), order B `== 5`. The existing assertions of 6/6 stay valid for the default of 1.
  - Service `Complete_caps_the_award_at_ten`: order A starts at 9, award 3 → 10.
  - Service `Complete_rejects_an_award_outside_zero_to_ten`: award −1 or 11 → `InvalidOperationException`, nothing applied.
  - Page `Result_step_takes_the_requisition_award`: `#rp1` and `#rp2` show `1`; change `#rp1` to `3`, walk to Review → that side's summary shows `5 → 8`; complete → order has 8. Typing a non-number or a value out of range falls back to the nearest of 0 and 10 (blank → 0).

- [ ] **Step 2: Run to verify they fail.**

- [ ] **Step 3: Implement.** Service: validate both awards in `Validate`; replace the `+ 1` with the side's award (`Math.Min(max, points + award)`). Page: two number inputs under the Victory Points row labelled "Requisition Points awarded", one per side, built like the VP inputs; `BuildSummary`'s `RpAfter` uses the award; the "already at the maximum" note shows when the award is above 0 and nothing was gained. Generate the migration and set `defaultValue: 1`.

- [ ] **Step 4: Update `GLOSSARY.md`**: Post-battle Sequence becomes "…record the result, record damage, award experience, test destroyed units, apply scars, rank up, and award Requisition Points as the Game Master decides."

- [ ] **Step 5: Run the suite** → all pass.

- [ ] **Step 6: Commit** — `feat: Game Master awards Requisition Points after a Battle`

---

### Task 4: Requisition editor in the Campaign Editor

**Files:**
- Create: `T'au/Components/Pages/Campaign/RequisitionEditor.razor` (place it beside the `GmNotes` component if that lives elsewhere)
- Modify: `T'au/Components/Pages/Campaign/CampaignEditor.razor`
- Test: `Warhammer.Tests/Components/RequisitionEditorTests.cs`

**Interfaces:**
- Consumes: `Campaign.Requisitions`, `RequisitionDefaults.Create()`.
- Produces component `RequisitionEditor` with parameters `[Parameter] Campaign Campaign`, `[Parameter] EventCallback OnChanged`, `[Parameter] EventCallback OnClose`. It mutates `Campaign.Requisitions` in place and raises `OnChanged` after every change; it does not save.
- DOM hooks: `#req-add`, `#req-restore`, rows `data-req="@Id"`, per row `data-req-name`, `data-req-cost`, `data-req-timing`, `data-req-effect`, `data-req-kind`, `data-req-amount`, `data-req-delete`.

- [ ] **Step 1: Write failing bUnit tests** (render the component with a `Campaign` holding the defaults):
  - Lists seven rows.
  - `#req-add` appends a row named "New Requisition", cost 1, kind `None`, and raises `OnChanged`.
  - Editing `data-req-cost` to `2` updates the model; a negative or blank cost becomes 0.
  - `data-req-amount` is rendered only for rows whose kind is `IncreaseSupplyLimit`; switching a row's kind to it makes the input appear.
  - `data-req-delete` removes the row and raises `OnChanged`.
  - `#req-restore` on an emptied list brings back seven rows; on a list that still holds "Repair" it adds only the defaults whose names are missing.

- [ ] **Step 2: Run to verify they fail.**

- [ ] **Step 3: Implement the component and wire it in.** Kind labels in the select: "Text only", "Add to Supply Limit", "Repair all wounds", "Reinforce all models". In `CampaignEditor`, add a "Requisitions" button next to "GM Notes" (`bi-box-seam`) that shows the panel the same way `_showGmNotes` does; `OnChanged` calls `MarkDirty`, so the existing Save button and unsaved-changes guard cover it.

- [ ] **Step 4: Run the suite** → all pass.

- [ ] **Step 5: Commit** — `feat: edit the Requisition list in the Campaign Editor`

---

### Task 5: Buying on the Order of Battle page

**Files:**
- Modify: `T'au/Components/Pages/Crusade/CrusadeDetail.razor`
- Test: `Warhammer.Tests/Components/CrusadeDetailTests.cs`

**Interfaces:**
- Consumes: `RequisitionRules.CannotBuy/UnitsAffected/Buy`, `CampaignManagerService.LoadCampaignAsync().Requisitions`, `OrderOfBattle.RequisitionLog`, the page's `SaveAsync`.
- DOM hooks: `#req-open`; per entry `data-buy="@Id"`; confirmation `#req-confirm`, cost input `#req-cost`, `#req-confirm-buy`, `#req-cancel`, message `#req-warning`; log rows `data-req-log`.

- [ ] **Step 1: Write failing tests**
  - `Buying_reinforce_restores_models_and_logs_it`: order with 2 points and a card `ModelCount = 10, ModelsLost = 3`; open, click `[data-buy]` for Reinforce → `#req-confirm` shows cost `1` and "1 unit"; confirm → reloaded card `ModelsLost == 0`, `RequisitionPoints == 1`, one `[data-req-log]` row containing "Reinforce".
  - `Buying_increase_supply_limit_adds_fifty`: `SupplyLimit` 1000 → 1050.
  - `Cannot_buy_without_enough_points`: order with 0 points → `#req-confirm-buy` disabled and `#req-warning` contains "Not enough Requisition Points".
  - `Cost_can_be_changed_before_buying`: set `#req-cost` to `2` on Repair and Recuperate → 2 points deducted, log shows 2; a negative cost disables confirm.
  - `Buying_repair_with_nothing_to_repair_warns_but_is_allowed`: `#req-warning` contains "No unit" and confirm is enabled.
  - `Cancel_changes_nothing`.

- [ ] **Step 2: Run to verify they fail.**

- [ ] **Step 3: Implement.** A "Requisitions" button under the Req. Points counter opens a panel listing the Campaign's entries (name, cost, timing, effect, Buy). Buy opens the confirmation with the cost input pre-filled from the list; for `RepairAll` / `ReinforceAll` it states "{n} unit(s) will be restored" or the "No unit needs this" warning. Confirm calls `RequisitionRules.Buy(_crusade, req, cost, DateTime.UtcNow)` then `SaveAsync`. Below the panel, "Requisitions bought" lists the log newest first as `date · name · cost`; hidden when empty. Use the page's existing modal pattern (the add-unit modal), not `JS confirm`.

- [ ] **Step 4: Run the suite** → all pass.

- [ ] **Step 5: Add **Requisition** to `GLOSSARY.md`** under Force layer ("An entry on the Campaign's Requisition list that an Order of Battle buys with Requisition Points. The Game Master edits the list in the Campaign Editor. _Avoid_: upgrade, purchase (alone)"), and in Requisition Point replace "such as Increase Supply Limit or Fresh Recruits" with "such as Increase Supply Limit, Repair or Reinforce".

- [ ] **Step 6: Commit** — `feat: buy Requisitions from the Order of Battle page`
