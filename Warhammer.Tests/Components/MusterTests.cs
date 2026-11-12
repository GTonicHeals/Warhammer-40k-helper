using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Battle;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class MusterTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    private readonly GatedDbContextFactory _gated;

    public MusterTests()
    {
        var factory = _gated = new GatedDbContextFactory(CreateFactory());
        _ctx.Services.AddSingleton(factory);
        _ctx.Services.AddSingleton(sp => new CampaignManagerService(factory, NullLogger<CampaignManagerService>.Instance));
        _ctx.Services.AddSingleton(sp => new CrusadeManagerService(factory,
            sp.GetRequiredService<CampaignManagerService>(), NullLogger<CrusadeManagerService>.Instance));
    }

    private CampaignManagerService Campaigns => _ctx.Services.GetRequiredService<CampaignManagerService>();
    private CrusadeManagerService Crusades => _ctx.Services.GetRequiredService<CrusadeManagerService>();

    private async Task SeedAsync()
    {
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "A", ArmyName = "Sept", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "a1", UnitName = "Breachers", TotalPoints = 90 },
                      new CrusadeUnitCard { Id = "a2", UnitName = "Riptide", TotalPoints = 950 } } });
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "B", ArmyName = "Chapter", PlayerId = "p2",
            Units = { new CrusadeUnitCard { Id = "b1", UnitName = "Intercessors", TotalPoints = 90, IsCharacter = true },
                      new CrusadeUnitCard { Id = "b2", UnitName = "Redemptor", TotalPoints = 950 } } });
    }

    [Fact]
    public async Task Over_limit_blocks_begin_and_begin_creates_a_mustered_battle()
    {
        await SeedAsync();
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "B" });
        await cut.Find("#points-limit").ChangeAsync(new ChangeEventArgs { Value = "1000" });
        await cut.Find("input[data-card='a1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='a2']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='b1']").ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Find("#points1").TextContent.Should().Contain("1040 / 1000");
        cut.Find("#begin-battle").HasAttribute("disabled").Should().BeTrue();

        await cut.Find("input[data-card='a2']").ChangeAsync(new ChangeEventArgs { Value = false });
        cut.Find("#begin-battle").HasAttribute("disabled").Should().BeFalse();

        await cut.Find("#begin-battle").ClickAsync(new MouseEventArgs());

        cut.WaitForAssertion(() =>
            _ctx.Services.GetRequiredService<NavigationManager>().Uri.Should().MatchRegex("/battle/.+/post$"));
        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        var battle = campaign.BattleHistory.Should().ContainSingle().Subject;
        battle.Status.Should().Be(BattleStatus.Mustered);
        battle.PointsLimit.Should().Be(1000);
        battle.Player1OrderId.Should().Be("A");
        battle.Player2OrderId.Should().Be("B");
        battle.UnitResults.Select(r => r.CardId).Should().BeEquivalentTo(new[] { "a1", "b1" });
        battle.MissionName.Should().Be(MissionCatalog.PrimaryMissions[0]);
    }

    [Fact]
    public async Task Begin_hint_says_what_is_missing()
    {
        await SeedAsync();
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        cut.Find("#begin-hint").TextContent.Should().Contain("Order of Battle for each side");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "B" });
        cut.Find("#begin-hint").TextContent.Should().Contain("at least one unit");

        await cut.Find("input[data-card='a1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='b1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("#mission").ChangeAsync(new ChangeEventArgs { Value = "Other" });
        cut.Find("#begin-hint").TextContent.Should().Contain("mission name");

        await cut.Find("#mission-other").InputAsync(new ChangeEventArgs { Value = "Ambush" });
        cut.FindAll("#begin-hint").Should().BeEmpty();
    }

    [Fact]
    public async Task Picking_a_side_disables_that_players_orders_on_the_other_side()
    {
        await SeedAsync();
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "C", ArmyName = "Second", PlayerId = "p1" });
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });

        cut.Find("#order2 option[value='A']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#order2 option[value='C']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("#order2 option[value='B']").HasAttribute("disabled").Should().BeFalse();
        cut.Find("#order1 option[value='C']").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public async Task Same_order_on_both_sides_blocks_begin_and_missing_character_warns()
    {
        await SeedAsync();
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "A" });

        cut.Find("#begin-battle").HasAttribute("disabled").Should().BeTrue();
        cut.FindAll(".muster-no-character").Should().NotBeEmpty();
    }

    [Fact]
    public async Task Two_orders_of_the_same_player_block_begin()
    {
        await SeedAsync();
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "C", ArmyName = "Second", PlayerId = "p1",
            Units = { new CrusadeUnitCard { Id = "c1", UnitName = "Strike", TotalPoints = 90 } } });
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "C" });
        await cut.Find("input[data-card='a1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='c1']").ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Find("#begin-battle").HasAttribute("disabled").Should().BeTrue();
        cut.Markup.Should().Contain("different players");
    }

    [Fact]
    public async Task Order_without_a_player_blocks_begin()
    {
        await SeedAsync();
        await Crusades.SaveCrusadeAsync(new OrderOfBattle { Id = "L", ArmyName = "Legacy", PlayerId = "",
            Units = { new CrusadeUnitCard { Id = "l1", UnitName = "Old", TotalPoints = 90 } } });
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");

        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "L" });
        await cut.Find("input[data-card='a1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='l1']").ChangeAsync(new ChangeEventArgs { Value = true });

        cut.Find("#begin-battle").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task Double_click_on_begin_creates_one_battle()
    {
        await SeedAsync();
        var cut = _ctx.RenderComponent<Muster>();
        cut.WaitForElement("#order1");
        await cut.Find("#order1").ChangeAsync(new ChangeEventArgs { Value = "A" });
        await cut.Find("#order2").ChangeAsync(new ChangeEventArgs { Value = "B" });
        await cut.Find("input[data-card='a1']").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find("input[data-card='b1']").ChangeAsync(new ChangeEventArgs { Value = true });

        var button = cut.Find("#begin-battle");
        _gated.Close();
        var first  = button.ClickAsync(new MouseEventArgs());
        var second = button.ClickAsync(new MouseEventArgs());
        _gated.Release();
        await Task.WhenAll(first, second);

        var campaign = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        campaign.BattleHistory.Should().ContainSingle();
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
