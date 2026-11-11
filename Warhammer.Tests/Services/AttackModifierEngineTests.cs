using FluentAssertions;
using Warhammer.Models.Calculator;
using static Warhammer.Services.AttackModifierEngine;

namespace Warhammer.Tests.Services;

public class AttackModifierEngineTests
{
    private static AttackFacts Facts(
        string[]? kw = null, string[]? atkKw = null, string[]? defKw = null,
        bool ranged = true, bool stealth = false, int? invuln = null,
        string? skill = "3+", int? s = 5, int? ap = -1, int? t = 4, int? sv = 4) =>
        new(skill, s, ap, ranged, kw ?? [], atkKw ?? [], defKw ?? [], stealth, t, sv, invuln);

    private static Situation Sit(
        bool stationary = false, bool charged = false, bool notVisible = false, bool cover = false, bool engaged = false,
        bool plusHit = false, bool plusWound = false, bool minusHit = false, bool minusWound = false, bool reduceAp = false) =>
        new(stationary, charged, notVisible, cover, engaged, plusHit, plusWound, minusHit, minusWound, reduceAp);

    private static ModChip Chip(AttackResolution r, string key) => r.Chips.Single(c => c.Key == key);

    [Fact]
    public void Torrent_makes_hit_automatic()
    {
        var r = Resolve(Facts(kw: ["Torrent"]), Sit());
        r.Input.HitRoll.Should().Be("AUTO");
        Chip(r, "torrent").State.Should().Be(ChipState.Auto);
    }

    [Fact]
    public void Heavy_waits_for_stationary_then_applies()
    {
        Chip(Resolve(Facts(kw: ["Heavy"]), Sit()), "heavy").State.Should().Be(ChipState.Waiting);
        var r = Resolve(Facts(kw: ["Heavy"]), Sit(stationary: true));
        r.Input.PlusOneHit.Should().BeTrue();
        Chip(r, "heavy").State.Should().Be(ChipState.Auto);
    }

    [Fact]
    public void Lance_applies_only_when_charged_and_melee()
    {
        Resolve(Facts(kw: ["Lance"], ranged: false), Sit(charged: true)).Input.PlusOneWound.Should().BeTrue();
        Chip(Resolve(Facts(kw: ["Lance"], ranged: false), Sit()), "lance").State.Should().Be(ChipState.Waiting);
        Resolve(Facts(kw: ["Lance"], ranged: true), Sit(charged: true)).Chips.Should().NotContain(c => c.Key == "lance");
    }

    [Fact]
    public void Indirect_not_visible_gives_minus_hit_and_cover()
    {
        var r = Resolve(Facts(kw: ["Indirect Fire"]), Sit(notVisible: true));
        r.Input.MinusOneHit.Should().BeTrue();
        r.Input.HasCover.Should().BeTrue();
        Chip(r, "indirect").State.Should().Be(ChipState.Auto);
        Chip(r, "cover").State.Should().Be(ChipState.Auto);
    }

    [Fact]
    public void Stealth_is_ranged_only()
    {
        Resolve(Facts(stealth: true), Sit()).Input.MinusOneHit.Should().BeTrue();
        Resolve(Facts(stealth: true, ranged: false), Sit()).Input.MinusOneHit.Should().BeFalse();
    }

    [Fact]
    public void Anti_matches_whole_keyword()
    {
        var r = Resolve(Facts(kw: ["Anti-Fly 4+"], defKw: ["Fly"]), Sit());
        r.AntiThreshold.Should().Be(4);
        Chip(r, "anti").State.Should().Be(ChipState.Auto);
        Resolve(Facts(kw: ["Anti-Vehicle 2+"], defKw: ["Fly"]), Sit()).AntiThreshold.Should().BeNull();
        Resolve(Facts(kw: ["Anti-Fly 4+"], defKw: ["Flyer"]), Sit()).AntiThreshold.Should().BeNull();
    }

    [Fact]
    public void Anti_with_multi_word_keyword()
    {
        Resolve(Facts(kw: ["Anti-Epic Hero 4+"], defKw: ["Epic Hero"]), Sit()).AntiThreshold.Should().Be(4);
        Resolve(Facts(kw: ["Anti-Epic Hero 4+"], defKw: ["Epic"]), Sit()).AntiThreshold.Should().BeNull();
    }

