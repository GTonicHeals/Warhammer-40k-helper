using Warhammer.Models.Campaign;

namespace Warhammer.Helpers;

public static class CampaignMapHelpers
{
    public enum EdgeKind { TerritoryLink, EventAttachment }

    public record JsEdge(string From, string To, EdgeKind Kind = EdgeKind.TerritoryLink);

    // Deduplicates bidirectional edges: for any pair (A,B) only one TerritoryLink edge is emitted.
    // Events do not participate in ConnectedNodeIds; their only edge is to their AttachedToNodeId.
    public static IReadOnlyList<JsEdge> BuildEdges(IEnumerable<Node> nodes)
    {
        var nodeList = nodes as IList<Node> ?? nodes.ToList();
        var nodeIds  = new HashSet<string>(nodeList.Select(n => n.Id));
        var seen     = new HashSet<string>();
        var edges    = new List<JsEdge>();

        foreach (var node in nodeList)
        {
            if (node is CampaignEvent) continue;

            foreach (var targetId in node.ConnectedNodeIds)
            {
                var key = string.CompareOrdinal(node.Id, targetId) < 0
                    ? $"{node.Id}|{targetId}"
                    : $"{targetId}|{node.Id}";

                if (seen.Add(key))
                    edges.Add(new JsEdge(node.Id, targetId, EdgeKind.TerritoryLink));
            }
        }

        foreach (var evt in nodeList.OfType<CampaignEvent>())
        {
            if (string.IsNullOrWhiteSpace(evt.AttachedToNodeId)) continue;
            if (!nodeIds.Contains(evt.AttachedToNodeId)) continue;

            edges.Add(new JsEdge(evt.AttachedToNodeId, evt.Id, EdgeKind.EventAttachment));
        }

        return edges;
    }
}
