using System.ComponentModel.DataAnnotations;

namespace Warhammer.Models.Campaign;

public class Campaign
{
    [Key]
    public int Id { get; set; }

    public string CampaignName   { get; set; } = "Narrative Campaign";
    public int    MapSeed        { get; set; } = Random.Shared.Next(1, 999_999);
    public string MapBiome       { get; set; } = "agri";
    public int    MapOctaves     { get; set; } = 4;
    public double MapLacunarity  { get; set; } = 2.0;
    public double MapPersistence { get; set; } = 0.5;
    public double MapScale       { get; set; } = 2.0;
    public double MapOffsetX     { get; set; } = 0.0;
    public double MapOffsetY     { get; set; } = 0.0;

    public List<CampaignPlayer> Players       { get; set; } = new();
    public List<Node>           MapNodes      { get; set; } = new();
    public List<BattleRecord>   BattleHistory { get; set; } = new();
    public List<GmNote>         GmNotebook    { get; set; } = new();

    // What an Order of Battle can buy with Requisition Points; edited in the Campaign Editor.
    public List<Requisition>    Requisitions  { get; set; } = new();
    // The default list is given once. Without this flag a list the Game Master emptied would refill.
    public bool                 RequisitionsSeeded { get; set; }
}
