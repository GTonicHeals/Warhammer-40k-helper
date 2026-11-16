using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;

namespace Warhammer.Services.Crusade;

// Buying a Requisition for an Order of Battle: the afford check, the deduction, the built-in
// effect and the log entry. Pure; the page saves the Order of Battle afterwards.
public static class RequisitionRules
{
    // null when the purchase is allowed, otherwise why not.
    public static string? CannotBuy(OrderOfBattle order, int cost) =>
          cost < 0                       ? "The cost cannot be negative."
        : cost > order.RequisitionPoints ? $"Not enough Requisition Points: needs {cost}, has {order.RequisitionPoints}."
        : null;

    // How many Crusade Cards a Repair or Reinforce would restore; 0 for every other kind.
    public static int UnitsAffected(OrderOfBattle order, Requisition req) => req.Kind switch
    {
        RequisitionKind.RepairAll    => order.Units.Count(u => u.WoundsLost > 0),
        RequisitionKind.ReinforceAll => order.Units.Count(u => u.ModelsLost > 0),
        _ => 0
    };

    // Validates before touching anything, so a refused purchase leaves the Order of Battle as it was.
    public static void Buy(OrderOfBattle order, Requisition req, int cost, DateTime nowUtc)
    {
        if (CannotBuy(order, cost) is { } reason) throw new InvalidOperationException(reason);

        order.RequisitionPoints -= cost;
        switch (req.Kind)
        {
            case RequisitionKind.IncreaseSupplyLimit:
                order.SupplyLimit += Math.Max(0, req.Amount);
                break;
            case RequisitionKind.RepairAll:
                foreach (var u in order.Units) u.WoundsLost = 0;
                break;
            case RequisitionKind.ReinforceAll:
                foreach (var u in order.Units) u.ModelsLost = 0;
                break;
        }
        order.RequisitionLog.Add(new RequisitionPurchase { DateUtc = nowUtc, Name = req.Name, Cost = cost });
    }
}
