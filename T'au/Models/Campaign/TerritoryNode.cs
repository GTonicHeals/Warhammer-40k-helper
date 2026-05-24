namespace Warhammer.Models.Campaign;

public class TerritoryNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string BuffEffect { get; set; } = "";
    public string? ControllingPlayerId { get; set; }
    public List<string> ConnectedNodeIds { get; set; } = new();
    public string SecretEventText { get; set; } = "";
    public bool IsEventRevealed { get; set; } = false;
    // Normalised map coordinates [0,1]. -1 = unplaced (auto-grid on first render).
    public double X { get; set; } = -1;
    public double Y { get; set; } = -1;
}
