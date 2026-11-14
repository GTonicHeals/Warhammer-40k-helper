# Wound Calculator Rebuild Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Wound Calculator page in the "datasheet duel" look with every Automatic Modifier applied from the datasheet, keeping every feature the page has today.

**Architecture:** A pure `AttackModifierEngine` turns weapon, attacker, defender and situation facts into a `WoundCalcInput` plus a list of modifier chips with states (Off, On, Auto, Waiting, Blocked); the page renders chips and never recomputes rules itself. The page is split into components (`DatasheetPanel`, `RollSpine`, `UnitPicker`, `ModifierChips`) with a shared `datasheet-tokens.css`. The prototype under `Components/Pages/Prototype/` is the visual reference, is moved to a throwaway branch, and is not promoted as code.

**Tech Stack:** Blazor Server, xUnit, bUnit, Google Fonts (Barlow, Barlow Condensed).

**Spec:** `docs/superpowers/specs/2026-11-10-crusade-campaign-and-10th-rules-spec.md` (Rules and Interface sections). Depends on plan 01. Independent of plans 02 and 03 except that Crusade honour modifiers keep working through `CrusadeUnitCard`.

## Global Constraints

- Roll-needed output only. Lethal Hits, Sustained Hits, Devastating Wounds, Twin-linked, Feel No Pain stay informational badges.
- Automatic Modifiers and Situation Toggles exactly as the spec lists them. Hit and wound modifiers are each capped at ±1 (existing `Calculate` behaviour).
- Keep: player filters, swap, inline stratagems per side, abilities and detachment rule with tooltips, datasheet modal, defender per-model wounds, `WoundCalcStateService` persistence, Crusade honour modifiers with the gold marker.
- Tablet landscape first: three-column grid at ≥ 900px, stacked below.
- Fonts: Barlow Condensed (display, stats), Barlow (body). Tokens: table `#1c1e21`, card `#ebe5d7`, card-2 `#e1dacb`, ink `#141414`, ink-2 `#5a564e`, rule `#b9b1a1`; roll tones easy `#dff0d2`, mid `#f6ecc4`, hard `#f2cfc4`; player colours from `PlayerTheme`.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- Stealth must not apply to melee attacks (pinned in Task 1).
- Heavy (stationary) and Big Guns Never Tire (in engagement range) together: +1 and −1 net to 0, chip states still show both (pinned in Task 1).
- Anti-X where X is a multi-word keyword such as "Anti-Fly 4+" vs "Anti-Vehicle 2+" must match the whole keyword, not a prefix (pinned in Task 1).
- Switching from a ranged to a melee weapon must clear the stationary and not-visible toggles (pinned in Task 1 via engine ignoring them, and Task 4 via page reset).
- Selection persistence: reopening the page restores attacker, weapon and defender and recomputes chips (pinned in Task 4).

---

### Task 1: `AttackModifierEngine`

**Files:**
- Create: `T'au/Models/Calculator/ModChip.cs`, `T'au/Services/AttackModifierEngine.cs`
- Test: `Warhammer.Tests/Services/AttackModifierEngineTests.cs`

**Interfaces:**
- Produces in `Warhammer.Models.Calculator`: `enum Die { Hit, Wound, Save }`, `enum ChipSide { Attacker, Defender }`, `enum ChipState { Off, On, Auto, Waiting, Blocked }`, `record ModChip(Die Die, ChipSide Side, string Key, string Label, string Detail, ChipState State)` where `Key` is one of `torrent, heavy, lance, indirect, anti, stealth, bgnt, plusHit, plusWound, minusHit, minusWound, reduceAp, cover, invuln`.
- Produces:
  ```csharp
  public record AttackFacts(
      string? Skill, int? Strength, int? Ap, bool IsRanged, IReadOnlyList<string> WeaponKeywords,
      IReadOnlyList<string> AttackerKeywords, IReadOnlyList<string> DefenderKeywords,
      bool DefenderHasStealth, int? DefenderToughness, int? DefenderSave, int? DefenderInvuln);
  public record Situation(bool Stationary, bool Charged, bool NotVisible, bool Cover, bool InEngagementRange,
      bool PlusHit, bool PlusWound, bool MinusHit, bool MinusWound, bool ReduceAp);
  public record AttackResolution(WoundCalcInput Input, IReadOnlyList<ModChip> Chips, int? AntiThreshold);
  public static class AttackModifierEngine { public static AttackResolution Resolve(AttackFacts f, Situation s); }
  ```
