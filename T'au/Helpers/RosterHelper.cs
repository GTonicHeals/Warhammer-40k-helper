using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;
using Warhammer.Models;

namespace Warhammer.Helpers
{
    public record KeywordToken(string Raw, string? Definition);

    public static class RosterHelper
    {
        public static readonly IReadOnlyDictionary<string, string> KeywordGlossary =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ASSAULT"]            = "Can be used to make attacks even if the bearer Advanced this turn.",
                ["HEAVY"]              = "If the bearer did not move this turn, add 1 to each Hit roll made with this weapon.",
                ["RAPID FIRE"]         = "If the bearer did not move this turn, make [X] additional attacks with this weapon.",
                ["PISTOL"]             = "Can be used in Engagement Range; must target the closest eligible enemy unit.",
                ["TORRENT"]            = "Each attack made with this weapon automatically hits — no Hit roll required.",
                ["BLAST"]              = "Add 1 attack for every 5 models (rounding down) in the target unit.",
                ["LETHAL HITS"]        = "Each Critical Hit automatically wounds the target — no Wound roll required.",
                ["SUSTAINED HITS"]     = "Each Critical Hit scores [X] additional hits.",
                ["DEVASTATING WOUNDS"] = "Each Critical Wound causes mortal wounds equal to the weapon's Damage; the attack sequence then ends.",
                ["MELTA"]              = "Add [X] to the Damage characteristic when the target is within half the weapon's Range.",
                ["INDIRECT FIRE"]      = "Can target units not visible to the bearer. Attacks against non-visible units have −1 to Hit.",
                ["LANCE"]              = "If the bearer charged this turn, improve Armour Penetration by 1.",
                ["ANTI"]               = "Wound rolls of [X]+ automatically succeed against the specified keyword target.",
                ["HAZARDOUS"]          = "After shooting, roll 1D6 per Hazardous weapon fired: on a 1, the bearer's unit suffers 3 mortal wounds.",
                ["ONE SHOT"]           = "This weapon can only be used once per battle.",
                ["PRECISION"]          = "On a Critical Hit, the attack can be allocated to a CHARACTER model in the target unit.",
                ["PSYCHIC"]            = "Bearer must be a PSYKER. On an unmodified Hit roll of 6, the attack inflicts 1 additional mortal wound.",
                ["TWIN-LINKED"]        = "You may re-roll the Wound roll for each attack made with this weapon.",
                ["IGNORES COVER"]      = "Attacks made with this weapon ignore the benefits of cover.",
                ["EXTRA ATTACKS"]      = "Each time the bearer fights, make this many additional attacks with this weapon.",
            };

        public static List<KeywordToken> ParseWeaponKeywords(string raw)
        {
            var result = new List<KeywordToken>();
            if (string.IsNullOrEmpty(raw) || raw == "-") return result;

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var keyword = part.Trim();
                if (string.IsNullOrEmpty(keyword)) continue;

                var baseKey = Regex.Replace(keyword, @"\s+\d+[+]?\s*$", "").Trim();
                if (baseKey.StartsWith("Anti", StringComparison.OrdinalIgnoreCase))
                    baseKey = "ANTI";

                KeywordGlossary.TryGetValue(baseKey, out var def);
                result.Add(new KeywordToken(keyword, def));
            }
            return result;
        }

        public static List<Profile> GetAllProfilesRecursive(Selection? current, string typeName)
        {
            var found = new List<Profile>();
            if (current == null) return found;
            if (current.Profiles != null)
                found.AddRange(current.Profiles.Where(p => p.TypeName == typeName));
            if (current.Selections != null)
                foreach (var child in current.Selections)
                    found.AddRange(GetAllProfilesRecursive(child, typeName));
            return found;
        }

        public static string GetChar(Profile? p, string statName) =>
            p?.Characteristics?.FirstOrDefault(c => c.Name == statName)?.Value ?? "-";

        public static List<Rule> CollectRules(Selection sel)
        {
            var list = new List<Rule>();
            if (sel.Rules != null) list.AddRange(sel.Rules);
            if (sel.Selections != null)
                foreach (var child in sel.Selections)
                    list.AddRange(CollectRules(child));
            return list;
        }

        public static List<Rule> GetAbilities(Selection unit, bool filterWeaponKeywords = false)
        {
            var list = new List<Rule>();
            var abilityProfiles = GetAllProfilesRecursive(unit, "Abilities");
            foreach (var p in abilityProfiles)
            {
                var desc = p.Characteristics?.FirstOrDefault(c => c.Name == "Description")?.Value ?? "";
                list.Add(new Rule { Name = p.Name, Description = desc });
            }
            list.AddRange(CollectRules(unit));
            if (filterWeaponKeywords)
            {
                return list
                    .Where(r => !KeywordGlossary.ContainsKey(r.Name) &&
                                !r.Name.StartsWith("Anti-", StringComparison.OrdinalIgnoreCase))
                    .DistinctBy(r => r.Name)
                    .OrderBy(r => r.Name)
                    .ToList();
            }
            return list.DistinctBy(r => r.Name).OrderBy(r => r.Name).ToList();
        }

        public static List<string> GetKeywords(Selection unit) =>
            unit.Categories?
                .Where(c => c.Name != "Configuration" && c.Name != "Uncategorized")
                .Select(c => c.Name)
                .ToList() ?? new List<string>();

        public static MarkupString FormatText(string text)
        {
            if (string.IsNullOrEmpty(text)) return new MarkupString("");
            text = text.Replace("^^", "");
            text = Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            text = text.Replace("\n", "<br/>");
            return new MarkupString(text);
        }

        public static string GetCleanWeaponName(string raw) =>
            Regex.Replace(raw, @"\s*\[.*?\]\s*", "").Trim();
    }
}
