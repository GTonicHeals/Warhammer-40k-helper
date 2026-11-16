using Microsoft.EntityFrameworkCore;
using Warhammer.Data;
using Warhammer.Models.Crusade;

namespace Warhammer.Services;

// Orders of Battle and their Crusade Cards, stored in the SQLite campaign database
// (ADR 0001). Pages hold the detached graphs from the snapshot, mutate them, and hand
// them back to SaveCrusadeAsync, which reconciles the cards by Id.
public class CrusadeManagerService(
    IDbContextFactory<AppDbContext>  dbFactory,
    CampaignManagerService           campaigns,
    ILogger<CrusadeManagerService>   logger)
{
    private readonly Dictionary<string, OrderOfBattle> _snapshot = new();

    public event Action? OnCrusadesChanged;

    // Snapshot of the last load/save.
    public IReadOnlyCollection<OrderOfBattle> Crusades => _snapshot.Values;

    public OrderOfBattle? GetCrusade(string id) => _snapshot.GetValueOrDefault(id);

    public async Task<IReadOnlyCollection<OrderOfBattle>> LoadAllCrusadesAsync()
    {
        var campaignId = await campaigns.GetCurrentCampaignIdAsync();
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var orders = await ctx.OrdersOfBattle
            .Where(o => o.CampaignId == campaignId)
            .Include(o => o.Units)
            .AsNoTracking()
            .OrderBy(o => o.CreatedUtc)
            .ToListAsync();

        _snapshot.Clear();
        foreach (var o in orders) _snapshot[o.Id] = o;
        return _snapshot.Values;
    }

    // True if an Order of Battle with this Id is stored in any campaign, not just the current one.
    public async Task<bool> ExistsAsync(string id)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        return await ctx.OrdersOfBattle.AnyAsync(o => o.Id == id);
    }

    public async Task SaveCrusadeAsync(OrderOfBattle crusade)
    {
        ArgumentNullException.ThrowIfNull(crusade);

        if (crusade.CampaignId == 0)
            crusade.CampaignId = await campaigns.GetCurrentCampaignIdAsync();
        crusade.UpdatedUtc = DateTime.UtcNow;

        await using var ctx = await dbFactory.CreateDbContextAsync();
        var existing = await ctx.OrdersOfBattle
            .Include(o => o.Units)
            .FirstOrDefaultAsync(o => o.Id == crusade.Id);

        if (existing is null)
        {
            ctx.OrdersOfBattle.Add(crusade);
        }
        else
        {
            ctx.Entry(existing).CurrentValues.SetValues(crusade);
            ReconcileUnits(ctx, existing.Units, crusade.Units);
        }

        await ctx.SaveChangesAsync();
        logger.LogDebug("Saved Order of Battle {Id}", crusade.Id);

        _snapshot[crusade.Id] = crusade;
        OnCrusadesChanged?.Invoke();
    }

    public async Task DeleteCrusadeAsync(string id)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var existing = await ctx.OrdersOfBattle.FirstOrDefaultAsync(o => o.Id == id);
        if (existing is not null)
        {
            ctx.Remove(existing);   // cards cascade
            await ctx.SaveChangesAsync();
        }

        if (_snapshot.Remove(id) || existing is not null)
            OnCrusadesChanged?.Invoke();
    }

    // Same diff as CampaignManagerService.Reconcile: delete missing, update present, insert new.
    private static void ReconcileUnits(AppDbContext ctx, List<CrusadeUnitCard> tracked, List<CrusadeUnitCard> incoming)
    {
        var trackedById  = tracked.ToDictionary(u => u.Id);
        var incomingById = incoming.ToDictionary(u => u.Id);

        foreach (var (id, card) in trackedById)
            if (!incomingById.ContainsKey(id))
                ctx.Remove(card);

        foreach (var (id, card) in incomingById)
        {
            if (trackedById.TryGetValue(id, out var t))
            {
                card.OrderOfBattleId = t.OrderOfBattleId;
                ctx.Entry(t).CurrentValues.SetValues(card);
            }
            else
            {
                tracked.Add(card);
            }
        }
    }
}
