using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Helpers;

/// <summary>
/// Tests for TerrainHelpers — the icon and label switch expressions used by the
/// Campaign Editor and War Room. Ensures every defined TerrainType maps to a stable,
/// non-empty value and that unknown enum values fall back gracefully.
/// </summary>
public class TerrainHelpersTests
{
    private static readonly TerrainType[] AllTerrainTypes =
        Enum.GetValues<TerrainType>();

    // ═════════════════════════════════════════════════════════════════════════
    // Icon — Bootstrap Icons class names
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(TerrainType.Standard,        "bi-hexagon")]
    [InlineData(TerrainType.Ruins,           "bi-building-slash")]
    [InlineData(TerrainType.HiveCity,        "bi-buildings-fill")]
    [InlineData(TerrainType.ForgeWorld,      "bi-gear-wide-connected")]
    [InlineData(TerrainType.Plain,           "bi-align-bottom")]
    [InlineData(TerrainType.Forest,          "bi-tree-fill")]
    [InlineData(TerrainType.City,            "bi-building-fill")]
    [InlineData(TerrainType.PowerPlant,      "bi-lightning-charge-fill")]
    [InlineData(TerrainType.WeaponStockpile, "bi-box-seam-fill")]
    [InlineData(TerrainType.MilitaryCamp,    "bi-flag-fill")]
    [InlineData(TerrainType.OrbitalDefence,  "bi-rocket-fill")]
    public void Icon_WithDefinedTerrainType_ReturnsExpectedBootstrapIcon(TerrainType terrain, string expectedIcon)
    {
        // Act
        var icon = TerrainHelpers.Icon(terrain);

        // Assert
        Assert.Equal(expectedIcon, icon);
    }

    [Fact]
    public void Icon_WithUnknownTerrainType_ReturnsFallbackIcon()
    {
        // Arrange — a value outside the defined enum range
        var unknown = (TerrainType)999;

        // Act
        var icon = TerrainHelpers.Icon(unknown);

        // Assert
        Assert.Equal("bi-question-circle", icon);
    }

    [Fact]
    public void Icon_ForEveryDefinedTerrainType_StartsWithBootstrapIconPrefix()
    {
        // Act & Assert — every defined enum value must yield a "bi-" class name
        foreach (var terrain in AllTerrainTypes)
        {
            var icon = TerrainHelpers.Icon(terrain);
            Assert.StartsWith("bi-", icon);
            Assert.False(string.IsNullOrWhiteSpace(icon));
        }
    }

    [Fact]
    public void Icon_ForEveryDefinedTerrainType_IsNotTheFallback()
    {
        // Catches the easy regression: a new TerrainType added without a switch arm
        foreach (var terrain in AllTerrainTypes)
        {
            var icon = TerrainHelpers.Icon(terrain);
            Assert.NotEqual("bi-question-circle", icon);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Label — human-readable display strings
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(TerrainType.Standard,        "Standard")]
    [InlineData(TerrainType.Ruins,           "Ruins")]
    [InlineData(TerrainType.HiveCity,        "Hive City")]
    [InlineData(TerrainType.ForgeWorld,      "Forge World")]
    [InlineData(TerrainType.Plain,           "Plain")]
    [InlineData(TerrainType.Forest,          "Forest")]
    [InlineData(TerrainType.City,            "City")]
    [InlineData(TerrainType.PowerPlant,      "Power Plant")]
    [InlineData(TerrainType.WeaponStockpile, "Stockpile")]
    [InlineData(TerrainType.MilitaryCamp,    "Mil. Camp")]
    [InlineData(TerrainType.OrbitalDefence,  "Orbital Defence")]
    public void Label_WithDefinedTerrainType_ReturnsExpectedDisplayString(TerrainType terrain, string expectedLabel)
    {
        // Act
        var label = TerrainHelpers.Label(terrain);

        // Assert
        Assert.Equal(expectedLabel, label);
    }

    [Fact]
    public void Label_WithUnknownTerrainType_ReturnsEnumToStringFallback()
    {
        // Arrange — an enum value outside the defined range
        var unknown = (TerrainType)999;

        // Act
        var label = TerrainHelpers.Label(unknown);

        // Assert — fallback is the enum value's own ToString() (numeric for unmapped values)
        Assert.Equal(unknown.ToString(), label);
    }

    [Fact]
    public void Label_ForEveryDefinedTerrainType_IsNonEmpty()
    {
        foreach (var terrain in AllTerrainTypes)
        {
            var label = TerrainHelpers.Label(terrain);
            Assert.False(string.IsNullOrWhiteSpace(label));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Cross-cutting invariants
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Icon_ForEveryDefinedTerrainType_ReturnsDistinctValue()
    {
        // Catches accidental duplicate icon assignments across the switch
        var icons = AllTerrainTypes.Select(TerrainHelpers.Icon).ToList();
        Assert.Equal(icons.Count, icons.Distinct().Count());
    }

    [Fact]
    public void Label_ForEveryDefinedTerrainType_ReturnsDistinctValue()
    {
        // Catches accidental duplicate labels across the switch
        var labels = AllTerrainTypes.Select(TerrainHelpers.Label).ToList();
        Assert.Equal(labels.Count, labels.Distinct().Count());
    }
}
