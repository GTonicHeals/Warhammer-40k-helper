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

        // A campaign gets the default Requisition list exactly once; after that the list is the Game Master's.
        // The write only lands while the row is still unseeded, so when two first loads race (two tabs,
        // two processes) one seeds and the other reads back what it wrote.
        if (!campaign.RequisitionsSeeded)
        {
            await using var seedCtx = await dbFactory.CreateDbContextAsync();
            var defaults = RequisitionDefaults.Create();
            var id = campaign.Id;
            var seeded = await seedCtx.Campaigns
                .Where(c => c.Id == id && !c.RequisitionsSeeded)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Requisitions, defaults)
                    .SetProperty(c => c.RequisitionsSeeded, true));

            campaign.Requisitions = seeded == 1
                ? defaults
                : await seedCtx.Campaigns.Where(c => c.Id == id).Select(c => c.Requisitions).SingleAsync();
            campaign.RequisitionsSeeded = true;
            if (seeded == 1) logger.LogInformation("Seeded the default Requisition list for campaign {Id}", campaign.Id);
        }

        Campaign = campaign;
        return campaign;
    }

    // saveRequisitions: only the Campaign Editor owns the Requisition list. Every other page holds a
    // copy of the campaign that may be older than the editor's last save, so by default the stored
    // list is kept and the caller's copy of it is ignored.
    public async Task SaveCampaignAsync(Campaign campaign, bool saveRequisitions = false)
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
        var storedRequisitions = existing.Requisitions;
        var storedSeeded       = existing.RequisitionsSeeded;
        ctx.Entry(existing).CurrentValues.SetValues(campaign);
        if (!saveRequisitions)
        {
            existing.Requisitions       = storedRequisitions;
            existing.RequisitionsSeeded = storedSeeded;
        }

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
