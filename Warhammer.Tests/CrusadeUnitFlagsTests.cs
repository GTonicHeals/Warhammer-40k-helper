using FluentAssertions;
using Warhammer.Models.Crusade;

namespace Warhammer.Tests;

public class CrusadeUnitFlagsTests
{
    [Fact]
    public void Plain_infantry_is_eligible_and_not_titanic()
    {
        var f = CrusadeUnitFlags.FromKeywords(["Infantry", "Battleline"]);
        f.Should().Be(new CrusadeUnitFlags(IsCharacter: false, IsTitanic: false, XpEligible: true));
    }

    [Fact]
    public void Titanic_keyword_sets_titanic_case_insensitively() =>
        CrusadeUnitFlags.FromKeywords(["Vehicle", "TITANIC"]).IsTitanic.Should().BeTrue();

    [Theory]
    [InlineData("Epic Hero")]
    [InlineData("Fortification")]
    [InlineData("Swarm")]
    public void Excluded_keywords_make_unit_xp_ineligible(string keyword) =>
        CrusadeUnitFlags.FromKeywords([keyword]).XpEligible.Should().BeFalse();

    [Fact]
    public void Character_keyword_sets_character() =>
        CrusadeUnitFlags.FromKeywords(["character", "Infantry"]).IsCharacter.Should().BeTrue();
}
