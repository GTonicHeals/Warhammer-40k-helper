using Warhammer.Models.Campaign;

namespace Warhammer.Helpers;

public static class CampaignMapHelpers
{
    public record JsEdge(string From, string To);

    // Deduplicates bidirectional edges: for any pair (A,B) only one edge is emitted.
    public static IReadOnlyList<JsEdge> BuildEdges(IEnumerable<Node> nodes)
    {
        var seen  = new HashSet<string>();
        var edges = new List<JsEdge>();

        foreach (var node in nodes)
        {
            foreach (var targetId in node.ConnectedNodeIds)
            {
                var key = string.CompareOrdinal(node.Id, targetId) < 0
                    ? $"{node.Id}|{targetId}"
                    : $"{targetId}|{node.Id}";

                if (seen.Add(key))
                    edges.Add(new JsEdge(node.Id, targetId));
            }
        }

        return edges;
    }
}
