using Microsoft.EntityFrameworkCore;

namespace Warhammer.Data;

// Brings any campaign.db up to the latest migration.
//
// campaign.db files created before migrations were adopted were built by EnsureCreated:
// they hold the Baseline schema but no __EFMigrationsHistory table, so a plain Migrate
// would try to recreate tables that already exist. For those we record the first
// (Baseline) migration as applied, then migrate normally. An empty file has no
// Campaigns table and takes the plain migrate path.
public static class DbBootstrap
{
    public static async Task MigrateAsync(AppDbContext ctx, CancellationToken ct = default)
    {
        var tables = await ctx.Database
            .SqlQueryRaw<string>("SELECT name AS \"Value\" FROM sqlite_master WHERE type = 'table'")
            .ToListAsync(ct);

        if (!tables.Contains("__EFMigrationsHistory") && tables.Contains("Campaigns"))
        {
            var baseline = ctx.Database.GetMigrations().First();
            var version  = typeof(DbContext).Assembly.GetName().Version!.ToString(3);

            await ctx.Database.ExecuteSqlRawAsync(
                "CREATE TABLE \"__EFMigrationsHistory\" (" +
                "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL)", ct);
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                [baseline, version], ct);
        }

        await ctx.Database.MigrateAsync(ct);
    }
}
