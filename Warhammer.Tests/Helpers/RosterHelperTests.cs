namespace Warhammer.Tests.Helpers;

/// <summary>
/// Tests for RosterHelper — the static utility class that parses BattleScribe
/// roster data: keyword tokenisation, recursive profile traversal,
/// characteristic extraction, ability aggregation, and text formatting.
/// </summary>
public class RosterHelperTests
{
    // ─── factory helpers ──────────────────────────────────────────────────────

    private static Selection EmptyUnit() => new() { Name = "Test Unit" };

    private static Characteristic Stat(string name, string value) =>
        new() { Name = name, Value = value };

    private static Profile UnitProfile(params Characteristic[] stats) =>
        new() { TypeName = "Unit", Name = "Unit Profile", Characteristics = stats.ToList() };

    private static Profile WeaponProfile(string typeName, string name, params Characteristic[] stats) =>
        new() { TypeName = typeName, Name = name, Characteristics = stats.ToList() };

    private static Profile AbilityProfile(string name, string description) =>
        new()
        {
            TypeName = "Abilities",
            Name = name,
            Characteristics = new List<Characteristic> { new() { Name = "Description", Value = description } }
        };

    private static Rule MakeRule(string name, string desc = "") => new() { Name = name, Description = desc };

    private static Category MakeCategory(string name) => new() { Name = name };

    // ═════════════════════════════════════════════════════════════════════════
    // ParseWeaponKeywords
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ParseWeaponKeywords_WithKnownKeyword_ReturnsTokenWithDefinition()
    {
        // Act
        var tokens = RosterHelper.ParseWeaponKeywords("LETHAL HITS");

        // Assert
        Assert.Single(tokens);
        Assert.Equal("LETHAL HITS", tokens[0].Raw);
        Assert.NotNull(tokens[0].Definition);
    }

    [Fact]
    public void ParseWeaponKeywords_WithUnknownKeyword_ReturnsTokenWithNullDefinition()
    {
        // Act
        var tokens = RosterHelper.ParseWeaponKeywords("TELEPORT HOMER");

        // Assert
        Assert.Single(tokens);
        Assert.Equal("TELEPORT HOMER", tokens[0].Raw);
        Assert.Null(tokens[0].Definition);
    }

    [Fact]
    public void ParseWeaponKeywords_WithMultipleKeywords_ReturnsAllTokensInOrder()
    {
        // Act
        var tokens = RosterHelper.ParseWeaponKeywords("RAPID FIRE 1, LETHAL HITS");

        // Assert
        Assert.Equal(2, tokens.Count);
        Assert.Equal("RAPID FIRE 1", tokens[0].Raw);
        Assert.Equal("LETHAL HITS", tokens[1].Raw);
    }

    [Fact]
    public void ParseWeaponKeywords_WithAntiKeyword_MapsToAntiGlossaryEntry()
    {
        // e.g. "Anti-Infantry 4+" should map to the "ANTI" glossary entry
        var tokens = RosterHelper.ParseWeaponKeywords("Anti-Infantry 4+");

        // Assert — definition should exist (ANTI is in the glossary)
        Assert.Single(tokens);
        Assert.NotNull(tokens[0].Definition);
    }

