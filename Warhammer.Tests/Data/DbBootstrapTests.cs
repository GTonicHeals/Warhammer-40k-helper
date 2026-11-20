using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Warhammer.Data;

namespace Warhammer.Tests.Data;

public class DbBootstrapTests : IDisposable
{
    // Own connection: SqliteTestBase calls EnsureCreated, which would pre-empt the paths under test.
    private readonly SqliteConnection _conn = new("Data Source=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    public DbBootstrapTests()
    {
        _conn.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options;
    }

    private AppDbContext Ctx() => new(_options);

    [Fact]
    public async Task Empty_database_applies_every_migration()
    {
        await using var ctx = Ctx();
        await DbBootstrap.MigrateAsync(ctx);
        (await ctx.Database.GetAppliedMigrationsAsync()).Should().BeEquivalentTo(ctx.Database.GetMigrations());
    }

    [Fact]
    public async Task Schema_without_history_table_is_baselined_then_migrated()
    {
        await using (var ctx = Ctx())
        {
            // Simulate a database built by EnsureCreated on the pre-Crusade model.
            var baseline = ctx.Database.GetMigrations().First();
            await ctx.GetService<IMigrator>().MigrateAsync(baseline);
            await ctx.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
            // Raw SQL: the row must fit the Baseline schema, not today's model.
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Campaigns (CampaignName, MapSeed, MapBiome, MapOctaves, MapLacunarity, MapPersistence, MapScale, MapOffsetX, MapOffsetY) " +
                "VALUES ('Live', 1, 'agri', 4, 2.0, 0.5, 2.0, 0.0, 0.0)");
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            (await ctx.Database.GetAppliedMigrationsAsync()).Should().BeEquivalentTo(ctx.Database.GetMigrations());
            (await ctx.Campaigns.CountAsync()).Should().Be(1); // table survived, not recreated
            // Columns added since the Baseline read cleanly on the old row.
            var live = await ctx.Campaigns.SingleAsync();
            live.Requisitions.Should().BeEmpty();
            live.RequisitionsSeeded.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Territories_that_existed_before_hiding_stay_revealed()
    {
        await using (var ctx = Ctx())
        {
            await ctx.GetService<IMigrator>().MigrateAsync("20261116035356_AddBattleRequisitionAwards");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Campaigns (CampaignName, MapSeed, MapBiome, MapOctaves, MapLacunarity, MapPersistence, MapScale, MapOffsetX, MapOffsetY) " +
                "VALUES ('Live', 1, 'agri', 4, 2.0, 0.5, 2.0, 0.0, 0.0)");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Nodes (Id, CampaignId, Name, Description, IsPublic, ConnectedNodeIds, X, Y, NodeType, IsEventRevealed) VALUES " +
                "('t1', 1, 'Hab', '', 0, '[]', 0.5, 0.5, 'Territory', NULL), " +
                "('e1', 1, 'Ambush', '', 0, '[]', 0.5, 0.5, 'CampaignEvent', 0)");
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            var nodes = await ctx.Set<Warhammer.Models.Campaign.Node>().ToDictionaryAsync(n => n.Id, n => n.IsPublic);
            nodes.Should().BeEquivalentTo(new Dictionary<string, bool> { ["t1"] = true, ["e1"] = false });
        }
    }

    [Fact]
    public async Task Players_survive_the_move_to_per_campaign_keys()
    {
        await using (var ctx = Ctx())
        {
            await ctx.GetService<IMigrator>().MigrateAsync("20261116230917_RevealExistingTerritories");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Campaigns (CampaignName, MapSeed, MapBiome, MapOctaves, MapLacunarity, MapPersistence, MapScale, MapOffsetX, MapOffsetY, Requisitions, RequisitionsSeeded) " +
                "VALUES ('Live', 1, 'agri', 4, 2.0, 0.5, 2.0, 0.0, 0.0, '[]', 1)");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO CampaignPlayers (Id, CampaignId, Name, VictoryPoints, CampaignPoints, Wins, Losses) VALUES " +
                "('p1', 1, 'Nico', 10, 2, 3, 1), ('p2', 1, 'Rival', 5, 1, 1, 3)");
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            var players = await ctx.CampaignPlayers.OrderBy(p => p.Id).ToListAsync();
            players.Select(p => (p.Id, p.CampaignId, p.Name, p.Wins)).Should().Equal(("p1", 1, "Nico", 3), ("p2", 1, "Rival", 1));

            // A second campaign can now hold its own p1/p2.
            ctx.Campaigns.Add(new Warhammer.Models.Campaign.Campaign
            {
                Players = { new() { Id = "p1" }, new() { Id = "p2" } }
            });
            await ctx.SaveChangesAsync();
            (await ctx.CampaignPlayers.CountAsync()).Should().Be(4);
        }
    }

    [Fact]
    public async Task Campaigns_from_before_world_surface_controls_get_the_defaults()
    {
        await using (var ctx = Ctx())
        {
            await ctx.GetService<IMigrator>().MigrateAsync("20261119020149_AddGmNoteTableMode");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Campaigns (CampaignName, MapSeed, MapBiome, MapOctaves, MapLacunarity, MapPersistence, MapScale, MapOffsetX, MapOffsetY, Requisitions, RequisitionsSeeded) " +
                "VALUES ('Live', 1, 'agri', 4, 2.0, 0.5, 2.0, 0.0, 0.0, '[]', 1)");
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            var live = await ctx.Campaigns.SingleAsync();
            live.MapSeaLevel.Should().Be(0.35);
            live.MapLandmass.Should().Be("continent");
            live.MapRivers.Should().Be(4);
            live.MapRuggedness.Should().Be(0.5);
            live.MapFeatures.Should().Be(0.5);
        }
    }

    [Fact]
    public async Task Bootstrap_is_idempotent()
    {
        await using var ctx = Ctx();
        await DbBootstrap.MigrateAsync(ctx);
        await DbBootstrap.MigrateAsync(ctx);
        (await ctx.Database.GetAppliedMigrationsAsync()).Should().HaveCount(ctx.Database.GetMigrations().Count());
    }

    [Fact]
    public async Task Battle_logged_before_muster_is_backfilled_as_completed()
    {
        await using (var ctx = Ctx())
        {
            await ctx.GetService<IMigrator>().MigrateAsync(ctx.Database.GetMigrations().First());
            await ctx.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO Campaigns (CampaignName, MapSeed, MapBiome, MapOctaves, MapLacunarity, MapPersistence, MapScale, MapOffsetX, MapOffsetY) " +
                "VALUES ('Live', 1, 'agri', 4, 2, 0.5, 2, 0, 0)");
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO BattleRecords (Id, CampaignId, DatePlayed, MissionName, WinnerPlayerId, Player1Vp, Player2Vp, Notes) " +
                "VALUES ('b1', 1, '2026-01-01', 'Old', 'p1', 10, 5, '')");
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            var b = await ctx.BattleRecords.SingleAsync();
            b.Status.Should().Be(Warhammer.Models.Campaign.BattleStatus.Completed);
            b.UnitResults.Should().BeEmpty();
            (b.Player1Id, b.Player2Id).Should().Be(("p1", "p2"));
        }
    }

    public void Dispose() => _conn.Dispose();
}
