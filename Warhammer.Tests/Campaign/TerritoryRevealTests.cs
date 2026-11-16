using FluentAssertions;
using Warhammer.Helpers;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class TerritoryRevealTests
{
    private static Territory T(string id, bool revealed = false, params string[] links) =>
        new() { Id = id, Name = id, IsPublic = revealed, ConnectedNodeIds = links.ToList() };

    [Fact]
    public void Capture_reveals_hidden_neighbours_and_the_captured_territory()
    {
        var nodes = new List<Node> { T("t1", false, "t2"), T("t2"), T("t3") };

        TerritoryReveal.RevealedByCapture(nodes, "t1").Select(t => t.Name).Should().Equal("t1", "t2");
    }

    [Fact]
    public void Capture_counts_connections_listed_only_on_the_neighbour()
    {
        var nodes = new List<Node> { T("t1", true), T("t2", false, "t1") };

        TerritoryReveal.RevealedByCapture(nodes, "t1").Select(t => t.Id).Should().Equal("t2");
    }

    [Fact]
    public void Capture_skips_already_revealed_and_events()
    {
        var nodes = new List<Node>
        {
            T("t1", true, "t2", "e1"), T("t2", true),
            new CampaignEvent { Id = "e1", Name = "e1", AttachedToNodeId = "t1" }
        };

        TerritoryReveal.RevealedByCapture(nodes, "t1").Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("missing")]
    public void No_capture_reveals_nothing(string? capturedId)
    {
        var nodes = new List<Node> { T("t1", false, "t2"), T("t2") };

        TerritoryReveal.RevealedByCapture(nodes, capturedId).Should().BeEmpty();
    }

    [Fact]
    public void War_room_hides_hidden_territories_and_their_events()
    {
        var nodes = new List<Node>
        {
            T("t1", true), T("t2"),
            new CampaignEvent { Id = "e1", IsEventRevealed = true,  AttachedToNodeId = "t2" },
            new CampaignEvent { Id = "e2", IsEventRevealed = true,  AttachedToNodeId = "t1" },
            new CampaignEvent { Id = "e3", IsEventRevealed = false, AttachedToNodeId = "t1" }
        };

        TerritoryReveal.VisibleOnWarRoom(nodes).Select(n => n.Id).Should().Equal("t1", "e2");
    }
}
