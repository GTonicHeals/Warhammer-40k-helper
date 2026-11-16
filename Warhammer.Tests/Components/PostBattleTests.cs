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

    private async Task<string> SeedAsync(Action<OrderOfBattle>? tweakA = null)
    {
        var campaign = await Campaigns.LoadCampaignAsync();
        campaign.MapNodes.Add(new Territory { Id = "t1", Name = "Hab" });
        var orderA = new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "A1", Xp = 14,
                          BattleHonours = { new AppliedTrait { Name = "Old", Type = CrusadeTraitTypes.BattleTrait } } },
                      new CrusadeUnitCard { Id = "a2", UnitName = "A2", XpEligible = false },
                      new CrusadeUnitCard { Id = "a3", UnitName = "A3" } } };
        tweakA?.Invoke(orderA);
        await Crusades.SaveCrusadeAsync(orderA);
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
                new BattleUnitResult { CardId = "a3", PlayerId = "p1", UnitName = "A3" },
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
    public async Task Out_of_action_explains_why_next_is_disabled_and_review_groups_by_side()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        cut.FindAll("#pb-next-hint").Should().BeEmpty();

        await Next(cut);
        await Change(cut, "input[data-destroyed='a2']", true);
        await Next(cut);
        await Next(cut);
        await Next(cut);

        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#pb-next-hint").TextContent.Should().Contain("Pass or Fail");

        await Click(cut, "[data-ooa-pass='a2']");
        cut.FindAll("#pb-next-hint").Should().BeEmpty();

        // No unit gained a rank, so Rank-ups is skipped.
        await Next(cut);
        cut.Find("#pb-step").GetAttribute("data-step").Should().Be("7");
        cut.Find("[data-goto='6']").ClassList.Should().Contain("is-skipped");
        cut.FindAll(".pb-review-side").Should().HaveCount(2);
    }

    [Fact]
    public async Task Winner_options_are_labelled_buttons_and_pick_the_winner()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        var labels = cut.FindAll("label.pb-winner-opt");
        labels.Should().HaveCount(3);
        foreach (var label in labels)
        {
            label.TextContent.Trim().Should().NotBeEmpty();
            cut.Find($"#{label.GetAttribute("for")}").GetAttribute("type").Should().Be("radio");
        }
        cut.Find("label[for='winner-draw']").TextContent.Should().Contain("Draw");

        await Change(cut, "#winner-p2", "p2");
        cut.Find("#winner-p2").HasAttribute("checked").Should().BeTrue();
        cut.Find("#winner-draw").HasAttribute("checked").Should().BeFalse();
    }

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

        // 3. Damage: nothing to enter
        await Next(cut);

        // 4. Tallies
        await Change(cut, "input[data-tally='a1']", "3");
        await Change(cut, "input[data-mark='a1']", "a1");
        await Next(cut);

        // 5. Out of Action
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        await Click(cut, "button[data-ooa-fail='a2']");
        await Click(cut, "button[data-penalty-scar='a2']");
        cut.Find("button[data-scar='a2']").TextContent.Should().Contain("Slow.");
        await Click(cut, "button[data-scar='a2'][data-scar-name='Crippled']");
        await Click(cut, "button[data-ooa-fail='b1']");
        cut.Markup.Should().Contain("struck from the Order of Battle");
        await Next(cut);

        // 6. Rank-ups: a1 goes 14 → 19, one honour owed
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeTrue();
        await Change(cut, "[data-rankup='a1'] .hp-type", CrusadeTraitTypes.BattleTrait);
        await Change(cut, "[data-rankup='a1'] .hp-trait", "Hardened");
        await Click(cut, "[data-rankup='a1'] .hp-add");
        await Next(cut);

        // 7. Review
        await Click(cut, "#pb-complete");

        cut.WaitForAssertion(() => cut.Find("#pb-done").TextContent.Should().Contain("Battle recorded"));
        cut.Find("#pb-done").TextContent.Should().Contain("Blooded → Battle-hardened");
        cut.FindAll("#pb-complete").Should().BeEmpty();
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
        // Nothing destroyed and nobody promoted: Out of Action and Rank-ups are skipped.
        for (var step = 0; step < 4; step++) await Next(cut);

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
        await Next(cut); await Next(cut); await Next(cut);

        await Change(cut, "input[data-mark='a1']", "a1");
        await Change(cut, "input[data-mark='a3']", "a3");

        cut.Find("input[data-mark='a1']").HasAttribute("checked").Should().BeFalse();
        cut.Find("input[data-mark='a3']").HasAttribute("checked").Should().BeTrue();
        cut.Find("input[data-mark='a2']").HasAttribute("disabled").Should().BeTrue("a unit that gains no XP cannot be marked");

        // Tapping the marked unit again clears the mark.
        await Change(cut, "input[data-mark='a3']", "a3");
        cut.Find("input[data-mark='a3']").HasAttribute("checked").Should().BeFalse();
    }

    [Fact]
    public async Task Progress_is_saved_on_every_step_and_survives_a_reload()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        await Change(cut, "#vp1", "40");
        await Next(cut);
        await Change(cut, "input[data-destroyed='a3']", true);
        await Next(cut);
        cut.Find("#pb-saved").TextContent.Should().Contain("Progress saved");

        var reloaded = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        reloaded.WaitForElement("#pb-next");
        reloaded.Find("#vp1").GetAttribute("value").Should().Be("40");
        await Next(reloaded);
        reloaded.Find("input[data-destroyed='a3']").HasAttribute("checked").Should().BeTrue();

        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        campaign.BattleHistory.Single().Status.Should().Be(BattleStatus.Mustered);
    }

    [Fact]
    public async Task Victory_points_pick_the_winner_and_a_draw_captures_no_territory()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        cut.Find("#territory").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#pb-territory-draw").TextContent.Should().Contain("Draw");

        await Change(cut, "#vp2", "55");
        cut.Find("#winner-p2").HasAttribute("checked").Should().BeTrue();
        cut.Find("#territory").HasAttribute("disabled").Should().BeFalse();
        await Change(cut, "#territory", "t1");

        // Overruling the score is allowed but flagged.
        await Change(cut, "#winner-p1", "p1");
        cut.Find("#pb-vp-mismatch").TextContent.Should().Contain("does not match");

        // Back to a Draw: the territory is dropped when the step is left.
        await Change(cut, "#winner-draw", "");
        await Next(cut);
        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        campaign.BattleHistory.Single().CapturedTerritoryId.Should().BeNull();
    }

    [Fact]
    public async Task Kill_buttons_update_the_tally_and_the_xp_preview()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Next(cut); await Next(cut); await Next(cut);

        cut.Find("[data-xp='a3']").TextContent.Should().Be("+1 XP");
        cut.Find("[data-kill-minus='a3']").HasAttribute("disabled").Should().BeTrue();
        for (var i = 0; i < 3; i++) await Click(cut, "[data-kill-plus='a3']");

        cut.Find("input[data-tally='a3']").GetAttribute("value").Should().Be("3");
        cut.Find("[data-xp='a3']").TextContent.Should().Be("+2 XP");

        await Click(cut, "[data-kill-minus='a3']");
        cut.Find("[data-xp='a3']").TextContent.Should().Be("+1 XP");
    }

    [Fact]
    public async Task Step_list_jumps_back_to_an_earlier_step()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        cut.Find("[data-goto='3']").HasAttribute("disabled").Should().BeTrue();

        await Next(cut); await Next(cut);
        await Click(cut, "[data-goto='1']");
        cut.Find("#pb-step").GetAttribute("data-step").Should().Be("1");

        await Click(cut, "[data-goto='3']");
        cut.Find("#pb-step").GetAttribute("data-step").Should().Be("3");
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

    // ── Damage step (attrition) ────────────────────────────────────────
    private static void Squad(OrderOfBattle a) { a.Units[0].ModelCount = 10; a.Units[0].WoundsPerModel = 2; }

    private async Task<IRenderedComponent<PostBattle>> AtDamageStepAsync(string id)
    {
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Next(cut); await Next(cut);
        cut.Find("#pb-step").GetAttribute("data-step").Should().Be("3");
        return cut;
    }

    private async Task<CrusadeUnitCard> CardAsync(string cardId) =>
        (await new CrusadeManagerService(CreateFactory(),
            new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync()).SelectMany(o => o.Units).Single(u => u.Id == cardId);

    [Fact]
    public async Task Step_titles_include_damage_after_casualties()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        cut.FindAll(".pb-steplabel").Select(e => e.TextContent)
            .Should().Equal("Result", "Casualties", "Damage", "Tallies", "Out of Action", "Rank-ups", "Review");
    }

    [Fact]
    public async Task Damage_step_starts_from_the_cards_condition_and_needs_no_input()
    {
        var id = await SeedAsync(a => { Squad(a); a.Units[0].ModelsLost = 2; });
        var cut = await AtDamageStepAsync(id);

        cut.Find("[data-models-lost='a1']").TextContent.Should().Be("2");
        cut.Find("[data-damage='a1']").TextContent.Should().Contain("8/10 models");
        cut.Find("[data-damage='a3']").TextContent.Should().Contain("Full strength");
        cut.Find("#pb-next").HasAttribute("disabled").Should().BeFalse();

        await Next(cut); await Next(cut);
        cut.FindAll("[data-damage-change]").Should().BeEmpty();
        await Click(cut, "#pb-complete");
        cut.WaitForElement("#pb-done");
        (await CardAsync("a1")).ModelsLost.Should().Be(2);
    }

    [Fact]
    public async Task Damage_steppers_stop_at_the_limits()
    {
        var cut = await AtDamageStepAsync(await SeedAsync(Squad));

        cut.Find("[data-models-minus='a1']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-wounds-minus='a1']").HasAttribute("disabled").Should().BeTrue();
        for (var i = 0; i < 9; i++) await Click(cut, "[data-models-plus='a1']");
        cut.Find("[data-models-lost='a1']").TextContent.Should().Be("9");
        cut.Find("[data-models-plus='a1']").HasAttribute("disabled").Should().BeTrue();

        await Click(cut, "[data-wounds-plus='a1']");
        cut.Find("[data-wounds-lost='a1']").TextContent.Should().Be("1");
        cut.Find("[data-wounds-plus='a1']").HasAttribute("disabled").Should().BeTrue();

        await Click(cut, "[data-models-minus='a1']");
        cut.Find("[data-models-lost='a1']").TextContent.Should().Be("8");
    }

    [Fact]
    public async Task Single_model_unit_has_no_models_to_lose()
    {
        var cut = await AtDamageStepAsync(await SeedAsync(a => { a.Units[0].ModelCount = 1; a.Units[0].WoundsPerModel = 14; }));

        cut.Find("[data-models-plus='a1']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("[data-wounds-plus='a1']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Unknown_size_allows_free_entry()
    {
        var cut = await AtDamageStepAsync(await SeedAsync());

        for (var i = 0; i < 12; i++) await Click(cut, "[data-models-plus='a1']");
        for (var i = 0; i < 12; i++) await Click(cut, "[data-wounds-plus='a1']");

        cut.Find("[data-models-lost='a1']").TextContent.Should().Be("12");
        cut.Find("[data-models-plus='a1']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("[data-wounds-plus='a1']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Entered_damage_reaches_the_review_and_the_card()
    {
        var cut = await AtDamageStepAsync(await SeedAsync(Squad));
        for (var i = 0; i < 3; i++) await Click(cut, "[data-models-plus='a1']");
        await Next(cut); await Next(cut);

        cut.Find("#pb-step").GetAttribute("data-step").Should().Be("7");
        cut.Find("[data-damage-change='a1']").TextContent.Should().Contain("7/10 models");
        cut.FindAll("[data-damage-change='a3']").Should().BeEmpty();

        await Click(cut, "#pb-complete");
        cut.WaitForElement("#pb-done");
        cut.Find("#pb-done [data-damage-change='a1']").TextContent.Should().Contain("7/10 models");
        (await CardAsync("a1")).ModelsLost.Should().Be(3);
    }

    [Fact]
    public async Task Repairing_a_unit_in_the_damage_step_reads_as_full_strength()
    {
        var cut = await AtDamageStepAsync(await SeedAsync(a => { Squad(a); a.Units[0].ModelsLost = 1; }));
        await Click(cut, "[data-models-minus='a1']");
        await Next(cut); await Next(cut);

        cut.Find("[data-damage-change='a1']").TextContent.Should().Contain("Full strength");
    }

    [Fact]
    public async Task Destroyed_units_are_listed_in_the_damage_step()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Next(cut);
        await Change(cut, "input[data-destroyed='a2']", true);
        await Next(cut);

        cut.Find("[data-damage='a2']").TextContent.Should().Contain("Destroyed");
        cut.Find("[data-damage='a3']").TextContent.Should().NotContain("Destroyed");
        cut.FindAll("[data-models-plus='a2']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Damage_survives_a_reload()
    {
        var id = await SeedAsync(Squad);
        var cut = await AtDamageStepAsync(id);
        await Click(cut, "[data-models-plus='a1']");
        await Click(cut, "[data-models-plus='a1']");
        await Next(cut);

        var reloaded = await AtDamageStepAsync(id);
        reloaded.Find("[data-models-lost='a1']").TextContent.Should().Be("2");
    }

    [Fact]
    public async Task Out_of_action_no_longer_promises_an_unharmed_return()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Next(cut);
        await Change(cut, "input[data-destroyed='a2']", true);
        await Next(cut); await Next(cut); await Next(cut);

        cut.Find("#pb-step").TextContent.Should().Contain("takes no penalty").And.NotContain("unharmed");
    }

    // ── Requisition Points awarded by the Game Master ──────────────────
    private async Task<int> PointsAsync(string orderId) =>
        (await new CrusadeManagerService(CreateFactory(),
            new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance),
            NullLogger<CrusadeManagerService>.Instance).LoadAllCrusadesAsync()).Single(o => o.Id == orderId).RequisitionPoints;

    [Fact]
    public async Task Result_step_takes_the_requisition_award()
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        cut.Find("#rp1").GetAttribute("value").Should().Be("1");
        cut.Find("#rp2").GetAttribute("value").Should().Be("1");

        await Change(cut, "#rp1", "3");
        await Change(cut, "#rp2", "0");
        for (var step = 0; step < 4; step++) await Next(cut);

        cut.Find("[data-side='p1']").TextContent.Should().Contain("5 → 8");
        cut.Find("[data-side='p2']").TextContent.Should().Contain("5 → 5");
        cut.Find("[data-side='p2']").TextContent.Should().NotContain("already at the maximum");

        await Click(cut, "#pb-complete");
        cut.WaitForElement("#pb-done");
        (await PointsAsync("A")).Should().Be(8);
        (await PointsAsync("B")).Should().Be(5);
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("abc", "0")]
    [InlineData("-4", "0")]
    [InlineData("25", "10")]
    public async Task Requisition_award_is_kept_between_zero_and_ten(string typed, string shown)
    {
        var id = await SeedAsync();
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");

        await Change(cut, "#rp1", typed);

        cut.Find("#rp1").GetAttribute("value").Should().Be(shown);
    }

    [Fact]
    public async Task Award_past_the_maximum_shows_the_capped_figure()
    {
        var id = await SeedAsync(a => a.RequisitionPoints = 9);
        var cut = _ctx.RenderComponent<PostBattle>(p => p.Add(x => x.Id, id));
        cut.WaitForElement("#pb-next");
        await Change(cut, "#rp1", "3");
        for (var step = 0; step < 4; step++) await Next(cut);

        cut.Find("[data-side='p1']").TextContent.Should().Contain("9 → 10");
    }

    [Fact]
    public async Task Stepping_back_to_the_cards_own_value_is_not_an_entry()
    {
        var id = await SeedAsync(a => { Squad(a); a.Units[0].ModelsLost = 3; });
        var cut = await AtDamageStepAsync(id);

        await Click(cut, "[data-models-plus='a1']");
        await Next(cut);
        var saved = (await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync())
            .BattleHistory.Single().UnitResults.Single(r => r.CardId == "a1");
        (saved.ModelsLostBefore, saved.ModelsLostAfter).Should().Be((3, 4));

        await Click(cut, "[data-goto='3']");
        await Click(cut, "[data-models-minus='a1']");
        await Next(cut);
        saved = (await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync())
            .BattleHistory.Single().UnitResults.Single(r => r.CardId == "a1");
        (saved.ModelsLostBefore, saved.ModelsLostAfter).Should().Be(((int?)null, (int?)null));
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _web.Dispose();
        Dispose();
    }
}
