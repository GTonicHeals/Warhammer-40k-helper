namespace Warhammer.Tests.Helpers;

/// <summary>
/// Tests for PlayerTheme — player colour, CSS-variable colour, gradient colour,
/// and label formatting. Ensures each player ID maps to a distinct, stable value
/// and that unknown IDs fall back gracefully.
/// </summary>
public class PlayerThemeTests
{
    // All canonical player IDs used in the app
    private static readonly string[] KnownPlayerIds = ["p1", "p2", "p3", "p4", "p5"];

    // ═════════════════════════════════════════════════════════════════════════
    // GetColor — hex values
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("p1", "#ffc107")]
    [InlineData("p2", "#5dade2")]
    [InlineData("p3", "#2ecc71")]
    [InlineData("p4", "#a855f7")]
    [InlineData("p5", "#e74c3c")]
    public void GetColor_WithKnownPlayerId_ReturnsExpectedHexColor(string playerId, string expectedColor)
    {
        // Act
        var color = PlayerTheme.GetColor(playerId);

        // Assert
        Assert.Equal(expectedColor, color);
    }

    [Fact]
    public void GetColor_WithUnknownPlayerId_ReturnsFallbackAccentColor()
    {
        // Act
        var color = PlayerTheme.GetColor("p99");

        // Assert — should return the accent fallback, not null or empty
        Assert.False(string.IsNullOrEmpty(color));
        Assert.StartsWith("#", color);
    }

    [Fact]
    public void GetColor_IsCaseInsensitive()
    {
        // Act — uppercase "P1" should return same as lowercase "p1"
        var lower = PlayerTheme.GetColor("p1");
        var upper = PlayerTheme.GetColor("P1");

        // Assert
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void GetColor_AllKnownPlayerIds_ReturnDistinctColors()
    {
        // Act
        var colors = KnownPlayerIds.Select(PlayerTheme.GetColor).ToList();

        // Assert — each player should have a unique colour
        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetColorVar — CSS custom property form
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("p1", "var(--c-p1")]
    [InlineData("p2", "var(--c-p2")]
    [InlineData("p3", "var(--c-p3")]
    [InlineData("p4", "var(--c-p4")]
    [InlineData("p5", "var(--c-p5")]
    public void GetColorVar_WithKnownPlayerId_ReturnsStringContainingCssVariable(string playerId, string expectedPrefix)
    {
        // Act
        var colorVar = PlayerTheme.GetColorVar(playerId);

        // Assert
        Assert.StartsWith(expectedPrefix, colorVar);
    }

    [Fact]
    public void GetColorVar_WithUnknownPlayerId_ReturnsCssVariableWithFallback()
    {
        // Act
        var colorVar = PlayerTheme.GetColorVar("p999");

        // Assert — must still be a var() expression with a hex fallback
        Assert.StartsWith("var(", colorVar);
        Assert.Contains("#", colorVar);
    }

    [Fact]
    public void GetColorVar_ContainsHexFallback_ForAllKnownPlayers()
    {
        // Act & Assert — every CSS var should embed a fallback hex colour
        foreach (var pid in KnownPlayerIds)
        {
            var colorVar = PlayerTheme.GetColorVar(pid);
            Assert.Contains("#", colorVar);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetGradientColor
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("p1")]
    [InlineData("p2")]
    [InlineData("p3")]
    [InlineData("p4")]
    [InlineData("p5")]
    public void GetGradientColor_WithKnownPlayerId_ReturnsNonEmptyString(string playerId)
    {
        // Act
        var gradient = PlayerTheme.GetGradientColor(playerId);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(gradient));
    }

    [Fact]
    public void GetGradientColor_AllKnownPlayerIds_ReturnDistinctValues()
    {
        // Act
        var gradients = KnownPlayerIds.Select(PlayerTheme.GetGradientColor).ToList();

        // Assert — each player should have a unique gradient colour
        Assert.Equal(gradients.Count, gradients.Distinct().Count());
    }

    [Fact]
    public void GetGradientColor_WithUnknownPlayerId_ReturnsFallback()
    {
        // Act
        var gradient = PlayerTheme.GetGradientColor("p99");

        // Assert — must return something, not null/empty
        Assert.False(string.IsNullOrWhiteSpace(gradient));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetLabel
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("p1", "P1")]
    [InlineData("p2", "P2")]
    [InlineData("p3", "P3")]
    [InlineData("p4", "P4")]
    [InlineData("p5", "P5")]
    public void GetLabel_WithStandardPlayerId_ReturnsFormattedLabel(string playerId, string expectedLabel)
    {
        // Act
        var label = PlayerTheme.GetLabel(playerId);

        // Assert
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void GetLabel_WithNonNumericId_ReturnsUpperCasedId()
    {
        // Arrange — edge case: a player ID that isn't "p{n}" format
        var label = PlayerTheme.GetLabel("custom");

        // Assert
        Assert.Equal("CUSTOM", label);
    }

    [Fact]
    public void GetLabel_WithUpperCaseInput_StillParsesCorrectly()
    {
        // "P2" should still produce "P2"
        var label = PlayerTheme.GetLabel("P2");

        Assert.Equal("P2", label);
    }
}
