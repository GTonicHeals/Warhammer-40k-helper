namespace Warhammer.Tools;

// Dispatcher for `dotnet run --project T'au -- seed <name>` dev commands.
// Each seed gets its own service class registered in DI; this only routes the CLI sub-command.
public static class SeedRunner
{
    public static async Task<int> RunAsync(IServiceProvider services, string[] seedArgs)
    {
        if (seedArgs.Length == 0)
        {
            Console.Error.WriteLine("Usage: dotnet run --project T'au -- seed <name>");
            Console.Error.WriteLine("  available seeds: gm-notes");
            return 1;
        }

        // Scope so we can resolve scoped services (CampaignManagerService, GmNoteSeeder).
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        switch (seedArgs[0])
        {
            case "gm-notes":
                var seeder = sp.GetRequiredService<GmNoteSeeder>();
                var (added, skipped, campaignId) = await seeder.SeedAsync();
                Console.WriteLine($"gm-notes: added {added}, skipped {skipped} (Campaign Id={campaignId})");
                return 0;

            default:
                Console.Error.WriteLine($"Unknown seed name: '{seedArgs[0]}'. Run with no name to list available seeds.");
                return 1;
        }
    }
}
