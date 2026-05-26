using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Warhammer.Models.Campaign;

namespace Warhammer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Campaign>       Campaigns       => Set<Campaign>();
    public DbSet<CampaignPlayer> CampaignPlayers => Set<CampaignPlayer>();
    public DbSet<Node>           Nodes           => Set<Node>();
    public DbSet<GmNote>         GmNotes         => Set<GmNote>();
    public DbSet<BattleRecord>   BattleRecords   => Set<BattleRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TPH: single Nodes table with a NodeType discriminator column.
        modelBuilder.Entity<Node>()
            .HasDiscriminator<string>("NodeType")
            .HasValue<Territory>("Territory")
            .HasValue<CampaignEvent>("CampaignEvent");

        // SQLite has no native array type — List<string> is stored as JSON text.
        // The ValueComparer ensures EF change-tracking detects in-place mutations
        // (e.g. .Add / .Remove on ConnectedNodeIds) rather than treating the list
        // as a replaced scalar.
        var listComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            list   => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            list   => list.ToList());

        modelBuilder.Entity<Node>()
            .Property(n => n.ConnectedNodeIds)
            .HasConversion(
                list => JsonSerializer.Serialize(list, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<List<string>>(json, (JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(listComparer);
    }
}
