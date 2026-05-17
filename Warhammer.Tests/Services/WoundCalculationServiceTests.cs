namespace Warhammer.Tests.Services;

/// <summary>
/// Tests for WoundCalculationService.Calculate() — the pure function that
/// implements the 10th-Edition wound sequence math (hit/wound/save rolls,
/// modifier stacking, AP, cover, invulnerable saves).
/// No mocks needed: all inputs are value types.
/// </summary>
public class WoundCalculationServiceTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Builds a baseline input with no modifiers and no defender.</summary>
    private static WoundCalcInput BaseInput(
        string? hitRoll = "3+",
        int? strength   = null,
        int? ap         = null,
        int? toughness  = null,
        int? save       = null) =>
        new(hitRoll, strength, ap, toughness, save,
            PlusOneHit: false, PlusOneWound: false, BgntApplied: false,
            MinusOneHit: false, MinusOneWound: false, ReduceAp: false,
            HasCover: false, InvulnSave: 0);

    // ═════════════════════════════════════════════════════════════════════════
    // WOUND ROLL — S vs T table
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_WhenStrengthIsDoubleOrMoreThanToughness_ReturnsWoundRollOf2Plus()
    {
        // Arrange
        var input = BaseInput(strength: 10, toughness: 5); // S=10 >= 2*T=10

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("2+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthExceedsDoubledToughness_StillReturns2Plus()
    {
        // Arrange — e.g. Lascannon (S12) vs T5
        var input = BaseInput(strength: 12, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("2+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthExceedsToughness_ReturnsWoundRollOf3Plus()
    {
        // Arrange — S6 vs T5
        var input = BaseInput(strength: 6, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthEqualsToughness_ReturnsWoundRollOf4Plus()
    {
        // Arrange — S5 vs T5
        var input = BaseInput(strength: 5, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthIsSlightlyLessThanToughness_ReturnsWoundRollOf5Plus()
    {
        // Arrange — S4 vs T5: S*2=8 > T=5, so neither the "6+" nor lower cases apply
        var input = BaseInput(strength: 4, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("5+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthIsHalfOrLessOfToughness_ReturnsWoundRollOf6Plus()
    {
        // Arrange — S2 vs T5: S*2=4 <= T=5
        var input = BaseInput(strength: 2, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("6+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenStrengthIsExactlyHalfToughness_ReturnsWoundRollOf6Plus()
    {
        // Arrange — S3 vs T6: S*2=6 == T=6 (boundary case)
        var input = BaseInput(strength: 3, toughness: 6);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("6+", result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenNoWeaponStrengthProvided_ReturnsNullWoundRoll()
    {
        // Arrange — attacker selected but no weapon yet
        var input = BaseInput(strength: null, toughness: 5);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Null(result.WoundRoll);
    }

    [Fact]
    public void Calculate_WhenNoDefenderToughnessProvided_ReturnsNullWoundRoll()
    {
        // Arrange — weapon selected but no defender
        var input = BaseInput(strength: 6, toughness: null);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Null(result.WoundRoll);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // HIT ROLL — modifier stack and effective roll
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_WithNoHitModifiers_EffectiveHitRollMatchesBaseRoll()
    {
        // Arrange
        var input = BaseInput(hitRoll: "3+");

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.EffectiveHitRoll);
        Assert.Equal(0, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithPlusOneHitModifier_LowersRequiredRollByOne()
    {
        // Arrange — attacker has +1 HIT (e.g. from a stratagem)
        var input = BaseInput(hitRoll: "4+") with { PlusOneHit = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.EffectiveHitRoll);
        Assert.Equal(1, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithMinusOneHitModifier_RaisesRequiredRollByOne()
    {
        // Arrange — defender imposes -1 HIT (e.g. in cover, or special rule)
        var input = BaseInput(hitRoll: "3+") with { MinusOneHit = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.EffectiveHitRoll);
        Assert.Equal(-1, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithPlusOneAndMinusOneHitModifiers_NetZeroNoChange()
    {
        // Arrange
        var input = BaseInput(hitRoll: "3+") with { PlusOneHit = true, MinusOneHit = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.EffectiveHitRoll);
        Assert.Equal(0, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithBgntApplied_WorsenHitRollByOne()
    {
        // Arrange — Big Guns Never Tire penalty active
        var input = BaseInput(hitRoll: "3+") with { BgntApplied = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.EffectiveHitRoll);
        Assert.Equal(-1, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithPlusOneHitAndBgntApplied_NetZeroNoChange()
    {
        // Arrange — +1 HIT from stratagem, -1 from BGNT: net 0
        var input = BaseInput(hitRoll: "3+") with { PlusOneHit = true, BgntApplied = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.EffectiveHitRoll);
        Assert.Equal(0, result.NetHitMod);
    }

    [Fact]
    public void Calculate_WithModifierThatWouldExceedCap_NetHitModClampedToMinus1()
    {
        // Arrange — MinusOneHit + BGNT would be -2 raw, but 10th Ed caps at -1
        var input = BaseInput(hitRoll: "3+") with { MinusOneHit = true, BgntApplied = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal(-1, result.NetHitMod);
        Assert.Equal("4+", result.EffectiveHitRoll); // only -1 applied, not -2
    }

    [Fact]
    public void Calculate_WithAutoHitWeapon_EffectiveHitRollRemainsAuto()
    {
        // Arrange — TORRENT keyword; no hit roll needed
        var input = BaseInput(hitRoll: "AUTO");

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("AUTO", result.EffectiveHitRoll);
    }

    [Fact]
    public void Calculate_WithPlusOneHitOn2PlusRoll_ClampedAt2Plus()
    {
        // Arrange — can't get better than 2+ even with +1 HIT
        var input = BaseInput(hitRoll: "2+") with { PlusOneHit = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("2+", result.EffectiveHitRoll);
    }

    [Fact]
    public void Calculate_WithMinusOneHitOn6PlusRoll_ReturnsNa()
    {
        // Arrange — 6+ is already the worst; -1 more makes it impossible
        var input = BaseInput(hitRoll: "6+") with { MinusOneHit = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("N/A", result.EffectiveHitRoll);
    }

    [Fact]
    public void Calculate_WithNullHitRoll_EffectiveHitRollIsNull()
    {
        // Arrange — no weapon selected yet
        var input = BaseInput(hitRoll: null);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Null(result.EffectiveHitRoll);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // WOUND ROLL — modifier stack
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_WithPlusOneWoundModifier_LowersRequiredWoundRollByOne()
    {
        // Arrange — S5 vs T5 is normally 4+; +1 WOUND → 3+
        var input = BaseInput(strength: 5, toughness: 5) with { PlusOneWound = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.EffectiveWoundRoll);
        Assert.Equal(1, result.NetWoundMod);
    }

    [Fact]
    public void Calculate_WithMinusOneWoundModifier_RaisesRequiredWoundRollByOne()
    {
        // Arrange — S5 vs T5 is 4+; -1 WOUND → 5+
        var input = BaseInput(strength: 5, toughness: 5) with { MinusOneWound = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("5+", result.EffectiveWoundRoll);
        Assert.Equal(-1, result.NetWoundMod);
    }

    [Fact]
    public void Calculate_WithMinusOneWoundOn6PlusWoundRoll_ReturnsNa()
    {
        // Arrange — 6+ wound roll with -1 → impossible
        var input = BaseInput(strength: 2, toughness: 5) with { MinusOneWound = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("N/A", result.EffectiveWoundRoll);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // SAVE ROLL — AP, cover, invulnerable
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Calculate_WithZeroAp_DefenderSaveIsUnmodified()
    {
        // Arrange — AP 0 weapon vs 3+ save
        var input = BaseInput(ap: 0, save: 3);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithNegativeAp_DefenderSaveIsWorsened()
    {
        // Arrange — AP-2 weapon vs 3+ save → effective save = 3 + 2 = 5+
        var input = BaseInput(ap: -2, save: 3);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("5+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WhenApMakesArmorSaveWorseThan6Plus_ReturnsNa()
    {
        // Arrange — AP-4 weapon vs 4+ save → raw save = 4 + 4 = 8 → N/A
        var input = BaseInput(ap: -4, save: 4);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("N/A", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithCover_ImprovesSaveByOne()
    {
        // Arrange — AP-2 vs 3+ normally gives 5+; cover gives 4+
        var input = BaseInput(ap: -2, save: 3) with { HasCover = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_CoverCannotImproveSaveBeyondBaseSave()
    {
        // Arrange — AP-0 vs 4+ save: raw = 4; cover would give 3+, but base is 4+
        var input = BaseInput(ap: 0, save: 4) with { HasCover = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert — cover has no effect when armor is already at base value
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithReduceApModifier_EffectiveApImprovedByOne()
    {
        // Arrange — AP-2 reduced to AP-1; vs 3+ save → 4+ instead of 5+
        var input = BaseInput(ap: -2, save: 3) with { ReduceAp = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_ReduceApCannotMakeApPositive()
    {
        // Arrange — AP0 reduced → still AP0 (can't go above 0)
        var input = BaseInput(ap: 0, save: 3) with { ReduceAp = true };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("3+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithBetterInvulnThanArmour_InvulnSaveTakesPrecedence()
    {
        // Arrange — AP-3 vs 3+ gives raw save 6+; invuln 4+ is better
        var input = BaseInput(ap: -3, save: 3) with { InvulnSave = 4 };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithWorseInvulnThanArmour_ArmorSaveTakesPrecedence()
    {
        // Arrange — AP-1 vs 3+ gives 4+; invuln 5+ is worse → use 4+
        var input = BaseInput(ap: -1, save: 3) with { InvulnSave = 5 };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WhenArmourIsNaAndInvulnIsSet_InvulnSaveIsUsed()
    {
        // Arrange — AP-5 vs 3+ gives 8+ (N/A); invuln 4+ kicks in
        var input = BaseInput(ap: -5, save: 3) with { InvulnSave = 4 };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("4+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithInvulnButNoDefenderSaveData_InvulnSaveIsUsed()
    {
        // Arrange — weapon AP is known but no armor save on record
        var input = BaseInput(ap: -2, save: null) with { InvulnSave = 5 };

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Equal("5+", result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithNoWeaponApData_FinalSaveRollIsNull()
    {
        // Arrange — defender exists but no weapon chosen yet
        var input = BaseInput(ap: null, save: 3);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Null(result.FinalSaveRoll);
    }

    [Fact]
    public void Calculate_WithFullyEmptyInput_ReturnsAllNullRolls()
    {
        // Arrange — nothing selected
        var input = BaseInput(hitRoll: null, strength: null, ap: null, toughness: null, save: null);

        // Act
        var result = WoundCalculationService.Calculate(input);

        // Assert
        Assert.Null(result.WoundRoll);
        Assert.Null(result.EffectiveHitRoll);
        Assert.Null(result.EffectiveWoundRoll);
        Assert.Null(result.FinalSaveRoll);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ParseRollNum helper
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("2+", 2)]
    [InlineData("3+", 3)]
    [InlineData("4+", 4)]
    [InlineData("5+", 5)]
    [InlineData("6+", 6)]
    public void ParseRollNum_WithValidRollString_ReturnsCorrectInteger(string roll, int expected)
    {
        // Act
        var result = WoundCalculationService.ParseRollNum(roll);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseRollNum_WithUnparsableString_ReturnsFallbackOf4()
    {
        // Arrange — e.g. the string "AUTO" or "N/A"
        var result = WoundCalculationService.ParseRollNum("AUTO");

        // Assert
        Assert.Equal(4, result);
    }
}
