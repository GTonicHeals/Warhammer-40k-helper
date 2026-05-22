using System.Text.Json.Serialization;

namespace Warhammer.Models.Crusade;

public enum CrusadeRank
{
    BattleReady,
    Blooded,
    BattleHardened,
    Heroic,
    Legendary
}

public class CrusadeUnitCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UnitName { get; set; } = "";
    public string DatasheetName { get; set; } = "";
    public int TotalPoints { get; set; }
    public int Xp { get; set; }
    public int UnitsDestroyed { get; set; }
    public int CrusadePoints { get; set; }
    public List<string> BattleHonours { get; set; } = new();
    public List<string> BattleScars { get; set; } = new();
    public bool IsCharacter { get; set; }
    public bool LegendaryVeteransUsed { get; set; }

    [JsonIgnore]
    public CrusadeRank Rank => RankCalculator.RankFor(Xp, IsCharacter, LegendaryVeteransUsed);
}

public static class RankCalculator
{
    // Tiers from crusade_rules.json: ranks_and_promotions.tiers
    //   0-5   Battle-ready
    //   6-15  Blooded
    //   16-30 Battle-hardened
    //   31-50 Heroic        (CHARACTER only, unless Legendary Veterans requisition was used)
    //   51+   Legendary     (CHARACTER only, unless Legendary Veterans requisition was used)
    public static CrusadeRank RankFor(int xp, bool isCharacter, bool legendaryVeteransUsed)
    {
        if (xp <= 5) return CrusadeRank.BattleReady;
        if (xp <= 15) return CrusadeRank.Blooded;
        if (xp <= 30) return CrusadeRank.BattleHardened;

        if (!isCharacter && !legendaryVeteransUsed) return CrusadeRank.BattleHardened;

        return xp <= 50 ? CrusadeRank.Heroic : CrusadeRank.Legendary;
    }
}
