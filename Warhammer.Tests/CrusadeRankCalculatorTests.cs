using FluentAssertions;
using Warhammer.Models.Crusade;

namespace Warhammer.Tests;

public class CrusadeRankCalculatorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public void Xp_AtOrBelow5_IsBattleReady(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: false, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.BattleReady);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(15)]
    public void Xp_6To15_IsBlooded(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: false, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.Blooded);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(30)]
    public void Xp_16To30_IsBattleHardened(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: false, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.BattleHardened);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(50)]
    public void Character_31To50_IsHeroic(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: true, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.Heroic);
    }

    [Theory]
    [InlineData(51)]
    [InlineData(120)]
    public void Character_51Plus_IsLegendary(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: true, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.Legendary);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(50)]
    [InlineData(100)]
    public void NonCharacter_Above30Xp_WithoutLegendaryVeterans_CapsAtBattleHardened(int xp)
    {
        RankCalculator.RankFor(xp, isCharacter: false, legendaryVeteransUsed: false)
            .Should().Be(CrusadeRank.BattleHardened);
    }

    [Fact]
    public void NonCharacter_WithLegendaryVeterans_PromotesToHeroic()
    {
        RankCalculator.RankFor(40, isCharacter: false, legendaryVeteransUsed: true)
            .Should().Be(CrusadeRank.Heroic);
    }

    [Fact]
    public void NonCharacter_WithLegendaryVeterans_AtHighXp_CanReachLegendary()
    {
        RankCalculator.RankFor(60, isCharacter: false, legendaryVeteransUsed: true)
            .Should().Be(CrusadeRank.Legendary);
    }
}
