using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Models.Crusade;
using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Services;

public class CrusadeManagerServiceTests : SqliteTestBase
{
    private CrusadeManagerService Svc() => new(CreateFactory(),
        new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
        NullLogger<CrusadeManagerService>.Instance);

    [Fact]
    public async Task Save_inserts_and_assigns_campaign_id()
    {
        var svc = Svc();
        var o = new OrderOfBattle { ArmyName = "A", PlayerId = "p1" };
        await svc.SaveCrusadeAsync(o);
        o.CampaignId.Should().BePositive();
        (await svc.LoadAllCrusadesAsync()).Should().ContainSingle(x => x.Id == o.Id);
    }

    [Fact]
    public async Task Save_updates_scalars_and_reconciles_cards()
    {
        var svc = Svc();
        var o = new OrderOfBattle
        {
            ArmyName = "A", PlayerId = "p1",
            Units = { new CrusadeUnitCard { UnitName = "U1" }, new CrusadeUnitCard { UnitName = "U2" } }
        };
        await svc.SaveCrusadeAsync(o);
        o.ArmyName = "B";
        o.Units.RemoveAt(1);
        o.Units[0].Xp = 9;
        o.Units.Add(new CrusadeUnitCard { UnitName = "U3" });
        await svc.SaveCrusadeAsync(o);

        var loaded = (await Svc().LoadAllCrusadesAsync()).Single();
        loaded.ArmyName.Should().Be("B");
        loaded.Units.Select(u => u.UnitName).Should().BeEquivalentTo(new[] { "U1", "U3" });
        loaded.Units.Single(u => u.UnitName == "U1").Xp.Should().Be(9);
    }

    [Fact]
    public async Task Delete_removes_order_and_its_cards_only()
    {
        var svc = Svc();
        var a = new OrderOfBattle { ArmyName = "A", PlayerId = "p1", Units = { new CrusadeUnitCard { UnitName = "U" } } };
        var b = new OrderOfBattle { ArmyName = "B", PlayerId = "p2", Units = { new CrusadeUnitCard { UnitName = "V" } } };
        await svc.SaveCrusadeAsync(a);
        await svc.SaveCrusadeAsync(b);
        await svc.DeleteCrusadeAsync(a.Id);

        await using var ctx = CreateContext();
        (await ctx.OrdersOfBattle.CountAsync()).Should().Be(1);
        (await ctx.CrusadeUnitCards.CountAsync()).Should().Be(1);
        svc.Crusades.Should().ContainSingle(o => o.Id == b.Id);
    }

    [Fact]
    public async Task Save_raises_changed_event()
    {
        var svc = Svc();
        var raised = 0;
        svc.OnCrusadesChanged += () => raised++;
        await svc.SaveCrusadeAsync(new OrderOfBattle { ArmyName = "A" });
        raised.Should().Be(1);
    }

    [Fact]
    public async Task GetCrusade_reads_from_loaded_snapshot()
    {
        var o = new OrderOfBattle { ArmyName = "A" };
        await Svc().SaveCrusadeAsync(o);
        var svc = Svc();
        await svc.LoadAllCrusadesAsync();
        svc.GetCrusade(o.Id)!.ArmyName.Should().Be("A");
    }
}
