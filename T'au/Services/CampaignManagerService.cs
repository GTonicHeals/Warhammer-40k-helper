using System.Text.Json;
using Microsoft.JSInterop;
using Warhammer.Models.Campaign;

namespace Warhammer.Services;

public class CampaignManagerService(IJSRuntime js, ILogger<CampaignManagerService> logger)
{
    private const string StorageKey = "campaign_1v1";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CampaignState? Campaign { get; private set; }

    // Must be called from OnAfterRenderAsync(firstRender: true) — JS interop
    // is unavailable during Blazor Server prerender.
    public async Task<CampaignState> LoadCampaignAsync()
    {
        // Return the in-memory state if it was already loaded successfully.
        // This is the authoritative source after a save (e.g. navigating back
        // from MapGenerator) so we don't have to rely on a localStorage round-trip.
        if (Campaign is not null) return Campaign;

        try
        {
            var json = await js.InvokeAsync<string?>("campaignStorage.load", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var state = JsonSerializer.Deserialize<CampaignState>(json, JsonOptions);
                if (state is not null)
                {
                    Campaign = state;
                    return state;
                }
            }
            // Empty storage — first use. Cache the default so subsequent loads are fast.
            Campaign = CreateDefault();
            return Campaign;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load campaign from localStorage — returning default");
            // Don't cache — keep Campaign null so the next call retries from localStorage
            // once the JS interop is actually available (i.e. after prerender).
            return CreateDefault();
        }
    }

    public async Task SaveCampaignAsync(CampaignState state)
    {
        Campaign = state;
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await js.InvokeVoidAsync("campaignStorage.save", StorageKey, json);
    }

    private static CampaignState CreateDefault() => new()
    {
        Players = new Dictionary<string, CampaignPlayer>
        {
            ["p1"] = new CampaignPlayer { Id = "p1", Name = "Player 1" },
            ["p2"] = new CampaignPlayer { Id = "p2", Name = "Player 2" }
        }
    };
}
