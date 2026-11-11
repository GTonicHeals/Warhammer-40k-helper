using System.Text.Json;
using Microsoft.JSInterop;
using Warhammer.Helpers;
using Warhammer.Models.Crusade;

namespace Warhammer.Services;

// One-time move of Orders of Battle from browser localStorage into SQLite (ADR 0001).
// Each imported (or already-present) key is removed from localStorage, so the next
// visit finds nothing to do. Keys with unreadable JSON stay put for manual recovery.
// Must run from OnAfterRenderAsync: JS interop is unavailable during prerender.
public class CrusadeLocalStorageImporter(
    IJSRuntime                            js,
    CrusadeManagerService                 crusades,
    RosterLoadService                     rosters,
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
                await ApplyKeywordFlagsAsync(order);
                try
                {
                    await crusades.SaveCrusadeAsync(order);
                }
                catch (Exception ex)
                {
                    // Runs from the nav menu on every page: a bad record must not take the circuit down.
                    logger.LogError(ex, "Could not import crusade key {Key}; left in localStorage", key);
                    continue;
                }
                imported++;
                logger.LogInformation("Imported Order of Battle {Id} from localStorage", order.Id);
            }

            await js.InvokeVoidAsync("crusadeStorage.remove", key);
        }

        return imported;
    }

    // Legacy JSON predates the TITANIC / XP-eligibility flags, so read them from the player's roster.
    private async Task ApplyKeywordFlagsAsync(OrderOfBattle order)
    {
        if (string.IsNullOrEmpty(order.PlayerId)) return;
        var roster = await rosters.LoadRosterAsync(order.PlayerId);
        var byName = RosterLoadService.GetPlayableUnits(roster)
            .DistinctBy(u => u.Name)
            .ToDictionary(u => u.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var card in order.Units)
        {
            if (!byName.TryGetValue(card.DatasheetName ?? "", out var sel)) continue;
            var flags = CrusadeUnitFlags.FromKeywords(RosterHelper.GetKeywords(sel));
            card.IsTitanic  = flags.IsTitanic;
            card.XpEligible = flags.XpEligible;
        }
    }
}
