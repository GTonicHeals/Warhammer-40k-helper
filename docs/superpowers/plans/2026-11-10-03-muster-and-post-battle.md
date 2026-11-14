# Muster and Post-battle Sequence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** One Battle is entered once: a Muster creates it with each side's Crusade Army, and a guided Post-battle Sequence updates every Crusade Card, both Orders of Battle and the Campaign Map.

**Architecture:** `BattleRecord` grows a status, the two Orders of Battle involved, and a JSON column of per-unit results, so it stays a child of `Campaign` and `CampaignManagerService.SaveCampaignAsync` keeps working unchanged. The rules (XP, rank-ups, Out of Action options) are pure static classes with unit tests. `PostBattleService.CompleteAsync` applies a finished `BattleRecord` to the cards, orders and campaign in one go and is tested against SQLite. Two new pages drive the flow.

**Tech Stack:** EF Core SQLite, Blazor Server, xUnit + FluentAssertions, bUnit.

**Spec:** `docs/superpowers/specs/2026-11-10-crusade-campaign-and-10th-rules-spec.md` (Crusade section). Depends on plan 02 being merged.

## Global Constraints

- Battles are one Order of Battle against one other. Points limits: 1000, 2000, 3000.
- XP: Battle Experience 1; Dealers of Death 1 per three enemy units destroyed on the cumulative tally; Marked for Greatness 3 to one unit per side. Units with `XpEligible == false` gain nothing.
- Out of Action: fail → Battle Scar or Devastating Blow (lose one honour). Three scars → must take Devastating Blow. No honours and three scars → removed from the Order of Battle.
- Each side gains 1 Requisition Point per Battle, capped at 10.
- Dice results are typed in, never rolled by the app.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- A unit whose cumulative tally goes from 2 to 4 enemy units destroyed gets exactly one Dealers of Death XP (pinned in Task 2).
- A unit crossing two rank thresholds in one Battle (e.g. 14 → 19 XP is one; 5 → 16 is two) owes that many honours (pinned in Task 2).
- An XP-ineligible unit that is destroyed still takes an Out of Action test (pinned in Task 4).
- Completing a Battle twice must throw, not double-award (pinned in Task 4).
- Marked for Greatness ticked on two units of one side must be rejected before completion (pinned in Task 6).

---

### Task 1: Battle model extension

**Files:**
- Modify: `T'au/Models/Campaign/BattleRecord.cs`, `T'au/Data/AppDbContext.cs`
- Create: `T'au/Models/Campaign/BattleUnitResult.cs`, `T'au/Models/Campaign/MissionCatalog.cs`, `T'au/Data/Migrations/<timestamp>_AddBattleMuster.cs` (generated)
- Test: `Warhammer.Tests/Campaign/BattleRecordMappingTests.cs`

**Interfaces:**
- Produces on `BattleRecord`: `BattleStatus Status` (enum `Mustered`, `Completed`, declared in `BattleRecord.cs`), `int PointsLimit`, `string Player1Id = "p1"`, `string Player2Id = "p2"`, `string? Player1OrderId`, `string? Player2OrderId`, `List<BattleUnitResult> UnitResults` (JSON column with `ValueComparer`).
- Produces `BattleUnitResult` (plain class, JSON): `string CardId`, `string PlayerId`, `string UnitName`, `int Points`, `bool Destroyed`, `int EnemyUnitsDestroyed`, `bool MarkedForGreatness`, `bool? OutOfActionPassed`, `OutOfActionPenalty Penalty` (enum `None`, `BattleScar`, `DevastatingBlow`, declared in `BattleUnitResult.cs`), `AppliedTrait? ScarTaken`, `string? HonourLost`, `List<AppliedTrait> HonoursGained`, `int XpGained`, `bool RemovedFromOrder`.
- Produces: `public static class MissionCatalog { public static readonly string[] PrimaryMissions = ["Take and Hold", "Scorched Earth", "Purge the Foe", "Sites of Power"]; }`

