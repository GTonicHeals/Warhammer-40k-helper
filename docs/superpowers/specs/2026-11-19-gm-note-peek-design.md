# GM note peek pop-up — design (small change)

## Intent
In the Campaign Editor, clicking a note pin on the map should show the whole note
large and readable, for the GM only. Today the only big view is "Reveal to players",
which also marks the note Public (it then appears on the War Room map). The GM reads
notes in the Campaign Editor off the TV; the TV only shows the War Room. So the peek
can show everything, GM-only text included, without a reveal-on-click guard.

## Goals
- Clicking a note pin in Prep **and** Play opens a centred pop-up with: title,
  GM-only/Public badge, linked location, tags, full Summary, full Details (unfolded),
  and the GM-only Secret in its dashed red box, visible immediately.
- Opening the peek never changes `IsPublic` / `RevealedAt`.
- Close via Close button, Esc, or clicking the backdrop.
- Prep: **Edit** closes the pop-up, leaving the note selected in the existing edit panel.
- Play: **Edit** closes it and opens the note in the side panel in edit mode;
  **Reveal to players** (or **Show to players** if already public) behaves as the
  side panel's button does today.

## Non-goals
- No change to the War Room.
- Territories and campaign events do not get a peek (note pins only).
- No peek for note cards in the Play list (they already open the side panel).

## Approaches
- **Shared peek pop-up component (picked):** one `GmNotePeek` component used by both
  Prep and Play; reuses the Reveal card look. Big, readable, works on a tablet.
- Wider side panel: still cramped beside the map. Rejected.
- Hover preview: too small for long notes, no hover on tablets. Rejected.

## Builder's calls
- Dragging a pin in Prep doesn't open the peek (`CampaignMap` already swallows the
  click after a drag).
- In Play, a pin click both opens the peek and selects the note in the side panel,
  so closing the peek leaves context in place.
