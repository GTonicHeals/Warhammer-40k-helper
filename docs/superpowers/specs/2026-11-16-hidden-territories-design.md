# Hidden Territories — design

Date: 2026-11-16 · Branch: Crusade-mode-intergration · Size: small change

## Intent
The GM wants to trickle the map out to the players: territories start hidden from the
War Room and are revealed over the campaign, either by hand or as a result of captures.

## Goals
- A territory can be Hidden or Revealed. Hidden territories don't appear on the War Room map.
- The GM can toggle Hidden/Revealed by hand in the Campaign Editor.
- Completing a battle that captures a territory reveals that territory's hidden neighbours.
- The post-battle review shows which territories the capture will reveal, before confirm.

## Non-goals / anti-goals
- No fog-of-war derived at render time: a revealed territory stays revealed until the GM hides it.
- Editing a territory's controller in the Campaign Editor does NOT reveal anything (avoids accidental leaks while setting up).
- Events keep their existing `IsEventRevealed` behaviour; this change is about territories.
- The War Room must not leak hidden territories: no node, no connection line, no name.

## Approaches considered
1. **Reveal on battle completion (chosen).** `PostBattleService` sets `IsPublic = true` on hidden
   territories connected to the captured one, persisted. GM keeps the final say.
2. Derived visibility at render (`IsPublic || adjacent to a held territory`). Rejected: GM can't hide
   a territory next to a held one, and territories vanish again if control is lost.
3. Reveal on any controller change, including editor edits. Rejected: editor misclicks leak territories.

## Design
- **Storage:** reuse the existing, unused `Node.IsPublic` as "revealed" for territories. No new column.
- **Migration (my call):** a data migration sets `IsPublic = 1` on every existing territory, so the
  current campaign map doesn't disappear. Territories created after this start hidden (`IsPublic = false`).
- **War Room:** shows only territories with `IsPublic`; connection lines are drawn only when both
  ends are visible; the control strip/counts count revealed territories only (my call — counts
  would otherwise hint at hidden ones). Events attached to a hidden territory stay off the map.
- **Campaign Editor:** a Hidden/Revealed toggle on the selected territory. Hidden territories render
  dimmed with a 🔒 prefix, matching how private GM notes already look. Optional "Reveal all" button (my call, cheap).
- **PostBattleService:** when `CapturedTerritoryId` is applied, for each id in the captured
  territory's `ConnectedNodeIds` (and territories that list the captured one — connections may be
  one-directional), set `IsPublic = true` on hidden territories. The captured territory itself is
  also revealed if hidden.
- **Post-battle review:** a "Reveals: A, B" line computed by the same helper, shown before confirm.
- **Tests:** PostBattleService reveal (neighbours revealed, non-neighbours untouched, no capture → no reveal,
  already-revealed untouched); War Room/map filtering excludes hidden nodes and their lines.

## Left to the builder
- Exact placement/styling of the toggle and review line within the existing layouts.
- Whether connection direction needs normalising (check how `ConnectedNodeIds` is written in the editor).
