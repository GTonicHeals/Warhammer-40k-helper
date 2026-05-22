using System.Text.Json;
using Microsoft.JSInterop;
using Warhammer.Models.Crusade;

namespace Warhammer.Services;

public class CrusadeManagerService
{
    private const string LocalStoragePrefix = "crusade_";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _js;
    private readonly ILogger<CrusadeManagerService> _logger;
    private readonly Dictionary<string, OrderOfBattle> _cache = new();
    private bool _loaded;

    public event Action? OnCrusadesChanged;

    public CrusadeManagerService(IJSRuntime js, ILogger<CrusadeManagerService> logger)
    {
        _js = js;
        _logger = logger;
    }

    // Must be called from OnAfterRenderAsync(firstRender: true), not OnInitializedAsync —
    // JS interop is unavailable during Blazor Server prerender.
    public async Task<IReadOnlyCollection<OrderOfBattle>> LoadAllCrusadesAsync()
    {
        if (_loaded) return _cache.Values;

        try
        {
            var entries = await _js.InvokeAsync<Dictionary<string, string>>("crusadeStorage.getAll");
            foreach (var (key, json) in entries)
            {
                try
                {
                    var crusade = JsonSerializer.Deserialize<OrderOfBattle>(json, JsonOptions);
                    if (crusade != null) _cache[crusade.Id] = crusade;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Bad crusade JSON for key {Key}; ignoring", key);
                }
            }
            _loaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crusades from localStorage");
        }

        return _cache.Values;
    }

    public IReadOnlyCollection<OrderOfBattle> Crusades => _cache.Values;

    public OrderOfBattle? GetCrusade(string id) => _cache.GetValueOrDefault(id);

    public async Task SaveCrusadeAsync(OrderOfBattle crusade)
    {
        ArgumentNullException.ThrowIfNull(crusade);

        crusade.UpdatedUtc = DateTime.UtcNow;
        _cache[crusade.Id] = crusade;

        var json = JsonSerializer.Serialize(crusade, JsonOptions);
        await _js.InvokeVoidAsync("crusadeStorage.save", LocalStoragePrefix + crusade.Id, json);

        OnCrusadesChanged?.Invoke();
    }

    public async Task DeleteCrusadeAsync(string id)
    {
        if (!_cache.Remove(id)) return;

        await _js.InvokeVoidAsync("crusadeStorage.remove", LocalStoragePrefix + id);
        OnCrusadesChanged?.Invoke();
    }
}
