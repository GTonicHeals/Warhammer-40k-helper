# Cover Rule Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Wound Calculator's Benefit of Cover and the Lance glossary entry match the 10th edition rules exactly.

**Architecture:** `WoundCalculationService.Calculate` is a pure function over a positional record; two new record fields with defaults (`IsRanged`, `IgnoresCover`) carry the facts the rule needs, so existing call sites keep compiling. The page passes the weapon type and keyword, and disables the cover toggle when it cannot apply.

**Tech Stack:** C# / .NET 10, xUnit, Blazor Server.

**Spec:** `docs/superpowers/specs/2026-11-10-crusade-campaign-and-10th-rules-spec.md` (Rules section).

## Global Constraints

- 10th edition only. Cover: +1 armour save, ranged attacks only, no bonus when Save ≤ 3 and effective AP = 0, denied by Ignores Cover, never applied to the invulnerable save.
- Do not start the dev server. Verify with `dotnet build` and `dotnet test`.
- Commit messages end with `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Review Focus

- A defender with Save 2+ against AP −1 in cover must get 2+ (pinned in Task 1).
- A 7+ armour result with cover but an invulnerable save must fall back to the invulnerable save unchanged (pinned in Task 1).
- The defender's `ReduceAp` modifier makes AP −1 into AP 0, which then triggers the "Save 3+ vs AP 0" exception (pinned in Task 1).
- A melee weapon selected after cover was toggled on must not keep applying cover (pinned in Task 2).
- Weapon keyword text is "Ignores Cover" with mixed case in roster files; matching must be case-insensitive (pinned in Task 2).

---

### Task 1: Cover rule in `WoundCalculationService`

**Files:**
- Modify: `T'au/Services/WoundCalculationService.cs`
- Test: `Warhammer.Tests/Services/WoundCalculationServiceTests.cs`

**Interfaces:**
- Produces: `WoundCalcInput` gains two trailing positional fields `bool IsRanged = true, bool IgnoresCover = false`. `Calculate` signature unchanged.

- [ ] **Step 1: Replace the obsolete test and add the rule tests**

Delete `Calculate_CoverCannotImproveSaveBeyondBaseSave` (it pins the wrong rule). Add, using the existing `BaseInput` helper and `with` expressions:

```csharp
[Fact] public void Calculate_Cover_Save4_Ap0_Ranged_Gives3Plus()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: 0, save: 4) with { HasCover = true });
    Assert.Equal("3+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_Save3_Ap0_Ranged_Stays3Plus()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: 0, save: 3) with { HasCover = true });
    Assert.Equal("3+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_Save2_ApMinus1_Gives2Plus()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: -1, save: 2) with { HasCover = true });
    Assert.Equal("2+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_Save3_ApMinus1_Gives3Plus()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: -1, save: 3) with { HasCover = true });
    Assert.Equal("3+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_MeleeWeapon_HasNoEffect()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: 0, save: 4) with { HasCover = true, IsRanged = false });
    Assert.Equal("4+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_IgnoresCoverWeapon_HasNoEffect()
{   var r = WoundCalculationService.Calculate(BaseInput(ap: -1, save: 4) with { HasCover = true, IgnoresCover = true });
    Assert.Equal("5+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_ReduceApToZero_TriggersSave3Exception()
{   // AP -1 reduced to 0 by the defender modifier; Save 3+ then gets no cover bonus
    var r = WoundCalculationService.Calculate(BaseInput(ap: -1, save: 3) with { HasCover = true, ReduceAp = true });
    Assert.Equal("3+", r.FinalSaveRoll); }

[Fact] public void Calculate_Cover_DoesNotTouchInvulnerableSave()
{   // Sv 6+, AP -2 -> armour 8, cover -> 7 (N/A); invuln 5+ wins unchanged
    var r = WoundCalculationService.Calculate(BaseInput(ap: -2, save: 6) with { HasCover = true, InvulnSave = 5 });
    Assert.Equal("5+", r.FinalSaveRoll); }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Warhammer.Tests --filter "FullyQualifiedName~WoundCalculationServiceTests" --nologo`
