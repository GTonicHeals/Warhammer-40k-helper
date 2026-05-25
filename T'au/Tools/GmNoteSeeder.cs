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
        new()
        {
            Title    = "The Vox Bleed",
            Content  = "Astropathic chatter from the northern sub-sector cuts out mid-transmission three nights in a row. Survivors of the relay station are found blind and mute. The player who first investigates loses 1 Crusade Point. If no one investigates by Turn 5, the player controlling the most adjacent nodes must concede one without a battle.",
            Tags     = "secret, astropathic, plot-hook",
            IsPublic = false
        },
        new()
        {
            Title    = "The Forge Awakens",
            Content  = "An unmapped Mechanicus reclamation cell has surfaced in the Salt Wastes. They will trade STC fragments — but only with the player holding the most battle wins over the last three turns. Resolves Turn 7; if tied, the offer is withdrawn and one random territory becomes Mechanicus-locked (no captures) for two turns.",
            Tags     = "mechanicus, faction-hook, reward",
            IsPublic = false
        },
        new()
        {
            Title    = "Warp Bloom",
            Content  = "Each turn, roll 1d6 against node count to pick a bloom site. Combat at the bloomed node grants the winner +2 Crusade Points but inflicts one Permanent Battle Scar on a unit of GM's choice. Don't tell the players which node is blooming until the battle is declared.",
            Tags     = "warp, hazard, escalation",
            IsPublic = false
        },
        new()
        {
            Title    = "The Lost Captain",
            Content  = "A named character (player's choice from their roster) was presumed dead three turns ago. They return this turn carrying a relic weapon, but will only fight for the player who first sacrifices a controlled territory. Once-only — first to pay claims them.",
            Tags     = "character, sacrifice, relic",
            IsPublic = false
        },
        new()
        {
            Title    = "Inquisitorial Favor",
            Content  = "When any player wins three consecutive battles without a loss, the Inquisition takes notice. Award that player one free Stratagem reroll per battle for the remainder of the campaign. Do not announce the threshold — let them discover it.",
            Tags     = "inquisition, reward, milestone",
            IsPublic = false
        },
        new()
        {
            Title    = "WAAAGH! Surge",
            Content  = "From Turn 5 onward, any battle ending in a draw spawns an Ork incursion at the contested node. Both players must agree to a joint defense within one turn, or the territory becomes neutral and unclaimable for the rest of the campaign.",
            Tags     = "orks, escalation, mid-campaign",
            IsPublic = false
        },

        // -- Public pinned map notes --
        new()
        {
            Title    = "Hab Block Theta-9",
            Content  = "Overcrowded refugee shelter — six million souls packed into ten square kilometers. Holder gains +1 Requisition per turn but loses 1 VP each time control changes hands beyond the first.",
            Tags     = "territory, requisition, hab",
            X        = 0.32,
            Y        = 0.58,
            IsPublic = true
        },
        new()
        {
            Title    = "The Caustic Mire",
            Content  = "Toxic runoff from a fallen Imperial forge. Any battle fought at this node costs the attacker 1 additional Reinforcement Point. The penalty does not stack across turns.",
            Tags     = "territory, hazard, terrain",
            X        = 0.65,
            Y        = 0.41,
            IsPublic = true
        },
        new()
        {
            Title    = "Astropath Relay 47-Theta",
            Content  = "Controlling player receives foreknowledge — they see the next GM-revealed event 12 hours before other players are told. The buff vanishes the instant control changes hands.",
            Tags     = "territory, intel, comms",
            X        = 0.18,
            Y        = 0.22,
            IsPublic = true
        },
        new()
        {
            Title    = "The Spire of Echoes",
            Content  = "An Aeldari ruin half-swallowed by the wastes. Once per campaign, the controlling player may declare a \"Whisper\" before any battle — re-roll one charge distance. Using the Whisper destroys the spire; the node remains but loses its effect.",
            Tags     = "territory, aeldari, one-shot",
            X        = 0.78,
            Y        = 0.74,
            IsPublic = true
        },
    ];
}
