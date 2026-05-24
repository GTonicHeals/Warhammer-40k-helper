using System.Text.Json;
using FluentAssertions;
using Warhammer.Models.Campaign;

namespace Warhammer.Tests.Campaign;

public class MapNoteTests
{
    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private static T RoundTrip<T>(T obj) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(obj, Opts), Opts)!;

    [Fact]
    public void GmNote_pin_fields_roundtrip()
    {
        var note = new GmNote
        {
            Id       = "gn-1",
            Title    = "Hidden Webway Gate",
            Content  = "Secret entrance beneath the ice.",
            IsPublic = true,
            X        = 0.42,
            Y        = 0.67
        };
        var result = RoundTrip(note);
        result.Id.Should().Be("gn-1");
        result.Title.Should().Be("Hidden Webway Gate");
        result.Content.Should().Be("Secret entrance beneath the ice.");
        result.IsPublic.Should().BeTrue();
        result.X.Should().Be(0.42);
        result.Y.Should().Be(0.67);
    }

    [Fact]
    public void GmNote_defaults_to_private_and_unplaced()
    {
        var note = new GmNote();
        note.IsPublic.Should().BeFalse();
        note.X.Should().Be(-1);
        note.Y.Should().Be(-1);
    }

    [Fact]
    public void CampaignState_GmNotebook_defaults_to_empty_list()
    {
        RoundTrip(new CampaignState()).GmNotebook.Should().BeEmpty();
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

    [Fact]
    public void CampaignState_GmNotebook_roundtrips_notes_with_pin_data()
    {
        var state = new CampaignState
        {
            GmNotebook =
            [
                new GmNote { Id = "x", Title = "Public Pin",  IsPublic = true,  X = 0.3, Y = 0.4 },
                new GmNote { Id = "y", Title = "Private Pin", IsPublic = false, X = 0.7, Y = 0.8 },
                new GmNote { Id = "z", Title = "Notebook Only", X = -1, Y = -1 }
            ]
        };
        var result = RoundTrip(state);
        result.GmNotebook.Should().HaveCount(3);
        result.GmNotebook[0].IsPublic.Should().BeTrue();
        result.GmNotebook[1].IsPublic.Should().BeFalse();
        result.GmNotebook[1].X.Should().Be(0.7);
        result.GmNotebook[2].X.Should().Be(-1);
    }
}