- Consumes: `WoundCalculationService.Calculate` is called by the page on `resolution.Input`; the page then caps the wound roll at `AntiThreshold` when set.
- Big Guns Never Tire is situational: a VEHICLE or MONSTER attacker firing a ranged non-Pistol weapon gets −1 to hit only while in Engagement Range. So the `bgnt` chip is `Waiting` until `InEngagementRange` is true, then `Auto`. The chip is absent when the attacker is neither VEHICLE nor MONSTER, the weapon is melee, or the weapon is a Pistol.

- [ ] **Step 1: Write the failing tests** (one per rule; `Facts()` helper with defaults `Skill "3+", S 5, AP -1, IsRanged true, T 4, Sv 4`):

```csharp
[Fact] public void Torrent_makes_hit_automatic() { var r = Resolve(Facts(kw: ["Torrent"]), Sit()); r.Input.HitRoll.Should().Be("AUTO"); Chip(r,"torrent").State.Should().Be(ChipState.Auto); }
[Fact] public void Heavy_waits_for_stationary_then_applies() { Chip(Resolve(Facts(kw:["Heavy"]), Sit()),"heavy").State.Should().Be(ChipState.Waiting); var r = Resolve(Facts(kw:["Heavy"]), Sit(stationary:true)); r.Input.PlusOneHit.Should().BeTrue(); Chip(r,"heavy").State.Should().Be(ChipState.Auto); }
[Fact] public void Lance_applies_only_when_charged_and_melee() { Resolve(Facts(kw:["Lance"], ranged:false), Sit(charged:true)).Input.PlusOneWound.Should().BeTrue(); Resolve(Facts(kw:["Lance"], ranged:true), Sit(charged:true)).Chips.Should().NotContain(c => c.Key=="lance"); }
[Fact] public void Indirect_not_visible_gives_minus_hit_and_cover() { var r = Resolve(Facts(kw:["Indirect Fire"]), Sit(notVisible:true)); r.Input.MinusOneHit.Should().BeTrue(); r.Input.HasCover.Should().BeTrue(); }
[Fact] public void Stealth_is_ranged_only() { Resolve(Facts(stealth:true), Sit()).Input.MinusOneHit.Should().BeTrue(); Resolve(Facts(stealth:true, ranged:false), Sit()).Input.MinusOneHit.Should().BeFalse(); }
[Fact] public void Anti_matches_whole_keyword() { Resolve(Facts(kw:["Anti-Fly 4+"], defKw:["Fly"]), Sit()).AntiThreshold.Should().Be(4); Resolve(Facts(kw:["Anti-Vehicle 2+"], defKw:["Fly"]), Sit()).AntiThreshold.Should().BeNull(); }
[Fact] public void Ignores_cover_blocks_cover_chip() { var r = Resolve(Facts(kw:["Ignores Cover"]), Sit(cover:true)); r.Input.IgnoresCover.Should().BeTrue(); Chip(r,"cover").State.Should().Be(ChipState.Blocked); }
[Fact] public void Melee_blocks_cover_chip() { Chip(Resolve(Facts(ranged:false), Sit(cover:true)),"cover").State.Should().Be(ChipState.Blocked); }
[Fact] public void Invuln_from_datasheet_is_auto() { var r = Resolve(Facts(invuln:5), Sit()); r.Input.InvulnSave.Should().Be(5); Chip(r,"invuln").State.Should().Be(ChipState.Auto); }
[Fact] public void Bgnt_waits_for_engagement_range_then_applies() { Chip(Resolve(Facts(atkKw:["Vehicle"]), Sit()),"bgnt").State.Should().Be(ChipState.Waiting); var r = Resolve(Facts(atkKw:["Vehicle"]), Sit(engaged:true)); r.Input.BgntApplied.Should().BeTrue(); Chip(r,"bgnt").State.Should().Be(ChipState.Auto); }
[Fact] public void Bgnt_absent_for_pistol_melee_or_infantry() { Resolve(Facts(atkKw:["Vehicle"], kw:["Pistol"]), Sit(engaged:true)).Chips.Should().NotContain(c => c.Key=="bgnt"); Resolve(Facts(atkKw:["Vehicle"], ranged:false), Sit(engaged:true)).Chips.Should().NotContain(c => c.Key=="bgnt"); Resolve(Facts(atkKw:["Infantry"]), Sit(engaged:true)).Chips.Should().NotContain(c => c.Key=="bgnt"); }
[Fact] public void Heavy_and_bgnt_both_show_and_net_to_zero() { var r = Resolve(Facts(kw:["Heavy"], atkKw:["Vehicle"]), Sit(stationary:true, engaged:true)); r.Input.PlusOneHit.Should().BeTrue(); r.Input.BgntApplied.Should().BeTrue(); WoundCalculationService.Calculate(r.Input).NetHitMod.Should().Be(0); }
[Fact] public void Manual_toggles_pass_through() { var r = Resolve(Facts(), Sit(plusHit:true, minusWound:true, reduceAp:true)); r.Input.PlusOneHit.Should().BeTrue(); r.Input.MinusOneWound.Should().BeTrue(); r.Input.ReduceAp.Should().BeTrue(); Chip(r,"plusHit").State.Should().Be(ChipState.On); }
```

