using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Campaign;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CampaignEditorTableModeTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignEditorTableModeTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    private CampaignManagerService Fresh() => new(CreateFactory(), NullLogger<CampaignManagerService>.Instance);

    [Fact]
    public async Task Play_swaps_the_map_editor_for_table_mode_and_quick_notes_autosave()
    {
        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement("#ce-mode-play");
        cut.FindAll(".gt-root").Should().BeEmpty("the editor opens in Prep");

        await cut.Find("#ce-mode-play").ClickAsync(new MouseEventArgs());
        cut.FindAll(".gt-root").Should().HaveCount(1);
        cut.FindAll(".ce-form-panel").Should().BeEmpty("node editing is a Prep job");

        await cut.Find("#gt-capture-open").ClickAsync(new MouseEventArgs());
        await cut.Find("#gt-capture-input").InputAsync(new() { Value = "P1 bribed the Kroot" });
        await cut.Find("#gt-capture-form").SubmitAsync();

        var saved = await Fresh().LoadCampaignAsync();
        saved.GmNotebook.Should().ContainSingle(n => n.Title == "P1 bribed the Kroot" && n.IsInbox);

        await cut.Find("#ce-mode-prep").ClickAsync(new MouseEventArgs());
        cut.FindAll(".gt-root").Should().BeEmpty();
        cut.FindAll(".ce-form-panel").Should().HaveCount(1);
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
