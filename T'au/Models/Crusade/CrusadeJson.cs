using System.Text.Json;

namespace Warhammer.Models.Crusade;

// One serializer configuration for Crusade data: the JSON columns in SQLite and the
// legacy localStorage import both go through it, so flat-string honours keep parsing.
public static class CrusadeJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new AppliedTraitListConverter() }
    };
}
