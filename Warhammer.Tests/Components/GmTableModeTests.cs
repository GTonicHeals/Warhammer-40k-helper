using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Warhammer.Components.UI;
using Warhammer.Models.Campaign;
using CampaignModel = Warhammer.Models.Campaign.Campaign;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class GmTableModeTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private int _saves;

    public GmTableModeTests() => _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

    private static CampaignModel Campaign() => new()
    {
        Players  = { new CampaignPlayer { Id = "p1", Name = "Nico" } },
        MapNodes =
        {
            new Territory { Id = "t1", Name = "Kroot Encampment", X = 0.3, Y = 0.4, ControllingPlayerId = "p1" },
            new Territory { Id = "t2", Name = "Hive Tarsus", X = 0.6, Y = 0.2 },
        },
        GmNotebook =
        {
            new GmNote { Id = "a", Title = "Kroot switch sides", Summary = "Shaper offers his warband", Secret = "Paid by an Inquisitor",
                         Content = "Trigger: P1 loses here", Tags = "twist, kroot", NodeId = "t1", IsStarred = true },
            new GmNote { Id = "b", Title = "Governor Vorne", Summary = "Hoards promethium", Tags = "npc", NodeId = "t2", IsStarred = true },
            new GmNote { Id = "c", Title = "Vault", Tags = "twist" },
        }
    };

    private IRenderedComponent<GmTableMode> Render(CampaignModel c) =>
        _ctx.RenderComponent<GmTableMode>(p => p
            .Add(x => x.Campaign, c)
            .Add(x => x.OnNotesChanged, EventCallback.Factory.Create(this, () => _saves++)));

    private static IEnumerable<string> ListedIds(IRenderedComponent<GmTableMode> cut) =>
        cut.FindAll("#gt-list .gt-card").Select(e => e.GetAttribute("data-note")!);

    [Fact]
    public void Opens_on_the_starred_session_list()
    {
        ListedIds(Render(Campaign())).Should().Equal("a", "b");
    }

    [Fact]
    public void Search_finds_notes_by_any_field_and_marks_the_match()
    {
        var cut = Render(Campaign());
        cut.Find("#gt-search").Input("inquisitor");

        ListedIds(cut).Should().Equal("a");
        cut.FindAll("#gt-list mark").Should().BeEmpty("the secret matched, and the card does not show it");

        cut.Find("#gt-search").Input("vorne");
        cut.Find("#gt-list mark").TextContent.Should().Be("Vorne");
    }

    [Fact]
    public void Tag_chips_filter_across_all_notes()
    {
        var cut = Render(Campaign());
        cut.Find(".gt-chip[data-tag='twist']").Click();
        ListedIds(cut).Should().BeEquivalentTo(["a", "c"]);

        cut.Find(".gt-chip[data-tag='kroot']").Click();
        ListedIds(cut).Should().Equal("a");
    }

    [Fact]
    public void Tapping_a_location_lists_the_notes_about_it()
    {
        var cut = Render(Campaign());
        cut.Find(".map-node[data-node-id='t1']").Click();

        cut.Find("#gt-side h2").TextContent.Should().Contain("Kroot Encampment");
        cut.FindAll("#gt-side .gt-card").Select(e => e.GetAttribute("data-note")).Should().Equal("a");
        cut.Find("#gt-side").TextContent.Should().Contain("Nico");
    }

    [Fact]
    public void Quick_note_goes_to_the_inbox_linked_to_the_selected_location()
    {
        var c = Campaign();
        var cut = Render(c);
        cut.Find(".map-node[data-node-id='t2']").Click();

        cut.Find("#gt-capture-open").Click();
        cut.Find("#gt-capture-input").Input("P2 wants to fortify");
        cut.Find("#gt-capture-form").Submit();

        var note = c.GmNotebook[0];
        note.Title.Should().Be("P2 wants to fortify");
        note.IsInbox.Should().BeTrue();
        note.NodeId.Should().Be("t2");
        _saves.Should().Be(1);
        cut.FindAll("#gt-capture-form").Should().BeEmpty();
    }

    [Fact]
    public void Reveal_makes_the_note_public_and_shows_it_without_the_secret()
    {
        var c = Campaign();
        var cut = Render(c);
        cut.Find("#gt-list .gt-card[data-note='a']").Click();
        cut.Find("#gt-side").TextContent.Should().NotContain("Paid by an Inquisitor", "the GM-only part starts hidden");

        cut.Find("#gt-reveal").Click();

        c.GmNotebook[0].IsPublic.Should().BeTrue();
        c.GmNotebook[0].RevealedAt.Should().NotBeNull();
        _saves.Should().Be(1);
        var overlay = cut.Find("#gt-reveal-overlay");
        overlay.TextContent.Should().Contain("Shaper offers his warband");
        overlay.TextContent.Should().NotContain("Paid by an Inquisitor");
    }

    [Fact]
    public void Delete_offers_an_undo_that_puts_the_note_back()
    {
        var c = Campaign();
        var cut = Render(c);
        cut.Find("#gt-list .gt-card[data-note='b']").Click();
        cut.Find("#gt-edit").Click();
        cut.Find("#gt-delete").Click();

        c.GmNotebook.Select(n => n.Id).Should().Equal("a", "c");

        cut.Find("#gt-undo").Click();
        c.GmNotebook.Select(n => n.Id).Should().Equal("a", "b", "c");
        _saves.Should().Be(2);
    }

    private static CampaignModel CampaignWithPin()
    {
        var c = Campaign();
        c.GmNotebook[0].X = 0.5;
        c.GmNotebook[0].Y = 0.5;
        return c;
    }

    [Fact]
    public void Tapping_a_note_pin_opens_the_peek_with_the_secret_and_reveals_nothing()
    {
        var c = CampaignWithPin();
        var cut = Render(c);
        cut.Find(".map-note-pin[data-node-id='a']").Click();

        var peek = cut.Find("#gnp-overlay");
        peek.TextContent.Should().Contain("Paid by an Inquisitor").And.Contain("Kroot Encampment");
        c.GmNotebook[0].IsPublic.Should().BeFalse();
        _saves.Should().Be(0);
        cut.Find("#gt-side .gt-title").TextContent.Should().Be("Kroot switch sides", "the side panel follows the pin");
    }

    [Fact]
    public async Task Escape_closes_the_peek_but_keeps_the_note_selected()
    {
        var cut = Render(CampaignWithPin());
        cut.Find(".map-note-pin[data-node-id='a']").Click();

        await cut.InvokeAsync(() => cut.Instance.OnHotkey("escape"));

        cut.FindAll("#gnp-overlay").Should().BeEmpty();
        cut.Find("#gt-side .gt-title").TextContent.Should().Be("Kroot switch sides");
    }

    [Fact]
    public void Reveal_from_the_peek_swaps_it_for_the_reveal_card()
    {
        var c = CampaignWithPin();
        var cut = Render(c);
        cut.Find(".map-note-pin[data-node-id='a']").Click();
        cut.Find("#gnp-reveal").Click();

        cut.FindAll("#gnp-overlay").Should().BeEmpty();
        cut.Find("#gt-reveal-overlay").TextContent.Should().NotContain("Paid by an Inquisitor");
        c.GmNotebook[0].IsPublic.Should().BeTrue();
    }

    [Fact]
    public void Edit_from_the_peek_opens_the_note_editor()
    {
        var cut = Render(CampaignWithPin());
        cut.Find(".map-note-pin[data-node-id='a']").Click();
        cut.Find("#gnp-edit").Click();

        cut.FindAll("#gnp-overlay").Should().BeEmpty();
        cut.FindAll("#gt-f-title").Should().HaveCount(1);
    }

    [Fact]
    public void Star_toggles_session_membership()
    {
        var c = Campaign();
        var cut = Render(c);
        cut.Find(".gt-star[data-star='b']").Click();

        c.GmNotebook[1].IsStarred.Should().BeFalse();
        ListedIds(cut).Should().Equal("a");
    }

    public void Dispose() => _ctx.Dispose();
}
