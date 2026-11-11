using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Warhammer.Models.Crusade;
using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Services;

public class CrusadeLocalStorageImporterTests : SqliteTestBase, IDisposable
{
    private readonly TempWebRoot _web = new();

    // p1 roster: an EPIC HERO and a TITANIC unit.
    private const string Roster = """
        { "roster": { "forces": [ { "catalogueName": "Xenos - T'au Empire", "rules": [], "selections": [
          { "name": "Commander Farsight", "number": 1, "rules": [], "costs": [], "selections": [], "profiles": [],
            "categories": [ { "name": "Character" }, { "name": "Epic Hero" } ] },
          { "name": "Ta'unar Supremacy Armour", "number": 1, "rules": [], "costs": [], "selections": [], "profiles": [],
            "categories": [ { "name": "Vehicle" }, { "name": "Titanic" } ] } ] } ], "costs": [], "costLimits": [] } }
        """;

    private const string Legacy = """
        {"id":"abc","armyName":"Old","playerId":"p1","crusadePoints":4,
         "units":[{"id":"u1","unitName":"Breachers","crusadePoints":3,"battleHonours":["Brutal [Weapon Modification] — S+1"]}]}
        """;

    private CrusadeManagerService Svc() => new(CreateFactory(),
        new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
        NullLogger<CrusadeManagerService>.Instance);

    private static Mock<IJSRuntime> JsWith(Dictionary<string, string> entries)
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<Dictionary<string, string>>("crusadeStorage.getAll", It.IsAny<object?[]?>()))
          .ReturnsAsync(entries);
        return js;
    }

    private CrusadeLocalStorageImporter Importer(IJSRuntime js, CrusadeManagerService svc) =>
        new(js, svc, new RosterLoadService(_web.BuildMockEnv().Object, NullLogger<RosterLoadService>.Instance),
            NullLogger<CrusadeLocalStorageImporter>.Instance);

    [Fact]
    public async Task Imported_cards_take_titanic_and_xp_flags_from_roster_keywords()
    {
        _web.WriteRosterFile("p1", Roster);
        var js = JsWith(new() { ["crusade_f"] = """
            {"id":"f","armyName":"Farsight","playerId":"p1",
             "units":[{"id":"u1","unitName":"Farsight","datasheetName":"Commander Farsight"},
                      {"id":"u2","unitName":"Big","datasheetName":"Ta'unar Supremacy Armour"}]}
            """ });

        await Importer(js.Object, Svc()).ImportAsync();

        var units = (await Svc().LoadAllCrusadesAsync()).Single().Units;
        units.Single(u => u.Id == "u1").XpEligible.Should().BeFalse();
        units.Single(u => u.Id == "u1").IsTitanic.Should().BeFalse();
        units.Single(u => u.Id == "u2").IsTitanic.Should().BeTrue();
        units.Single(u => u.Id == "u2").XpEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Failed_save_is_logged_and_key_left_in_place()
    {
        var js = JsWith(new() { ["crusade_dup"] = """
            {"id":"dup","armyName":"Dup","units":[{"id":"u1","unitName":"A"},{"id":"u1","unitName":"B"}]}
            """, ["crusade_abc"] = Legacy });

        var imported = await Importer(js.Object, Svc()).ImportAsync();

        imported.Should().Be(1);
        js.Verify(j => j.InvokeAsync<IJSVoidResult>("crusadeStorage.remove",
            It.Is<object?[]?>(a => (string)a![0]! == "crusade_dup")), Times.Never);
    }

    void IDisposable.Dispose()
    {
        _web.Dispose();
        Dispose();
    }

    [Fact]
    public async Task Imports_legacy_json_then_removes_key()
    {
        var js = JsWith(new() { ["crusade_abc"] = Legacy });

        var imported = await Importer(js.Object, Svc()).ImportAsync();

        imported.Should().Be(1);
        var loaded = (await Svc().LoadAllCrusadesAsync()).Single();
        loaded.ArmyName.Should().Be("Old");
        loaded.Units.Single().BattleHonours.Single().Name.Should().Be("Brutal");
        js.Verify(j => j.InvokeAsync<IJSVoidResult>("crusadeStorage.remove",
            It.Is<object?[]?>(a => (string)a![0]! == "crusade_abc")), Times.Once);
    }

    [Fact]
    public async Task Skips_orders_already_in_database()
    {
        await Svc().SaveCrusadeAsync(new OrderOfBattle { Id = "abc", ArmyName = "Kept" });
        var js = JsWith(new() { ["crusade_abc"] = Legacy });

        var imported = await Importer(js.Object, Svc()).ImportAsync();

        imported.Should().Be(0);
        (await Svc().LoadAllCrusadesAsync()).Single().ArmyName.Should().Be("Kept");
        js.Verify(j => j.InvokeAsync<IJSVoidResult>("crusadeStorage.remove",
            It.Is<object?[]?>(a => (string)a![0]! == "crusade_abc")), Times.Once);
    }

    [Fact]
    public async Task Bad_json_is_left_in_place()
    {
        var js = JsWith(new() { ["crusade_bad"] = "{not json" });

        (await Importer(js.Object, Svc()).ImportAsync()).Should().Be(0);

        js.Verify(j => j.InvokeAsync<IJSVoidResult>("crusadeStorage.remove", It.IsAny<object?[]?>()), Times.Never);
    }

    [Fact]
    public async Task Returns_zero_when_js_unavailable()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<Dictionary<string, string>>("crusadeStorage.getAll", It.IsAny<object?[]?>()))
          .ThrowsAsync(new InvalidOperationException("prerender"));

        (await Importer(js.Object, Svc()).ImportAsync()).Should().Be(0);
    }
}
