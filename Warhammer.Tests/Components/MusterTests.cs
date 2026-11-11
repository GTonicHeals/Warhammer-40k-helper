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

    public MusterTests()
    {
        var factory = CreateFactory();
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

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
