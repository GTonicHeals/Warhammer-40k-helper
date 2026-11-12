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

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        _webRoot.Dispose();
        base.Dispose();
    }
}
