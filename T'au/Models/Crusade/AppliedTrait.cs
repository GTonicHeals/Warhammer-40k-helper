using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warhammer.Models.Crusade;

// A trait that's been applied to a unit (honour or scar). Stores the structured
// data so the UI can render the name only, with effect available on hover/click.
public class AppliedTrait
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "";
    [JsonPropertyName("category")] public string Category { get; set; } = "";
    [JsonPropertyName("effect")] public string Effect { get; set; } = "";

    // Carried over from the source CrusadeTrait so the calculator can apply
    // mechanical mods (S/AP/T/Sv/keyword) without re-resolving the trait by name.
    [JsonPropertyName("modifier")] public MachineModifier? Modifier { get; set; }

    // Weapon-targeting (see CrusadeTrait.WeaponScope for the vocabulary).
    // WeaponScope is copied from the source CrusadeTrait at apply time.
    // WeaponName is set only when the user picks a specific weapon (scope = "weapon*").
    // null + null  → mod applies to every weapon (legacy behavior for old applied traits)
    [JsonPropertyName("weaponScope")] public string? WeaponScope { get; set; }
    [JsonPropertyName("weaponName")] public string? WeaponName { get; set; }

    public static AppliedTrait FromTrait(CrusadeTrait t, string? weaponName = null) => new()
    {
        Name = t.Name,
        Type = t.Type,
        Category = t.Category,
        Effect = t.Effect,
        Modifier = t.Modifier,
        WeaponScope = t.WeaponScope,
        WeaponName = weaponName
    };

    // Parses legacy stored strings: "Name [Category] — Effect" / "Name — Effect" / "Name"
    public static AppliedTrait ParseLegacy(string s)
    {
        var raw = (s ?? "").Trim();
        if (raw.Length == 0) return new AppliedTrait();

        string namePart = raw;
        string effect = "";

        var emDash = raw.IndexOf(" — ", StringComparison.Ordinal);
        if (emDash >= 0)
        {
            namePart = raw[..emDash].Trim();
            effect = raw[(emDash + 3)..].Trim();
        }

        string category = "";
        if (namePart.EndsWith("]", StringComparison.Ordinal))
        {
            var bracket = namePart.LastIndexOf(" [", StringComparison.Ordinal);
            if (bracket >= 0)
            {
                category = namePart.Substring(bracket + 2, namePart.Length - bracket - 3);
                namePart = namePart[..bracket].Trim();
            }
        }

        return new AppliedTrait { Name = namePart, Category = category, Effect = effect };
    }
}

// Reads either the new object format or the legacy flat-string format,
// so existing localStorage data keeps loading after the schema change.
public class AppliedTraitListConverter : JsonConverter<List<AppliedTrait>>
{
    public override List<AppliedTrait> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<AppliedTrait>();
        if (reader.TokenType == JsonTokenType.Null) return list;
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for AppliedTrait list");

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                list.Add(AppliedTrait.ParseLegacy(reader.GetString() ?? ""));
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                var t = JsonSerializer.Deserialize<AppliedTrait>(ref reader, options);
                if (t != null) list.Add(t);
            }
            else
            {
                reader.Skip();
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<AppliedTrait> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var t in value)
        {
            JsonSerializer.Serialize(writer, t, options);
        }
        writer.WriteEndArray();
    }
}