- [ ] **Step 1: Write the failing mapping test (`SqliteTestBase`)**: seed a `Campaign` with one `BattleRecord { Status = Mustered, PointsLimit = 2000, Player1OrderId = "o1", UnitResults = { new() { CardId = "c1", UnitName = "U", Points = 90 } } }`, reload through `CampaignManagerService.LoadCampaignAsync`, assert `BattleHistory.Single().UnitResults.Single().CardId == "c1"` and `Status == BattleStatus.Mustered`. Then mutate `UnitResults[0].Destroyed = true`, `SaveCampaignAsync`, reload, assert persisted (this pins the `ValueComparer`).

- [ ] **Step 2: Run to verify it fails** — `dotnet test Warhammer.Tests --filter "BattleRecordMappingTests" --nologo` → build error.

- [ ] **Step 3: Add the properties, the JSON conversion (same pattern as `ConnectedNodeIds`, using `CrusadeJson.Options`), generate the migration**

Run: `dotnet ef migrations add AddBattleMuster --project "T'au/Warhammer.csproj" --output-dir Data/Migrations` → `Up` adds columns to `BattleRecords` only.

- [ ] **Step 4: Run the suite** — `dotnet test Warhammer.Tests --nologo` → all pass.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Models/Campaign" "T'au/Data" Warhammer.Tests/Campaign/BattleRecordMappingTests.cs
git commit -m "feat: Battle records carry status, orders and per-unit results

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: XP and rank rules

**Files:**
- Create: `T'au/Services/Crusade/XpRules.cs`
- Test: `Warhammer.Tests/Crusade/XpRulesTests.cs`

**Interfaces:**
- Produces: `public static class XpRules { public const int BattleExperience = 1; public const int MarkedForGreatness = 3; public static int DealersOfDeathXp(int tallyBefore, int tallyAfter); public static int XpFor(BattleUnitResult r, CrusadeUnitCard card); public static int RanksGained(int xpBefore, int xpAfter, bool isCharacter, bool legendaryVeteransUsed); }`
- Consumes: `RankCalculator.RankFor` (existing).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory] [InlineData(0,2,0)] [InlineData(2,4,1)] [InlineData(0,6,2)] [InlineData(3,3,0)]
public void Dealers_of_death_counts_every_third(int before, int after, int xp) =>
    XpRules.DealersOfDeathXp(before, after).Should().Be(xp);

[Fact] public void XpFor_sums_experience_tally_and_mark()
{   var card = new CrusadeUnitCard { UnitsDestroyed = 2 };
    var r = new BattleUnitResult { EnemyUnitsDestroyed = 1, MarkedForGreatness = true };
    XpRules.XpFor(r, card).Should().Be(1 + 1 + 3);
}
[Fact] public void XpFor_is_zero_for_ineligible_unit() =>
    XpRules.XpFor(new BattleUnitResult { MarkedForGreatness = true }, new CrusadeUnitCard { XpEligible = false }).Should().Be(0);

[Theory] [InlineData(14,19,1)] [InlineData(5,16,2)] [InlineData(0,5,0)] [InlineData(30,35,0)]
public void Ranks_gained_for_non_character(int before, int after, int ranks) =>
    XpRules.RanksGained(before, after, isCharacter: false, legendaryVeteransUsed: false).Should().Be(ranks);
[Fact] public void Ranks_gained_character_reaches_heroic() =>
    XpRules.RanksGained(30, 31, isCharacter: true, legendaryVeteransUsed: false).Should().Be(1);
```

- [ ] **Step 2: Run to verify they fail** — build error.
- [ ] **Step 3: Implement** — `RanksGained` = `max(0, (int)RankFor(after,…) − (int)RankFor(before,…))`.
- [ ] **Step 4: Run** — `dotnet test Warhammer.Tests --filter "XpRulesTests" --nologo` → all pass.
- [ ] **Step 5: Commit** — `feat: XP and rank-up rules` with the attribution line.

---

### Task 3: Out of Action options

**Files:**
- Create: `T'au/Services/Crusade/OutOfActionRules.cs`
- Test: `Warhammer.Tests/Crusade/OutOfActionRulesTests.cs`

