using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Warhammer.Components.UI;
using Warhammer.Models.Campaign;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class GmNotePeekTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private int _closed, _edited, _revealed;

    public GmNotePeekTests() => _ctx.JSInterop.Mode = JSRuntimeMode.Loose;

    private static GmNote Note() => new()
    {
        Title = "Kroot switch sides", Summary = "Shaper offers his warband",
        Content = "Trigger: P1 loses here", Secret = "Paid by an Inquisitor", Tags = "twist, kroot",
    };

    private IRenderedComponent<GmNotePeek> Render(GmNote note, bool withReveal = false, bool handleEscape = true) =>
        _ctx.RenderComponent<GmNotePeek>(p =>
        {
            p.Add(x => x.Note, note)
             .Add(x => x.LocationName, "Kroot Encampment")
             .Add(x => x.HandleEscape, handleEscape)
             .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => _closed++))
             .Add(x => x.OnEdit, EventCallback.Factory.Create(this, () => _edited++));
            if (withReveal) p.Add(x => x.OnReveal, EventCallback.Factory.Create(this, () => _revealed++));
        });

    [Fact]
    public void Shows_the_whole_note_secret_included()
    {
        var text = Render(Note()).Find("#gnp-overlay").TextContent;

        text.Should().Contain("Kroot switch sides")
            .And.Contain("Shaper offers his warband")
            .And.Contain("Trigger: P1 loses here")
            .And.Contain("Paid by an Inquisitor")
            .And.Contain("Kroot Encampment")
            .And.Contain("#twist").And.Contain("#kroot")
            .And.Contain("GM only");
    }

    [Fact]
    public void Close_backdrop_and_escape_close_it_without_changing_the_note()
    {
        var note = Note();
        var cut = Render(note);

        cut.Find("#gnp-close").Click();
        cut.Find("#gnp-overlay").Click();
        cut.Find(".gnp-card").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        _closed.Should().Be(3);
        note.IsPublic.Should().BeFalse();
        note.RevealedAt.Should().BeNull();
    }

    [Fact]
    public void Escape_is_ignored_when_the_host_handles_it()
    {
        Render(Note(), handleEscape: false).Find(".gnp-card").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        _closed.Should().Be(0);
    }

    [Fact]
    public void Edit_raises_OnEdit()
    {
        Render(Note()).Find("#gnp-edit").Click();
        _edited.Should().Be(1);
    }

    [Fact]
    public void Reveal_button_only_appears_when_the_host_offers_it()
    {
        Render(Note()).FindAll("#gnp-reveal").Should().BeEmpty();

        var cut = Render(Note(), withReveal: true);
        cut.Find("#gnp-reveal").TextContent.Should().Contain("Reveal to players");
        cut.Find("#gnp-reveal").Click();
        _revealed.Should().Be(1);
    }

    [Fact]
    public void Public_note_offers_show_instead_of_reveal()
    {
        var note = Note();
        note.IsPublic = true;
        var cut = Render(note, withReveal: true);

        cut.Find("#gnp-reveal").TextContent.Should().Contain("Show to players");
        cut.Find("#gnp-overlay").TextContent.Should().Contain("Public");
    }

    [Fact]
    public void Empty_note_renders_a_placeholder_and_no_empty_sections()
    {
        var cut = Render(new GmNote { Title = "" });

        cut.Find("h2").TextContent.Should().Be("Untitled note");
        cut.FindAll(".gnp-secret").Should().BeEmpty();
        cut.FindAll(".gnp-body").Should().BeEmpty();
    }

    public void Dispose() => _ctx.Dispose();
}
