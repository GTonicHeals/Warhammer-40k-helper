using Microsoft.EntityFrameworkCore;
using Warhammer.Data;
using Warhammer.Models.Campaign;

namespace Warhammer.Services;

public class CampaignManagerService(
    IDbContextFactory<AppDbContext>  dbFactory,
    ILogger<CampaignManagerService>  logger)
{
    // Convenience accessor exposing the last-loaded/saved instance.
    // Not a source of truth — callers should use the return value of LoadCampaignAsync.
    public Campaign? Campaign { get; private set; }

    public async Task<Campaign> LoadCampaignAsync()
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();

        var campaign = await ctx.Campaigns
            .Include(c => c.Players)
            .Include(c => c.MapNodes)
            .Include(c => c.BattleHistory)
            .Include(c => c.GmNotebook)        // GmNotebook covers both notebook entries and map pins
            .AsSplitQuery()                    // avoid Cartesian explosion across 4 collections
            .AsNoTracking()                    // return a detached graph; SaveCampaignAsync re-attaches
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        if (campaign is null)
        {
            campaign = CreateDefault();
            ctx.Campaigns.Add(campaign);
            await ctx.SaveChangesAsync();
            logger.LogInformation("Seeded default campaign with Id={Id}", campaign.Id);
        }

        Campaign = campaign;
        return campaign;
    }

    public async Task SaveCampaignAsync(Campaign campaign)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();

        // INSERT path: brand-new campaign that has never been persisted.
        if (campaign.Id == 0)
        {
            ctx.Campaigns.Add(campaign);
            await ctx.SaveChangesAsync();
            Campaign = campaign;
            return;
        }

        // UPDATE path: load the tracked graph, then reconcile each child collection
        // by primary key. We can't use ctx.Update(campaign) because every child carries
        // a client-generated GUID — EF cannot distinguish "new" from "existing" on its
        // own and would mis-classify inserts as updates (zero-row UPDATEs).
        var existing = await ctx.Campaigns
            .Include(c => c.Players)
            .Include(c => c.MapNodes)
            .Include(c => c.BattleHistory)
            .Include(c => c.GmNotebook)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == campaign.Id);

        if (existing is null)
        {
            // The Id the caller is holding has disappeared from the DB (e.g. it was
            // reset between Load and Save). Treat as an insert so the user's work survives.
            ctx.Entry(campaign).State = EntityState.Added;
            await ctx.SaveChangesAsync();
            Campaign = campaign;
            logger.LogWarning("Campaign {Id} not found on save — inserted as new", campaign.Id);
            return;
        }

        // Copy scalar values (CampaignName, MapSeed, MapBiome, MapScale, ...) from the
        // incoming detached entity onto the tracked one. Navigation collections are
        // handled below — SetValues only touches mapped scalars and primitive collections.
        ctx.Entry(existing).CurrentValues.SetValues(campaign);

        Reconcile(ctx, existing.Players,       campaign.Players,       p => p.Id);
        Reconcile(ctx, existing.MapNodes,      campaign.MapNodes,      n => n.Id);
        Reconcile(ctx, existing.BattleHistory, campaign.BattleHistory, b => b.Id);
        Reconcile(ctx, existing.GmNotebook,    campaign.GmNotebook,    g => g.Id);

        await ctx.SaveChangesAsync();
        Campaign = campaign;
    }

    // Removes one Battle from the log. Deletes the row directly rather than saving the whole
    // graph, so a caller holding a stale Campaign can't overwrite unrelated changes. Results a
    // Completed Battle already applied (XP, honours, RP, win/loss, territory) are left as-is.
    public async Task DeleteBattleAsync(string battleId)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();

        var deleted = await ctx.BattleRecords.Where(b => b.Id == battleId).ExecuteDeleteAsync();
        Campaign?.BattleHistory.RemoveAll(b => b.Id == battleId);

        if (deleted > 0) logger.LogInformation("Deleted Battle {Id}", battleId);
    }

    // Diffs the tracked collection against the incoming one by primary key:
    //   - present in DB, absent from incoming  -> Remove (DELETE)
    //   - present in both                      -> SetValues (UPDATE)
    //   - absent from DB, present in incoming  -> add to tracked collection (INSERT)
    // EF infers the FK on inserts from the parent's collection navigation.
    private static void Reconcile<T>(
        AppDbContext      ctx,
        List<T>           tracked,
        List<T>           incoming,
        Func<T, string>   keySelector) where T : class
    {
        var trackedByKey  = tracked.ToDictionary(keySelector);
        var incomingByKey = incoming.ToDictionary(keySelector);

        foreach (var (key, entity) in trackedByKey)
            if (!incomingByKey.ContainsKey(key))
                ctx.Remove(entity);

        foreach (var (key, entity) in incomingByKey)
        {
            if (trackedByKey.TryGetValue(key, out var trackedEntity))
                ctx.Entry(trackedEntity).CurrentValues.SetValues(entity);
            else
                tracked.Add(entity);
        }
    }

    private static Campaign CreateDefault() => new()
    {
        Players =
        {
            new CampaignPlayer { Id = "p1", Name = "Player 1" },
            new CampaignPlayer { Id = "p2", Name = "Player 2" }
        }
    };
}
