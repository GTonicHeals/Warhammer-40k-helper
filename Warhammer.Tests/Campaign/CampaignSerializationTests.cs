using System.Text.Json;
using FluentAssertions;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class CampaignSerializationTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static T RoundTrip<T>(T obj) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, Opts), Opts)!;

    [Fact]
    public void CampaignState_roundtrips_empty_state()
    {
        var state = new CampaignState { CampaignName = "Test Campaign" };
        var result = RoundTrip(state);

        result.CampaignName.Should().Be("Test Campaign");
        result.Players.Should().BeEmpty();
        result.MapNodes.Should().BeEmpty();
        result.BattleHistory.Should().BeEmpty();
    }

    [Fact]
    public void TerritoryNode_ConnectedNodeIds_roundtrip()
    {
        var nodeA = new TerritoryNode { Id = "node-a", Name = "Hab Block" };
        var nodeB = new TerritoryNode { Id = "node-b", Name = "Manufactorum" };
        nodeA.ConnectedNodeIds.Add(nodeB.Id);
        nodeB.ConnectedNodeIds.Add(nodeA.Id);

        var resultA = RoundTrip(nodeA);
        var resultB = RoundTrip(nodeB);

        resultA.ConnectedNodeIds.Should().ContainSingle().Which.Should().Be("node-b");
        resultB.ConnectedNodeIds.Should().ContainSingle().Which.Should().Be("node-a");
    }

    [Fact]
    public void TerritoryNode_ControllingPlayerId_null_roundtrips()
    {
        var node = new TerritoryNode { Id = "node-x", ControllingPlayerId = null };
        RoundTrip(node).ControllingPlayerId.Should().BeNull();
    }

    [Fact]
    public void TerritoryNode_ControllingPlayerId_set_roundtrips()
    {
        var node = new TerritoryNode { Id = "node-x", ControllingPlayerId = "p1" };
        RoundTrip(node).ControllingPlayerId.Should().Be("p1");
    }

    [Fact]
    public void BattleRecord_CapturedTerritoryId_nullable_roundtrip()
    {
        var rec = new BattleRecord { MissionName = "Test", CapturedTerritoryId = null };
        RoundTrip(rec).CapturedTerritoryId.Should().BeNull();

        var rec2 = new BattleRecord { MissionName = "Test", CapturedTerritoryId = "node-a" };
        RoundTrip(rec2).CapturedTerritoryId.Should().Be("node-a");
    }

    [Fact]
    public void CampaignState_Players_dictionary_roundtrip()
    {
        var state = new CampaignState
        {
            Players = new()
            {
                ["p1"] = new CampaignPlayer { Id = "p1", Name = "Player 1", Wins = 3, VictoryPoints = 42 },
                ["p2"] = new CampaignPlayer { Id = "p2", Name = "Player 2", Losses = 2, CampaignPoints = 10 }
            }
        };

        var result = RoundTrip(state);

        result.Players.Should().HaveCount(2);
        result.Players["p1"].Name.Should().Be("Player 1");
        result.Players["p1"].Wins.Should().Be(3);
        result.Players["p1"].VictoryPoints.Should().Be(42);
        result.Players["p2"].Losses.Should().Be(2);
        result.Players["p2"].CampaignPoints.Should().Be(10);
    }

    [Fact]
    public void CampaignState_with_interconnected_nodes_serializes_without_circular_reference_exception()
    {
        var nodeA = new TerritoryNode { Id = "a", Name = "Alpha", ControllingPlayerId = "p1" };
        var nodeB = new TerritoryNode { Id = "b", Name = "Beta", ControllingPlayerId = "p2" };
        nodeA.ConnectedNodeIds.Add("b");
        nodeB.ConnectedNodeIds.Add("a");

        var state = new CampaignState
        {
            CampaignName = "Siege of Secondus",
            Players = new()
            {
                ["p1"] = new CampaignPlayer { Id = "p1", Name = "Player 1" },
                ["p2"] = new CampaignPlayer { Id = "p2", Name = "Player 2" }
            },
            MapNodes = [nodeA, nodeB],
            BattleHistory =
            [
                new BattleRecord
                {
                    MissionName = "Sweep and Clear",
                    WinnerPlayerId = "p1",
                    Player1Vp = 75,
                    Player2Vp = 50,
                    CapturedTerritoryId = "b"
                }
            ]
        };

        var act = () => RoundTrip(state);
        act.Should().NotThrow();

        var result = act();
        result.MapNodes.Should().HaveCount(2);
        result.BattleHistory.Should().HaveCount(1);
        result.BattleHistory[0].CapturedTerritoryId.Should().Be("b");
    }
}
