# Campaign switching — design

## Intent

The Game Master wants to run another game without destroying the current one: keep any number of
campaigns in the database and swap which one the app is working on.

## Goals

- Any number of campaigns, each with its own map, players, battles, GM notes, Requisitions and
  Orders of Battle.
- One **current campaign** at a time. Every campaign page (War Room, Campaign Editor, Map Generator,
  Muster, Post-Battle, Amend Battle, Crusade pages, seeders) works on it.
- A Campaigns page: list campaigns, switch, create a new one, rename.

## Non-goals

- Two campaigns open in two tabs at once (rejected approach B).
- Sharing one Order of Battle across campaigns. An army belongs to the campaign it was created in
  (the existing `OrderOfBattle.CampaignId` FK).
- Copying or templating a campaign from another one.

## Anti-goals

- Switching or creating a campaign must never change or delete data in another campaign.

## Approaches

- **A. Current campaign stored in the database — chosen.** One stored setting is shared by all
  tabs and devices and survives restarts. Pages keep calling `LoadCampaignAsync`.
- B. Campaign id in the URL. Allows parallel tabs, but every page, link and the sidebar would have to
  carry the id. Too much churn for one GM.
- C. Per-browser (localStorage). Devices would disagree and a cleared browser loses the choice.
  Dropped.

## Design

- **Storage (my call):** add a nullable `LastOpenedUtc` column to `Campaign` (EF migration). The
  current campaign is the one with the latest `LastOpenedUtc`, falling back to the lowest Id.
  Switching stamps `LastOpenedUtc = now`. There is no "only one flag is set" rule to keep true, and
  existing databases keep working without a backfill.
- **`CampaignManagerService`:**
  - `LoadCampaignAsync()` loads the current campaign as defined above. It still seeds a default
    campaign when none exist.
  - New `ListCampaignsAsync()` returns a lightweight summary per campaign (Id, name, player count,
    battle count, last opened).
  - New `SwitchToAsync(id)` stamps `LastOpenedUtc`.
  - New `CreateCampaignAsync(name)` inserts a default campaign (two placeholder players, Requisitions
    seeded on its first load as today) and makes it current.
  - Rename goes through the existing Campaign Editor name field, plus inline on the Campaigns page.
- **`CrusadeManagerService.LoadAllCrusadesAsync`:** filter by the current campaign's Id. New Orders of
  Battle already pick up the loaded campaign's Id. They must use the *current* one, so load it rather
  than trusting a stale `campaigns.Campaign`.
- **Switching UI:** a new `/campaigns` page with one row per campaign showing its name, players,
  battles and last played, a "Current" badge on the current one, and Open and Rename buttons, plus a
  "New campaign" button. After a switch, navigate with `forceLoad: true` so every scoped service
  (cached `Campaign`, Crusade snapshot, sidebar) starts fresh.
- **Sidebar:** shows the current campaign's name above the campaign links, linking to `/campaigns`.

## Delete (my call)

Deletion is allowed only for a campaign that is **not** current, behind a confirmation that makes you
type the campaign's name. The cascade FKs already remove its children. Old test campaigns can be
cleaned up this way, and the game you're running can't be deleted by accident.

## Left to the builder

- Exact page styling (follow the existing Campaign Editor look).
- Wording of empty states.
- Tests: service tests for current-campaign selection, switch, create, Crusade filtering and delete
  isolation, in the style of `CampaignManagerServiceTests` (SqliteTestBase).
