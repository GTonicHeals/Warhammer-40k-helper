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
- Hit roll, wound roll, armor save, and invulnerable save resolution
- Strength vs. Toughness wound table
- Modifiers for Lethal Hits, Devastating Wounds, AP, and mortal wounds
- Visual attack sequence flow
- Select a unit from your roster to pre-fill weapon profiles

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
   - `roster.json` — Player 1 army
   - `enemy.json` — Player 2 army (optional)
3. Verify `wwwroot/data/Stratagems.json` is present (included in repo).

**Run:**
```bash
cd "T'au"
dotnet watch run
```

The app will be available at `https://localhost:7000` (or the port shown in terminal output).

---

## Roster Format

Rosters are BattleScribe JSON exports. The stratagem mapper reads the `stratagems` field on each unit entry to build the per-unit stratagem list:

```json
{
  "data": {
    "units": [
      {
        "name": "Hellblaster Squad",
        "stratagems": "COMMAND RE-ROLL|GO TO GROUND|FIRE OVERWATCH"
      }
    ]
  }
}
```

---

## Roadmap

- Global search bar in the Roster view
- MathHammer probability calculator for damage output analysis
- Browser-based roster file upload (replace static JSON dependency)

---

## Disclaimer

This project is for personal and educational use. Warhammer 40,000 is a trademark of Games Workshop. This application is not affiliated with or endorsed by Games Workshop.
