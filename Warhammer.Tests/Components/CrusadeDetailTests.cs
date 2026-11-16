using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Crusade;
using Warhammer.Models.Crusade;
using Warhammer.Services;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CrusadeDetailTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly TempWebRoot _webRoot = new();

    public CrusadeDetailTests()
    {
        var factory = CreateFactory();
        var env = _webRoot.BuildMockEnv().Object;
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
        _ctx.Services.AddSingleton(new RosterLoadService(env, NullLogger<RosterLoadService>.Instance));
        _ctx.Services.AddSingleton(new CrusadeTraitsService(env, NullLogger<CrusadeTraitsService>.Instance));
    }

    private CrusadeManagerService Crusades => _ctx.Services.GetRequiredService<CrusadeManagerService>();

    [Fact]
    public async Task Changing_the_Id_parameter_loads_the_other_crusade()
    {
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept" });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "B", ArmyName = "Chapter" });

        var cut = _ctx.RenderComponent<CrusadeDetail>(p => p.Add(c => c.Id, "A"));
        cut.WaitForAssertion(() => cut.Find(".crusade-title-input").GetAttribute("value").Should().Be("Sept"));

        cut.SetParametersAndRender(p => p.Add(c => c.Id, "B"));

        cut.WaitForAssertion(() => cut.Find(".crusade-title-input").GetAttribute("value").Should().Be("Chapter"));
    }

    private const string RosterJson = """
        { "roster": { "forces": [ { "selections": [
          { "name": "Breacher Team", "type": "unit", "number": 1, "costs": [ { "name": "pts", "value": 90 } ],
            "selections": [
              { "name": "Shas'ui", "type": "model", "number": 1,
                "profiles": [ { "typeName": "Unit", "name": "Shas'ui", "characteristics": [ { "name": "W", "$text": "2" } ] } ] },
              { "name": "Fire Warriors", "type": "model", "number": 9,
                "profiles": [ { "typeName": "Unit", "name": "Fire Warriors", "characteristics": [ { "name": "W", "$text": "1" } ] } ] } ] },
          { "name": "Riptide Battlesuit", "type": "model", "number": 1, "costs": [ { "name": "pts", "value": 190 } ],
            "profiles": [ { "typeName": "Unit", "name": "Riptide", "characteristics": [ { "name": "W", "$text": "14" } ] } ] }
        ] } ] } }
        """;

    private async Task<CrusadeUnitCard> CardAsync(string id) =>
        (await new CrusadeManagerService(CreateFactory(), _ctx.Services.GetRequiredService<CampaignManagerService>(),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync()).SelectMany(o => o.Units).Single(u => u.Id == id);

    private IRenderedComponent<CrusadeDetail> Render(string id)
    {
        var cut = _ctx.RenderComponent<CrusadeDetail>(p => p.Add(c => c.Id, id));
        cut.WaitForElement(".crusade-title-input");
        return cut;
    }

    [Fact]
    public async Task Editing_damage_saves_to_the_card_and_shows_its_strength()
    {
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "Breachers", ModelCount = 10, WoundsPerModel = 2 } } });
        var cut = Render("A");
        cut.FindAll("[data-strength='a1']").Should().BeEmpty();

        await cut.Find("[data-models-lost='a1']").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "2" });

        cut.Find("[data-strength='a1']").TextContent.Should().Contain("8/10 models");
        (await CardAsync("a1")).ModelsLost.Should().Be(2);
    }

    [Fact]
    public async Task Lowering_the_unit_size_pulls_damage_into_range()
    {
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "Breachers", ModelCount = 10, ModelsLost = 6 } } });
        var cut = Render("A");

        await cut.Find("[data-model-count='a1']").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "5" });

        var card = await CardAsync("a1");
        (card.ModelCount, card.ModelsLost).Should().Be((5, 4));
    }

    [Fact]
    public async Task Unit_size_is_filled_from_the_roster_for_cards_that_lack_it()
    {
        _webRoot.WriteRosterFile("p1", RosterJson);
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "Breachers", DatasheetName = "Breacher Team" },
                      new CrusadeUnitCard { Id = "a2", UnitName = "Big one", DatasheetName = "Riptide Battlesuit", ModelCount = 2, WoundsPerModel = 3 },
                      new CrusadeUnitCard { Id = "a3", UnitName = "Mystery", DatasheetName = "Not in roster" } } });

        var cut = Render("A");

        cut.Find("[data-model-count='a1']").GetAttribute("value").Should().Be("10");
        var a1 = await CardAsync("a1");
        (a1.ModelCount, a1.WoundsPerModel).Should().Be((10, 1));
        var a2 = await CardAsync("a2");
        (a2.ModelCount, a2.WoundsPerModel).Should().Be((2, 3), "a size already on the card is the Game Master's");
        (await CardAsync("a3")).ModelCount.Should().Be(0);
    }

    [Fact]
    public async Task Adding_a_unit_takes_its_size_from_the_roster()
    {
        _webRoot.WriteRosterFile("p1", RosterJson);
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1" });
        var cut = Render("A");

        await cut.Find(".add-unit-form button").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.FindAll(".modal [data-unit]").Single(e => e.GetAttribute("data-unit") == "Riptide Battlesuit")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find(".modal-footer .btn-warning").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        var order = (await new CrusadeManagerService(CreateFactory(), _ctx.Services.GetRequiredService<CampaignManagerService>(),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync()).Single();
        var card = order.Units.Single();
        (card.ModelCount, card.WoundsPerModel).Should().Be((1, 14));
    }

    // ── Buying Requisitions ────────────────────────────────────────────
    private static Task Click(IRenderedComponent<CrusadeDetail> cut, string selector) =>
        cut.Find(selector).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

    private async Task<OrderOfBattle> OrderAsync(string id) =>
        (await new CrusadeManagerService(CreateFactory(), _ctx.Services.GetRequiredService<CampaignManagerService>(),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync()).Single(o => o.Id == id);

    private async Task<string> RequisitionIdAsync(string name) =>
        (await _ctx.Services.GetRequiredService<CampaignManagerService>().LoadCampaignAsync()).Requisitions.Single(r => r.Name == name).Id;

    // Saves the Order of Battle, opens its page and the Requisitions panel, and picks one entry.
    private async Task<IRenderedComponent<CrusadeDetail>> PickAsync(OrderOfBattle order, string requisition)
    {
        await Crusades.SaveCrusadeAsync(order);
        var reqId = await RequisitionIdAsync(requisition);
        var cut = Render(order.Id);
        await Click(cut, "#req-open");
        await Click(cut, $"[data-buy='{reqId}']");
        return cut;
    }

    private static OrderOfBattle Damaged(int points) => new()
    {
        Id = "A", ArmyName = "Sept", RequisitionPoints = points, SupplyLimit = 1000,
        Units = { new CrusadeUnitCard { Id = "a1", UnitName = "Breachers", ModelCount = 10, WoundsPerModel = 2, ModelsLost = 3 },
                  new CrusadeUnitCard { Id = "a2", UnitName = "Riptide", ModelCount = 1, WoundsPerModel = 14 } }
    };

    [Fact]
    public async Task Requisitions_panel_lists_the_campaigns_requisitions()
    {
        await Crusades.SaveCrusadeAsync(Damaged(2));
        var cut = Render("A");
        cut.FindAll("[data-buy]").Should().BeEmpty();

        await Click(cut, "#req-open");

        cut.FindAll("[data-buy]").Should().HaveCount(7);
        cut.Find("#req-panel").TextContent.Should().Contain("Reinforce").And.Contain("Restore every lost model");
    }

    [Fact]
    public async Task Buying_reinforce_restores_models_and_logs_it()
    {
        var cut = await PickAsync(Damaged(2), "Reinforce");

        cut.Find("#req-cost").GetAttribute("value").Should().Be("1");
        cut.Find("#req-confirm").TextContent.Should().Contain("1 unit");
        cut.FindAll("#req-warning").Should().BeEmpty();

        await Click(cut, "#req-confirm-buy");

        var order = await OrderAsync("A");
        order.Units.Single(u => u.Id == "a1").ModelsLost.Should().Be(0);
        order.RequisitionPoints.Should().Be(1);
        cut.FindAll("#req-confirm").Should().BeEmpty();
        cut.FindAll("[data-req-log]").Should().ContainSingle().Which.TextContent.Should().Contain("Reinforce");
        cut.FindAll("[data-strength='a1']").Should().BeEmpty();
    }

    [Fact]
    public async Task Buying_increase_supply_limit_adds_fifty()
    {
        var cut = await PickAsync(Damaged(2), "Increase Supply Limit");

        await Click(cut, "#req-confirm-buy");

        (await OrderAsync("A")).SupplyLimit.Should().Be(1050);
    }

    [Fact]
    public async Task Cannot_buy_without_enough_points()
    {
        var cut = await PickAsync(Damaged(0), "Reinforce");

        cut.Find("#req-confirm-buy").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#req-warning").TextContent.Should().Contain("Not enough Requisition Points");
    }

    [Fact]
    public async Task Cost_can_be_changed_before_buying()
    {
        var cut = await PickAsync(Damaged(3), "Repair and Recuperate");

        await cut.Find("#req-cost").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "-1" });
        cut.Find("#req-confirm-buy").HasAttribute("disabled").Should().BeTrue();

        await cut.Find("#req-cost").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "" });
        cut.Find("#req-confirm-buy").HasAttribute("disabled").Should().BeTrue("a blank cost is not a free purchase");

        await cut.Find("#req-cost").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "2" });
        await Click(cut, "#req-confirm-buy");

        var order = await OrderAsync("A");
        order.RequisitionPoints.Should().Be(1);
        order.RequisitionLog.Should().ContainSingle(l => l.Name == "Repair and Recuperate" && l.Cost == 2);
    }

    [Fact]
    public async Task Buying_repair_with_nothing_to_repair_warns_but_is_allowed()
    {
        var cut = await PickAsync(Damaged(2), "Repair");

        cut.Find("#req-warning").TextContent.Should().Contain("No unit");
        cut.Find("#req-confirm-buy").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Cancel_changes_nothing()
    {
        var cut = await PickAsync(Damaged(2), "Reinforce");

        await Click(cut, "#req-cancel");

        cut.FindAll("#req-confirm").Should().BeEmpty();
        var order = await OrderAsync("A");
        (order.RequisitionPoints, order.Units.Single(u => u.Id == "a1").ModelsLost).Should().Be((2, 3));
        order.RequisitionLog.Should().BeEmpty();
    }

    [Fact]
    public async Task Purchase_log_lists_newest_first()
    {
        var order = Damaged(2);
        order.RequisitionLog.Add(new Warhammer.Models.Campaign.RequisitionPurchase { Name = "Old one", Cost = 1, DateUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) });
        var cut = await PickAsync(order, "Reinforce");

        await Click(cut, "#req-confirm-buy");

        cut.FindAll("[data-req-log]").Select(e => e.TextContent).Should().SatisfyRespectively(
            first => first.Should().Contain("Reinforce"),
            second => second.Should().Contain("Old one"));
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _webRoot.Dispose();
        base.Dispose();
    }
}