**Interfaces:**
- Produces: `public record OutOfActionOptions(bool CanTakeScar, bool CanTakeDevastatingBlow, bool MustTakeDevastatingBlow, bool WillBeRemoved);` and `public static class OutOfActionRules { public const int MaxScars = 3; public static OutOfActionOptions OptionsFor(CrusadeUnitCard card); }`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Fresh_unit_can_take_scar_but_has_nothing_to_lose() =>
    OutOfActionRules.OptionsFor(new CrusadeUnitCard()).Should().Be(new OutOfActionOptions(true, false, false, false));
[Fact] public void Unit_with_honour_may_choose_either() =>
    OutOfActionRules.OptionsFor(Card(honours: 1, scars: 0)).Should().Be(new OutOfActionOptions(true, true, false, false));
[Fact] public void Three_scars_force_devastating_blow() =>
    OutOfActionRules.OptionsFor(Card(honours: 2, scars: 3)).Should().Be(new OutOfActionOptions(false, true, true, false));
[Fact] public void Three_scars_and_no_honours_means_removal() =>
    OutOfActionRules.OptionsFor(Card(honours: 0, scars: 3)).Should().Be(new OutOfActionOptions(false, false, true, true));
```

- [ ] **Step 2: Run to verify they fail.** — [ ] **Step 3: Implement.** — [ ] **Step 4: Run, all pass.** — [ ] **Step 5: Commit** `feat: Out of Action options` with attribution.

---

### Task 4: `PostBattleService.CompleteAsync`

**Files:**
- Create: `T'au/Services/Crusade/PostBattleService.cs`
- Modify: `T'au/Program.cs` (register scoped)
- Test: `Warhammer.Tests/Crusade/PostBattleServiceTests.cs`

**Interfaces:**
- Produces: `public class PostBattleService(CampaignManagerService campaigns, CrusadeManagerService crusades, ILogger<PostBattleService> logger) { public Task CompleteAsync(string battleId); }`
- Consumes: `XpRules`, `OutOfActionRules`, `CrusadePointsCalculator`, `CrusadeManagerService.SaveCrusadeAsync`, `CampaignManagerService.SaveCampaignAsync`.
- Behaviour, in order, inside one call: load campaign and the battle (throw `InvalidOperationException` if `Status == Completed` or either order id is null); load both orders; for each `BattleUnitResult`: find the card (skip if missing, log), `XpGained = XpRules.XpFor`, `card.Xp += XpGained`, `card.BattlesPlayed++`, `card.BattlesSurvived++` when not destroyed, `card.UnitsDestroyed += EnemyUnitsDestroyed`; if destroyed and `OutOfActionPassed == false`: `Penalty == BattleScar` → add `ScarTaken` to `BattleScars`; `Penalty == DevastatingBlow` → remove the honour named `HonourLost` (nothing to remove when it is null); if `RemovedFromOrder` → remove the card from the order; add `HonoursGained` to `BattleHonours`; validate `HonoursGained.Count == XpRules.RanksGained(before, after, …)` else throw `InvalidOperationException` naming the unit. Then each order `RequisitionPoints = Math.Min(10, +1)`, save both orders; campaign: winner `Wins++`, loser `Losses++`, `CapturedTerritoryId` territory `ControllingPlayerId = WinnerPlayerId`, `Status = Completed`, save campaign.

- [ ] **Step 1: Write the failing tests (`SqliteTestBase`, seed through the services)**

Seed: campaign with players p1/p2 and one `Territory { Id = "t1" }`; order A (p1) with cards `a1` (Xp 14, one honour), `a2` (XpEligible false), order B (p2) with card `b1` (two scars... use three scars and zero honours); battle `Mustered` with `UnitResults`: `a1 { EnemyUnitsDestroyed = 3, MarkedForGreatness = true, HonoursGained = { trait } }` (14 → 14+1+1+3 = 19, one rank → one honour), `a2 { Destroyed = true, OutOfActionPassed = false, Penalty = BattleScar, ScarTaken = scar }`, `b1 { Destroyed = true, OutOfActionPassed = false, Penalty = DevastatingBlow, RemovedFromOrder = true }`; `WinnerPlayerId = "p1"`, `CapturedTerritoryId = "t1"`.

