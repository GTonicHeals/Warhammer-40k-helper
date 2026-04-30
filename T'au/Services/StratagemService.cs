using System.Text.Json;
using Warhammer.Models;

namespace Warhammer.Services;

public class StratagemService(IWebHostEnvironment env)
{
    private readonly IWebHostEnvironment _env = env;
    private readonly List<StratagemModelWithDetachment> _allStratagems = new();
    private readonly Dictionary<string, List<string>> _unitStratagemMap = new();
    private bool _loaded;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await _loadLock.WaitAsync();
        try
        {
            if (_loaded) return;
            await LoadCoreAsync();
            _loaded = true;
        }
        finally { _loadLock.Release(); }
    }

    private async Task LoadCoreAsync()
    {
        var filePath = Path.Combine(_env.WebRootPath, "data", "Stratagems.json");
        if (!File.Exists(filePath)) return;
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var data)) return;

            if (data.TryGetProperty("stratagems", out var stratsArr))
            {
                foreach (var s in stratsArr.EnumerateArray())
                {
                    _allStratagems.Add(new StratagemModelWithDetachment
                    {
                        Name        = Prop(s, "name"),
                        Cost        = Prop(s, "cp_cost"),
                        Type        = Prop(s, "type"),
                        Turn        = Prop(s, "turn"),
                        Phase       = Prop(s, "phase"),
                        Description = Prop(s, "description"),
                        FactionId   = Prop(s, "faction_id"),
                        Detachment  = Prop(s, "detachment")
                    });
                }
            }

            if (data.TryGetProperty("units", out var unitsArr))
            {
                foreach (var u in unitsArr.EnumerateArray())
                {
                    var uName = Prop(u, "name");
                    var sList = Prop(u, "stratagems");
                    if (!string.IsNullOrEmpty(uName) && !string.IsNullOrEmpty(sList))
                        _unitStratagemMap[uName.ToLower()] =
                            sList.Split('|').Select(x => x.Trim()).ToList();
                }
            }
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    public List<StratagemModelWithDetachment> GetStratagemsForUnit(string unitName, string detachmentName)
    {
        if (string.IsNullOrEmpty(unitName)) return new();

        if (!_unitStratagemMap.TryGetValue(unitName.ToLower(), out var names))
        {
            var key = _unitStratagemMap.Keys.FirstOrDefault(k =>
                k.Contains(unitName.ToLower()) || unitName.ToLower().Contains(k));
            names = key != null ? _unitStratagemMap[key] : null;
        }
        if (names == null || names.Count == 0) return new();

        return names
            .Select(n => _allStratagems.FirstOrDefault(s =>
                s.Name.Equals(n, StringComparison.OrdinalIgnoreCase)))
            .Where(s => s != null && !string.IsNullOrEmpty(s.FactionId))
            .Where(s =>
            {
                if (string.IsNullOrEmpty(s!.Detachment) || string.IsNullOrEmpty(detachmentName)) return true;
                return detachmentName.Contains(s.Detachment, StringComparison.OrdinalIgnoreCase) ||
                       s.Detachment.Contains(detachmentName, StringComparison.OrdinalIgnoreCase);
            })
            .Cast<StratagemModelWithDetachment>()
            .ToList();
    }

    public StratagemModelWithDetachment? GetByName(string name) =>
        _allStratagems.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string Prop(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return "";
        return v.ValueKind == JsonValueKind.Number ? v.GetRawText() : v.GetString() ?? "";
    }
}
