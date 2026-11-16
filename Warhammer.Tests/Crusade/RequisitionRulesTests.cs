using FluentAssertions;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;
using Warhammer.Services.Crusade;

namespace Warhammer.Tests.Crusade;

public class RequisitionRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 20, 0, 0, DateTimeKind.Utc);

    private static OrderOfBattle Order(int points = 3) => new()
    {
        RequisitionPoints = points, SupplyLimit = 1000,
        Units =
        {
            new CrusadeUnitCard { Id = "u1", ModelsLost = 2, WoundsLost = 1 },
            new CrusadeUnitCard { Id = "u2", ModelsLost = 0, WoundsLost = 3 },
            new CrusadeUnitCard { Id = "u3" }
        }
    };

    private static Requisition Req(RequisitionKind kind, string name = "R", int cost = 1, int amount = 0) =>
        new() { Name = name, Cost = cost, Kind = kind, Amount = amount };

    [Fact]
    public void Increase_supply_limit_adds_the_amount_and_logs_the_purchase()
    {
        var order = Order();

        RequisitionRules.Buy(order, Req(RequisitionKind.IncreaseSupplyLimit, "Increase Supply Limit", amount: 50), 1, Now);

        order.SupplyLimit.Should().Be(1050);
        order.RequisitionPoints.Should().Be(2);
        order.RequisitionLog.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new RequisitionPurchase { Name = "Increase Supply Limit", Cost = 1, DateUtc = Now });
    }

    [Fact]
    public void Repair_restores_every_wound_and_leaves_models()
    {
        var order = Order();
        var repair = Req(RequisitionKind.RepairAll);
        RequisitionRules.UnitsAffected(order, repair).Should().Be(2);

        RequisitionRules.Buy(order, repair, 1, Now);

        order.Units.Should().OnlyContain(u => u.WoundsLost == 0);
        order.Units[0].ModelsLost.Should().Be(2);
        RequisitionRules.UnitsAffected(order, repair).Should().Be(0);
    }

    [Fact]
    public void Reinforce_restores_every_model_and_leaves_wounds()
    {
        var order = Order();
        var reinforce = Req(RequisitionKind.ReinforceAll);
        RequisitionRules.UnitsAffected(order, reinforce).Should().Be(1);

        RequisitionRules.Buy(order, reinforce, 1, Now);

        order.Units.Should().OnlyContain(u => u.ModelsLost == 0);
        order.Units[1].WoundsLost.Should().Be(3);
    }

    [Fact]
    public void Text_only_requisition_changes_points_and_log_only()
    {
        var order = Order();
        var text = Req(RequisitionKind.None, "Legendary Veterans", cost: 3);
        RequisitionRules.UnitsAffected(order, text).Should().Be(0);

        RequisitionRules.Buy(order, text, 3, Now);

        order.RequisitionPoints.Should().Be(0);
        order.SupplyLimit.Should().Be(1000);
        order.Units[0].ModelsLost.Should().Be(2);
        order.Units[1].WoundsLost.Should().Be(3);
        order.RequisitionLog.Should().ContainSingle(l => l.Name == "Legendary Veterans" && l.Cost == 3);
    }

    [Theory]
    [InlineData(4, "Not enough Requisition Points: needs 4, has 3.")]
    [InlineData(-1, "The cost cannot be negative.")]
    public void A_purchase_that_cannot_be_paid_changes_nothing(int cost, string reason)
    {
        var order = Order();
        RequisitionRules.CannotBuy(order, cost).Should().Be(reason);

        var buy = () => RequisitionRules.Buy(order, Req(RequisitionKind.RepairAll), cost, Now);

        buy.Should().Throw<InvalidOperationException>().WithMessage(reason);
        order.RequisitionPoints.Should().Be(3);
        order.Units[1].WoundsLost.Should().Be(3);
        order.RequisitionLog.Should().BeEmpty();
    }

    [Fact]
    public void A_free_requisition_is_allowed()
    {
        var order = Order(points: 0);
        RequisitionRules.CannotBuy(order, 0).Should().BeNull();

        RequisitionRules.Buy(order, Req(RequisitionKind.ReinforceAll), 0, Now);

        order.RequisitionPoints.Should().Be(0);
        order.Units[0].ModelsLost.Should().Be(0);
    }

    [Fact]
    public void The_log_records_the_cost_paid_not_the_list_price()
    {
        var order = Order();

        RequisitionRules.Buy(order, Req(RequisitionKind.None, "Repair and Recuperate", cost: 1), 2, Now);

        order.RequisitionPoints.Should().Be(1);
        order.RequisitionLog.Single().Cost.Should().Be(2);
    }

    [Fact]
    public void A_negative_supply_amount_never_lowers_the_limit()
    {
        var order = Order();

        RequisitionRules.Buy(order, Req(RequisitionKind.IncreaseSupplyLimit, amount: -200), 1, Now);

        order.SupplyLimit.Should().Be(1000);
    }
}