    [Fact]
    public void Ignores_cover_blocks_cover_chip()
    {
        var r = Resolve(Facts(kw: ["Ignores Cover"]), Sit(cover: true));
        r.Input.IgnoresCover.Should().BeTrue();
        Chip(r, "cover").State.Should().Be(ChipState.Blocked);
    }

    [Fact]
    public void Melee_blocks_cover_chip() =>
        Chip(Resolve(Facts(ranged: false), Sit(cover: true)), "cover").State.Should().Be(ChipState.Blocked);

    [Fact]
    public void Cover_toggle_turns_chip_on() =>
        Chip(Resolve(Facts(), Sit(cover: true)), "cover").State.Should().Be(ChipState.On);

    [Fact]
    public void Invuln_from_datasheet_is_auto()
    {
        var r = Resolve(Facts(invuln: 5), Sit());
        r.Input.InvulnSave.Should().Be(5);
        Chip(r, "invuln").State.Should().Be(ChipState.Auto);
    }

    [Fact]
    public void Bgnt_waits_for_engagement_range_then_applies()
    {
        Chip(Resolve(Facts(atkKw: ["Vehicle"]), Sit()), "bgnt").State.Should().Be(ChipState.Waiting);
        var r = Resolve(Facts(atkKw: ["Vehicle"]), Sit(engaged: true));
        r.Input.BgntApplied.Should().BeTrue();
        Chip(r, "bgnt").State.Should().Be(ChipState.Auto);
    }

    [Fact]
    public void Bgnt_absent_for_pistol_melee_or_infantry()
    {
        Resolve(Facts(atkKw: ["Vehicle"], kw: ["Pistol"]), Sit(engaged: true)).Chips.Should().NotContain(c => c.Key == "bgnt");
        Resolve(Facts(atkKw: ["Vehicle"], ranged: false), Sit(engaged: true)).Chips.Should().NotContain(c => c.Key == "bgnt");
        Resolve(Facts(atkKw: ["Infantry"]), Sit(engaged: true)).Chips.Should().NotContain(c => c.Key == "bgnt");
    }

    [Fact]
    public void Heavy_and_bgnt_both_show_and_net_to_zero()
    {
        var r = Resolve(Facts(kw: ["Heavy"], atkKw: ["Vehicle"]), Sit(stationary: true, engaged: true));
        r.Input.PlusOneHit.Should().BeTrue();
        r.Input.BgntApplied.Should().BeTrue();
        Chip(r, "heavy").State.Should().Be(ChipState.Auto);
        Chip(r, "bgnt").State.Should().Be(ChipState.Auto);
        Warhammer.Services.WoundCalculationService.Calculate(r.Input).NetHitMod.Should().Be(0);
    }

    [Fact]
    public void Ranged_only_situations_are_ignored_for_melee()
    {
        var r = Resolve(Facts(kw: ["Heavy", "Indirect Fire"], atkKw: ["Vehicle"], ranged: false),
                        Sit(stationary: true, notVisible: true, engaged: true));
        r.Input.PlusOneHit.Should().BeFalse();
        r.Input.MinusOneHit.Should().BeFalse();
        r.Input.BgntApplied.Should().BeFalse();
        r.Input.HasCover.Should().BeFalse();
    }

    [Fact]
    public void Manual_toggles_pass_through()
    {
        var r = Resolve(Facts(), Sit(plusHit: true, minusWound: true, reduceAp: true));
        r.Input.PlusOneHit.Should().BeTrue();
        r.Input.MinusOneWound.Should().BeTrue();
        r.Input.ReduceAp.Should().BeTrue();
        Chip(r, "plusHit").State.Should().Be(ChipState.On);
        Chip(r, "minusHit").State.Should().Be(ChipState.Off);
    }

    [Fact]
    public void Chips_come_in_die_order()
    {
        var r = Resolve(Facts(kw: ["Torrent", "Lance"], ranged: false, invuln: 4), Sit());
        r.Chips.Select(c => c.Die).Should().BeInAscendingOrder();
    }
}
