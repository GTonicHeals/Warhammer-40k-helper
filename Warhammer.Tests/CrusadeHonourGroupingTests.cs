using FluentAssertions;
using Warhammer.Models.Crusade;

namespace Warhammer.Tests;

/// <summary>
/// Verifies the GroupBy projection used in the CrusadeDetail honour trait dropdown:
///   honourOptions.GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "General" : t.Category)
/// </summary>
public class CrusadeHonourGroupingTests
{
    private static string CategoryKey(CrusadeTrait t) =>
        string.IsNullOrWhiteSpace(t.Category) ? "General" : t.Category;

    private static CrusadeTrait Trait(string name, string? category = null) =>
        new() { Name = name, Category = category ?? "" };

    [Fact]
    public void Null_category_falls_back_to_General()
    {
        var trait = new CrusadeTrait { Name = "Iron Will", Category = null! };
        CategoryKey(trait).Should().Be("General");
    }

    [Fact]
    public void Empty_category_falls_back_to_General()
    {
        CategoryKey(Trait("Iron Will", "")).Should().Be("General");
    }

    [Fact]
    public void Whitespace_category_falls_back_to_General()
    {
        CategoryKey(Trait("Iron Will", "   ")).Should().Be("General");
    }

    [Fact]
    public void Named_category_is_preserved()
    {
        CategoryKey(Trait("Relic Blade", "Artificer")).Should().Be("Artificer");
    }

    [Fact]
    public void Mixed_traits_produce_correct_group_keys()
    {
        var traits = new List<CrusadeTrait>
        {
            Trait("A", "Artificer"),
            Trait("B", ""),
            Trait("C", "Legendary"),
            Trait("D", null),
            Trait("E", "Artificer"),
        };

        var groups = traits
            .GroupBy(CategoryKey)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Name).ToList());

        groups.Keys.Should().BeEquivalentTo(["Artificer", "General", "Legendary"]);
        groups["Artificer"].Should().BeEquivalentTo(["A", "E"]);
        groups["General"].Should().BeEquivalentTo(["B", "D"]);
        groups["Legendary"].Should().BeEquivalentTo(["C"]);
    }

    [Fact]
    public void Empty_list_produces_no_groups()
    {
        var groups = new List<CrusadeTrait>().GroupBy(CategoryKey).ToList();
        groups.Should().BeEmpty();
    }
}
