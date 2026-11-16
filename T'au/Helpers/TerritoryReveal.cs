using Warhammer.Models.Campaign;

namespace Warhammer.Helpers;

// A Territory is revealed to the players when Node.IsPublic is true. The GM reveals them by hand,
// and capturing a Territory reveals it and its hidden neighbours.
public static class TerritoryReveal
{
    // Hidden Territories a capture would reveal: the captured one and every Territory linked to it
    // in either direction. Pure: the caller sets IsPublic.
    public static IReadOnlyList<Territory> RevealedByCapture(IEnumerable<Node> nodes, string? capturedTerritoryId)
    {
        var territories = nodes.OfType<Territory>().ToList();
        var captured = territories.FirstOrDefault(t => t.Id == capturedTerritoryId);
        if (captured is null) return [];

        return territories
            .Where(t => !t.IsPublic &&
                        (t == captured ||
                         captured.ConnectedNodeIds.Contains(t.Id) ||
                         t.ConnectedNodeIds.Contains(captured.Id)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public static bool IsVisible(Node node, IEnumerable<Node> nodes) => node switch
    {
        Territory t      => t.IsPublic,
        CampaignEvent ev => ev.IsEventRevealed &&
                            nodes.OfType<Territory>().Any(t => t.Id == ev.AttachedToNodeId && t.IsPublic),
        _                => false
    };

    public static List<Node> VisibleOnWarRoom(IEnumerable<Node> nodes)
    {
        var list = nodes as IList<Node> ?? nodes.ToList();
        return list.Where(n => IsVisible(n, list)).ToList();
    }
}