    [Fact]
    public void ParseWeaponKeywords_WithEmptyString_ReturnsEmptyList()
    {
        // Act
        var tokens = RosterHelper.ParseWeaponKeywords("");

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void ParseWeaponKeywords_WithDashValue_ReturnsEmptyList()
    {
        // BattleScribe uses "-" to mean "no keywords"
        var tokens = RosterHelper.ParseWeaponKeywords("-");

        // Assert
        Assert.Empty(tokens);
    }

    [Fact]
    public void ParseWeaponKeywords_WithSustainedHits2_NumberStrippedAndMappedToGlossary()
    {
        // "SUSTAINED HITS 2" → base key "SUSTAINED HITS" → has a definition
        var tokens = RosterHelper.ParseWeaponKeywords("SUSTAINED HITS 2");

        Assert.Single(tokens);
        Assert.Equal("SUSTAINED HITS 2", tokens[0].Raw);
        Assert.NotNull(tokens[0].Definition);
    }

    [Fact]
    public void ParseWeaponKeywords_TrimsWhitespaceAroundEachToken()
    {
        // Act
        var tokens = RosterHelper.ParseWeaponKeywords("  TORRENT ,  BLAST  ");

        // Assert
        Assert.Equal(2, tokens.Count);
        Assert.Equal("TORRENT", tokens[0].Raw);
        Assert.Equal("BLAST", tokens[1].Raw);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetChar
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetChar_WithExistingCharacteristic_ReturnsItsValue()
    {
        // Arrange
        var profile = UnitProfile(Stat("T", "5"), Stat("W", "10"));

        // Act
        var value = RosterHelper.GetChar(profile, "T");

        // Assert
        Assert.Equal("5", value);
    }

    [Fact]
    public void GetChar_WithMissingCharacteristicName_ReturnsDash()
    {
        // Arrange
        var profile = UnitProfile(Stat("T", "5"));

        // Act
        var value = RosterHelper.GetChar(profile, "SV");

        // Assert
        Assert.Equal("-", value);
    }

    [Fact]
    public void GetChar_WithNullProfile_ReturnsDash()
    {
        // Act
        var value = RosterHelper.GetChar(null, "T");

        // Assert
        Assert.Equal("-", value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetAllProfilesRecursive
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAllProfilesRecursive_WithMatchingProfileOnRoot_ReturnsThatProfile()
    {
        // Arrange
        var unit = EmptyUnit();
        unit.Profiles.Add(UnitProfile(Stat("T", "4")));

        // Act
        var profiles = RosterHelper.GetAllProfilesRecursive(unit, "Unit");

        // Assert
        Assert.Single(profiles);
    }

    [Fact]
    public void GetAllProfilesRecursive_WithMatchingProfileOnChildSelection_ReturnsIt()
    {
        // Arrange — weapon profile lives on a child (e.g. a gun sub-selection)
        var child = new Selection { Name = "Pulse Carbine" };
        child.Profiles.Add(WeaponProfile("Ranged Weapons", "Pulse Carbine",
            Stat("A", "2"), Stat("BS", "4+"), Stat("S", "5")));

        var unit = EmptyUnit();
        unit.Selections.Add(child);

        // Act
        var profiles = RosterHelper.GetAllProfilesRecursive(unit, "Ranged Weapons");

        // Assert
        Assert.Single(profiles);
        Assert.Equal("Pulse Carbine", profiles[0].Name);
    }

    [Fact]
    public void GetAllProfilesRecursive_WithDeeplyNestedProfile_StillFindsIt()
    {
        // Arrange — three levels deep
        var grandchild = new Selection { Name = "Deep" };
        grandchild.Profiles.Add(WeaponProfile("Melee Weapons", "Knife"));

        var child = new Selection { Name = "Mid" };
        child.Selections.Add(grandchild);

        var unit = EmptyUnit();
        unit.Selections.Add(child);

        // Act
        var profiles = RosterHelper.GetAllProfilesRecursive(unit, "Melee Weapons");

        // Assert
        Assert.Single(profiles);
    }

    [Fact]
    public void GetAllProfilesRecursive_WithNoMatchingType_ReturnsEmptyList()
    {
        // Arrange
        var unit = EmptyUnit();
        unit.Profiles.Add(UnitProfile(Stat("T", "4")));

        // Act — look for "Ranged Weapons" when only "Unit" profiles exist
        var profiles = RosterHelper.GetAllProfilesRecursive(unit, "Ranged Weapons");

        // Assert
        Assert.Empty(profiles);
    }

    [Fact]
    public void GetAllProfilesRecursive_WithNullSelection_ReturnsEmptyList()
    {
        // Act
        var profiles = RosterHelper.GetAllProfilesRecursive(null, "Unit");

        // Assert
        Assert.Empty(profiles);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetKeywords
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetKeywords_ExcludesConfigurationAndUncategorizedCategories()
    {
        // Arrange
        var unit = EmptyUnit();
        unit.Categories.Add(MakeCategory("Configuration"));
        unit.Categories.Add(MakeCategory("Uncategorized"));
        unit.Categories.Add(MakeCategory("INFANTRY"));
        unit.Categories.Add(MakeCategory("T'AU EMPIRE"));

        // Act
        var keywords = RosterHelper.GetKeywords(unit);

        // Assert
        Assert.DoesNotContain("Configuration", keywords);
        Assert.DoesNotContain("Uncategorized", keywords);
        Assert.Contains("INFANTRY", keywords);
        Assert.Contains("T'AU EMPIRE", keywords);
    }

    [Fact]
    public void GetKeywords_WithNoCategories_ReturnsEmptyList()
    {
        // Arrange
        var unit = EmptyUnit();

        // Act
        var keywords = RosterHelper.GetKeywords(unit);

        // Assert
        Assert.Empty(keywords);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CollectRules
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void CollectRules_WithRulesOnRootSelection_ReturnsThem()
    {
        // Arrange
        var unit = EmptyUnit();
        unit.Rules.Add(MakeRule("For the Greater Good", "Reaction fire rule."));

        // Act
        var rules = RosterHelper.CollectRules(unit);

        // Assert
        Assert.Single(rules);
        Assert.Equal("For the Greater Good", rules[0].Name);
    }

    [Fact]
    public void CollectRules_WithRulesOnChildSelection_ReturnsAll()
    {
        // Arrange
        var child = new Selection { Name = "Equipment" };
        child.Rules.Add(MakeRule("Markerlight", "Reduces BS penalty."));

        var unit = EmptyUnit();
        unit.Rules.Add(MakeRule("For the Greater Good"));
        unit.Selections.Add(child);

        // Act
        var rules = RosterHelper.CollectRules(unit);

        // Assert
        Assert.Equal(2, rules.Count);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetAbilities
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetAbilities_WithFilterOn_ExcludesWeaponKeywordTermsFromGlossary()
    {
        // Arrange — "LETHAL HITS" is in the keyword glossary; should be filtered
        var unit = EmptyUnit();
        unit.Rules.Add(MakeRule("LETHAL HITS", "glossary term"));
        unit.Rules.Add(MakeRule("Invulnerable Save", "Has a 4+ invulnerable save."));

        // Act
        var abilities = RosterHelper.GetAbilities(unit, filterWeaponKeywords: true);

        // Assert
        Assert.DoesNotContain(abilities, a => a.Name == "LETHAL HITS");
        Assert.Contains(abilities, a => a.Name == "Invulnerable Save");
    }

    [Fact]
    public void GetAbilities_WithoutFilter_IncludesAllRules()
    {
        // Arrange
        var unit = EmptyUnit();
        unit.Rules.Add(MakeRule("LETHAL HITS"));
        unit.Rules.Add(MakeRule("Invulnerable Save"));

        // Act
        var abilities = RosterHelper.GetAbilities(unit, filterWeaponKeywords: false);

        // Assert
        Assert.Contains(abilities, a => a.Name == "LETHAL HITS");
        Assert.Contains(abilities, a => a.Name == "Invulnerable Save");
    }

    [Fact]
    public void GetAbilities_DeduplicatesByName_ReturnsSingleInstancePerName()
    {
        // Arrange — same rule defined on both the unit and a child selection
        var child = new Selection { Name = "Equipment" };
        child.Rules.Add(MakeRule("Shared Ability", "description"));

        var unit = EmptyUnit();
        unit.Rules.Add(MakeRule("Shared Ability", "description"));
        unit.Selections.Add(child);

        // Act
        var abilities = RosterHelper.GetAbilities(unit);

        // Assert
        Assert.Single(abilities, a => a.Name == "Shared Ability");
    }

    [Fact]
    public void GetAbilities_IncludesAbilitiesFromAbilityProfiles()
    {
        // Arrange — ability defined as a Profile of TypeName "Abilities"
        var unit = EmptyUnit();
        unit.Profiles.Add(AbilityProfile("Tau Caste", "All <SEPT> units gain bonuses."));

        // Act
        var abilities = RosterHelper.GetAbilities(unit);

        // Assert
        Assert.Contains(abilities, a => a.Name == "Tau Caste");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FormatText
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FormatText_WithBoldMarkdown_ConvertsToHtmlStrongTag()
    {
        // Act
        var result = RosterHelper.FormatText("Roll **1D6** and apply.");

        // Assert
        Assert.Contains("<strong>1D6</strong>", result.Value);
    }

    [Fact]
    public void FormatText_WithNewline_ConvertsToHtmlLineBreak()
    {
        // Act
        var result = RosterHelper.FormatText("Line one\nLine two");

        // Assert
        Assert.Contains("<br/>", result.Value);
    }

    [Fact]
    public void FormatText_WithCaretCaretSequence_RemovesThem()
    {
        // BattleScribe uses "^^" as a formatting marker; should be stripped
        var result = RosterHelper.FormatText("Text^^More text");

        // Assert
        Assert.DoesNotContain("^^", result.Value);
        Assert.Contains("Text", result.Value);
        Assert.Contains("More text", result.Value);
    }

    [Fact]
    public void FormatText_WithEmptyString_ReturnsEmptyMarkupString()
    {
        // Act
        var result = RosterHelper.FormatText("");

        // Assert
        Assert.Equal("", result.Value);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetCleanWeaponName
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetCleanWeaponName_WithBracketedSuffix_RemovesBrackets()
    {
        // BattleScribe appends variant notes like "[x2]" or "[Leader]"
        var clean = RosterHelper.GetCleanWeaponName("Pulse Rifle [x2]");

        // Assert
        Assert.Equal("Pulse Rifle", clean);
    }

    [Fact]
    public void GetCleanWeaponName_WithNoBrackets_ReturnsNameUnchanged()
    {
        // Act
        var clean = RosterHelper.GetCleanWeaponName("Ion Blaster");

        // Assert
        Assert.Equal("Ion Blaster", clean);
    }

    [Fact]
    public void GetCleanWeaponName_WithMultipleBrackets_RemovesAll()
    {
        // Act
        var clean = RosterHelper.GetCleanWeaponName("Rail Rifle [Leader] [x2]");

        // Assert
        Assert.Equal("Rail Rifle", clean);
    }
}
