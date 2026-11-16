# Campaign Switching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep any number of campaigns and switch which one the app works on, without touching the others.

**Architecture:** A nullable `Campaign.LastOpenedUtc` column. The current campaign is the latest-opened one, falling back to the lowest Id. `CampaignManagerService` gains list, switch, create and delete calls. `CrusadeManagerService` filters Orders of Battle by the current campaign. A new `/campaigns` page and a sidebar link drive switching. Each switch does a full reload so scoped caches reset.

**Tech Stack:** .NET / Blazor Server, EF Core + SQLite, xUnit + FluentAssertions (SqliteTestBase).

**Spec:** `docs/superpowers/specs/2026-11-16-campaign-switching-design.md`

## Global Constraints

- Switching or creating a campaign never changes or deletes data in another campaign.
- The current campaign cannot be deleted. Deletion requires typing the campaign's exact name.
- Existing databases keep working with no backfill (`LastOpenedUtc` null → lowest Id is current).

## Review Focus

- Existing single-campaign DB (all `LastOpenedUtc` null): the old campaign is still current → test in Task 1.
- A new Order of Battle saved after a switch lands in the *new* campaign, not a stale cached one → test in Task 2.
- Deleting a non-current campaign leaves the current campaign's children intact → test in Task 1.
- Delete called on the current campaign is refused (throws) → test in Task 1.
- Creating a campaign gives it its own default players and Requisitions, independent of others → test in Task 1.

---

### Task 1: Current campaign in `CampaignManagerService`