The existing `CanApplyBgnt` in `WoundCalculator.razor:1420-1430` is the visibility condition for the chip; the engagement-range fact is new and comes from `Situation.InEngagementRange`.

- [ ] **Step 2: Run to verify they fail** — build error.
- [ ] **Step 3: Implement `Resolve`** — chips are emitted in this order: Hit (torrent, heavy, indirect, stealth, bgnt, plusHit, minusHit), Wound (lance, anti, plusWound, minusWound), Save (cover, reduceAp, invuln). Keyword checks are `StartsWith(..., OrdinalIgnoreCase)` on the raw keyword; Anti parses `^Anti-(.+?)\s+(\d)\+$` and compares the group to defender keywords with `Equals(..., OrdinalIgnoreCase)`.
- [ ] **Step 4: Run** — `dotnet test Warhammer.Tests --filter "AttackModifierEngineTests" --nologo` → all pass.
- [ ] **Step 5: Commit** — `feat: AttackModifierEngine derives modifiers from the datasheet` with attribution.

---

### Task 2: Design tokens and fonts

**Files:**
- Create: `T'au/wwwroot/css/datasheet-tokens.css`
- Modify: `T'au/Components/App.razor` (stylesheet link after `warhammer-shared.css`; Google Fonts link for Barlow 400/600 and Barlow Condensed 500/600/700 with `display=swap`)

**Interfaces:**
- Produces CSS custom properties on `:root`: `--ds-table, --ds-card, --ds-card-2, --ds-ink, --ds-ink-2, --ds-rule, --ds-tone-easy, --ds-tone-mid, --ds-tone-hard, --ds-font-display, --ds-font-body` with the values from Global Constraints, plus classes `.ds-sheet`, `.ds-band`, `.ds-stat`, `.ds-chip` (and state modifiers `-on -auto -waiting -blocked`), `.ds-roll` (and tone modifiers), ported from `WoundCalcVariantA.razor` with the `pa-` prefix replaced by `ds-`.

- [ ] **Step 1: Port the styles, build, commit** — `dotnet build "T'au/Warhammer.csproj" --nologo -v q` → `Build succeeded.`; commit `feat: datasheet design tokens` with attribution.

---

### Task 3: Calculator components

**Files:**
- Create: `T'au/Components/Calculator/UnitPicker.razor`, `T'au/Components/Calculator/DatasheetPanel.razor`, `T'au/Components/Calculator/ModifierChips.razor`, `T'au/Components/Calculator/RollSpine.razor`
- Test: `Warhammer.Tests/Components/ModifierChipsTests.cs`, `Warhammer.Tests/Components/RollSpineTests.cs` (bUnit)

**Interfaces:**
- `UnitPicker`: parameters `IReadOnlyList<UnitEntry> Units`, `string? PlayerFilter`, `UnitEntry? Selected`, `EventCallback<string?> FilterChanged`, `EventCallback<UnitEntry> Picked`. Renders the player filter buttons and the grouped unit list (one row per unit: label, `T`, `Sv`, player colour stripe). `UnitEntry` moves from the page's private record to `T'au/Models/Calculator/UnitEntry.cs` as a public record with the same fields.
- `DatasheetPanel`: parameters `string Role` ("Attacker"/"Defender"), `UnitEntry? Unit`, `bool PickerOpen`, `EventCallback BandClicked`, `RenderFragment Picker`, `RenderFragment? Weapons`, `RenderFragment? Body` (abilities, stratagems, detachment rule, wounds), `RenderFragment? Chips`, `EventCallback DatasheetClicked`. Renders band, stat strip (M T Sv W Ld OC + Invuln box), then the fragments in that order.
- `ModifierChips`: parameters `IReadOnlyList<ModChip> Chips`, `EventCallback<string> Toggled` (chip key). Chip button `disabled` when state is `Blocked` or the key is `torrent`, `anti`, `stealth`, `invuln`. `Auto` chips show the word `auto`; `Waiting` chips render dashed.
- `RollSpine`: parameters `string? Hit, Wound, Save`, `EventCallback Swap`. Three `.ds-roll` tiles with tone from the roll number (2–3 easy, 4 mid, 5+ hard, AUTO small text, null dimmed) and the swap button.

- [ ] **Step 1: Write the failing bUnit tests**: `ModifierChips` renders one button per chip, `Blocked` is disabled, clicking an `Off` chip invokes `Toggled` with its key, an `Auto` chip contains the text `auto`; `RollSpine` with `Hit="3+"` has class `ds-roll-easy` on the first tile and `Save=null` has `ds-roll-none` on the third.
- [ ] **Step 2: Run to verify they fail.** — [ ] **Step 3: Build the four components.** — [ ] **Step 4: Run** — all pass. — [ ] **Step 5: Commit** `feat: calculator datasheet components` with attribution.

---

### Task 4: Rebuild `WoundCalculator.razor`

**Files:**
- Modify: `T'au/Components/Pages/WoundCalculator.razor` (rewrite markup; keep `@code` logic for loading, stratagems, abilities, modals, wounds, persistence; replace the modifier fields and `Recalculate` with the engine)
- Test: `Warhammer.Tests/Components/WoundCalculatorPageTests.cs` (bUnit)

