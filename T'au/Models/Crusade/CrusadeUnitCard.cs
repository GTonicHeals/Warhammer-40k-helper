using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OrderOfBattleId { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string DatasheetName { get; set; } = "";
    public int TotalPoints { get; set; }
    public int Xp { get; set; }
    public int UnitsDestroyed { get; set; }
    public int BattlesPlayed { get; set; }
    public int BattlesSurvived { get; set; }
    public bool IsTitanic { get; set; }
    // EPIC HERO, FORTIFICATION and SWARM units never gain XP.
    public bool XpEligible { get; set; } = true;

    // Derived; legacy JSON carrying a typed "crusadePoints" number is ignored on read.
    [NotMapped, JsonIgnore]
    public int CrusadePoints => CrusadePointsCalculator.For(this);

    [JsonConverter(typeof(AppliedTraitListConverter))]
    public List<AppliedTrait> BattleHonours { get; set; } = new();

    [JsonConverter(typeof(AppliedTraitListConverter))]
    public List<AppliedTrait> BattleScars { get; set; } = new();
    public bool IsCharacter { get; set; }
    public bool LegendaryVeteransUsed { get; set; }

    [NotMapped, JsonIgnore]
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

    // XP at which the unit reaches its next rank, or null when it can't promote further.
    public static int? NextRankXp(int xp, bool isCharacter, bool legendaryVeteransUsed)
    {
        if (xp <= 5) return 6;
        if (xp <= 15) return 16;
        if (!isCharacter && !legendaryVeteransUsed) return null;
        if (xp <= 30) return 31;
        return xp <= 50 ? 51 : null;
    }

    public static string Label(CrusadeRank rank) => rank switch
    {
        CrusadeRank.BattleReady    => "Battle-ready",
        CrusadeRank.Blooded        => "Blooded",
        CrusadeRank.BattleHardened => "Battle-hardened",
        CrusadeRank.Heroic         => "Heroic",
        CrusadeRank.Legendary      => "Legendary",
        _                          => rank.ToString()
    };
}
