using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Services;

/// <summary>
/// Tests for ObjectiveService and PlayerState — deck loading from JSON,
/// player state routing, VP scoring math, and reset behaviour.
/// </summary>
public class ObjectiveServiceTests : IDisposable
{
    private readonly TempWebRoot _temp = new();
    private readonly Mock<ILogger<ObjectiveService>> _logger = new();

    public void Dispose() => _temp.Dispose();

    private ObjectiveService BuildService()
    {
        var env = _temp.BuildMockEnv();
        return new ObjectiveService(env.Object, _logger.Object);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Constructor — JSON loading
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Constructor_WithValidObjectivesJson_LoadsDefaultDeckForBothPlayers()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);

        // Act
        var svc = BuildService();
        svc.P1State.Reset();

        // Assert — deck should have the 3 objectives from our minimal JSON
        Assert.Equal(3, svc.P1State.Deck.Count);
    }

    [Fact]
    public void Constructor_WithMissingObjectivesFile_ProducesEmptyDeckWithoutThrowing()
    {
        // Arrange — no file written
        ObjectiveService? svc = null;

        // Act
        var ex = Record.Exception(() => svc = BuildService());

        // Assert
        Assert.Null(ex);
        svc!.P1State.Reset();
        Assert.Empty(svc.P1State.Deck);
    }

    [Fact]
    public void Constructor_WithMalformedJson_ProducesEmptyDeckWithoutThrowing()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", "{ NOT VALID JSON [[[");
        ObjectiveService? svc = null;

        // Act
        var ex = Record.Exception(() => svc = BuildService());

        // Assert
        Assert.Null(ex);
        svc!.P1State.Reset();
        Assert.Empty(svc.P1State.Deck);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetState — routing
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetState_WithP1Side_ReturnsP1State()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        var state = svc.GetState("p1");

        // Assert — same reference as P1State
        Assert.Same(svc.P1State, state);
    }

    [Fact]
    public void GetState_WithP2Side_ReturnsP2State()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        var state = svc.GetState("p2");

        // Assert
        Assert.Same(svc.P2State, state);
    }

    [Fact]
    public void GetState_WithUnknownSide_FallsBackToP1State()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        var state = svc.GetState("p999");

        // Assert — unknown side returns P1
        Assert.Same(svc.P1State, state);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PlayerState.Reset
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerState_Reset_PopulatesDeckFromDefaultDeck()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        svc.P1State.Reset();

        // Assert
        Assert.Equal(3, svc.P1State.Deck.Count);
        Assert.Contains(svc.P1State.Deck, o => o.Name == "Assassination");
    }

    [Fact]
    public void PlayerState_Reset_CreatesNewCopiesOfObjectives_NotSharedReferences()
    {
        // Arrange — both players must have independent deck instances
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        svc.P1State.Reset();
        svc.P2State.Reset();

        // Mutate P1's deck
        svc.P1State.Deck.RemoveAt(0);

        // Assert — P2's deck is unaffected
        Assert.Equal(3, svc.P2State.Deck.Count);
    }

    [Fact]
    public void PlayerState_Reset_ClearsActiveMissionsAndScoredMissions()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.ActiveMissions.Add(new Objective("X", "Y", "Z", "1VP"));
        svc.P1State.ScoredMissions.Add(new Objective("A", "B", "C", "2VP"));

        // Act
        svc.P1State.Reset();

        // Assert
        Assert.Empty(svc.P1State.ActiveMissions);
        Assert.Empty(svc.P1State.ScoredMissions);
    }

    [Fact]
    public void PlayerState_Reset_ZerosPrimaryScores()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.PrimaryScores[0] = 15;
        svc.P1State.PrimaryScores[3] = 20;

        // Act
        svc.P1State.Reset();

        // Assert
        Assert.All(svc.P1State.PrimaryScores, score => Assert.Equal(0, score));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PlayerState VP scoring math
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerState_PrimaryTotal_CapAt50()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        // Sum each round to 60 total (above 50 cap)
        svc.P1State.PrimaryScores[0] = 20;
        svc.P1State.PrimaryScores[1] = 20;
        svc.P1State.PrimaryScores[2] = 20;

        // Act
        var total = svc.P1State.PrimaryTotal;

        // Assert
        Assert.Equal(50, total);
    }

    [Fact]
    public void PlayerState_PrimaryTotal_BelowCapReturnsActualSum()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.PrimaryScores[0] = 10;
        svc.P1State.PrimaryScores[1] = 15;

        // Act & Assert
        Assert.Equal(25, svc.P1State.PrimaryTotal);
    }

    [Fact]
    public void PlayerState_SecondaryTotal_CapAt40()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.SecondaryVP = 55;

        // Act & Assert
        Assert.Equal(40, svc.P1State.SecondaryTotal);
    }

    [Fact]
    public void PlayerState_TotalScore_IsCorrectSumOfPrimaryAndSecondary()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.PrimaryScores[0] = 30;
        svc.P1State.SecondaryVP = 25;

        // Act & Assert
        Assert.Equal(55, svc.P1State.TotalScore);
    }

    [Fact]
    public void PlayerState_TotalScore_WithBothCapped_MaximumIs90()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();
        svc.P1State.PrimaryScores[0] = 100; // will be capped at 50
        svc.P1State.SecondaryVP = 100;      // will be capped at 40

        // Act & Assert
        Assert.Equal(90, svc.P1State.TotalScore);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PlayerState.IsInitialized
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerState_IsInitialized_IsTrueAfterReset()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act
        svc.P1State.Reset(); // populates Deck

        // Assert
        Assert.True(svc.P1State.IsInitialized);
    }

    [Fact]
    public void PlayerState_IsInitialized_IsFalseWhenAllCollectionsEmpty()
    {
        // Arrange
        _temp.WriteDataFile("TacticalObjectives.json", TempWebRoot.MinimalObjectivesJson);
        var svc = BuildService();

        // Act — do NOT call Reset(); state is brand new and empty

        // Assert
        Assert.False(svc.P1State.IsInitialized);
    }
}
