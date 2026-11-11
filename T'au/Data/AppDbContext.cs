using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;

namespace Warhammer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Campaign>       Campaigns       => Set<Campaign>();
    public DbSet<CampaignPlayer> CampaignPlayers => Set<CampaignPlayer>();
    public DbSet<Node>           Nodes           => Set<Node>();
    public DbSet<GmNote>         GmNotes         => Set<GmNote>();
    public DbSet<BattleRecord>   BattleRecords   => Set<BattleRecord>();
    public DbSet<OrderOfBattle>   OrdersOfBattle   => Set<OrderOfBattle>();
    public DbSet<CrusadeUnitCard> CrusadeUnitCards => Set<CrusadeUnitCard>();

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

        // Orders of Battle belong to a campaign; their cards cascade with them.
        modelBuilder.Entity<OrderOfBattle>(e =>
        {
            e.HasOne<Campaign>().WithMany().HasForeignKey(o => o.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(o => o.Units).WithOne().HasForeignKey(u => u.OrderOfBattleId).OnDelete(DeleteBehavior.Cascade);
            e.Property(o => o.CustomTraits).HasJsonConversion();
        });

        modelBuilder.Entity<BattleRecord>()
            .Property(b => b.UnitResults).HasJsonConversion();

        modelBuilder.Entity<CrusadeUnitCard>(e =>
        {
            e.Property(u => u.BattleHonours).HasJsonConversion();
            e.Property(u => u.BattleScars).HasJsonConversion();
        });
    }
}

internal static class JsonColumnExtensions
{
    // Stores a list as JSON text using CrusadeJson.Options. The comparer compares the
    // serialized form so in-place mutations (Add/Remove, edits to an element) are detected.
    public static PropertyBuilder<List<T>> HasJsonConversion<T>(this PropertyBuilder<List<T>> property)
    {
        property.HasConversion(v => JsonColumn<T>.Serialize(v), json => JsonColumn<T>.Deserialize(json))
            .Metadata.SetValueComparer(new ValueComparer<List<T>>(
                (a, b) => JsonColumn<T>.Serialize(a) == JsonColumn<T>.Serialize(b),
                v => JsonColumn<T>.Serialize(v).GetHashCode(),
                v => JsonColumn<T>.Deserialize(JsonColumn<T>.Serialize(v))));
        return property;
    }
}

internal static class JsonColumn<T>
{
    public static string Serialize(List<T>? v) => JsonSerializer.Serialize(v ?? new List<T>(), CrusadeJson.Options);
    public static List<T> Deserialize(string json) => JsonSerializer.Deserialize<List<T>>(json, CrusadeJson.Options) ?? new List<T>();
}