```csharp
[Fact] public async Task Complete_applies_xp_tallies_scars_removal_rp_and_map()
{   await Svc().CompleteAsync(battleId);
    var a = (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p1");
    var a1 = a.Units.Single(u => u.Id == "a1");
    a1.Xp.Should().Be(19); a1.BattlesPlayed.Should().Be(1); a1.BattlesSurvived.Should().Be(1); a1.UnitsDestroyed.Should().Be(3);
    a1.BattleHonours.Should().HaveCount(2);
    var a2 = a.Units.Single(u => u.Id == "a2");
    a2.Xp.Should().Be(0); a2.BattlesSurvived.Should().Be(0); a2.BattleScars.Should().ContainSingle();
    a.RequisitionPoints.Should().Be(6);
    var b = (await Crusades().LoadAllCrusadesAsync()).Single(o => o.PlayerId == "p2");
    b.Units.Should().BeEmpty();
    var campaign = await Campaigns().LoadCampaignAsync();
    campaign.BattleHistory.Single().Status.Should().Be(BattleStatus.Completed);
    campaign.Players.Single(p => p.Id == "p1").Wins.Should().Be(1);
    campaign.Players.Single(p => p.Id == "p2").Losses.Should().Be(1);
    ((Territory)campaign.MapNodes.Single()).ControllingPlayerId.Should().Be("p1");
}
[Fact] public async Task Complete_twice_throws() { await Svc().CompleteAsync(battleId); await Svc().Invoking(s => s.CompleteAsync(battleId)).Should().ThrowAsync<InvalidOperationException>(); }
[Fact] public async Task Complete_rejects_wrong_honour_count() { /* a1 with HonoursGained empty → throws, nothing saved: a1.Xp still 14 */ }
[Fact] public async Task Rp_caps_at_ten() { /* order with RequisitionPoints 10 stays 10 */ }
```

- [ ] **Step 2: Run to verify they fail** — build error.
- [ ] **Step 3: Implement the service as specified; validate every unit before mutating anything so a throw leaves no partial save.**
- [ ] **Step 4: Run** — `dotnet test Warhammer.Tests --nologo` → all pass.
- [ ] **Step 5: Commit** — `feat: post-battle sequence applies a Battle to cards, orders and map` with attribution.

---

### Task 5: Muster page

**Files:**
- Create: `T'au/Components/Pages/Battle/Muster.razor` (`@page "/battle/muster"`)
- Modify: `T'au/Components/Layout/NavMenu.razor` (add "Muster a Battle" under Campaign, above War Room), `T'au/Components/Pages/Campaign/WarRoom.razor` (Engagement log: show `Status`; a `Resume` link to `/battle/{id}/post` for Mustered rows; a "Muster a Battle" button in the panel head)
- Test: `Warhammer.Tests/Components/MusterTests.cs` (bUnit)

**Interfaces:**
- Consumes: `CrusadeManagerService.LoadAllCrusadesAsync`, `CampaignManagerService.LoadCampaignAsync/SaveCampaignAsync`, `MissionCatalog.PrimaryMissions`, `BattleRecord` from Task 1.
- Page state: `string? Order1Id, Order2Id; int PointsLimit = 2000; string Mission = MissionCatalog.PrimaryMissions[0]; string MissionOther = ""; HashSet<string> Picked1, Picked2;` Derived: `Points1 = Σ TotalPoints of picked cards`, `Over1 = Points1 > PointsLimit`, `NoCharacter1 = !picked.Any(IsCharacter)`; same for side 2. "Begin battle" is disabled when either order is missing, the two orders are the same, or either side is over the limit.
- On "Begin battle": add `new BattleRecord { Status = Mustered, PointsLimit, MissionName = Mission == "Other" ? MissionOther : Mission, Player1Id = order1.PlayerId, Player2Id = order2.PlayerId, Player1OrderId, Player2OrderId, UnitResults = picked cards → new BattleUnitResult { CardId, PlayerId, UnitName, Points } }` to `campaign.BattleHistory`, save, navigate to `/battle/{id}/post`.

