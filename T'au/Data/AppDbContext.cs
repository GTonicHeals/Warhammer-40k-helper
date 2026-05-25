using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Warhammer.Models.Campaign;

namespace Warhammer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Campaign>         Campaigns       => Set<Campaign>();
    public DbSet<CampaignPlayer>   CampaignPlayers => Set<CampaignPlayer>();
    public DbSet<TerritoryNode>    TerritoryNodes  => Set<TerritoryNode>();
    public DbSet<GmNote>           GmNotes         => Set<GmNote>();
    public DbSet<BattleRecord>     BattleRecords   => Set<BattleRecord>();

    // No MapNotes DbSet: GmNote is the single entity that represents both notebook
    // entries and map pins (pinned == X/Y >= 0). The split is deferred to a later step.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite has no native array/list type, so List<string> columns must be
        // serialised to JSON text. The value comparer ensures EF change-tracking
        // detects in-place mutations of the list (e.g. .Add) — without it, edits
        // to ConnectedNodeIds would silently fail to persist.
        var listComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            list   => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            list   => list.ToList());

        modelBuilder.Entity<TerritoryNode>()
            .Property(t => t.ConnectedNodeIds)
            .HasConversion(
                list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(listComparer);
    }
}
