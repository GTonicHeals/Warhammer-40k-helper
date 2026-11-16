using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Warhammer.Components.Pages.Campaign;
using Warhammer.Tests.TestInfrastructure;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class CampaignEditorRequisitionsTests : SqliteTestBase, IDisposable
{
    private readonly TestContext _ctx = new();

    public CampaignEditorRequisitionsTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddSingleton(new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance));
    }

    [Fact]
    public async Task Requisitions_panel_opens_from_the_header_and_saves_with_the_campaign()
    {
        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement("#req-open");
        cut.FindAll("#req-editor").Should().BeEmpty();

        await cut.Find("#req-open").ClickAsync(new MouseEventArgs());
        cut.FindAll("#req-editor [data-req]").Should().HaveCount(7);

        await cut.Find("#req-add").ClickAsync(new MouseEventArgs());
        await cut.Find("#campaign-save").ClickAsync(new MouseEventArgs());

        var saved = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        saved.Requisitions.Should().HaveCount(8);

        await cut.Find("#req-editor .btn-close").ClickAsync(new MouseEventArgs());
        cut.FindAll("#req-editor").Should().BeEmpty();
    }

    [Fact]
    public async Task Gm_notes_autosave_leaves_unconfirmed_requisition_edits_unsaved()
    {
        var cut = _ctx.RenderComponent<CampaignEditor>();
        cut.WaitForElement("#req-open");
        await cut.Find("#req-open").ClickAsync(new MouseEventArgs());
        var first = cut.Find("[data-req-cost]");
        var storedCost = int.Parse(first.GetAttribute("value")!);
        await first.ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = (storedCost + 4).ToString() });

        // A GM Notes change autosaves the campaign without the Save button.
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "GM Notes").ClickAsync(new MouseEventArgs());
        await cut.FindAll("button").First(b => b.TextContent.Contains("Add New Note")).ClickAsync(new MouseEventArgs());

        var saved = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        saved.GmNotebook.Should().HaveCount(1, "the note itself is autosaved");
        saved.Requisitions[0].Cost.Should().Be(storedCost, "the requisition edit was not confirmed with Save");
        cut.FindComponent<Warhammer.Components.UI.UnsavedChangesGuard>().Instance.IsDirty.Should().BeTrue();

        await cut.Find("#campaign-save").ClickAsync(new MouseEventArgs());
        saved = await new CampaignManagerService(CreateFactory(), NullLogger<CampaignManagerService>.Instance).LoadCampaignAsync();
        saved.Requisitions[0].Cost.Should().Be(storedCost + 4);
        cut.FindComponent<Warhammer.Components.UI.UnsavedChangesGuard>().Instance.IsDirty.Should().BeFalse();
    }

    void IDisposable.Dispose()
    {
        _ctx.Dispose();
        base.Dispose();
    }
}
