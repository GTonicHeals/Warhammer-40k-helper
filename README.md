# Warhammer 40k: Tactical Dashboard

A Blazor Server web application for Warhammer 40,000 10th Edition gameplay. Parses army roster exports to provide an interactive game-day companion with wound tracking, stratagem lookup, damage calculation, objective scoring, and faction rules reference.

## Features

### Roster Viewer
- Multi-army support — loads `p1.json` (Player 1), `p2.json` (Player 2) and `p{n}.json` (Player n) simultaneously
- Unit datasheet cards with stats, weapon profiles, and abilities
- Per-unit wound tracking with +/- buttons and destroyed state
- Battle-Shock status indicators
- Points totals and limits display
- Color-coded theme per player (orange for P1, blue for P2, etc)
- Detachment rule display in context

### Wound Calculator
- "Datasheet duel" look: the attacker and defender appear as two datasheet cards facing each other, with the three rolls (Hit, Wound, Save) on a spine between them. Three columns on tablet landscape (≥ 900px), stacked on phones. Fonts: Barlow Condensed and Barlow; tokens in `wwwroot/css/datasheet-tokens.css`
- Shows the roll needed only (10th edition); hit and wound modifiers capped at ±1
- Tap a card's header to pick a unit (player filter All / P1 / P2 / …); tap a weapon row to select it; ⇄ swaps attacker and defender
- Automatic modifiers derived from the datasheets, shown as chips marked "auto": Torrent (auto-hit), Heavy, Lance, Indirect Fire, Stealth (ranged only), Anti-X (wound roll capped at X+ against the matching keyword), Ignores Cover, Big Guns Never Tire, invulnerable save
- Situation toggles: tap a dashed (waiting) chip to say the attacker remained stationary, charged, the target is not visible, or the unit is in Engagement Range; In Cover is a chip on the defender (blocked against melee, Ignores Cover, and Sv 3+ vs AP 0)
- Manual modifiers: +1 Hit, +1 Wound, −1 Hit, −1 Wound, AP −1; invulnerable save override None/3+/4+/5+/6+
- Crusade Battle Honours and Battle Scars adjust stats automatically, with a gold asterisk naming the honour
- Weapon keyword badges with glossary tooltips; per-model wounds for mixed units
- Inline stratagems for each side, unit abilities and detachment rule with tooltips and detail modals; Datasheet button opens the full datasheet
- Selections persist when you navigate away and back

### Stratagem Management
- Database of 1,300+ stratagems loaded from `wwwroot/data/Stratagems.json`
- Keyword-based unit-to-stratagem matching
- Faction and detachment filtering (illegal stratagems removed automatically)
- Grouped by turn availability: Your Turn / Opponent's Turn / Either

### Tactical Objectives Tracker
- Dual-player scoring with separate states
- Primary objective scoring across 4 rounds (capped at 50 VP)
- Secondary objective VP counter (capped at 40 VP)
- 15-card objective deck: draw, activate, and score missions
- Automated total calculation per 10th Edition rules

### Game State Bar
- Persistent header bar showing current battle round (1–5)
- Per-player Command Points counter with +/- controls
- Global reset for all game state

### Quick Reference
- Full 10th Edition turn sequence (Command → Movement → Shooting → Charge → Fight → Morale)
- Searchable keyword glossary for 30+ universal special rules (LETHAL HITS, BLAST, ANTI, etc.)
- Real-time text highlighting via regex

### Faction Rules
Dedicated reference pages for supported factions:
- T'au Empire
- Space Marines
- Black Templars
- Necrons
- Orks
- Tyranids

Navigation links appear dynamically based on which factions are loaded in the active rosters.

### Scenarios
Battle scenario reference for matched and crusade play.

