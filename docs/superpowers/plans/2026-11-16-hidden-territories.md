# Hidden Territories Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Territories can be hidden from the War Room and are revealed by hand (Campaign Editor) or when a completed battle captures a neighbouring territory.

**Architecture:** `Node.IsPublic` (already a column, unused for territories) means "revealed". A pure static helper `TerritoryReveal` decides what a capture reveals and what the War Room may show; `PostBattleService`, `PostBattle.razor`, `WarRoom.razor` and `CampaignEditor.razor` all call it. A data migration marks existing territories revealed.

**Tech Stack:** .NET 10 Blazor Server, EF Core SQLite, xUnit + FluentAssertions + bUnit.

**Spec:** `docs/superpowers/specs/2026-11-16-hidden-territories-design.md`

## Global Constraints
- No new column: territory visibility is `Node.IsPublic`. Events keep `IsEventRevealed`.
- New territories start hidden (`IsPublic = false`, the existing default).
- Editor controller changes (`SetController`) must NOT reveal anything.
- The War Room must not show a hidden territory's node, name, connection line, or count.

## Review Focus
- A capture whose territory has a connection to an **event** node: events are never "revealed" by this feature (only `Territory` neighbours). → test in Task 1.
- Connection listed only on the neighbour's side (one-directional data): still counts. → test in Task 1.
- An event attached to a hidden territory must not appear on the War Room even if `IsEventRevealed`. → test in Task 1.
- The War Room side panel "links" list for a selected territory must not name hidden neighbours. → handled in Task 4 via `TerritoryReveal.IsVisible`.
- A draw / no captured territory reveals nothing. → test in Task 2.

---

### Task 1: `TerritoryReveal` helper

**Files:**
- Create: `T'au/Helpers/TerritoryReveal.cs`
- Test: `Warhammer.Tests/Campaign/TerritoryRevealTests.cs`

**Interfaces — Produces:**
- `public static IReadOnlyList<Territory> RevealedByCapture(IEnumerable<Node> nodes, string? capturedTerritoryId)` — hidden territories that a capture reveals: the captured territory itself if hidden, plus every hidden `Territory` connected to it in either direction (A lists B, or B lists A). Empty when id is null/unknown. Ordered by `Name`. Does not mutate.
- `public static bool IsVisible(Node node, IEnumerable<Node> nodes)` — `Territory` → `IsPublic`; `CampaignEvent` → `IsEventRevealed` AND its `AttachedToNodeId` territory exists and `IsPublic`.
- `public static List<Node> VisibleOnWarRoom(IEnumerable<Node> nodes)` — nodes where `IsVisible`.

- [ ] **Step 1: Write failing tests** (namespace `Warhammer.Tests.Campaign`, plain `[Fact]`s, no DB):
  - `Capture_reveals_hidden_neighbours_and_the_captured_territory`: t1 (hidden, connects t2), t2 hidden, t3 hidden unconnected → names `["t1","t2"]`.
  - `Capture_counts_connections_listed_only_on_the_neighbour`: t1 has no links, t2 lists t1, t2 hidden → contains t2.
  - `Capture_skips_already_revealed_and_events`: t1 public connects t2 (public) and e1 (CampaignEvent) → empty.
  - `No_capture_reveals_nothing`: id null and id "missing" → empty.
  - `War_room_hides_hidden_territories_and_their_events`: t1 public, t2 hidden, e1 revealed attached to t2, e2 revealed attached to t1, e3 unrevealed attached to t1 → ids `["t1","e2"]`.
- [ ] **Step 2:** `dotnet test --filter TerritoryRevealTests` → FAIL (type not found).
- [ ] **Step 3:** Implement `TerritoryReveal` in namespace `Warhammer.Helpers`.
- [ ] **Step 4:** `dotnet test --filter TerritoryRevealTests` → PASS.
- [ ] **Step 5:** Commit `feat: TerritoryReveal decides what captures reveal and what the War Room shows`.

### Task 2: Captures reveal on battle completion

**Files:**
- Modify: `T'au/Services/Crusade/PostBattleService.cs:50-52`
- Modify: `T'au/Components/Pages/Battle/PostBattle.razor` (review step, ~line 880; territory `<option>` ~line 440)
- Test: `Warhammer.Tests/Crusade/PostBattleServiceTests.cs`, `Warhammer.Tests/Components/PostBattleTests.cs`

**Interfaces — Consumes:** `TerritoryReveal.RevealedByCapture`.

- [ ] **Step 1: Failing tests.**
  - Service `Complete_reveals_the_captured_territory_and_its_hidden_neighbours`: SeedAsync tweak can't add nodes, so after `SeedAsync()` load the campaign, set t1 `ConnectedNodeIds = ["t2"]`, add `Territory { Id="t2", Name="Ridge" }` and `Territory { Id="t3", Name="Far" }` (both hidden), save; complete; assert t1, t2 `IsPublic` true, t3 false.
  - Service `A_draw_reveals_nothing`: same setup with `WinnerPlayerId = null` via tweak → t1, t2 stay hidden.
  - Component `Review_lists_the_territories_the_capture_reveals`: after SeedAsync add hidden t2 "Ridge" linked to t1; pick winner p1 and territory t1, walk to Review (copy the walk from `Walking_the_flow_completes_the_battle`); `#pb-reveals` text contains `"Hab"` and `"Ridge"`. With no capture, `#pb-reveals` absent.
