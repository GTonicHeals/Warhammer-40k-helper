namespace Warhammer.Tests.Services;

/// <summary>
/// Tests for GameStateService — battle round tracking, per-player CP management,
/// event notification, and reset behaviour.
/// </summary>
public class GameStateServiceTests
{
    private static GameStateService CreateService() => new();

    // ═════════════════════════════════════════════════════════════════════════
    // Initial state
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InitialState_BattleRoundIsOne()
    {
        // Arrange & Act
        var svc = CreateService();

        // Assert
        Assert.Equal(1, svc.BattleRound);
    }

    [Fact]
    public void InitialState_CommandPointsForAnyPlayerAreZero()
    {
        // Arrange & Act
        var svc = CreateService();

        // Assert
        Assert.Equal(0, svc.GetCP("p1"));
        Assert.Equal(0, svc.GetCP("p2"));
    }

    [Fact]
    public void GetCP_ForUnknownPlayer_ReturnsZero()
    {
        // Arrange
        var svc = CreateService();

        // Act
        var cp = svc.GetCP("p99");

        // Assert
        Assert.Equal(0, cp);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Battle round
    // ═════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void SetBattleRound_WithValidRound_SetsRoundCorrectly(int round)
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.SetBattleRound(round);

        // Assert
        Assert.Equal(round, svc.BattleRound);
    }

    [Fact]
    public void SetBattleRound_WithRoundBelowOne_ClampsToOne()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.SetBattleRound(0);

        // Assert
        Assert.Equal(1, svc.BattleRound);
    }

    [Fact]
    public void SetBattleRound_WithNegativeRound_ClampsToOne()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.SetBattleRound(-10);

        // Assert
        Assert.Equal(1, svc.BattleRound);
    }

    [Fact]
    public void SetBattleRound_WithRoundAboveFive_ClampsToFive()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.SetBattleRound(99);

        // Assert
        Assert.Equal(5, svc.BattleRound);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Command points — AddCP
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddCP_ToNewPlayer_StartsFromZeroAndAddsAmount()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.AddCP("p1", 3);

        // Assert
        Assert.Equal(3, svc.GetCP("p1"));
    }

    [Fact]
    public void AddCP_MultipleTimes_AccumulatesCorrectly()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.AddCP("p1", 2);
        svc.AddCP("p1", 3);

        // Assert
        Assert.Equal(5, svc.GetCP("p1"));
    }

    [Fact]
    public void AddCP_ForDifferentPlayers_TrackedIndependently()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.AddCP("p1", 4);
        svc.AddCP("p2", 6);

        // Assert
        Assert.Equal(4, svc.GetCP("p1"));
        Assert.Equal(6, svc.GetCP("p2"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Command points — SpendCP
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SpendCP_ReducesCommandPointsByAmount()
    {
        // Arrange
        var svc = CreateService();
        svc.AddCP("p1", 5);

        // Act
        svc.SpendCP("p1", 2);

        // Assert
        Assert.Equal(3, svc.GetCP("p1"));
    }

    [Fact]
    public void SpendCP_WhenAmountExceedsAvailable_FloorsAtZero()
    {
        // Arrange
        var svc = CreateService();
        svc.AddCP("p1", 2);

        // Act
        svc.SpendCP("p1", 10);

        // Assert
        Assert.Equal(0, svc.GetCP("p1"));
    }

    [Fact]
    public void SpendCP_WhenCpIsAlreadyZero_StaysAtZero()
    {
        // Arrange
        var svc = CreateService();

        // Act
        svc.SpendCP("p1", 1);

        // Assert
        Assert.Equal(0, svc.GetCP("p1"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Reset
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Reset_SetsBattleRoundBackToOne()
    {
        // Arrange
        var svc = CreateService();
        svc.SetBattleRound(4);

        // Act
        svc.Reset();

        // Assert
        Assert.Equal(1, svc.BattleRound);
    }

    [Fact]
    public void Reset_ClearsCommandPointsForAllPlayers()
    {
        // Arrange
        var svc = CreateService();
        svc.AddCP("p1", 5);
        svc.AddCP("p2", 8);

        // Act
        svc.Reset();

        // Assert
        Assert.Equal(0, svc.GetCP("p1"));
        Assert.Equal(0, svc.GetCP("p2"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // OnChange event
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetBattleRound_FiresOnChangeEvent()
    {
        // Arrange
        var svc = CreateService();
        int callCount = 0;
        svc.OnChange += () => callCount++;

        // Act
        svc.SetBattleRound(3);

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void AddCP_FiresOnChangeEvent()
    {
        // Arrange
        var svc = CreateService();
        int callCount = 0;
        svc.OnChange += () => callCount++;

        // Act
        svc.AddCP("p1", 2);

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void SpendCP_FiresOnChangeEvent()
    {
        // Arrange
        var svc = CreateService();
        svc.AddCP("p1", 5);
        int callCount = 0;
        svc.OnChange += () => callCount++;

        // Act
        svc.SpendCP("p1", 1);

        // Assert
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void Reset_FiresOnChangeEvent()
    {
        // Arrange
        var svc = CreateService();
        int callCount = 0;
        svc.OnChange += () => callCount++;

        // Act
        svc.Reset();

        // Assert
        Assert.Equal(1, callCount);
    }
}
