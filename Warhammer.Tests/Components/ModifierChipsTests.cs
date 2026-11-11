using Bunit;
using FluentAssertions;
using Warhammer.Components.Calculator;
using Warhammer.Models.Calculator;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class ModifierChipsTests : TestContext
{
    private static ModChip Chip(string key, ChipState state, Die die = Die.Hit) =>
        new(die, ChipSide.Attacker, key, key.ToUpperInvariant(), "detail " + key, state);

    private IRenderedComponent<ModifierChips> Render(IReadOnlyList<ModChip> chips, Action<string>? toggled = null) =>
        RenderComponent<ModifierChips>(p => p
            .Add(x => x.Chips, chips)
            .Add(x => x.Toggled, (string k) => toggled?.Invoke(k)));

    [Fact]
    public void Renders_one_button_per_chip()
    {
        var cut = Render([Chip("plusHit", ChipState.Off), Chip("heavy", ChipState.Waiting), Chip("cover", ChipState.Blocked, Die.Save)]);
        cut.FindAll("button.ds-chip").Should().HaveCount(3);
    }

    [Fact]
    public void Blocked_chip_is_disabled()
    {
        var cut = Render([Chip("cover", ChipState.Blocked, Die.Save)]);
        cut.Find("button[data-chip='cover']").HasAttribute("disabled").Should().BeTrue();
        cut.Find("button[data-chip='cover']").ClassList.Should().Contain("ds-chip-blocked");
    }

    [Theory]
    [InlineData("torrent")]
    [InlineData("anti")]
    [InlineData("stealth")]
    [InlineData("invuln")]
    public void Datasheet_only_chips_are_disabled(string key) =>
        Render([Chip(key, ChipState.Auto)]).Find("button").HasAttribute("disabled").Should().BeTrue();

    [Fact]
    public void Clicking_off_chip_raises_toggled_with_key()
    {
        string? got = null;
        var cut = Render([Chip("plusHit", ChipState.Off)], k => got = k);
        cut.Find("button[data-chip='plusHit']").Click();
        got.Should().Be("plusHit");
    }

    [Fact]
    public void Waiting_chip_is_clickable_and_dashed()
    {
        string? got = null;
        var cut = Render([Chip("heavy", ChipState.Waiting)], k => got = k);
        var b = cut.Find("button[data-chip='heavy']");
        b.ClassList.Should().Contain("ds-chip-waiting");
        b.Click();
        got.Should().Be("heavy");
    }

    [Fact]
    public void Auto_chip_shows_auto_marker() =>
        Render([Chip("heavy", ChipState.Auto)]).Find("button").TextContent.Should().Contain("auto");
}

public class RollSpineTests : TestContext
{
    [Fact]
    public void Tones_follow_the_roll()
    {
        var cut = RenderComponent<RollSpine>(p => p.Add(x => x.Hit, "3+").Add(x => x.Wound, "5+").Add(x => x.Save, null));
        var tiles = cut.FindAll(".ds-roll");
        tiles.Should().HaveCount(3);
        tiles[0].ClassList.Should().Contain("ds-roll-easy");
        tiles[1].ClassList.Should().Contain("ds-roll-hard");
        tiles[2].ClassList.Should().Contain("ds-roll-none");
    }

    [Theory]
    [InlineData("4+", "ds-roll-mid")]
    [InlineData("2+", "ds-roll-easy")]
    [InlineData("AUTO", "ds-roll-auto")]
    [InlineData("N/A", "ds-roll-hard")]
    public void Single_tile_tone(string roll, string cls) =>
        RenderComponent<RollSpine>(p => p.Add(x => x.Hit, roll)).FindAll(".ds-roll")[0].ClassList.Should().Contain(cls);

    [Fact]
    public void Swap_button_raises_swap()
    {
        var swapped = false;
        var cut = RenderComponent<RollSpine>(p => p.Add(x => x.Swap, () => swapped = true));
        cut.Find(".ds-swap").Click();
        swapped.Should().BeTrue();
    }

    [Fact]
    public void Tiles_show_the_roll_text()
    {
        var cut = RenderComponent<RollSpine>(p => p.Add(x => x.Hit, "3+").Add(x => x.Wound, "4+").Add(x => x.Save, "5+"));
        cut.Find("[data-roll='hit'] strong").TextContent.Should().Be("3+");
        cut.Find("[data-roll='save'] strong").TextContent.Should().Be("5+");
    }
}
