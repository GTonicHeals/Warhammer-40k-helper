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

    public void Dispose() => _conn.Dispose();
}
