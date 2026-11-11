namespace Warhammer.Models.Crusade;

// Crusade-relevant facts read from a datasheet's keywords when a unit joins an Order of Battle.
//   TITANIC                         → Battle Honours cost 2 Crusade Points
//   EPIC HERO, FORTIFICATION, SWARM → never gain XP
public record CrusadeUnitFlags(bool IsCharacter, bool IsTitanic, bool XpEligible)
{
    private static readonly string[] NoXpKeywords = ["Epic Hero", "Fortification", "Swarm"];

    public static CrusadeUnitFlags FromKeywords(IEnumerable<string> keywords)
    {
        var set = new HashSet<string>(keywords, StringComparer.OrdinalIgnoreCase);
        return new CrusadeUnitFlags(
            IsCharacter: set.Contains("Character"),
            IsTitanic:   set.Contains("Titanic"),
            XpEligible:  !NoXpKeywords.Any(set.Contains));
    }
}
