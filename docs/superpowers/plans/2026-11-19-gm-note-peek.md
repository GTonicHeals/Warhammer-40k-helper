# GM Note Peek Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clicking a note pin in the Campaign Editor (Prep and Play) opens a GM-only pop-up showing the whole note.

**Architecture:** New `GmNotePeek` UI component (overlay + card, styled like the Reveal card) taking the note and callbacks. `CampaignEditor` (Prep) and `GmTableMode` (Play) open it from their map-pin handlers.

**Tech Stack:** Blazor Server (.NET), bUnit + xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-11-19-gm-note-peek-design.md`

## Global Constraints
- Opening or closing the peek never changes `GmNote.IsPublic` or `RevealedAt`.
- Secret shows immediately in the peek (Campaign Editor is never on the TV).
- War Room untouched.

## Review Focus
- Esc in Play must close only the peek, not also clear the side-panel selection → Play passes `HandleEscape=false`; `OnHotkey("escape")` closes the peek first.
- Empty fields (fresh "Add Note" pin with no title/summary/secret) → peek shows "Untitled note" and no empty sections.
- Reveal from the peek → peek closes, Reveal overlay opens (no two overlays stacked).

---

### Task 1: `GmNotePeek` component

**Files:** Create `T'au/Components/UI/GmNotePeek.razor`; Test `Warhammer.Tests/Components/GmNotePeekTests.cs`

**Interfaces — Produces:**
- `[Parameter, EditorRequired] GmNote Note`, `[Parameter] string? LocationName`,
  `[Parameter] EventCallback OnClose`, `[Parameter] EventCallback OnEdit`,
  `[Parameter] EventCallback OnReveal` (button shown only when `OnReveal.HasDelegate`; label "Show to players" if `Note.IsPublic` else "Reveal to players"),
  `[Parameter] bool HandleEscape = true`.
- Ids: `#gnp-overlay`, `#gnp-close`, `#gnp-edit`, `#gnp-reveal`.

- [ ] Tests: shows title/summary/content/secret/location/tags; backdrop click and Close raise OnClose; Edit raises OnEdit; reveal button absent without delegate; Escape keydown raises OnClose; empty note renders "Untitled note".
- [ ] Implement; run `dotnet test --filter GmNotePeekTests`; commit.

### Task 2: Wire into Prep and Play

**Files:** Modify `T'au/Components/Pages/Campaign/CampaignEditor.razor` (`HandleNodeSelected`), `T'au/Components/UI/GmTableMode.razor` (`HandleMapSelected`, `OnHotkey`); Tests in `CampaignEditorTableModeTests.cs`, `GmTableModeTests.cs`.

- [ ] Test (Play): clicking `.map-note-pin` opens `#gnp-overlay` with the secret, note stays non-public; Reveal from peek closes it and opens `#gt-reveal-overlay`; `OnHotkey("escape")` closes peek but keeps note selected.
- [ ] Test (Prep): seeded pinned note, click pin → peek with secret; `#gnp-edit` closes peek and the edit card remains.
- [ ] Implement; run full `dotnet test`; commit.
