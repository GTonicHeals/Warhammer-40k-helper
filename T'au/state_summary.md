
# Project State Summary: Warhammer 40k Tactical Dashboard

## 1. Project Overview
A Blazor Web Application (Interactive Server Mode, .NET 8) designed to act as a digital dashboard for Warhammer 40,000 (10th Edition) matches. It parses exported BattleScribe JSON rosters, dynamically links units to playable stratagems, and provides interactive quick-reference tools.

## 2. Tech Stack
* **Framework:** ASP.NET Core Blazor
* **Language:** C# 12, HTML, CSS, JavaScript (Interop)
* **Styling:** Bootstrap 5, Custom CSS (Dark Theme)
* **Data Handling:** `System.Text.Json` (Deserialization and DOM parsing)

## 3. Core Components & Features

### A. Roster Viewer (`Roster.razor`)
* **Dual Routing:** Accepts a routing parameter (`/roster/{Side?}`) to load either `roster.json` (Player 1) or `enemy.json` (Player 2).
* **Dynamic Theming:** * Player 1 uses an Orange/Dark styling.
  * Player 2 (`Side == "p2"`) overrides CSS to use a Blue/Dark styling.212121
* **Datasheet Parsing:** Recursively extracts Profiles, Characteristics, Weapons (Ranged/Melee), Abilities, and Keywords from the BattleScribe JSON schema.
* **Smart Stratagem Integration:** * Parses a local `Stratagems.json` file.
  * Links units to stratagems using a string-split mapping logic.
  * Filters out Core Stratagems (handled elsewhere) and Stratagems that do not match the parsed Detachment Name.
* **Tactical Color Coding:** Stratagem buttons are dynamically colored based on the `Turn` requirement:
  * Blue: Your Turn / Your Shooting / Your Charge
  * Red: Opponent's Turn
  * Green: Either Player's Turn
* **Stratagem Modal:** Clicking a stratagem button opens a Bootstrap modal displaying the full CP cost, Phase, and Rule text (formatted via Regex).
* **Navigation:** JavaScript interop for smooth scrolling to specific unit datasheets via URL fragments.

### B. Quick Reference (`QuickRef.razor`)
* **Turn Sequence:** A visual, step-by-step breakdown of the 5 main phases (Command, Movement, Shooting, Charge, Fight) with key mechanical reminders (e.g., Battle-shock, Desperate Escape).
* **Interactive Wound Calculator:** Two input bindings (Strength vs Toughness) that instantly calculate the required D6 wound roll and update the UI with color-coded results.
* **Keyword Glossary:** A searchable list of Universal Special Rules.
  * Utilizes `Regex` to highlight the matched search string within the keyword name and definition.
  * Uses `sessionStorage` via JS interop to persist the search filter text across page navigations.
* **Sticky Navigation:** In-page anchor links to jump between the Sequence, Calculator, and Keywords sections.

### C. Core Stratagems (`CoreStratagems.razor`)
* A dedicated view for universal/core stratagems (e.g., Command Re-roll, Grenades).
* Uses a hardcoded `List<string>` to enforce standard rulebook ordering over alphabetical sorting.

## 4. Data Architecture

### A. Roster Data (`roster.json` / `enemy.json`)
Follows the standard BattleScribe JSON export schema:
* `roster.costs`: For calculating total points.
* `roster.forces[0].selections`: The primary array of units.
* Nested `selections` and `profiles` require recursive C# methods to accurately extract weapon stats and abilities.

### B. Stratagem Database (`data/Stratagems.json`)
A custom JSON structure containing two main arrays:
1. **`stratagems`**: Contains the rule data (`id`, `name`, `cp_cost`, `turn`, `phase`, `description`, `detachment`, `faction_id`).
2. **`units`**: Contains the mapping linking a unit name to a pipe-separated string of stratagem names (e.g., `"name": "Intercessors", "stratagems": "ARMOUR OF CONTEMPT|HONOUR THE CHAPTER"`).

## 5. Current State & Known Behaviors
* **Browser Scrolling:** SPA routing intercepts standard anchor links. JavaScript functions (`scrollToElementId`) and Blazor lifecycle methods (`OnLocationChanged`, `OnAfterRenderAsync`) are heavily utilized to manage scroll positions and fragment navigation.
* **Data Loading:** File I/O (`File.ReadAllTextAsync`) is currently used to read static JSON files from the `wwwroot` directory during `OnParametersSetAsync`.

## 6. Future Roadmap
1. **Global Search:** Add a search bar to `Roster.razor` to quickly find specific weapons, abilities, or units across the entire army.
2. **MathHammer Tool:** Build a probability calculator to determine expected damage output based on Attacks, BS/WS, S vs T, AP, and Damage characteristics.
3. **Dynamic File Uploads:** Transition from reading static files (`roster.json`) from `wwwroot` to an `InputFile` component, allowing users to upload their own `.json` or `.rosz` files directly via the browser.