### Crusade
- Orders of Battle and Crusade Cards are stored in the SQLite campaign database (`T'au/campaign.db`) next to the Campaign Map, so every browser sees the same forces.
- Orders of Battle created in an older version (browser localStorage) are imported into the database automatically the first time the app is opened in that browser.
- Crusade Points are derived from Battle Honours (+1, +2 if TITANIC; relics +1/+2/+3 by tier) and Battle Scars (−1); they are never typed.
- Each Crusade Card shows Battles played, Battles survived and enemy units destroyed. TITANIC and XP-ineligible units (EPIC HERO, FORTIFICATION, SWARM) are flagged from the datasheet keywords.
- The schema is managed with EF Core migrations; the app applies pending migrations at startup.
- **Muster a Battle** (`/battle/muster`): pick two Orders of Battle, a points limit (1000/2000/3000) and a mission (a primary mission or free text), then tick each side's Crusade Army. The Battle cannot begin while a side is over the limit; a side with no CHARACTER gets a warning.
- **Post-battle Sequence** (`/battle/{id}/post`, also reachable from the War Room's Engagement log): a guided flow of Result → Casualties → Tallies (enemy units destroyed, one Marked for Greatness per side) → Out of Action tests (typed in; a fail takes a Battle Scar or a Devastating Blow, three scars force the blow, and a unit with three scars and no honour is struck off) → Rank-ups (pick the Battle Honours owed) → Review. Completing it awards XP (Battle Experience, Dealers of Death, Marked for Greatness; EPIC HERO/FORTIFICATION/SWARM gain none), updates the tallies, adds +1 Requisition Point per side (max 10), records wins and losses, and hands the captured territory to the winner. A Battle is completed once and never applied twice.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core Blazor (Interactive Server) |
| Language | C# / .NET 10.0 |
| Frontend | Bootstrap 5, Custom CSS3 |
| Data | System.Text.Json |
| Interop | JavaScript Runtime (DOM, smooth scroll) |

---

## Setup

**Prerequisites:** .NET 10.0 SDK

1. Clone the repository.
2. Place your BattleScribe-exported roster files in `T'au/wwwroot/`:
   - `p1.json` — Player 1 army
   - `p2.json` — Player 2 army (optional)
   - `p3.json`, `p4.json`, … — additional players (optional)
3. Verify `wwwroot/data/Stratagems.json` is present (included in repo).

**Run:**
```bash
cd "T'au"
dotnet watch run
```

The app will be available at `https://localhost:7000` (or the port shown in terminal output).

---

## GM access

The Campaign Editor (map, GM notes, table mode) is for the Game Master only. There is no password:
the app is served through `tailscale serve`, which adds the visitor's Tailscale login as the
`Tailscale-User-Login` request header. A login listed in `Gm:TailscaleLogins` is the GM; everyone
else is a guest, who doesn't see the editor link and gets a "GM only" notice at `/campaign-editor`.

- **Development** (`dotnet run`): everyone is GM, since Tailscale isn't in front.
- **Production with no header** (e.g. opening `localhost:7200` on the host): guest.
- The header is trusted because the app only listens on localhost. It identifies the device's
  Tailscale account, not the person holding it.

The committed `appsettings.json` leaves the list empty. Set your login on the hosted service once
(admin PowerShell). `AppEnvironmentExtra` **replaces** the whole list, so read it first and re-set
every existing entry along with the new one:

```powershell
C:\Tools\nssm-2.24\win64\nssm.exe get WarhammerHelper AppEnvironmentExtra
C:\Tools\nssm-2.24\win64\nssm.exe set WarhammerHelper AppEnvironmentExtra <existing entries...> Gm__TailscaleLogins__0=<your-tailscale-login>
C:\Tools\nssm-2.24\win64\nssm.exe restart WarhammerHelper
```

A co-GM is `Gm__TailscaleLogins__1=<their-login>`.

---

## Roster Format

Rosters are New Recruit JSON exports.

---

## Roadmap

- Nothing as for now

---

## Disclaimer

This project is for personal and educational use. Warhammer 40,000 is a trademark of Games Workshop. This application is not affiliated with or endorsed by Games Workshop.
