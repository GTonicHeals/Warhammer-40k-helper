# Crusade Campaign and 10th Edition Rules: Spec

Agreed 2026-11-10 in a grilling session. Vocabulary is in `GLOSSARY.md`; the storage decision is `docs/adr/0001-crusade-data-in-sqlite.md`. This document is the source the four implementation plans argue from.

## Goals

1. The Wound Calculator implements the 10th edition attack-sequence rules exactly and applies every modifier it can derive from the datasheet by itself.
2. The Crusade side of the app is complete enough to run a Crusade Campaign from the first Battle: Orders of Battle persist in SQLite, a Muster picks the Crusade Army, and a Post-battle Sequence updates every Crusade Card and the Campaign Map from one entry.
3. The app gets the "datasheet duel" look (prototype variant A), calculator first.

## Decisions

### Rules
- 10th edition is the only ruleset. No edition switch, no versioned rule data.
- The Wound Calculator shows rolls needed only. No expected-damage or probability output.
- Benefit of Cover: +1 to the armour save against ranged attacks only. A model with Save 3+ or better gets no bonus against AP 0. Ignores Cover denies it. Cover never touches an invulnerable save.
- Automatic Modifiers, applied with an "auto" marker and a one-tap override: Torrent (hit is automatic), Heavy (+1 hit, needs the stationary toggle), Lance (+1 wound, needs the charged toggle), Indirect Fire (−1 hit and target gains cover, needs the not-visible toggle), Stealth on the defender (−1 hit, ranged only), Anti-X (wound roll capped at X+ when the defender has keyword X), Ignores Cover (cover chip blocked), invulnerable save from the datasheet, Big Guns Never Tire (existing).
- Situation Toggles: remained stationary, charged this turn, target not visible, in cover, and firing while in engagement range (the fact Big Guns Never Tire needs; it is −1 to hit only then).
- Keywords that do not change a roll needed (Lethal Hits, Sustained Hits, Devastating Wounds, Twin-linked, Feel No Pain) stay as informational badges.
- Reference pages get a stale-content audit. Known error: the Lance glossary entry says "improve AP by 1"; the rule is "+1 to the wound roll if the bearer charged this turn".

### Crusade
- Orders of Battle and Crusade Cards live in the SQLite campaign database. A one-time import pulls any Orders of Battle found in browser localStorage.
- Battles are strictly one Order of Battle against another.
- Muster: the Game Master picks two Orders of Battle, a points limit (1000, 2000 or 3000), a mission (from the Mission Generator's primary missions or free text), and ticks the Crusade Army for each side. Validation: army points within the limit; warning when no CHARACTER.
- Post-battle Sequence, one guided flow: result (winner, VP per side, captured territory), casualties ticked by hand, per-unit enemy-units-destroyed tally and one Marked for Greatness per side, Out of Action test results typed in (pass or fail; on fail a Battle Scar chosen or a Devastating Blow naming the honour lost; three scars force a Devastating Blow; no honours left means the unit is removed), rank-up honours picked inside the flow with a weapon picker when the honour targets a weapon, +1 Requisition Point per side capped at 10, map capture and win/loss tallies. Completing the flow marks the Battle complete and it is never entered twice.
- XP: Battle Experience +1 for every unit in the Crusade Army; Dealers of Death +1 per three enemy units destroyed (cumulative tally); Marked for Greatness +3 to one unit. EPIC HERO, FORTIFICATION and SWARM units never gain XP.
- Crusade Points are derived: +1 per Battle Honour (+2 if TITANIC), relics +1/+2/+3 by tier (Artificer/Antiquity/Legendary), −1 per Battle Scar.
- Dice are rolled at the table; the app only records results and offers valid options.
- Requisition spending comes after the Post-battle Sequence; Agendas after that. Faction Crusade content for T'au and Space Marines (Ultramarines use the Codex Space Marines set) is encoded from pages the Game Master supplies; everything else is core only.
- The Game Master is the only editor. No logins, no player view.

### Interface
- Prototype variant A ("Datasheet duel") is the look: two datasheet cards facing each other, bone card faces on a dark table, black header bands with a player-colour stripe, boxed stat strip, a vertical spine of three roll tiles between the sheets, modifier chips in each sheet's footer. Type: Barlow Condensed for display and stats, Barlow for body.
- Tablet landscape first; phone layout stacks attacker, spine, defender.
- The rebuilt calculator keeps every existing feature: player filters, inline stratagems, abilities and detachment rule with tooltips, datasheet modal, defender per-model wounds, selection persistence across navigation.
- Bootstrap stays as the structural layer; a shared token stylesheet carries the look.
- Rollout after the calculator: Crusade pages as they are built, sidebar and game bar, War Room and Campaign Editor, reference pages.

## Order of work (one plan each)
1. Cover rule fix and Lance glossary fix.
2. Crusade storage move to SQLite with migrations.
3. Muster and Post-battle Sequence.
4. Calculator rebuild: modifier engine plus the variant A look.

Later, planned after these land: Requisitions, Agendas, faction content, the look across the rest of the app, reference audit.

## Constraints
- .NET 10, Blazor Interactive Server, EF Core 10 with SQLite, xUnit + FluentAssertions + bUnit + Moq in `Warhammer.Tests`.
- Never start the dev server from the agent; the user runs it.
- `T'au/campaign.db` is tracked in git and holds live data; schema changes go through EF migrations, never a drop-and-recreate.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Out of scope for these four plans
Expected-damage math, Agendas, Requisition spending flows, faction Crusade content, player logins or read-only views, the Roster Viewer feeding casualties, multiplayer Battles, 11th edition.