Expected: build error on `IsRanged`/`IgnoresCover` (fields do not exist yet).

- [ ] **Step 3: Add the fields and implement the rule**

In `WoundCalcInput`, append `bool IsRanged = true, bool IgnoresCover = false`. In `Calculate`, replace the three-line armour block with:

```csharp
int rawArmour    = i.DefenderSave.Value + Math.Abs(effectiveAp);
bool coverApplies = i.HasCover && i.IsRanged && !i.IgnoresCover
                    && !(i.DefenderSave.Value <= 3 && effectiveAp == 0);
int armourSave   = coverApplies ? rawArmour - 1 : rawArmour;
```

Keep the invulnerable comparison that follows unchanged.

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test Warhammer.Tests --nologo`
Expected: all pass, including `Calculate_WithCover_ImprovesSaveByOne` (AP −2 vs 3+ in cover → 4+).

- [ ] **Step 5: Commit**

```bash
git add "T'au/Services/WoundCalculationService.cs" Warhammer.Tests/Services/WoundCalculationServiceTests.cs
git commit -m "fix: benefit of cover follows the 10th edition exceptions

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Pass weapon facts from the calculator page

**Files:**
- Modify: `T'au/Components/Pages/WoundCalculator.razor:492-499` (cover button), `:1300-1316` (`Recalculate` input), `:1325` (`ToggleCover`)

**Interfaces:**
- Consumes: `WoundCalcInput.IsRanged`, `WoundCalcInput.IgnoresCover` from Task 1.

- [ ] **Step 1: Add two private helpers in the `@code` block**

```csharp
private bool SelectedWeaponIsRanged => SelectedWeapon?.TypeName == "Ranged Weapons";
private bool SelectedWeaponIgnoresCover =>
    SelectedWeaponKeywords.Any(k => k.Raw.StartsWith("Ignores Cover", StringComparison.OrdinalIgnoreCase));
private bool CoverCanApply => SelectedWeapon == null || (SelectedWeaponIsRanged && !SelectedWeaponIgnoresCover);
```

- [ ] **Step 2: Pass them in `Recalculate`**

Add `IsRanged: SelectedWeaponIsRanged, IgnoresCover: SelectedWeaponIgnoresCover` to the `WoundCalcInput` constructor call. In `SelectWeapon`, after the keyword parse, set `_hasCover = false` when `!CoverCanApply`.

- [ ] **Step 3: Disable the cover button when it cannot apply**

On the `wc-cover-btn` element add `disabled="@(!CoverCanApply)"` and a `title` of `"No benefit of cover against melee"` when the weapon is melee, `"This weapon ignores cover"` when it has Ignores Cover, otherwise empty. Keep the label text as is.

- [ ] **Step 4: Build**

Run: `dotnet build "T'au/Warhammer.csproj" --nologo -v q`
Expected: `Build succeeded.` with no new warnings in `WoundCalculator.razor`.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Components/Pages/WoundCalculator.razor"
git commit -m "feat: cover toggle knows ranged, melee and ignores-cover weapons

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Lance glossary text

**Files:**
- Modify: `T'au/Helpers/RosterHelper.cs:29` (the `["LANCE"]` entry)
- Test: `Warhammer.Tests/Helpers/RosterHelperTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void KeywordGlossary_Lance_IsPlusOneToWoundOnCharge()
{
    RosterHelper.KeywordGlossary["LANCE"].Should().Be(
        "If the bearer made a Charge move this turn, add 1 to the Wound roll for each attack made with this weapon.");
}
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test Warhammer.Tests --filter "KeywordGlossary_Lance" --nologo`
Expected: FAIL, actual text mentions Armour Penetration.

- [ ] **Step 3: Replace the entry text with the string in the test**

- [ ] **Step 4: Run it to verify it passes**

Run: `dotnet test Warhammer.Tests --filter "KeywordGlossary_Lance" --nologo`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add "T'au/Helpers/RosterHelper.cs" Warhammer.Tests/Helpers/RosterHelperTests.cs
git commit -m "fix: Lance keyword glossary states +1 to wound on charge

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
