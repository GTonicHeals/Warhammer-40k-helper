using System.Text.Json.Serialization;

namespace WarHammerRoster.Models
{
    public class RosterRoot
    {
        [JsonPropertyName("roster")]
        public Roster Roster { get; set; }
    }

    public class Roster
    {
        [JsonPropertyName("forces")]
        public List<Force> Forces { get; set; } = new();

        [JsonPropertyName("costs")]
        public List<Cost> Costs { get; set; } = new();

        [JsonPropertyName("costLimits")]
        public List<Cost> CostLimits { get; set; } = new();
    }

    public class Force
    {
        [JsonPropertyName("selections")]
        public List<Selection> Selections { get; set; } = new();
    }

    public class Selection
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        // Recursive: Units have weapons, which are also "Selections"
        [JsonPropertyName("selections")]
        public List<Selection> Selections { get; set; } = new();

        [JsonPropertyName("profiles")]
        public List<Profile> Profiles { get; set; } = new();

        [JsonPropertyName("costs")]
        public List<Cost> Costs { get; set; } = new();
    }

    public class Profile
    {
        [JsonPropertyName("typeName")]
        public string TypeName { get; set; } // e.g., "Unit", "Abilities", "Ranged Weapons"

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("characteristics")]
        public List<Characteristic> Characteristics { get; set; } = new();
    }

    public class Characteristic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        // The JSON uses "$text" for the value
        [JsonPropertyName("$text")]
        public string Value { get; set; }
    }

    public class Cost
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("value")]
        public double Value { get; set; }
    }
}