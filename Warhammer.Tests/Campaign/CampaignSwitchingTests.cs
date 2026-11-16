using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Campaign;
using Warhammer.Services;
using Warhammer.Tests.TestInfrastructure;
using CampaignEntity = Warhammer.Models.Campaign.Campaign;

namespace Warhammer.Tests.Campaign;

public class CampaignSwitchingTests : SqliteTestBase
{
    private CampaignManagerService Svc() =>
        new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    private async Task<(int A, int B)> SeedTwoAsync()
    {
        await using var ctx = CreateContext();
        var a = new CampaignEntity { CampaignName = "A", MapNodes = { new Territory { Id = "na", Name = "Hab A" } } };
        var b = new CampaignEntity { CampaignName = "B", MapNodes = { new Territory { Id = "nb", Name = "Hab B" } } };
        ctx.Campaigns.AddRange(a, b);
        await ctx.SaveChangesAsync();
        return (a.Id, b.Id);
    }

    [Fact]
    public async Task Legacy_db_without_last_opened_uses_lowest_id()
    {
        await SeedTwoAsync();

        (await Svc().LoadCampaignAsync()).CampaignName.Should().Be("A");
    }

    [Fact]
    public async Task Switch_makes_that_campaign_current()
    {
        var (_, b) = await SeedTwoAsync();
        var svc = Svc();
        await svc.LoadCampaignAsync();

        await svc.SwitchToAsync(b);

        svc.Campaign.Should().BeNull("the cached campaign belongs to the old one");
        (await svc.LoadCampaignAsync()).Id.Should().Be(b);
        (await Svc().LoadCampaignAsync()).Id.Should().Be(b, "the choice is stored, not per instance");
        var list = await Svc().ListCampaignsAsync();
        list[0].Id.Should().Be(b);
        list[0].IsCurrent.Should().BeTrue();
        list.Count(c => c.IsCurrent).Should().Be(1);
    }

    [Fact]
    public async Task Create_makes_a_new_current_campaign_and_leaves_old_untouched()
    {
        var svc = Svc();
        var first = await svc.LoadCampaignAsync();
        first.MapNodes.Add(new Territory { Id = "t1", Name = "Forge" });
        await svc.SaveCampaignAsync(first);

        var second = await svc.CreateCampaignAsync("Second");

        second.Id.Should().NotBe(first.Id);
        second.CampaignName.Should().Be("Second");
        second.Players.Select(p => p.Id).Should().BeEquivalentTo(new[] { "p1", "p2" });
        second.MapNodes.Should().BeEmpty();
        second.Requisitions.Should().HaveCount(7);
        (await Svc().GetCurrentCampaignIdAsync()).Should().Be(second.Id);

        await svc.SwitchToAsync(first.Id);
        (await svc.LoadCampaignAsync()).MapNodes.Should().ContainSingle().Which.Id.Should().Be("t1");
    }

    [Fact]
    public async Task Switch_to_unknown_id_throws()
    {
        await SeedTwoAsync();

        await Svc().Invoking(s => s.SwitchToAsync(999)).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Delete_current_campaign_throws()
    {
        var (a, _) = await SeedTwoAsync();

        await Svc().Invoking(s => s.DeleteCampaignAsync(a)).Should().ThrowAsync<InvalidOperationException>();

        await using var ctx = CreateContext();
        (await ctx.Campaigns.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Delete_other_campaign_removes_only_it()
    {
        var (a, b) = await SeedTwoAsync();
        await using (var ctx = CreateContext())
        {
            ctx.OrdersOfBattle.Add(new Warhammer.Models.Crusade.OrderOfBattle { Id = "oa", CampaignId = a });
            ctx.OrdersOfBattle.Add(new Warhammer.Models.Crusade.OrderOfBattle { Id = "ob", CampaignId = b });
            await ctx.SaveChangesAsync();
        }

        await Svc().DeleteCampaignAsync(b);

        await using var check = CreateContext();
        (await check.Campaigns.SingleAsync()).Id.Should().Be(a);
        (await check.Nodes.SingleAsync()).Id.Should().Be("na");
        (await check.OrdersOfBattle.SingleAsync()).Id.Should().Be("oa");
    }

    [Fact]
    public async Task List_reports_counts()
    {
        var svc = Svc();
        var c = await svc.LoadCampaignAsync();
        c.BattleHistory.Add(new BattleRecord { Id = "b1" });
        c.BattleHistory.Add(new BattleRecord { Id = "b2" });
        await svc.SaveCampaignAsync(c);

        var row = (await svc.ListCampaignsAsync()).Single();

        row.Name.Should().Be("Narrative Campaign");
        row.PlayerCount.Should().Be(2);
        row.BattleCount.Should().Be(2);
        row.IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task Rename_changes_only_name()
    {
        var (a, b) = await SeedTwoAsync();

        await Svc().RenameCampaignAsync(b, "  Bravo  ");
        await Svc().RenameCampaignAsync(b, "   ");

        var list = await Svc().ListCampaignsAsync();
        list.Single(c => c.Id == b).Name.Should().Be("Bravo");
        list.Single(c => c.IsCurrent).Id.Should().Be(a);
    }
}
