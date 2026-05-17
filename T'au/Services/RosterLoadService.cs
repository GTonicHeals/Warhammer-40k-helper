using System.Collections.Concurrent;
using System.Text.Json;
using Warhammer.Models;

namespace Warhammer.Services;

// Singleton — reads each player JSON file once per app lifetime and caches the result.
public class RosterLoadService
{
    private static readonly string[] IgnoredSelectionNames =
        ["Configuration", "Battle Size", "Detachment", "Show/Hide Options"];

    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly string _wwwroot;
    private readonly ILogger<RosterLoadService> _logger;
    private readonly ConcurrentDictionary<string, RosterRoot?> _cache = new();
    private IReadOnlyList<string>? _activePlayerIds;
    private readonly SemaphoreSlim _discoverLock = new(1, 1);

    public RosterLoadService(IWebHostEnvironment env, ILogger<RosterLoadService> logger)
    {
        _wwwroot = env.WebRootPath;
        _logger = logger;
    }

    public async Task<RosterRoot?> LoadRosterAsync(string playerId)
    {
        if (_cache.TryGetValue(playerId, out var cached)) return cached;

        var filePath = Path.Combine(_wwwroot, $"{playerId}.json");
        if (!File.Exists(filePath))
        {
            _cache[playerId] = null;
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var roster = JsonSerializer.Deserialize<RosterRoot>(json, JsonOptions);
            _cache[playerId] = roster;
            return roster;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load roster file {PlayerId}.json", playerId);
            _cache[playerId] = null;
            return null;
        }
    }

    // Returns ordered player IDs (p1, p2, …) for all roster files that exist and contain units.
    public async Task<IReadOnlyList<string>> GetActivePlayerIdsAsync()
    {
        if (_activePlayerIds != null) return _activePlayerIds;

        await _discoverLock.WaitAsync();
        try
        {
            if (_activePlayerIds != null) return _activePlayerIds;

            var ids = new List<string>();
            for (int i = 1; i <= 10; i++)
            {
                var pid = $"p{i}";
                var roster = await LoadRosterAsync(pid);
                if (roster?.Roster?.Forces?.FirstOrDefault() == null) break;
                var units = GetPlayableUnits(roster);
                if (!units.Any()) break;
                ids.Add(pid);
            }
            _activePlayerIds = ids;
            return _activePlayerIds;
        }
        finally
        {
            _discoverLock.Release();
        }
    }

    public static IEnumerable<Selection> GetPlayableUnits(RosterRoot? roster) =>
        roster?.Roster?.Forces?.FirstOrDefault()?.Selections?
            .Where(s => !IgnoredSelectionNames.Contains(s.Name))
        ?? Enumerable.Empty<Selection>();
}
