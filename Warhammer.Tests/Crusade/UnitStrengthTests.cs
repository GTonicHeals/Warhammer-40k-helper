using FluentAssertions;
using Warhammer.Models;
using Warhammer.Models.Crusade;

namespace Warhammer.Tests.Crusade;

public class UnitStrengthTests
{
    private static CrusadeUnitCard Card(int count, int w, int ml = 0, int wl = 0) =>
        new() { ModelCount = count, WoundsPerModel = w, ModelsLost = ml, WoundsLost = wl };

    [Theory]
    [InlineData(10, 2, 3, 1, "7/10 models · 1 wound lost")]
    [InlineData(10, 2, 3, 0, "7/10 models")]
    [InlineData(1, 14, 0, 5, "5 wounds lost")]
    [InlineData(0, 0, 3, 2, "3 models lost · 2 wounds lost")]
    [InlineData(0, 0, 1, 0, "1 model lost")]
    [InlineData(10, 2, 0, 0, "")]
    public void Describe_words_damage(int count, int w, int ml, int wl, string expected) =>
        UnitStrength.Describe(Card(count, w, ml, wl)).Should().Be(expected);

    [Fact]
    public void Describe_can_word_a_condition_other_than_the_cards_own() =>
        UnitStrength.Describe(Card(10, 2), 4, 0).Should().Be("6/10 models");

    [Fact]
    public void IsDamaged_follows_either_number()
    {
        Card(10, 2).IsDamaged.Should().BeFalse();
        Card(10, 2, ml: 1).IsDamaged.Should().BeTrue();
        Card(10, 2, wl: 1).IsDamaged.Should().BeTrue();
    }

    [Fact]
    public void Limits_are_unknown_when_size_is_unknown()
    {
        UnitStrength.MaxModelsLost(Card(0, 0)).Should().BeNull();
        UnitStrength.MaxWoundsLost(Card(0, 0)).Should().BeNull();
        UnitStrength.InRange(Card(0, 0), 99, 99).Should().BeTrue();
        UnitStrength.InRange(Card(0, 0), -1, 0).Should().BeFalse();
    }

    [Fact]
    public void Single_model_unit_cannot_lose_models()
    {
        UnitStrength.MaxModelsLost(Card(1, 14)).Should().Be(0);
        UnitStrength.InRange(Card(1, 14), 1, 0).Should().BeFalse();
        UnitStrength.InRange(Card(1, 14), 0, 13).Should().BeTrue();
        UnitStrength.InRange(Card(1, 14), 0, 14).Should().BeFalse();
    }

    [Fact]
    public void Clamp_pulls_damage_into_range()
    {
        var c = Card(5, 2, ml: 9, wl: 7);
        UnitStrength.Clamp(c);
        (c.ModelsLost, c.WoundsLost).Should().Be((4, 1));
    }

    [Fact]
    public void Clamp_leaves_unknown_sizes_alone_but_never_negative()
    {
        var c = Card(0, 0, ml: 9, wl: -2);
        UnitStrength.Clamp(c);
        (c.ModelsLost, c.WoundsLost).Should().Be((9, 0));
    }

    private static Selection Model(string name, int number, string w) => new()
    {
        Name = name, Type = "model", Number = number,
        Profiles = { new Profile { TypeName = "Unit", Name = name, Characteristics = { new Characteristic { Name = "W", Value = w } } } }
    };

    private static Selection Unit(string name, params Selection[] models) =>
        new() { Name = name, Type = "unit", Number = 1, Selections = models.ToList() };

    [Fact]
    public void FromRoster_sums_models_and_takes_wounds_from_the_largest_group() =>
        UnitStrength.FromRoster(Unit("Breacher Team", Model("Shas'ui", 1, "2"), Model("Fire Warriors", 9, "1")))
            .Should().Be((10, 1));

    [Fact]
    public void FromRoster_reads_a_single_model_unit() =>
        UnitStrength.FromRoster(Model("Riptide Battlesuit", 1, "14")).Should().Be((1, 14));

    [Fact]
    public void FromRoster_takes_the_first_group_on_a_tie()
    {
        UnitStrength.FromRoster(Unit("Crisis", Model("Shas'vre", 1, "5"), Model("Shas'ui", 2, "5"))).Should().Be((3, 5));
        UnitStrength.FromRoster(Unit("Pair", Model("A", 1, "4"), Model("B", 1, "3"))).Should().Be((2, 4));
    }

    [Fact]
    public void FromRoster_ignores_upgrades_nested_under_models()
    {
        var model = Model("Warriors", 5, "1");
        model.Selections.Add(new Selection { Name = "Pulse rifle", Type = "upgrade", Number = 5 });
        UnitStrength.FromRoster(Unit("Strike Team", model)).Should().Be((5, 1));
    }

    [Fact]
    public void FromRoster_gives_zero_when_it_cannot_tell()
    {
        UnitStrength.FromRoster(Unit("Empty")).Should().Be((0, 0));
        UnitStrength.FromRoster(Unit("Odd", Model("Thing", 3, "D6"))).Should().Be((3, 0));
        UnitStrength.FromRoster(null).Should().Be((0, 0));
    }
}