- [ ] **Step 2:** run the three tests → FAIL.
- [ ] **Step 3:** In `PostBattleService`, after setting `ControllingPlayerId`, `foreach (var t in TerritoryReveal.RevealedByCapture(campaign.MapNodes, battle.CapturedTerritoryId)) t.IsPublic = true;` (inside the same winner branch, so a draw reveals nothing). In `PostBattle.razor` StepReview, under the headline: when the winner is set and `RevealedByCapture` is non-empty, render `<p id="pb-reveals" class="small text-info"><i class="bi bi-eye me-1"></i>Reveals: A, B</p>`. In the territory dropdown, suffix hidden territories' option text with ` (hidden)`.
- [ ] **Step 4:** `dotnet test --filter "PostBattle"` → all PASS.
- [ ] **Step 5:** Commit `feat: capturing a territory reveals its hidden neighbours`.

### Task 3: Migration keeps existing territories visible

**Files:**
- Create via `dotnet ef migrations add RevealExistingTerritories` (run from `T'au/`), then edit `Up`/`Down`.

- [ ] **Step 1:** Generate the migration; it should be empty (no model change). Put in `Up`: `migrationBuilder.Sql("UPDATE Nodes SET IsPublic = 1 WHERE NodeType = 'Territory';");` `Down`: no-op (comment why: original per-row values are unknown).
- [ ] **Step 2:** `dotnet build` succeeds and `dotnet test` full suite passes (test DBs migrate through it).
- [ ] **Step 3:** Commit `feat: existing territories start revealed`.

### Task 4: War Room shows revealed territories only

**Files:**
- Modify: `T'au/Components/Pages/Campaign/WarRoom.razor:67-69` (map nodes), `:121` (links list), `RefreshStats` (`:316-335`)

- [ ] **Step 1:** Map `Nodes="TerritoryReveal.VisibleOnWarRoom(_campaign.MapNodes)"`; the "NO TERRITORIES" guard checks `Any(t => t.IsPublic)` among territories. Lines need no change: `CampaignMap` skips edges whose ends aren't in `Nodes`.
- [ ] **Step 2:** Links list: only names of nodes where `TerritoryReveal.IsVisible(n, _campaign.MapNodes)`.
- [ ] **Step 3:** `RefreshStats` iterates `MapNodes.OfType<Territory>().Where(t => t.IsPublic)` for both the per-player dictionary and totals.
- [ ] **Step 4:** Test in `WarRoomTests`: `Hidden_territories_are_not_counted_or_named` — seed t1 public p1-controlled "Hab", t2 hidden p1-controlled "Secret Vault"; set `_ctx.JSInterop.Mode = JSRuntimeMode.Loose` in that test; render; markup does not contain "Secret Vault"; `.map-node[data-node-id='t2']` absent; `.map-node[data-node-id='t1']` present. Run `dotnet test --filter WarRoomTests` → PASS.
- [ ] **Step 5:** Commit `feat: War Room hides territories that aren't revealed`.

### Task 5: Campaign Editor toggle, Reveal all, hidden styling

**Files:**
- Modify: `T'au/Components/Pages/Campaign/CampaignEditor.razor` (territory card ~line 220; map toolbar ~line 89)
- Modify: `T'au/Components/UI/CampaignMap.razor:31-49` (+ CSS)
- Test: create `Warhammer.Tests/Components/CampaignEditorHiddenTerritoryTests.cs` (copy the ctor of `CampaignEditorRequisitionsTests`)

- [ ] **Step 1: Failing tests.**
  - `Toggle_reveals_a_hidden_territory`: seed hidden t1 "Hab"; render; click `.map-node[data-node-id='t1']`; `#ce-visibility` text contains "Hidden"; click it → text contains "Revealed"; click `#campaign-save`; reload → t1 `IsPublic` true.
  - `Reveal_all_reveals_every_territory`: seed hidden t1, t2; click `#ce-reveal-all`, then save → both `IsPublic`.
  - `Hidden_territories_are_marked_on_the_editor_map`: `.map-node[data-node-id='t1']` has class `node-hidden` and label starts with "🔒".
- [ ] **Step 2:** run → FAIL.
- [ ] **Step 3:** Editor: in the territory card meta (next to the controller badge) a button `id="ce-visibility"`, `btn-outline-info` with `bi-eye` "Revealed" when public, `btn-outline-secondary` with `bi-eye-slash` "Hidden" otherwise; click flips `IsPublic` and `MarkDirty()`. Toolbar button `id="ce-reveal-all"` (`bi-eye`, "Reveal all"), disabled when no hidden territory; sets all territories `IsPublic = true` and `MarkDirty()`. Map: when `!IsReadOnly && node is Territory { IsPublic: false }`, add class `node-hidden` (CSS `opacity: 0.55`) and prefix label with `"\U0001f512 "` — same pattern as private notes at line 54.
- [ ] **Step 4:** `dotnet test` full suite → PASS.
- [ ] **Step 5:** Commit `feat: hide and reveal territories from the Campaign Editor`.

### Task 6: Verify in the browser
- [ ] Run `dotnet run --launch-profile https` in `T'au/`; Campaign Editor shows existing territories revealed; add a territory → 🔒 and absent from War Room; toggle → appears in War Room.
