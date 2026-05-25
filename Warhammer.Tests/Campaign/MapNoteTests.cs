using FluentAssertions;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

// Pure-CLR tests for the GmNote model and the projection rule that decides which
// notes surface on the player-facing map. Persistence is now covered by
// CampaignManagerServiceTests against an in-memory SQLite database.
public class MapNoteTests
{
    [Fact]
    public void GmNote_defaults_to_private_and_unplaced()
    {
        var note = new GmNote();
        note.IsPublic.Should().BeFalse();
        note.X.Should().Be(-1);
        note.Y.Should().Be(-1);
    }

    [Fact]
    public void WarRoom_projection_excludes_private_and_unplaced_notes()
    {
        var notes = new List<GmNote>
        {
            new() { Id = "a", IsPublic = true,  X = 0.5, Y = 0.5 },
            new() { Id = "b", IsPublic = false, X = 0.5, Y = 0.5 },
            new() { Id = "c", IsPublic = true,  X = -1,  Y = -1  },
            new() { Id = "d", IsPublic = true,  X = 0.3, Y = 0.7 }
        };
        var visible = notes.Where(n => n.X >= 0 && n.IsPublic).ToList();
        visible.Should().HaveCount(2);
        visible.Should().Contain(n => n.Id == "a");
        visible.Should().Contain(n => n.Id == "d");
    }

    [Fact]
    public void WarRoom_projection_with_all_private_returns_empty()
    {
        var notes = new List<GmNote>
        {
            new() { Id = "a", IsPublic = false, X = 0.5, Y = 0.5 },
            new() { Id = "b", IsPublic = false, X = 0.3, Y = 0.3 }
        };
        notes.Where(n => n.X >= 0 && n.IsPublic).Should().BeEmpty();
    }

    [Fact]
    public void WarRoom_projection_with_all_public_placed_returns_all()
    {
        var notes = new List<GmNote>
        {
            new() { Id = "a", IsPublic = true, X = 0.1, Y = 0.2 },
            new() { Id = "b", IsPublic = true, X = 0.8, Y = 0.9 }
        };
        notes.Where(n => n.X >= 0 && n.IsPublic).Should().HaveCount(2);
    }
}
