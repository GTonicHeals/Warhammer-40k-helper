using FluentAssertions;
using Warhammer.Helpers;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class CampaignMapHelpersTests
{
    [Fact]
    public void BuildEdges_empty_nodes_returns_no_edges()
    {
        var edges = CampaignMapHelpers.BuildEdges([]);
        edges.Should().BeEmpty();
    }

    [Fact]
    public void BuildEdges_single_node_no_connections_returns_no_edges()
    {
        var node = new TerritoryNode { Id = "a" };
        var edges = CampaignMapHelpers.BuildEdges([node]);
        edges.Should().BeEmpty();
    }

    [Fact]
    public void BuildEdges_bidirectional_pair_emits_exactly_one_edge()
    {
        var a = new TerritoryNode { Id = "a", ConnectedNodeIds = ["b"] };
        var b = new TerritoryNode { Id = "b", ConnectedNodeIds = ["a"] };

        var edges = CampaignMapHelpers.BuildEdges([a, b]);

        edges.Should().HaveCount(1);
        edges[0].From.Should().Be("a");
        edges[0].To.Should().Be("b");
    }

    [Fact]
    public void BuildEdges_three_nodes_fully_connected_emits_three_edges()
    {
        var a = new TerritoryNode { Id = "a", ConnectedNodeIds = ["b", "c"] };
        var b = new TerritoryNode { Id = "b", ConnectedNodeIds = ["a", "c"] };
        var c = new TerritoryNode { Id = "c", ConnectedNodeIds = ["a", "b"] };

        var edges = CampaignMapHelpers.BuildEdges([a, b, c]);

        edges.Should().HaveCount(3);
    }

    [Fact]
    public void BuildEdges_unidirectional_orphan_reference_still_emits_edge()
    {
        var a = new TerritoryNode { Id = "a", ConnectedNodeIds = ["b"] };
        var b = new TerritoryNode { Id = "b" };

        var edges = CampaignMapHelpers.BuildEdges([a, b]);

        edges.Should().HaveCount(1);
    }

}