**Files:**
- Modify: `T'au/Models/Campaign/Campaign.cs` (add `public DateTime? LastOpenedUtc { get; set; }`)
- Modify: `T'au/Models/Campaign/CampaignPlayer.cs`, `T'au/Data/AppDbContext.cs` (found during the build: player Ids "p1"/"p2" were a database-wide PK, so a second campaign couldn't be created. The key becomes `(CampaignId, Id)`. Test: `DbBootstrapTests.Players_survive_the_move_to_per_campaign_keys`)
- Create: migration `AddCampaignSwitching` via `dotnet ef migrations add AddCampaignSwitching --project "T'au"`
- Modify: `T'au/Services/CampaignManagerService.cs`
- Create: `T'au/Models/Campaign/CampaignSummary.cs`
- Test: `Warhammer.Tests/Campaign/CampaignSwitchingTests.cs`

**Interfaces:**
- Produces:
  - `record CampaignSummary(int Id, string Name, int PlayerCount, int BattleCount, DateTime? LastOpenedUtc, bool IsCurrent)`
  - `Task<int> GetCurrentCampaignIdAsync()` returns the current Id, seeding the default campaign if none exist
  - `Task<IReadOnlyList<CampaignSummary>> ListCampaignsAsync()`, current first, then by `LastOpenedUtc` desc, then Id
  - `Task SwitchToAsync(int campaignId)` stamps `LastOpenedUtc = DateTime.UtcNow` and clears the cached `Campaign`. Throws `InvalidOperationException` for an unknown Id
  - `Task<Campaign> CreateCampaignAsync(string name)` inserts a default campaign (players p1/p2), switches to it, and returns `LoadCampaignAsync()`
  - `Task DeleteCampaignAsync(int campaignId)` throws `InvalidOperationException` if the Id is the current campaign. Otherwise it deletes the row, and the cascade removes its children and Orders of Battle
  - `Task RenameCampaignAsync(int campaignId, string name)` writes only `CampaignName` (`ExecuteUpdateAsync`), trimmed, and ignores blank names
  - `LoadCampaignAsync()` loads the campaign whose Id is `GetCurrentCampaignIdAsync()`

- [ ] **Step 1: Write failing tests** in `CampaignSwitchingTests : SqliteTestBase`:
  - `Legacy_db_without_last_opened_uses_lowest_id`: insert "A" then "B" directly, so `LoadCampaignAsync().CampaignName == "A"`
  - `Switch_makes_that_campaign_current`: A and B exist, `SwitchToAsync(B.Id)`, so `LoadCampaignAsync().Id == B.Id` (also from a new service instance) and `ListCampaignsAsync()[0]` is B with `IsCurrent`
  - `Create_makes_a_new_current_campaign_and_leaves_old_untouched`: load default, add a Territory and save. `CreateCampaignAsync("Second")` returns name "Second", 2 players, 0 nodes and 7 Requisitions. Switching back to the first shows its Territory
  - `Switch_to_unknown_id_throws`
  - `Delete_current_campaign_throws`
  - `Delete_other_campaign_removes_only_it`: A has a node and B has a node, current is A, `DeleteCampaignAsync(B.Id)` leaves 1 campaign and A's node intact
  - `List_reports_counts`: player count 2, battle count matches
  - `Rename_changes_only_name`: rename B while A is current, so B's name changes and A is still current
- [ ] **Step 2:** Run `dotnet test --filter CampaignSwitchingTests`. Expect a compile failure or FAIL.
- [ ] **Step 3:** Add the column and migration, `CampaignSummary`, and the service members above.
- [ ] **Step 4:** Run `dotnet test`. Expect the whole suite to PASS, including the existing `CampaignManagerServiceTests`.
- [ ] **Step 5:** Commit `feat: switch between campaigns in CampaignManagerService`.

### Task 2: Crusades follow the current campaign

**Files:**
- Modify: `T'au/Services/CrusadeManagerService.cs` (`LoadAllCrusadesAsync`, `SaveCrusadeAsync`)
- Test: `Warhammer.Tests/Services/CrusadeManagerServiceTests.cs`

**Interfaces:**
- Consumes: `CampaignManagerService.GetCurrentCampaignIdAsync()`, `SwitchToAsync`, `CreateCampaignAsync`

- [ ] **Step 1: Write failing tests:**
  - `Load_returns_only_current_campaigns_orders`: save an order in campaign 1, `CreateCampaignAsync("Two")`, save an order, and Load returns only the second. After `SwitchToAsync(first)`, Load returns only the first
  - `New_order_after_switch_joins_new_campaign`: use the same service instance whose `campaigns.Campaign` was loaded before the switch. The order saved after `CreateCampaignAsync` has the new campaign's Id
- [ ] **Step 2:** Run them and expect FAIL.
- [ ] **Step 3:** `LoadAllCrusadesAsync` filters `o.CampaignId == await campaigns.GetCurrentCampaignIdAsync()`. `SaveCrusadeAsync` uses `GetCurrentCampaignIdAsync()` when `CampaignId == 0`, not the cached `campaigns.Campaign`.
- [ ] **Step 4:** Run `dotnet test` and expect PASS.
- [ ] **Step 5:** Commit `feat: Crusade list follows the current campaign`.

### Task 3: Campaigns page and sidebar link

**Files:**
- Create: `T'au/Components/Pages/Campaign/Campaigns.razor` (`@page "/campaigns"`)
- Modify: `T'au/Components/Layout/NavMenu.razor` (a "Campaigns" link showing the current campaign name, above "Active Crusade")

**Interfaces:**
- Consumes: all Task 1 members.

- [ ] **Step 1:** The page has a table of `CampaignSummary` rows (name, players, battles, last opened, "Current" badge). Each row has **Open** (`SwitchToAsync`, then `Nav.NavigateTo("war-room", forceLoad: true)`), **Rename** (inline input, then `RenameCampaignAsync`) and **Delete** (non-current only, opens an inline confirm that enables only when the typed text equals the name exactly). A "New campaign" input and button calls `CreateCampaignAsync`, then `forceLoad` navigates to `campaign-editor`.
- [ ] **Step 2:** NavMenu: inject `CampaignManagerService`, load `ListCampaignsAsync()` in `OnAfterRenderAsync` alongside the crusades, and show `bi-collection` "Campaigns" with the current name below in small text.
- [ ] **Step 3:** Run `dotnet build` and `dotnet test`, then open the app in the browser and check: create a campaign, see an empty war room, switch back, and see the old map and armies.
- [ ] **Step 4:** Commit `feat: Campaigns page for switching, creating, renaming and deleting`.
