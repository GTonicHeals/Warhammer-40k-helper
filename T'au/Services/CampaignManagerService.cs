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

    // The current campaign: the one opened most recently, else (a database from before switching
    // existed) the oldest. Seeds a default campaign when there are none.
    public async Task<int> GetCurrentCampaignIdAsync()
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var id = await CurrentIdQuery(ctx).FirstOrDefaultAsync();
        if (id != 0) return id;

        var campaign = CreateDefault();
        ctx.Campaigns.Add(campaign);
        await ctx.SaveChangesAsync();
        logger.LogInformation("Seeded default campaign with Id={Id}", campaign.Id);
        return campaign.Id;
    }

    private static IQueryable<int> CurrentIdQuery(AppDbContext ctx) =>
        ctx.Campaigns
            .OrderByDescending(c => c.LastOpenedUtc.HasValue)
            .ThenByDescending(c => c.LastOpenedUtc)
            .ThenBy(c => c.Id)
            .Select(c => c.Id);

    public async Task<IReadOnlyList<CampaignSummary>> ListCampaignsAsync()
    {
        var currentId = await GetCurrentCampaignIdAsync();
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var rows = await ctx.Campaigns
            .Select(c => new CampaignSummary(c.Id, c.CampaignName, c.Players.Count, c.BattleHistory.Count,
                                             c.LastOpenedUtc, c.Id == currentId))
            .ToListAsync();

        return rows
            .OrderByDescending(r => r.IsCurrent)
            .ThenByDescending(r => r.LastOpenedUtc ?? DateTime.MinValue)
            .ThenBy(r => r.Id)
            .ToList();
    }

    public async Task SwitchToAsync(int campaignId)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var updated = await ctx.Campaigns.Where(c => c.Id == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastOpenedUtc, now));
        if (updated == 0) throw new InvalidOperationException($"Campaign {campaignId} does not exist.");

        Campaign = null;
        logger.LogInformation("Switched to campaign {Id}", campaignId);
    }

    public async Task<Campaign> CreateCampaignAsync(string name)
    {
        var campaign = CreateDefault();
        if (!string.IsNullOrWhiteSpace(name)) campaign.CampaignName = name.Trim();

        await using (var ctx = await dbFactory.CreateDbContextAsync())
        {
            ctx.Campaigns.Add(campaign);
            await ctx.SaveChangesAsync();
        }
        logger.LogInformation("Created campaign {Id} ({Name})", campaign.Id, campaign.CampaignName);

        await SwitchToAsync(campaign.Id);
        return await LoadCampaignAsync();
    }

    public async Task RenameCampaignAsync(int campaignId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        var trimmed = name.Trim();
        await using var ctx = await dbFactory.CreateDbContextAsync();
        await ctx.Campaigns.Where(c => c.Id == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CampaignName, trimmed));
        if (Campaign?.Id == campaignId) Campaign.CampaignName = trimmed;
    }

    // The current campaign can't be deleted, so the game being played is never lost by accident.
    // Its children (players, map, battles, notes, Orders of Battle) go with it via cascade FKs.
    public async Task DeleteCampaignAsync(int campaignId)
    {
        if (campaignId == await GetCurrentCampaignIdAsync())
            throw new InvalidOperationException("The current campaign can't be deleted. Switch to another one first.");

        await using var ctx = await dbFactory.CreateDbContextAsync();
        var existing = await ctx.Campaigns.FindAsync(campaignId);
        if (existing is null) return;
        ctx.Campaigns.Remove(existing);
        await ctx.SaveChangesAsync();
        logger.LogInformation("Deleted campaign {Id}", campaignId);
    }

    public async Task<Campaign> LoadCampaignAsync()
    {
        var currentId = await GetCurrentCampaignIdAsync();
        await using var ctx = await dbFactory.CreateDbContextAsync();

        var campaign = await ctx.Campaigns
            .Include(c => c.Players)
            .Include(c => c.MapNodes)
            .Include(c => c.BattleHistory)
            .Include(c => c.GmNotebook)        // GmNotebook covers both notebook entries and map pins
            .AsSplitQuery()                    // avoid Cartesian explosion across 4 collections
            .AsNoTracking()                    // return a detached graph; SaveCampaignAsync re-attaches
            .SingleAsync(c => c.Id == currentId);

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

        // Defaults seeded before entries carried a DefaultKey get theirs once, so Restore defaults
        // recognises them after a rename. Only the Requisitions column is written.
        if (RequisitionDefaults.AssignMissingKeys(campaign.Requisitions))
        {
            await using var keyCtx = await dbFactory.CreateDbContextAsync();
            var id = campaign.Id;
            var keyed = campaign.Requisitions;
            await keyCtx.Campaigns.Where(c => c.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Requisitions, keyed));
            logger.LogInformation("Gave default keys to the Requisition list of campaign {Id}", campaign.Id);
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
        var storedLastOpened   = existing.LastOpenedUtc;
        ctx.Entry(existing).CurrentValues.SetValues(campaign);
        existing.LastOpenedUtc = storedLastOpened;   // only SwitchToAsync moves the current campaign
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

    // Marks Events revealed. Writes only that flag, so a caller holding a stale Campaign
    // (the War Room) can't roll back captures or Territory reveals saved since it loaded.
    public async Task RevealEventsAsync(IReadOnlyCollection<string> eventIds)
    {
        if (eventIds.Count == 0) return;
        await using var ctx = await dbFactory.CreateDbContextAsync();

        await ctx.Set<CampaignEvent>()
            .Where(e => eventIds.Contains(e.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.IsEventRevealed, true));
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
