using System.Text.Json.Serialization;

namespace Warhammer.Models
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

        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new();
    }

    public class Selection
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("selections")]
        public List<Selection> Selections { get; set; } = new();

        [JsonPropertyName("profiles")]
        public List<Profile> Profiles { get; set; } = new();

        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; } = new(); 

        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; } = new(); 

        [JsonPropertyName("costs")]
        public List<Cost> Costs { get; set; } = new();
    }

    public class Profile
    {
        [JsonPropertyName("typeName")]
        public string TypeName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("characteristics")]
        public List<Characteristic> Characteristics { get; set; } = new();
    }

    public class Characteristic
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

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

     public class Rule
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }

    public class Category
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }
}