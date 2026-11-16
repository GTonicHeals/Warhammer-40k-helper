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

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _webRoot.Dispose();
        base.Dispose();
    }
}
