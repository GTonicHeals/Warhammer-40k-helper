using System.Text.Json;
using Warhammer.Models.Crusade;

namespace Warhammer.Services;

// Singleton — reads wwwroot/data/crusade_traits.json once per app lifetime.
// The JSON groups traits into 4 top-level arrays; this service flattens them
// into a single list with Type assigned from the source array.
public class CrusadeTraitsService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly string _filePath;
    private readonly ILogger<CrusadeTraitsService> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private List<CrusadeTrait>? _traits;

    public CrusadeTraitsService(IWebHostEnvironment env, ILogger<CrusadeTraitsService> logger)
    {
        _filePath = Path.Combine(env.WebRootPath, "data", "crusade_traits.json");
        _logger = logger;
    }

    public IReadOnlyList<CrusadeTrait> All => _traits ?? (IReadOnlyList<CrusadeTrait>)Array.Empty<CrusadeTrait>();

    public async Task EnsureLoadedAsync()
    {
        if (_traits != null) return;

        await _loadLock.WaitAsync();
        try
        {
            if (_traits != null) return;

            if (!File.Exists(_filePath))
            {
                _logger.LogWarning("crusade_traits.json not found at {Path}", _filePath);
                _traits = new List<CrusadeTrait>();
                return;
            }

            var json = await File.ReadAllTextAsync(_filePath);
            var file = JsonSerializer.Deserialize<CrusadeTraitsFile>(json, JsonOptions)
                       ?? new CrusadeTraitsFile();

            var all = new List<CrusadeTrait>(
                file.WeaponModifications.Count + file.BattleTraits.Count +
                file.CrusadeRelics.Count + file.BattleScars.Count);

            Tag(file.WeaponModifications, CrusadeTraitTypes.WeaponModification, all);
            Tag(file.BattleTraits, CrusadeTraitTypes.BattleTrait, all);
            Tag(file.CrusadeRelics, CrusadeTraitTypes.CrusadeRelic, all);
            Tag(file.BattleScars, CrusadeTraitTypes.BattleScar, all);

            _traits = all;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crusade_traits.json");
            _traits = new List<CrusadeTrait>();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private static void Tag(List<CrusadeTrait> source, string type, List<CrusadeTrait> dest)
    {
        foreach (var t in source)
        {
            t.Type = type;
            dest.Add(t);
        }
    }

    public IEnumerable<CrusadeTrait> ByType(string type) =>
        All.Where(t => string.Equals(t.Type, type, StringComparison.OrdinalIgnoreCase));
}
