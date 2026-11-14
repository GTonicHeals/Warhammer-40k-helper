using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Campaign;
using Warhammer.Services;
using Warhammer.Tests.TestInfrastructure;
// Alias because the enclosing namespace Warhammer.Tests.Campaign shadows the
// type Warhammer.Models.Campaign.Campaign.
using CampaignEntity = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Campaign;

// Behavioural coverage for CampaignManagerService against an in-memory SQLite database.
// Inherits SqliteTestBase, so each [Fact] runs against a fresh schema with no test-to-test
// state bleed.
public class CampaignManagerServiceTests : SqliteTestBase
{
    private CampaignManagerService CreateService() =>
        new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    [Fact]
    public async Task LoadCampaignAsync_seeds_default_with_two_players_when_db_is_empty()
    {
        var campaign = await CreateService().LoadCampaignAsync();

        campaign.Id.Should().BePositive();
        campaign.CampaignName.Should().Be("Narrative Campaign");
        campaign.Players.Select(p => p.Id).Should().BeEquivalentTo(new[] { "p1", "p2" });
        campaign.MapNodes.Should().BeEmpty();
        campaign.BattleHistory.Should().BeEmpty();
        campaign.GmNotebook.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadCampaignAsync_eagerly_loads_children_via_include()
    {
        // Seed all four child collections directly, then verify the service's
        // .Include chain materialises every one of them. This is the explicit Phase 4
        // eager-load test.
        int campaignId;
        await using (var ctx = CreateContext())
        {
            var seed = new CampaignEntity
            {
                CampaignName = "Seeded",
                Players       = { new CampaignPlayer { Id = "p1", Name = "P1" } },
                MapNodes      = { new Territory       { Id = "n1", Name = "Hab" } },
                BattleHistory = { new BattleRecord    { Id = "b1", MissionName = "Skirmish" } },
                GmNotebook    = { new GmNote          { Id = "g1", Title = "Note" } }
            };
            ctx.Campaigns.Add(seed);
            await ctx.SaveChangesAsync();
            campaignId = seed.Id;
        }

        var loaded = await CreateService().LoadCampaignAsync();

        loaded.Id.Should().Be(campaignId);
        loaded.Players      .Should().ContainSingle().Which.Id.Should().Be("p1");
        loaded.MapNodes     .Should().ContainSingle().Which.Id.Should().Be("n1");
        loaded.BattleHistory.Should().ContainSingle().Which.Id.Should().Be("b1");
        loaded.GmNotebook   .Should().ContainSingle().Which.Id.Should().Be("g1");
    }

    [Fact]
    public async Task SaveCampaignAsync_inserts_campaign_with_relational_children()
    {
        // Build a brand-new campaign carrying a Territory (map node) and a pinned,
        // public GmNote (the "MapNote" — a GmNote with X/Y >= 0 and IsPublic = true).
        // Then verify, by querying the raw child tables in an independent context,
        // that the rows landed with correct FKs.
        var campaign = new CampaignEntity
        {
            CampaignName = "Insert With Children",
            MapNodes     = { new Territory { Id = "node-1", Name = "Forge World" } },
            GmNotebook   = { new GmNote   { Id = "note-1", Title = "Pinned", X = 0.5, Y = 0.5, IsPublic = true } }
        };

        await CreateService().SaveCampaignAsync(campaign);

        campaign.Id.Should().BePositive("EF should populate the Id after the insert");

        await using var ctx = CreateContext();
        var node = await ctx.Nodes  .AsNoTracking().SingleAsync(n => n.Id == "node-1");
        var note = await ctx.GmNotes.AsNoTracking().SingleAsync(g => g.Id == "note-1");
        node.CampaignId.Should().Be(campaign.Id, "the Territory FK should point at the new campaign");
        node.Name      .Should().Be("Forge World");
        note.CampaignId.Should().Be(campaign.Id, "the GmNote FK should point at the new campaign");
        note.IsPublic  .Should().BeTrue();
        note.X         .Should().Be(0.5);
        note.Y         .Should().Be(0.5);
    }

    [Fact]
    public async Task LoadCampaignAsync_returns_existing_campaign_with_all_four_collections_populated()
    {
        var initial = await CreateService().LoadCampaignAsync();
        initial.MapNodes     .Add(new Territory    { Name = "Hab Block", X = 0.1, Y = 0.2 });
        initial.BattleHistory.Add(new BattleRecord { MissionName = "Sweep and Clear", WinnerPlayerId = "p1" });
        initial.GmNotebook   .Add(new GmNote       { Title = "Sector 4 Anomaly" });
        await CreateService().SaveCampaignAsync(initial);

        var loaded = await CreateService().LoadCampaignAsync();

        loaded.Id           .Should().Be(initial.Id);
        loaded.Players      .Should().HaveCount(2);
        loaded.MapNodes     .Should().ContainSingle().Which.Name.Should().Be("Hab Block");
        loaded.BattleHistory.Should().ContainSingle().Which.MissionName.Should().Be("Sweep and Clear");
        loaded.GmNotebook   .Should().ContainSingle().Which.Title.Should().Be("Sector 4 Anomaly");
    }

    [Fact]
    public async Task SaveCampaignAsync_inserts_when_id_is_zero()
    {
        var brandNew = new CampaignEntity { CampaignName = "Brand New Crusade" };

        await CreateService().SaveCampaignAsync(brandNew);

        brandNew.Id.Should().BePositive("EF should populate the Id after the insert");
        var loaded = await CreateService().LoadCampaignAsync();
        loaded.Id          .Should().Be(brandNew.Id);
        loaded.CampaignName.Should().Be("Brand New Crusade");
    }

    [Fact]
    public async Task SaveCampaignAsync_updates_scalar_properties_on_existing_campaign()
    {
        var c = await CreateService().LoadCampaignAsync();
        c.CampaignName = "Renamed";
        c.MapSeed      = 12_345;
        c.MapBiome     = "ice";
        c.MapScale     = 3.5;

        await CreateService().SaveCampaignAsync(c);

        var loaded = await CreateService().LoadCampaignAsync();
        loaded.CampaignName.Should().Be("Renamed");
        loaded.MapSeed     .Should().Be(12_345);
        loaded.MapBiome    .Should().Be("ice");
        loaded.MapScale    .Should().Be(3.5);
    }

    [Fact]
    public async Task SaveCampaignAsync_inserts_newly_added_child_entities()
    {
        var c = await CreateService().LoadCampaignAsync();
        var nodeId = Guid.NewGuid().ToString();
        c.MapNodes  .Add(new Territory { Id = nodeId, Name = "Forge World" });
        c.GmNotebook.Add(new GmNote   { Title = "Pinned", X = 0.5, Y = 0.5, IsPublic = true });

        await CreateService().SaveCampaignAsync(c);

        var loaded = await CreateService().LoadCampaignAsync();
        loaded.MapNodes.Should().ContainSingle().Which.Id.Should().Be(nodeId);
        loaded.MapNodes[0].Name.Should().Be("Forge World");
        loaded.GmNotebook.Should().ContainSingle().Which.Title.Should().Be("Pinned");
    }

    [Fact]
    public async Task SaveCampaignAsync_updates_mutated_child_entities()
    {
        var c = await CreateService().LoadCampaignAsync();
        var p1 = c.Players.First(p => p.Id == "p1");
        p1.VictoryPoints = 50;
        p1.Wins          = 3;
        p1.Name          = "Imperium";

        await CreateService().SaveCampaignAsync(c);

        var loaded = await CreateService().LoadCampaignAsync();
        var reloadedP1 = loaded.Players.First(p => p.Id == "p1");
        reloadedP1.VictoryPoints.Should().Be(50);
        reloadedP1.Wins         .Should().Be(3);
        reloadedP1.Name         .Should().Be("Imperium");
    }

    [Fact]
    public async Task SaveCampaignAsync_deletes_removed_child_entities()
    {
        var c = await CreateService().LoadCampaignAsync();
        c.MapNodes.Add(new Territory { Id = "n1", Name = "Alpha" });
        c.MapNodes.Add(new Territory { Id = "n2", Name = "Beta"  });
        await CreateService().SaveCampaignAsync(c);

        var reload = await CreateService().LoadCampaignAsync();
        reload.MapNodes.RemoveAll(n => n.Id == "n1");
        await CreateService().SaveCampaignAsync(reload);

        var final = await CreateService().LoadCampaignAsync();
        final.MapNodes.Should().ContainSingle().Which.Id.Should().Be("n2");
    }

    [Fact]
    public async Task DeleteBattleAsync_removes_only_that_battle()
    {
        var svc = CreateService();
        var c = await svc.LoadCampaignAsync();
        c.Players.First(p => p.Id == "p1").Wins = 2;
        c.MapNodes.Add(new Territory { Id = "n1", Name = "Hab", ControllingPlayerId = "p1" });
        c.BattleHistory.Add(new BattleRecord { Id = "b1", MissionName = "Keep",   Status = BattleStatus.Mustered });
        c.BattleHistory.Add(new BattleRecord { Id = "b2", MissionName = "Delete", Status = BattleStatus.Completed,
                                               WinnerPlayerId = "p1", CapturedTerritoryId = "n1" });
        await svc.SaveCampaignAsync(c);

        await svc.DeleteBattleAsync("b2");

        svc.Campaign!.BattleHistory.Should().ContainSingle().Which.Id.Should().Be("b1",
            "the cached campaign should drop the battle too");

        var loaded = await CreateService().LoadCampaignAsync();
        loaded.BattleHistory.Should().ContainSingle().Which.Id.Should().Be("b1");
        loaded.Players.First(p => p.Id == "p1").Wins.Should().Be(2, "applied results are kept");
        loaded.MapNodes.OfType<Territory>().Single().ControllingPlayerId.Should().Be("p1");
    }

    [Fact]
    public async Task DeleteBattleAsync_ignores_unknown_id()
    {
        var svc = CreateService();
        var c = await svc.LoadCampaignAsync();
        c.BattleHistory.Add(new BattleRecord { Id = "b1" });
        await svc.SaveCampaignAsync(c);

        await svc.DeleteBattleAsync("nope");

        (await CreateService().LoadCampaignAsync()).BattleHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task DeleteBattleAsync_then_save_of_cached_campaign_does_not_resurrect_battle()
    {
        var svc = CreateService();
        var c = await svc.LoadCampaignAsync();
        c.BattleHistory.Add(new BattleRecord { Id = "b1" });
        await svc.SaveCampaignAsync(c);

        await svc.DeleteBattleAsync("b1");
        await svc.SaveCampaignAsync(svc.Campaign!);

        (await CreateService().LoadCampaignAsync()).BattleHistory.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveCampaignAsync_when_id_not_found_treats_as_insert()
    {
        var orphan = new CampaignEntity { Id = 999, CampaignName = "Orphan" };

        await CreateService().SaveCampaignAsync(orphan);

        var loaded = await CreateService().LoadCampaignAsync();
        loaded.CampaignName.Should().Be("Orphan", "the orphan should have been inserted rather than silently dropped");
    }

    [Fact]
    public async Task Node_ConnectedNodeIds_roundtrips_and_updates_through_json_conversion()
    {
        // Same shape as the focused unit test in TerritoryNodeTests, but goes through
        // the full service round-trip (LoadCampaignAsync / SaveCampaignAsync) instead of
        // direct AppDbContext access — catches regressions in the service-level
        // reconciliation flow specifically.
        var c = await CreateService().LoadCampaignAsync();
        c.MapNodes.Add(new Territory
        {
            Id               = "hub",
            Name             = "Hub",
            ConnectedNodeIds = { "alpha", "beta" }
        });
        await CreateService().SaveCampaignAsync(c);

        var afterInsert = await CreateService().LoadCampaignAsync();
        afterInsert.MapNodes.Single().ConnectedNodeIds
            .Should().BeEquivalentTo(new[] { "alpha", "beta" });

        afterInsert.MapNodes.Single().ConnectedNodeIds.Add("gamma");
        await CreateService().SaveCampaignAsync(afterInsert);

        var afterUpdate = await CreateService().LoadCampaignAsync();
        afterUpdate.MapNodes.Single().ConnectedNodeIds
            .Should().BeEquivalentTo(new[] { "alpha", "beta", "gamma" });
    }

    [Fact]
    public async Task Deleting_campaign_cascades_to_all_child_collections()
    {
        var c = await CreateService().LoadCampaignAsync();
        c.MapNodes     .Add(new Territory   { Name = "Hab" });
        c.BattleHistory.Add(new BattleRecord { MissionName = "Skirmish" });
        c.GmNotebook   .Add(new GmNote      { Title = "Note" });
        await CreateService().SaveCampaignAsync(c);

        // Delete the campaign root. The FK ON DELETE CASCADE constraints on
        // Players/MapNodes/GmNotes/BattleRecords should remove all dependents.
        await using (var ctx = CreateContext())
        {
            var existing = await ctx.Campaigns.FindAsync(c.Id);
            ctx.Campaigns.Remove(existing!);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext())
        {
            (await ctx.Campaigns      .AnyAsync()).Should().BeFalse();
            (await ctx.CampaignPlayers.AnyAsync()).Should().BeFalse();
            (await ctx.Nodes          .AnyAsync()).Should().BeFalse();
            (await ctx.BattleRecords  .AnyAsync()).Should().BeFalse();
            (await ctx.GmNotes        .AnyAsync()).Should().BeFalse();
        }
    }
}
