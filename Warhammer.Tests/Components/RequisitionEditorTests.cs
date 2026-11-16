using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Warhammer.Components.UI;
using Warhammer.Models.Campaign;
using CampaignEntity = Warhammer.Models.Campaign.Campaign;
using TestContext = Bunit.TestContext;

namespace Warhammer.Tests.Components;

public class RequisitionEditorTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly CampaignEntity _campaign = new() { Requisitions = RequisitionDefaults.Create() };
    private int _changes;

    private IRenderedComponent<RequisitionEditor> Render() =>
        _ctx.RenderComponent<RequisitionEditor>(p => p
            .Add(x => x.Campaign, _campaign)
            .Add(x => x.OnChanged, () => _changes++));

    private static Task Click(IRenderedComponent<RequisitionEditor> cut, string selector) =>
        cut.Find(selector).ClickAsync(new MouseEventArgs());
    private static Task Change(IRenderedComponent<RequisitionEditor> cut, string selector, object value) =>
        cut.Find(selector).ChangeAsync(new ChangeEventArgs { Value = value });

    private Requisition Named(string name) => _campaign.Requisitions.Single(r => r.Name == name);

    [Fact]
    public void Lists_every_requisition()
    {
        var cut = Render();

        cut.FindAll("[data-req]").Should().HaveCount(7);
        cut.Find($"[data-req-name='{Named("Repair").Id}']").GetAttribute("value").Should().Be("Repair");
    }

    [Fact]
    public async Task Add_appends_a_text_only_requisition()
    {
        var cut = Render();

        await Click(cut, "#req-add");

        _campaign.Requisitions.Should().HaveCount(8);
        var added = _campaign.Requisitions[^1];
        (added.Name, added.Cost, added.Kind).Should().Be(("New Requisition", 1, RequisitionKind.None));
        cut.FindAll("[data-req]").Should().HaveCount(8);
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task Fields_edit_the_requisition()
    {
        var cut = Render();
        var repair = Named("Repair");

        await Change(cut, $"[data-req-name='{repair.Id}']", "Field Repairs");
        await Change(cut, $"[data-req-cost='{repair.Id}']", "2");
        await Change(cut, $"[data-req-timing='{repair.Id}']", "Before a battle");
        await Change(cut, $"[data-req-effect='{repair.Id}']", "Patch everyone up.");

        (repair.Name, repair.Cost, repair.Timing, repair.Effect).Should().Be(("Field Repairs", 2, "Before a battle", "Patch everyone up."));
        _changes.Should().Be(4);
    }

    [Theory]
    [InlineData("-3")]
    [InlineData("")]
    [InlineData("lots")]
    public async Task A_cost_that_is_not_a_whole_number_from_zero_up_becomes_zero(string typed)
    {
        var cut = Render();
        var repair = Named("Repair");

        await Change(cut, $"[data-req-cost='{repair.Id}']", typed);

        repair.Cost.Should().Be(0);
    }

    [Fact]
    public async Task Amount_shows_only_for_supply_limit_requisitions()
    {
        var cut = Render();
        var supply = Named("Increase Supply Limit");
        var repair = Named("Repair");
        cut.Find($"[data-req-amount='{supply.Id}']").GetAttribute("value").Should().Be("50");
        cut.FindAll($"[data-req-amount='{repair.Id}']").Should().BeEmpty();

        await Change(cut, $"[data-req-kind='{repair.Id}']", nameof(RequisitionKind.IncreaseSupplyLimit));

        repair.Kind.Should().Be(RequisitionKind.IncreaseSupplyLimit);
        await Change(cut, $"[data-req-amount='{repair.Id}']", "100");
        repair.Amount.Should().Be(100);
    }

    [Fact]
    public async Task Delete_removes_the_requisition()
    {
        var cut = Render();
        var id = Named("Rearm and Resupply").Id;

        await Click(cut, $"[data-req-delete='{id}']");

        _campaign.Requisitions.Should().HaveCount(6).And.NotContain(r => r.Id == id);
        cut.FindAll($"[data-req='{id}']").Should().BeEmpty();
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task Restore_defaults_refills_an_empty_list()
    {
        _campaign.Requisitions.Clear();
        var cut = Render();
        cut.FindAll("[data-req]").Should().BeEmpty();

        await Click(cut, "#req-restore");

        _campaign.Requisitions.Select(r => r.Name).Should().Equal(RequisitionDefaults.Create().Select(r => r.Name));
        _changes.Should().Be(1);
    }

    [Fact]
    public async Task Restore_defaults_adds_only_what_is_missing_and_keeps_edits()
    {
        _campaign.Requisitions.RemoveAll(r => r.Name != "Repair");
        Named("Repair").Cost = 4;
        var cut = Render();

        await Click(cut, "#req-restore");

        _campaign.Requisitions.Should().HaveCount(7);
        Named("Repair").Cost.Should().Be(4);
    }

    public void Dispose() => _ctx.Dispose();
}
