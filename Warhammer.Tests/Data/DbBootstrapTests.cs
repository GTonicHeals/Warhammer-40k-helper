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
            ctx.Campaigns.Add(new Warhammer.Models.Campaign.Campaign { CampaignName = "Live" });
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = Ctx())
        {
            await DbBootstrap.MigrateAsync(ctx);
            (await ctx.Database.GetAppliedMigrationsAsync()).Should().BeEquivalentTo(ctx.Database.GetMigrations());
            (await ctx.Campaigns.CountAsync()).Should().Be(1); // table survived, not recreated
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