**Interfaces:**
- Consumes: Task 1 engine, Task 3 components, `WoundCalculationService.Calculate` with the Task-1 `IsRanged`/`IgnoresCover` fields (plan 01).
- Page state replaces `_atkPlusOneHit … _defReduceAp, _hasCover, _bgntPenalty` with one `Situation _situation` (the invulnerable save keeps its manual override buttons, feeding `AttackFacts.DefenderInvuln`); `Recalculate()` builds `AttackFacts` from the selections (after Crusade honour modifiers are applied, as today), calls `AttackModifierEngine.Resolve`, then `WoundCalculationService.Calculate`, caps `EffectiveWoundRoll` at `AntiThreshold`, stores `Chips`. `Toggled(key)` flips the field the chip stands for: `heavy` → `Stationary`, `lance` → `Charged`, `indirect` → `NotVisible`, `bgnt` → `InEngagementRange`, `cover` → `Cover`, `plusHit/plusWound/minusHit/minusWound/reduceAp` → the same-named field; then recalculates. Selecting a melee weapon resets `Stationary`, `NotVisible` and `InEngagementRange`.
- Layout: `.ds-arena` grid (sheet, spine, sheet) at ≥ 900px; stacked below. Attacker sheet: band → stats → weapons table (tap row to select; keyword badges with the existing glossary tooltips) → `ModifierChips` (attacker side) → stratagems → detachment rule and abilities → datasheet button. Defender sheet: band → stats → per-model wounds → `ModifierChips` (defender side) → stratagems → detachment rule and abilities → datasheet button. The three modals stay as they are.

- [ ] **Step 1: Write the failing bUnit test**: with `RosterLoadService` on a `TempWebRoot` holding a two-unit roster (one Vehicle with a Heavy ranged weapon, one Infantry with Stealth and a 5+ invulnerable save), render the page, pick attacker, weapon and defender, assert the spine shows `Hit 4+` (3+ base, Stealth −1, Heavy waiting, BGNT waiting), click the `heavy` chip (sets stationary) and assert `Hit 3+`, and assert `Save 5+` comes from the invulnerable chip marked auto. Keep it to those spine assertions plus one that `WoundCalcStateService` holds the three keys. A second test re-renders the page with the state service pre-filled and asserts the spine is populated without clicks.
- [ ] **Step 2: Run to verify it fails.** — [ ] **Step 3: Rebuild the page.** — [ ] **Step 4: Run** — `dotnet test Warhammer.Tests --nologo` all pass; `dotnet build` clean. — [ ] **Step 5: Commit** `feat: Wound Calculator rebuilt in the datasheet look with automatic modifiers` with attribution.

---

### Task 5: Retire the prototype and update docs

**Files:**
- Delete from this branch: `T'au/Components/Pages/Prototype/` (all files), `T'au/Components/UI/PrototypeSwitcher.razor`
- Modify: `README.md` (Wound Calculator feature list: automatic modifiers, situation toggles, datasheet look)

- [ ] **Step 1: Capture the prototype as a primary source**

```bash
git branch prototype/woundcalc-variants
git rm -r "T'au/Components/Pages/Prototype" "T'au/Components/UI/PrototypeSwitcher.razor"
git commit -m "chore: retire wound calculator prototype (kept on prototype/woundcalc-variants; variant A chosen)

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

- [ ] **Step 2: Update the README feature list and commit** `docs: calculator automation` with attribution.
- [ ] **Step 3: Run the full suite one last time** — `dotnet test Warhammer.Tests --nologo` → all pass.
