using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
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

    public PostBattleTests()
    {
        _web.WriteDataFile("crusade_traits.json", TraitsJson);
        var env = _web.BuildMockEnv().Object;
        var factory = CreateFactory();
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new PostBattleService(sp.GetRequiredService<CampaignManagerService>(),
            sp.GetRequiredService<CrusadeManagerService>(), NullLogger<PostBattleService>.Instance));
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

    private static void Next(IRenderedComponent<PostBattle> cut) => cut.Find("#pb-next").Click();

    [Fact]
    public async Task Walking_the_flow_completes_the_battle()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        // 1. Result
        cut.Find("input[name='winner'][value='p1']").Change("p1");
        cut.Find("#territory").Change("t1");
        Next(cut);

        // 2. Casualties
        cut.Find("input[data-destroyed='a2']").Change(true);
        cut.Find("input[data-destroyed='b1']").Change(true);
        Next(cut);

        // 3. Tallies
        cut.Find("input[data-tally='a1']").Change("3");
        cut.Find("input[data-mark='a1']").Change("a1");
        Next(cut);

        // 4. Out of Action
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button[data-ooa-fail='a2']").Click();
        cut.Find("button[data-penalty-scar='a2']").Click();
        cut.Find("select[data-scar='a2']").Change("Crippled");
        cut.Find("button[data-ooa-fail='b1']").Click();
        cut.Markup.Should().Contain("struck from the Order of Battle");
        Next(cut);

        // 5. Rank-ups: a1 goes 14 → 19, one honour owed
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-rankup='a1'] .hp-type").Change(CrusadeTraitTypes.BattleTrait);
        cut.Find("[data-rankup='a1'] .hp-trait").Change("Hardened");
        cut.Find("[data-rankup='a1'] .hp-add").Click();
        Next(cut);

        // 6. Review
        cut.Find("#pb-complete").Click();

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
    public async Task Marking_a_second_unit_moves_the_mark()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        Next(cut); Next(cut);

        cut.Find("input[data-mark='a1']").Change("a1");
        cut.Find("input[data-mark='a2']").Change("a2");

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
