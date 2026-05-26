using Microsoft.EntityFrameworkCore;
using Warhammer.Data;
using Warhammer.Models.Campaign;
using Warhammer.Services;

namespace Warhammer.Tools;

// Dev-only seeder. Inserts a curated batch of GmNotes against the active campaign.
// Idempotent on Title — if a note with the same Title already exists on the campaign,
// it is skipped, so running `dotnet run -- seed gm-notes` twice is harmless.
//
// Invoked exclusively through SeedRunner / the `seed gm-notes` CLI argument; not used
// anywhere in the running web app.
public sealed class GmNoteSeeder(
    CampaignManagerService           campaigns,
    IDbContextFactory<AppDbContext>  dbFactory,
    ILogger<GmNoteSeeder>            logger)
{
    public async Task<(int Added, int Skipped, int CampaignId)> SeedAsync(CancellationToken ct = default)
    {
        // Reuses the existing service's "create default campaign on empty DB" path so
        // we don't duplicate the default-seed logic here.
        var campaign = await campaigns.LoadCampaignAsync();

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        var existing = (await ctx.GmNotes
                .Where(n => n.CampaignId == campaign.Id)
                .Select(n => n.Title)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        int added = 0, skipped = 0;
        foreach (var note in DefaultNotes())
        {
            if (existing.Contains(note.Title)) { skipped++; continue; }
            note.CampaignId = campaign.Id;
            ctx.GmNotes.Add(note);
            added++;
        }

        if (added > 0)
            await ctx.SaveChangesAsync(ct);

        logger.LogInformation(
            "GmNoteSeeder finished: added {Added}, skipped {Skipped} for Campaign Id={CampaignId}",
            added, skipped, campaign.Id);

        return (added, skipped, campaign.Id);
    }

    // 10 grim-dark Warhammer 40k flavored GM notes covering both GM-only narrative
    // entries (unpinned, private) and public pinned map notes. Edit freely.
    private static IEnumerable<GmNote> DefaultNotes() =>
    [
        // -- GM-only narrative entries --
        //new()
        //{
        //    Title    = "The Vox Bleed",
        //    Content  = "Astropathic chatter from the northern sub-sector cuts out mid-transmission three nights in a row. Survivors of the relay station are found blind and mute. The player who first investigates loses 1 Crusade Point. If no one investigates by Turn 5, the player controlling the most adjacent nodes must concede one without a battle.",
        //    Tags     = "secret, astropathic, plot-hook",
        //    IsPublic = false
        //}
    ];
}
