using FluentAssertions;
using Warhammer.Helpers;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

// Table Mode finds notes by search text, tag chips and the Session / All / Inbox tabs.
public class GmNoteQueryTests
{
    private static readonly List<Node> Nodes =
    [
        new Territory { Id = "t1", Name = "Kroot Encampment" },
        new Territory { Id = "t2", Name = "Hive Tarsus" },
    ];

    private static List<GmNote> Notes() =>
    [
        new() { Id = "a", Title = "Kroot switch sides", Summary = "Shaper offers warband", Tags = "twist, Kroot", NodeId = "t1", IsStarred = true },
        new() { Id = "b", Title = "Governor Vorne", Content = "Hoards promethium", Tags = "npc, lore", NodeId = "t2", IsStarred = true },
        new() { Id = "c", Title = "Vault", Secret = "Servitor guard wakes", Tags = "lore, twist" },
        new() { Id = "d", Title = "Ethereal backstory", IsInbox = true },
    ];

    [Fact]
    public void Parse_tags_trims_and_drops_empties()
    {
        GmNoteQuery.ParseTags(" lore, ,Twist ,").Should().Equal("lore", "Twist");
    }

    [Fact]
    public void Tag_counts_merge_case_and_sort_by_name()
    {
        GmNoteQuery.TagCounts(Notes()).Should().Equal(
            ("kroot", 1), ("lore", 2), ("npc", 1), ("twist", 2));
    }

    [Fact]
    public void Session_view_shows_starred_notes_only()
    {
        Ids(GmNoteQuery.Find(Notes(), Nodes, "", [], GmNoteView.Session)).Should().Equal("a", "b");
    }

    [Fact]
    public void All_view_leaves_the_inbox_out_and_inbox_view_shows_only_it()
    {
        Ids(GmNoteQuery.Find(Notes(), Nodes, "", [], GmNoteView.All)).Should().BeEquivalentTo(["a", "b", "c"]);
        Ids(GmNoteQuery.Find(Notes(), Nodes, "", [], GmNoteView.Inbox)).Should().Equal("d");
    }

    [Theory]
    [InlineData("promethium", "b")]      // content
    [InlineData("SHAPER", "a")]          // summary, any case
    [InlineData("servitor", "c")]        // the GM-only part is searchable
    [InlineData("hive tarsus", "b")]     // the linked location's name
    [InlineData("backstory", "d")]       // a search covers the inbox too
    public void Search_looks_in_every_field_across_all_notes(string query, string expected)
    {
        Ids(GmNoteQuery.Find(Notes(), Nodes, query, [], GmNoteView.Session)).Should().Equal(expected);
    }

    [Fact]
    public void Several_tags_narrow_to_notes_that_have_all_of_them()
    {
        Ids(GmNoteQuery.Find(Notes(), Nodes, "", ["twist"], GmNoteView.Session)).Should().BeEquivalentTo(["a", "c"]);
        Ids(GmNoteQuery.Find(Notes(), Nodes, "", ["twist", "LORE"], GmNoteView.Session)).Should().Equal("c");
    }

    [Fact]
    public void Highlight_splits_text_around_every_match()
    {
        GmNoteQuery.Highlight("Kroot meet kroot", "kroot").Should().Equal(
            ("Kroot", true), (" meet ", false), ("kroot", true));
        GmNoteQuery.Highlight("Plain", "").Should().Equal(("Plain", false));
    }

    private static IEnumerable<string> Ids(IEnumerable<GmNote> notes) => notes.Select(n => n.Id);
}
