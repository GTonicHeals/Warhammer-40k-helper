using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warhammer.Data;
using Warhammer.Services;
using Warhammer.Tests.TestInfrastructure;
using Warhammer.Tools;

namespace Warhammer.Tests.Campaign;

// Tests the CLI dispatcher in isolation. Stands up a minimal ServiceProvider that
// mirrors the relevant slice of Program.cs (DbContextFactory + CampaignManagerService +
// GmNoteSeeder), wired against this fixture's in-memory SQLite connection so the
// SeedRunner -> GmNoteSeeder integration path is exercised end-to-end without
// starting the actual web host.
public class SeedRunnerTests : SqliteTestBase
{
    private IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<AppDbContext>(options => options.UseSqlite(Connection));
        services.AddScoped<CampaignManagerService>();
        services.AddScoped<GmNoteSeeder>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunAsync_with_no_seed_args_returns_exit_code_1()
    {
        var exit = await SeedRunner.RunAsync(BuildServices(), Array.Empty<string>());

        exit.Should().Be(1, "missing sub-command is a usage error");

        await using var ctx = CreateContext();
        (await ctx.GmNotes.AnyAsync()).Should().BeFalse("no seeder should have run");
    }

    [Fact]
    public async Task RunAsync_with_unknown_seed_name_returns_exit_code_1_and_writes_nothing()
    {
        var exit = await SeedRunner.RunAsync(BuildServices(), new[] { "does-not-exist" });

        exit.Should().Be(1);

        await using var ctx = CreateContext();
        (await ctx.GmNotes  .AnyAsync()).Should().BeFalse();
        (await ctx.Campaigns.AnyAsync()).Should().BeFalse("unknown seed must not even trigger campaign seeding");
    }

    [Fact]
    public async Task RunAsync_with_gm_notes_returns_exit_code_0_and_seeds_ten_notes()
    {
        var exit = await SeedRunner.RunAsync(BuildServices(), new[] { "gm-notes" });

        exit.Should().Be(0);

        await using var ctx = CreateContext();
        (await ctx.GmNotes.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task RunAsync_gm_notes_invoked_twice_is_idempotent()
    {
        var services = BuildServices();

        (await SeedRunner.RunAsync(services, new[] { "gm-notes" })).Should().Be(0);
        (await SeedRunner.RunAsync(services, new[] { "gm-notes" })).Should().Be(0);

        await using var ctx = CreateContext();
        (await ctx.GmNotes.CountAsync()).Should().Be(10, "second run must not duplicate rows");
    }
}
