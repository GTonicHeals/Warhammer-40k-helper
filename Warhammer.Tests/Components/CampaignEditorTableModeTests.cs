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

    [Fact]
    public async Task Prep_note_pin_opens_the_peek_and_Edit_leaves_the_edit_card()
    {
        var svc = Fresh();
        var campaign = await svc.LoadCampaignAsync();
        campaign.GmNotebook.Add(new Warhammer.Models.Campaign.GmNote
        {
            Title = "Vault of Tarsus", Summary = "Sealed since M39", Secret = "Holds a C'tan shard", X = 0.4, Y = 0.4,
        });
        await svc.SaveCampaignAsync(campaign);
        var noteId = campaign.GmNotebook.Single().Id;

        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement($".map-note-pin[data-node-id='{noteId}']");
        await cut.Find($".map-note-pin[data-node-id='{noteId}']").ClickAsync(new MouseEventArgs());

        var peek = cut.Find("#gnp-overlay");
        peek.TextContent.Should().Contain("Sealed since M39").And.Contain("Holds a C'tan shard");
        cut.FindAll("#gnp-reveal").Should().BeEmpty("Prep has no reveal flow");

        await cut.Find("#gnp-edit").ClickAsync(new MouseEventArgs());
        cut.FindAll("#gnp-overlay").Should().BeEmpty();
        cut.Find(".ce-editor-title-input").GetAttribute("value").Should().Be("Vault of Tarsus");
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
