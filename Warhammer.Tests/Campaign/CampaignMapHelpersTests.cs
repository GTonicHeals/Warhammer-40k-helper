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
        var node = new Territory { Id = "a" };
        var edges = CampaignMapHelpers.BuildEdges([node]);
        edges.Should().BeEmpty();
    }

    [Fact]
    public void BuildEdges_bidirectional_pair_emits_exactly_one_edge()
    {
        var a = new Territory { Id = "a", ConnectedNodeIds = ["b"] };
        var b = new Territory { Id = "b", ConnectedNodeIds = ["a"] };

        var edges = CampaignMapHelpers.BuildEdges([a, b]);

        edges.Should().HaveCount(1);
        edges[0].From.Should().Be("a");
        edges[0].To.Should().Be("b");
    }

    [Fact]
    public void BuildEdges_three_nodes_fully_connected_emits_three_edges()
    {
        var a = new Territory { Id = "a", ConnectedNodeIds = ["b", "c"] };
        var b = new Territory { Id = "b", ConnectedNodeIds = ["a", "c"] };
        var c = new Territory { Id = "c", ConnectedNodeIds = ["a", "b"] };

        var edges = CampaignMapHelpers.BuildEdges([a, b, c]);

        edges.Should().HaveCount(3);
    }

    [Fact]
    public void BuildEdges_unidirectional_orphan_reference_still_emits_edge()
    {
        var a = new Territory { Id = "a", ConnectedNodeIds = ["b"] };
        var b = new Territory { Id = "b" };

        var edges = CampaignMapHelpers.BuildEdges([a, b]);

        edges.Should().HaveCount(1);
    }

    [Fact]
    public void BuildEdges_event_emits_attachment_edge_to_its_territory()
    {
        var t   = new Territory    { Id = "t" };
        var evt = new CampaignEvent { Id = "e", AttachedToNodeId = "t" };

        var edges = CampaignMapHelpers.BuildEdges([t, evt]);

        edges.Should().HaveCount(1);
        edges[0].From.Should().Be("t");
        edges[0].To.Should().Be("e");
        edges[0].Kind.Should().Be(CampaignMapHelpers.EdgeKind.EventAttachment);
    }

    [Fact]
    public void BuildEdges_event_with_unattached_id_emits_no_edge()
    {
        var evt = new CampaignEvent { Id = "e", AttachedToNodeId = null };

        var edges = CampaignMapHelpers.BuildEdges([evt]);

        edges.Should().BeEmpty();
    }

    [Fact]
    public void BuildEdges_event_attached_to_missing_territory_emits_no_edge()
    {
        var evt = new CampaignEvent { Id = "e", AttachedToNodeId = "ghost" };

        var edges = CampaignMapHelpers.BuildEdges([evt]);

        edges.Should().BeEmpty();
    }

    [Fact]
    public void BuildEdges_event_ConnectedNodeIds_are_ignored()
    {
        var t   = new Territory    { Id = "t" };
        var evt = new CampaignEvent { Id = "e", AttachedToNodeId = "t", ConnectedNodeIds = ["t"] };

        var edges = CampaignMapHelpers.BuildEdges([t, evt]);

        edges.Should().HaveCount(1);
        edges[0].Kind.Should().Be(CampaignMapHelpers.EdgeKind.EventAttachment);
    }

}
