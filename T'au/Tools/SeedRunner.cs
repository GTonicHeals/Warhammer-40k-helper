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
            Console.Error.WriteLine("  available seeds: gm-notes, nodes");
            return 1;
        }

        // Scope so we can resolve scoped services (CampaignManagerService, seeders).
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        switch (seedArgs[0])
        {
            case "gm-notes":
                var gmSeeder = sp.GetRequiredService<GmNoteSeeder>();
                var (gmAdded, gmSkipped, gmCampaignId) = await gmSeeder.SeedAsync();
                Console.WriteLine($"gm-notes: added {gmAdded}, skipped {gmSkipped} (Campaign Id={gmCampaignId})");
                return 0;

            case "nodes":
                var nodeSeeder = sp.GetRequiredService<NodeSeeder>();
                var (nAdded, nSkipped, nCampaignId) = await nodeSeeder.SeedAsync();
                Console.WriteLine($"nodes: added {nAdded}, skipped {nSkipped} (Campaign Id={nCampaignId})");
                return 0;

            default:
                Console.Error.WriteLine($"Unknown seed name: '{seedArgs[0]}'. Run with no name to list available seeds.");
                return 1;
        }
    }
}
