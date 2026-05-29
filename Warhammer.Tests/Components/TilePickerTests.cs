using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Warhammer.Components.UI;
using Warhammer.Models.Campaign;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

/// <summary>
/// Tests for the generic TilePicker component used by CampaignEditor for the
/// node-type, terrain, and attachment grids. Verifies rendering, selected-state
/// styling, click-to-callback wiring, and that the generic TValue works for
/// both reference-type and value-type parameters.
/// </summary>
public class TilePickerTests : TestContext
{
    private static List<TilePickerOption<string>> StringOptions() => new()
    {
        new("alpha", "Alpha", "bi-a"),
        new("beta",  "Beta",  "bi-b"),
        new("gamma", "Gamma", null),
    };

    // ═════════════════════════════════════════════════════════════════════════
    // Rendering
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Renders_OneButtonPerOption()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        cut.FindAll("button.tile").Should().HaveCount(3);
    }

    [Fact]
    public void AppliesContainerCssClass_ToOuterDiv()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "my-custom-grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        cut.Find("div.my-custom-grid").Should().NotBeNull();
    }

    [Fact]
    public void EmptyOptions_RendersContainerWithNoButtons()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   new List<TilePickerOption<string>>())
            .Add(x => x.Selected,  "anything")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        cut.Find("div.grid").Should().NotBeNull();
        cut.FindAll("button.tile").Should().BeEmpty();
    }

    [Fact]
    public void RendersLabel_ForEachOption()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        var tiles = cut.FindAll("button.tile");
        tiles[0].TextContent.Should().Contain("Alpha");
        tiles[1].TextContent.Should().Contain("Beta");
        tiles[2].TextContent.Should().Contain("Gamma");
    }

    [Fact]
    public void RendersIcon_OnlyWhenOptionHasIcon()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        var tiles = cut.FindAll("button.tile");
        tiles[0].QuerySelector("i.bi.bi-a").Should().NotBeNull("alpha has the bi-a icon");
        tiles[1].QuerySelector("i.bi.bi-b").Should().NotBeNull("beta has the bi-b icon");
        tiles[2].QuerySelector("i.bi").Should().BeNull("gamma has no icon configured");
    }

    [Fact]
    public void ButtonType_IsExplicitlyButton_NotSubmit()
    {
        // Important when the picker lives inside a <form> — implicit type="submit" would submit the form
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        foreach (var button in cut.FindAll("button.tile"))
            button.GetAttribute("type").Should().Be("button");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Selected-state CSS class application
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void AppliesOnClass_OnlyToSelectedTile()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "beta")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        cut.FindAll("button.tile.tile-on").Should().HaveCount(1);
        cut.Find("button.tile.tile-on").TextContent.Should().Contain("Beta");
    }

    [Fact]
    public void AppliesOnClass_ToNoTiles_WhenSelectedDoesNotMatchAnyOption()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "no-such-value")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        cut.FindAll("button.tile.tile-on").Should().BeEmpty();
    }

    [Fact]
    public void AppliesOnClass_ToNullValueOption_WhenSelectedIsNull()
    {
        // Mirrors the attachment grid's "— None —" tile that holds a null id
        var options = new List<TilePickerOption<string>>
        {
            new(null!, "None", "bi-slash-circle"),
            new("a",   "A",    "bi-a"),
        };

        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   options)
            .Add(x => x.Selected,  null!)
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        var tiles = cut.FindAll("button.tile");
        tiles[0].GetAttribute("class").Should().Contain("tile-on");
        tiles[1].GetAttribute("class").Should().NotContain("tile-on");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Click → SelectedChanged callback
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Click_InvokesSelectedChangedWithTileValue()
    {
        string? captured = null;
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,         StringOptions())
            .Add(x => x.Selected,        "alpha")
            .Add(x => x.SelectedChanged, EventCallback.Factory.Create<string>(this, v => captured = v))
            .Add(x => x.CssClass,        "grid")
            .Add(x => x.TileClass,       "tile")
            .Add(x => x.OnClass,         "tile-on"));

        cut.FindAll("button.tile")[2].Click();

        captured.Should().Be("gamma");
    }

    [Fact]
    public void Click_OnAlreadySelectedTile_StillInvokesCallback()
    {
        // No "skip if same" optimization — re-clicking the active tile still fires.
        // Important for forms where the callback also has a side effect (MarkDirty, etc.).
        var clickCount = 0;
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,         StringOptions())
            .Add(x => x.Selected,        "alpha")
            .Add(x => x.SelectedChanged, EventCallback.Factory.Create<string>(this, _ => clickCount++))
            .Add(x => x.CssClass,        "grid")
            .Add(x => x.TileClass,       "tile")
            .Add(x => x.OnClass,         "tile-on"));

        cut.FindAll("button.tile")[0].Click();

        clickCount.Should().Be(1);
    }

    [Fact]
    public void Click_WithoutBoundCallback_DoesNotThrow()
    {
        var cut = RenderComponent<TilePicker<string>>(p => p
            .Add(x => x.Options,   StringOptions())
            .Add(x => x.Selected,  "alpha")
            .Add(x => x.CssClass,  "grid")
            .Add(x => x.TileClass, "tile")
            .Add(x => x.OnClass,   "tile-on"));

        var act = () => cut.FindAll("button.tile")[1].Click();

        act.Should().NotThrow();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Generic TValue: value types
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public void WorksWithEnumTValue_TerrainType()
    {
        var options = new List<TilePickerOption<TerrainType>>
        {
            new(TerrainType.Standard, "Standard", "bi-hexagon"),
            new(TerrainType.Ruins,    "Ruins",    "bi-building-slash"),
        };

        TerrainType? captured = null;
        var cut = RenderComponent<TilePicker<TerrainType>>(p => p
            .Add(x => x.Options,         options)
            .Add(x => x.Selected,        TerrainType.Standard)
            .Add(x => x.SelectedChanged, EventCallback.Factory.Create<TerrainType>(this, v => captured = v))
            .Add(x => x.CssClass,        "g")
            .Add(x => x.TileClass,       "t")
            .Add(x => x.OnClass,         "on"));

        // Initially Standard is selected
        cut.FindAll("button.t.on").Should().HaveCount(1);

        // Click Ruins
        cut.FindAll("button.t")[1].Click();

        captured.Should().Be(TerrainType.Ruins);
    }

    [Fact]
    public void WorksWithBoolTValue_NodeTypeStylePicker()
    {
        var options = new List<TilePickerOption<bool>>
        {
            new(false, "Territory", "bi-geo-alt-fill"),
            new(true,  "Event",     "bi-calendar-event-fill"),
        };

        bool? captured = null;
        var cut = RenderComponent<TilePicker<bool>>(p => p
            .Add(x => x.Options,         options)
            .Add(x => x.Selected,        false)
            .Add(x => x.SelectedChanged, EventCallback.Factory.Create<bool>(this, v => captured = v))
            .Add(x => x.CssClass,        "g")
            .Add(x => x.TileClass,       "t")
            .Add(x => x.OnClass,         "on"));

        // Territory (false) starts selected
        var tiles = cut.FindAll("button.t");
        tiles[0].GetAttribute("class").Should().Contain("on");
        tiles[1].GetAttribute("class").Should().NotContain("on");

        // Click Event (true)
        tiles[1].Click();

        captured.Should().BeTrue();
    }
}
