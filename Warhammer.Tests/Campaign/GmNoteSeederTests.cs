using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Campaign;
using Warhammer.Services;
using Warhammer.Tests.TestInfrastructure;
using Warhammer.Tools;

namespace Warhammer.Tests.Campaign;

// Behavioural tests for the dev-only GmNoteSeeder. Each [Fact] runs against a fresh
// in-memory SQLite database (per-test isolation via SqliteTestBase).
public class GmNoteSeederTests : SqliteTestBase
{
    private GmNoteSeeder CreateSeeder(out CampaignManagerService campaignSvc)
    {
        var factory = CreateFactory();
        campaignSvc  = new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance);
        return new GmNoteSeeder(campaignSvc, factory, NullLogger<GmNoteSeeder>.Instance);
    }

    private GmNoteSeeder CreateSeeder() => CreateSeeder(out _);

    [Fact]
    public async Task SeedAsync_on_empty_db_seeds_default_campaign_then_inserts_all_ten_notes()
    {
        await using (var ctx = CreateContext())
        {
            (await ctx.Campaigns.AnyAsync()).Should().BeFalse();
            (await ctx.GmNotes  .AnyAsync()).Should().BeFalse();
        }

        var (added, skipped, campaignId) = await CreateSeeder().SeedAsync();

        added     .Should().Be(10);
        skipped   .Should().Be(0);
        campaignId.Should().BePositive("LoadCampaignAsync should have seeded the default campaign");

        await using var verify = CreateContext();
        (await verify.GmNotes.CountAsync(n => n.CampaignId == campaignId)).Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_second_run_is_idempotent_on_title()
    {
        await CreateSeeder().SeedAsync();   // first run: 10 inserted

        var (added, skipped, _) = await CreateSeeder().SeedAsync();

        added  .Should().Be(0,  "the seeder must dedup by Title and never insert duplicates");
        skipped.Should().Be(10);

        await using var ctx = CreateContext();
        (await ctx.GmNotes.CountAsync()).Should().Be(10, "no extra rows should have been written");
    }

    [Fact]
    public async Task SeedAsync_skips_only_titles_that_already_exist()
    {
        // Stand up the campaign first, then pre-insert one of the seeder's canonical
        // titles. The seeder should detect that title and skip exactly one entry.
        var seeder = CreateSeeder(out var campaignSvc);
        var campaign = await campaignSvc.LoadCampaignAsync();

        await using (var ctx = CreateContext())
        {
            ctx.GmNotes.Add(new GmNote
            {
                CampaignId = campaign.Id,
                Title      = "The Vox Bleed",
                Content    = "preexisting content",
                Tags       = ""
            });
            await ctx.SaveChangesAsync();
        }

        var (added, skipped, _) = await seeder.SeedAsync();

        added  .Should().Be(9, "9 of the 10 canonical titles are new");
        skipped.Should().Be(1, "only 'The Vox Bleed' should be skipped");

        await using var verify = CreateContext();
        (await verify.GmNotes.CountAsync()).Should().Be(10, "1 preexisting + 9 newly inserted");
    }

    [Fact]
    public async Task SeedAsync_persists_public_pinned_notes_with_coords_and_isPublic_flag()
    {
        await CreateSeeder().SeedAsync();

        await using var ctx = CreateContext();
        var pinned = await ctx.GmNotes
            .Where(n => n.IsPublic && n.X >= 0 && n.Y >= 0)
            .OrderBy(n => n.Title)
            .ToListAsync();

        pinned.Should().HaveCount(4, "the canonical seed contains four public pinned map notes");
        pinned.Select(n => n.Title).Should().BeEquivalentTo(new[]
        {
            "Astropath Relay 47-Theta",
            "Hab Block Theta-9",
            "The Caustic Mire",
            "The Spire of Echoes"
        });
        pinned.Should().AllSatisfy(n =>
        {
            n.X.Should().BeInRange(0, 1, "pinned coordinates are normalised [0,1]");
            n.Y.Should().BeInRange(0, 1, "pinned coordinates are normalised [0,1]");
        });
    }

    [Fact]
    public async Task SeedAsync_attaches_every_note_to_the_returned_campaign_id()
    {
        var (added, _, campaignId) = await CreateSeeder().SeedAsync();

        added.Should().Be(10);

        await using var ctx = CreateContext();
        var notes = await ctx.GmNotes.ToListAsync();
        notes.Should().HaveCount(10);
        notes.Should().AllSatisfy(n =>
            n.CampaignId.Should().Be(campaignId,
                "every seeded note must FK to the campaign the seeder reported"));
    }
}