- [ ] **Step 1: Write the failing bUnit test**: register `CampaignManagerService` and `CrusadeManagerService` on a `SqliteTestBase` factory, seed two orders with two cards each (90 and 950 points), render `<Muster />`, pick both orders, tick both cards on side 1 with `PointsLimit = 1000`, assert the side-1 points text reads `1040 / 1000` and the Begin button is disabled; untick the 950 card, assert enabled; click, assert `campaign.BattleHistory` has one Mustered battle with one unit result.

- [ ] **Step 2: Run to verify it fails.** — [ ] **Step 3: Build the page using the existing Crusade page CSS classes (plain Bootstrap; the datasheet look arrives with plan 04 rollout).** — [ ] **Step 4: Run** — all pass. — [ ] **Step 5: Commit** `feat: Muster page creates a Battle from two Crusade Armies` with attribution.

---

### Task 6: Post-battle page

**Files:**
- Create: `T'au/Components/Pages/Battle/PostBattle.razor` (`@page "/battle/{Id}/post"`), `T'au/Components/Pages/Battle/HonourPicker.razor`
- Test: `Warhammer.Tests/Components/PostBattleTests.cs` (bUnit)

**Interfaces:**
- Consumes: `PostBattleService.CompleteAsync`, `OutOfActionRules.OptionsFor`, `XpRules.RanksGained`, `CrusadeTraitsService.ByType`, `RosterLoadService.LoadRosterAsync(order.PlayerId)` for the weapon names a per-weapon honour needs, the honour-options logic now in `CrusadeDetail.razor` (`HonourOptionsFor`, `NeedsWeaponPick`, weapon list per unit) — move those three into `HonourPicker.razor` with parameters `OrderOfBattle Order, CrusadeUnitCard Card, Selection? RosterUnit, EventCallback<AppliedTrait> Picked`, and make `CrusadeDetail.razor` use the component.
- Steps rendered one at a time with Back/Next, all editing the in-memory `BattleRecord`:
  1. Result: winner (radio of the two players), VP per side (numbers), captured territory (select of `Territory` nodes plus "none"), notes.
  2. Casualties: per side, a checkbox per unit result → `Destroyed`.
  3. Tallies: per unit a number input → `EnemyUnitsDestroyed`; per side one radio group → `MarkedForGreatness` (at most one per side by construction).
  4. Out of Action: per destroyed unit, Pass/Fail buttons; on Fail show the options from `OutOfActionRules.OptionsFor` as buttons: "Battle Scar" opens a select of `Traits.ByType(BattleScar)` → `ScarTaken`; "Devastating Blow" opens a select of the card's honours → `HonourLost`; `WillBeRemoved` sets `RemovedFromOrder` and shows "This unit will be struck from the Order of Battle".
  5. Rank-ups: for each unit where `XpRules.RanksGained(card.Xp, card.Xp + XpRules.XpFor(r, card), …) > 0`, render that many `HonourPicker`s; Next disabled until every picker has a value.
  6. Review: summary table per unit (XP gained, outcome), then "Complete battle" → `CompleteAsync` → navigate to `/war-room`. Errors from the service are shown inline, not swallowed.
- Mustered battles not found or already Completed render a one-line message with a link to the War Room.

- [ ] **Step 1: Write the failing bUnit test**: seed as in Task 4, render `<PostBattle Id="…" />`, walk the steps by clicking Next, on step 4 click Fail for `a2` and pick a scar, on step 5 pick one honour for `a1`, click Complete, assert the battle is Completed and `a1` has two honours (through the services).

- [ ] **Step 2: Run to verify it fails.** — [ ] **Step 3: Build the page and `HonourPicker`, refactor `CrusadeDetail.razor` to use the picker.** — [ ] **Step 4: Run** — `dotnet test Warhammer.Tests --nologo` all pass; `dotnet build` clean. — [ ] **Step 5: Commit** `feat: guided post-battle sequence` with attribution.

---

### Task 7: Documentation

**Files:**
- Modify: `README.md` (Crusade section: Muster, Post-battle Sequence, data in `campaign.db`), `GLOSSARY.md` (no change expected; verify terms match the UI copy: "Muster", "Crusade Army", "Post-battle Sequence", "Out of Action Test")

- [ ] **Step 1: Update the README and check the glossary wording against the two pages.** — [ ] **Step 2: Commit** `docs: Crusade flow` with attribution.
