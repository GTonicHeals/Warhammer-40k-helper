using System.Text.Json;
using Microsoft.JSInterop;
using Warhammer.Models.Crusade;

namespace Warhammer.Services;

// One-time move of Orders of Battle from browser localStorage into SQLite (ADR 0001).
// Each imported (or already-present) key is removed from localStorage, so the next
// visit finds nothing to do. Keys with unreadable JSON stay put for manual recovery.
// Must run from OnAfterRenderAsync: JS interop is unavailable during prerender.
public class CrusadeLocalStorageImporter(
    IJSRuntime                            js,
    CrusadeManagerService                 crusades,
    ILogger<CrusadeLocalStorageImporter>  logger)
{
    public async Task<int> ImportAsync()
    {
        Dictionary<string, string> entries;
        try
        {
            entries = await js.InvokeAsync<Dictionary<string, string>>("crusadeStorage.getAll");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "localStorage unavailable; Crusade import skipped");
            return 0;
        }
        if (entries.Count == 0) return 0;

        await crusades.LoadAllCrusadesAsync();
        var imported = 0;

        foreach (var (key, json) in entries)
        {
            OrderOfBattle? order;
            try
            {
                order = JsonSerializer.Deserialize<OrderOfBattle>(json, CrusadeJson.Options);
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Bad crusade JSON for key {Key}; left in localStorage", key);
                continue;
            }
            if (order is null) continue;

            if (crusades.GetCrusade(order.Id) is null)
            {
                order.CampaignId = 0;   // legacy data has none; SaveCrusadeAsync assigns the current campaign
                await crusades.SaveCrusadeAsync(order);
                imported++;
                logger.LogInformation("Imported Order of Battle {Id} from localStorage", order.Id);
            }

            await js.InvokeVoidAsync("crusadeStorage.remove", key);
        }

        return imported;
    }
}
