using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class WoundCalculatorPageTests : SqliteTestBase, IDisposable
{
    // p1: a VEHICLE with a Heavy ranged weapon (BS 3+). p2: INFANTRY with Stealth and a 5+ invulnerable save.
    private const string P1 = """
        { "roster": { "forces": [ { "catalogueName": "Xenos - T'au Empire", "rules": [], "selections": [
          { "name": "Hammerhead", "number": 1, "rules": [], "costs": [], "selections": [],
            "categories": [ { "name": "Vehicle" } ],
            "profiles": [
              { "typeName": "Unit", "name": "Hammerhead", "characteristics": [
                { "name": "M", "$text": "10\"" }, { "name": "T", "$text": "10" }, { "name": "SV", "$text": "3+" },
                { "name": "W", "$text": "14" }, { "name": "LD", "$text": "7+" }, { "name": "OC", "$text": "3" } ] },
              { "typeName": "Ranged Weapons", "name": "Railgun", "characteristics": [
                { "name": "Range", "$text": "72\"" }, { "name": "A", "$text": "1" }, { "name": "BS", "$text": "3+" },
                { "name": "S", "$text": "20" }, { "name": "AP", "$text": "-5" }, { "name": "D", "$text": "D6+6" },
                { "name": "Keywords", "$text": "Heavy" } ] } ] } ] } ], "costs": [], "costLimits": [] } }
        """;

    private const string P2 = """
        { "roster": { "forces": [ { "catalogueName": "Xenos - T'au Empire", "rules": [], "selections": [
          { "name": "Pathfinders", "number": 1, "costs": [], "selections": [],
            "categories": [ { "name": "Infantry" } ],
            "rules": [ { "name": "Stealth", "description": "Subtract 1 from the Hit roll of ranged attacks that target this unit." } ],
            "profiles": [
              { "typeName": "Unit", "name": "Pathfinder", "characteristics": [
                { "name": "M", "$text": "7\"" }, { "name": "T", "$text": "3" }, { "name": "SV", "$text": "4+" },
                { "name": "W", "$text": "1" }, { "name": "LD", "$text": "7+" }, { "name": "OC", "$text": "1" } ] },
              { "typeName": "Abilities", "name": "Invulnerable Save", "characteristics": [
                { "name": "Description", "$text": "This model has a 5+ invulnerable save." } ] } ] } ] } ], "costs": [], "costLimits": [] } }
        """;

    private readonly TestContext _ctx = new();
    private readonly TempWebRoot _web = new();

    public WoundCalculatorPageTests()
    {
        _web.WriteRosterFile("p1", P1);
        _web.WriteRosterFile("p2", P2);
        var env = _web.BuildMockEnv().Object;
        var factory = CreateFactory();
        _ctx.Services.AddSingleton(new RosterLoadService(env, NullLogger<RosterLoadService>.Instance));
        _ctx.Services.AddSingleton(new StratagemService(env, NullLogger<StratagemService>.Instance));
        _ctx.Services.AddSingleton<WoundCalcStateService>();
        // The page mirrors its selection to localStorage; no browser here, so those calls are no-ops.
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
    }

    private static string Roll(IRenderedComponent<WoundCalculator> cut, string die) =>
        cut.Find($"[data-roll='{die}'] strong").TextContent.Trim();

    [Fact]
    public async Task Picking_units_and_weapon_fills_the_spine_with_automatic_modifiers()
    {
        var cut = _ctx.RenderComponent<WoundCalculator>();
        cut.WaitForElement("[data-role='attacker'] button[data-unit='p1_Hammerhead']");

        await cut.Find("[data-role='attacker'] button[data-unit='p1_Hammerhead']").ClickAsync(new MouseEventArgs());
        await cut.Find("button[data-weapon='Railgun']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-role='defender'] button[data-unit='p2_Pathfinders']").ClickAsync(new MouseEventArgs());

        // 3+ base, Stealth −1; Heavy and Big Guns wait for their toggles.
        Roll(cut, "hit").Should().Be("4+");
        cut.Find("button[data-chip='heavy']").ClassList.Should().Contain("ds-chip-waiting");
        cut.Find("button[data-chip='bgnt']").ClassList.Should().Contain("ds-chip-waiting");
        cut.Find("button[data-chip='invuln']").TextContent.Should().Contain("auto");
        Roll(cut, "save").Should().Be("5+");

        await cut.Find("button[data-chip='heavy']").ClickAsync(new MouseEventArgs());
        Roll(cut, "hit").Should().Be("3+");

        var state = _ctx.Services.GetRequiredService<WoundCalcStateService>();
        state.AttackerUnitKey.Should().Be("p1_Hammerhead");
        state.WeaponName.Should().Be("Railgun");
        state.DefenderUnitKey.Should().Be("p2_Pathfinders");
    }

    [Fact]
    public void Saved_selection_is_restored_and_recomputed()
    {
        var state = _ctx.Services.GetRequiredService<WoundCalcStateService>();
        state.AttackerUnitKey = "p1_Hammerhead";
        state.WeaponName = "Railgun";
        state.DefenderUnitKey = "p2_Pathfinders";

        var cut = _ctx.RenderComponent<WoundCalculator>();

        cut.WaitForAssertion(() => Roll(cut, "hit").Should().Be("4+"));
        Roll(cut, "wound").Should().Be("2+");
        Roll(cut, "save").Should().Be("5+");
    }

    [Fact]
    public async Task Situation_toggles_survive_leaving_the_page()
    {
        var cut = _ctx.RenderComponent<WoundCalculator>();
        cut.WaitForElement("[data-role='attacker'] button[data-unit='p1_Hammerhead']");
        await cut.Find("[data-role='attacker'] button[data-unit='p1_Hammerhead']").ClickAsync(new MouseEventArgs());
        await cut.Find("button[data-weapon='Railgun']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-role='defender'] button[data-unit='p2_Pathfinders']").ClickAsync(new MouseEventArgs());
        await cut.Find("button[data-chip='heavy']").ClickAsync(new MouseEventArgs());
        Roll(cut, "hit").Should().Be("3+");

        // A fresh page (same circuit state) comes back with Heavy still on.
        var again = _ctx.RenderComponent<WoundCalculator>();
        again.WaitForAssertion(() => Roll(again, "hit").Should().Be("3+"));
        again.Find("button[data-chip='heavy']").ClassList.Should().NotContain("ds-chip-waiting");
    }

    [Fact]
    public void State_round_trips_through_its_browser_storage_copy()
    {
        var state = new WoundCalcStateService
        {
            AttackerUnitKey = "p1_Hammerhead", WeaponName = "Railgun", DefenderUnitKey = "p2_Pathfinders",
            Situation = Warhammer.Models.Calculator.Situation.None with { Cover = true }, Invuln = 4
        };

        var copy = new WoundCalcStateService();
        copy.TryLoadJson(state.ToJson()).Should().BeTrue();

        copy.AttackerUnitKey.Should().Be("p1_Hammerhead");
        copy.WeaponName.Should().Be("Railgun");
        copy.Situation!.Cover.Should().BeTrue();
        copy.Invuln.Should().Be(4);
        copy.IsEmpty.Should().BeFalse();
        new WoundCalcStateService().TryLoadJson("not json").Should().BeFalse();
    }

    [Fact]
    public async Task Reset_clears_both_sheets()
    {
        var cut = _ctx.RenderComponent<WoundCalculator>();
        cut.WaitForElement("[data-role='attacker'] button[data-unit='p1_Hammerhead']");
        await cut.Find("[data-role='attacker'] button[data-unit='p1_Hammerhead']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-role='defender'] button[data-unit='p2_Pathfinders']").ClickAsync(new MouseEventArgs());

        await cut.Find(".ds-reset").ClickAsync(new MouseEventArgs());

        var state = _ctx.Services.GetRequiredService<WoundCalcStateService>();
        state.AttackerUnitKey.Should().BeNull();
        state.DefenderUnitKey.Should().BeNull();
        cut.FindAll("[data-role='attacker'] button[data-unit]").Should().NotBeEmpty();
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _web.Dispose();
        Dispose();
    }
}
