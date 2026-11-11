using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Battle;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class PostBattleTests : SqliteTestBase, IDisposable
{
    private const string TraitsJson = """
        {
          "battleTraits": [ { "name": "Hardened", "category": "Infantry", "effect": "Tough." } ],
          "weaponModifications": [],
          "crusadeRelics": [],
          "battleScars": [ { "name": "Crippled", "effect": "Slow." } ]
        }
        """;

    private readonly TestContext _ctx = new();
    private readonly TempWebRoot _web = new();

    private readonly GatedDbContextFactory _gated;
    private readonly ListLogger<PostBattleService> _postBattleLog = new();

    public PostBattleTests()
    {
        _web.WriteDataFile("crusade_traits.json", TraitsJson);
        var env = _web.BuildMockEnv().Object;
        var factory = _gated = new GatedDbContextFactory(CreateFactory());
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new PostBattleService(sp.GetRequiredService<CampaignManagerService>(),
            sp.GetRequiredService<CrusadeManagerService>(), _postBattleLog));
        _ctx.Services.AddSingleton(new CrusadeTraitsService(env, NullLogger<CrusadeTraitsService>.Instance));
        _ctx.Services.AddSingleton(new RosterLoadService(env, NullLogger<RosterLoadService>.Instance));
    }

    private CampaignManagerService Campaigns => _ctx.Services.GetRequiredService<CampaignManagerService>();
    private CrusadeManagerService Crusades => _ctx.Services.GetRequiredService<CrusadeManagerService>();

    private static AppliedTrait Scar(string n) => new() { Name = n, Type = CrusadeTraitTypes.BattleScar };

    private async Task<string> SeedAsync()
    {
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab" });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "A1", Xp = 14,
                          BattleHonours = { new AppliedTrait { Name = "Old", Type = CrusadeTraitTypes.BattleTrait } } },
                      new CrusadeUnitCard { Id = "a2", UnitName = "A2", XpEligible = false } } });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "B", ArmyName = "Chapter", PlayerId = "p2",
            Units = { new CrusadeUnitCard { Id = "b1", UnitName = "B1", BattleScars = { Scar("s1"), Scar("s2"), Scar("s3") } } } });

        var battle = new BattleRecord
        {
            Status = BattleStatus.Mustered, PointsLimit = 1000, MissionName = "Take and Hold",
            Player1OrderId = "A", Player2OrderId = "B",
            UnitResults =
            {
                new BattleUnitResult { CardId = "a1", PlayerId = "p1", UnitName = "A1" },
                new BattleUnitResult { CardId = "a2", PlayerId = "p1", UnitName = "A2" },
                new BattleUnitResult { CardId = "b1", PlayerId = "p2", UnitName = "B1" }
            }
        };
        campaign.BattleHistory.Add(battle);
        await Campaigns.SaveCampaignAsync(campaign);
        return battle.Id;
    }

    // Awaited event helpers: they wait for the handler and the re-render, and surface handler exceptions.
    private static Task Next(IRenderedComponent<PostBattle> cut) => Click(cut, "#pb-next");
    private static Task Click(IRenderedComponent<PostBattle> cut, string selector) =>
        cut.Find(selector).ClickAsync(new MouseEventArgs());
    private static Task Change(IRenderedComponent<PostBattle> cut, string selector, object value) =>
        cut.Find(selector).ChangeAsync(new ChangeEventArgs { Value = value });

    [Fact]
    public async Task Walking_the_flow_completes_the_battle()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        // 1. Result
        await Change(cut, "input[name='winner'][value='p1']", "p1");
        await Change(cut, "#territory", "t1");
        await Next(cut);

        // 2. Casualties
        await Change(cut, "input[data-destroyed='a2']", true);
        await Change(cut, "input[data-destroyed='b1']", true);
        await Next(cut);

        // 3. Tallies
        await Change(cut, "input[data-tally='a1']", "3");
        await Change(cut, "input[data-mark='a1']", "a1");
        await Next(cut);

        // 4. Out of Action
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        await Click(cut, "button[data-ooa-fail='a2']");
        await Click(cut, "button[data-penalty-scar='a2']");
        await Change(cut, "select[data-scar='a2']", "Crippled");
        await Click(cut, "button[data-ooa-fail='b1']");
        cut.Markup.Should().Contain("struck from the Order of Battle");
        await Next(cut);

        // 5. Rank-ups: a1 goes 14 → 19, one honour owed
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        await Change(cut, "[data-rankup='a1'] .hp-type", CrusadeTraitTypes.BattleTrait);
        await Change(cut, "[data-rankup='a1'] .hp-trait", "Hardened");
        await Click(cut, "[data-rankup='a1'] .hp-add");
        await Next(cut);

        // 6. Review
        await Click(cut, "#pb-complete");

        cut.WaitForAssertion(() =>
            _ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("/war-room"));
        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        campaign.BattleHistory.Single().Status.Should().Be(BattleStatus.Completed);
        var orders = await new CrusadeManagerService(CreateFactory(),
            new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync();
        var a = orders.Single(o => o.Id == "A");
        a.Units.Single(u => u.Id == "a1").BattleHonours.Select(h => h.Name).Should().BeEquivalentTo(new[] { "Old", "Hardened" });
        a.Units.Single(u => u.Id == "a2").BattleScars.Should().ContainSingle(s => s.Name == "Crippled");
        orders.Single(o => o.Id == "B").Units.Should().BeEmpty();
    }

    [Fact]
    public async Task Double_click_on_complete_applies_the_sequence_once()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        await Change(cut, "input[name='winner'][value='p1']", "p1");
        for (var step = 0; step < 5; step++) await Next(cut);

        var button = cut.Find("#pb-complete");
        _gated.Close();
        var first  = button.ClickAsync(new MouseEventArgs());
        var second = button.ClickAsync(new MouseEventArgs());
        _gated.Release();
        await Task.WhenAll(first, second);

        _postBattleLog.Messages.Count(m => m.StartsWith("Completed Battle")).Should().Be(1);
        cut.FindAll("#pb-error").Should().BeEmpty();
        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        campaign.BattleHistory.Single(b => b.Id == id).Status.Should().Be(BattleStatus.Completed);
    }

    [Fact]
    public async Task Marking_a_second_unit_moves_the_mark()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Next(cut); await Next(cut);

        await Change(cut, "input[data-mark='a1']", "a1");
        await Change(cut, "input[data-mark='a2']", "a2");

        cut.Find("input[data-mark='a1']").HasAttribute("checked").Should().BeFalse();
        cut.Find("input[data-mark='a2']").HasAttribute("checked").Should().BeTrue();
    }

    [Fact]
    public async Task Completed_battle_shows_a_message()
    {
        var id = await SeedAsync();
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.BattleHistory.Single().Status = BattleStatus.Completed;
        await Campaigns.SaveCampaignAsync(campaign);

        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("already been completed"));
        cut.FindAll("#pb-next").Should().BeEmpty();
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _web.Dispose();
        Dispose();
    }
}
