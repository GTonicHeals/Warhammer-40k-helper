namespace Warhammer.Models.Campaign;

public class Territory : Node
{
    public string     BuffEffect          { get; set; } = "";
    public string?    ControllingPlayerId { get; set; }
    public int        VictoryPointValue   { get; set; } = 1;
    public TerrainType TerrainType        { get; set; } = TerrainType.Standard;
}
