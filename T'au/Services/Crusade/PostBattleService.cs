using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;

namespace Warhammer.Services.Crusade;

// Applies a finished Battle to both Orders of Battle and the Campaign in one call.
// Everything is validated before anything is mutated, so a rejected Battle leaves
// the cards, orders and snapshot untouched; a Completed Battle is never applied twice.
public class PostBattleService(
    CampaignManagerService      campaigns,
    CrusadeManagerService       crusades,
    ILogger<PostBattleService>  logger)
{
    private sealed record Step(BattleUnitResult Result, OrderOfBattle Order, CrusadeUnitCard Card, int Xp, bool Remove);

    public async Task CompleteAsync(string battleId)
    {
        var campaign = await campaigns.LoadCampaignAsync();
        var battle = campaign.BattleHistory.FirstOrDefault(b => b.Id == battleId)
            ?? throw new InvalidOperationException($"Battle {battleId} not found.");
        if (battle.Status == BattleStatus.Completed)
            throw new InvalidOperationException("This Battle has already been completed.");
        if (battle.Player1OrderId is null || battle.Player2OrderId is null)
            throw new InvalidOperationException("Both sides need an Order of Battle.");

        await crusades.LoadAllCrusadesAsync();
        var order1 = crusades.GetCrusade(battle.Player1OrderId)
            ?? throw new InvalidOperationException("Player 1's Order of Battle no longer exists.");
        var order2 = crusades.GetCrusade(battle.Player2OrderId)
            ?? throw new InvalidOperationException("Player 2's Order of Battle no longer exists.");

        var steps = Validate(battle, order1, order2);

        foreach (var s in steps) Apply(s);

        foreach (var (order, award) in new[] { (order1, battle.Player1RpAwarded), (order2, battle.Player2RpAwarded) })
        {
            order.RequisitionPoints = Math.Min(OrderOfBattle.RequisitionPointsMax, order.RequisitionPoints + award);
            await crusades.SaveCrusadeAsync(order);
        }

        if (!string.IsNullOrEmpty(battle.WinnerPlayerId))
        {
            var loserId = battle.WinnerPlayerId == battle.Player1Id ? battle.Player2Id : battle.Player1Id;
            var winner = campaign.Players.FirstOrDefault(p => p.Id == battle.WinnerPlayerId);
            var loser  = campaign.Players.FirstOrDefault(p => p.Id == loserId);
            if (winner is not null) winner.Wins++;
            if (loser is not null) loser.Losses++;

            if (battle.CapturedTerritoryId is not null &&
                campaign.MapNodes.OfType<Territory>().FirstOrDefault(t => t.Id == battle.CapturedTerritoryId) is { } territory)
                territory.ControllingPlayerId = battle.WinnerPlayerId;
        }

        battle.Status = BattleStatus.Completed;
        await campaigns.SaveCampaignAsync(campaign);
        logger.LogInformation("Completed Battle {Id}", battle.Id);
    }

    private List<Step> Validate(BattleRecord battle, OrderOfBattle order1, OrderOfBattle order2)
    {
        foreach (var award in new[] { battle.Player1RpAwarded, battle.Player2RpAwarded })
            if (award < 0 || award > OrderOfBattle.RequisitionPointsMax)
                throw new InvalidOperationException($"Requisition Points awarded must be between 0 and {OrderOfBattle.RequisitionPointsMax}.");

        foreach (var side in battle.UnitResults.GroupBy(r => r.PlayerId))
            if (side.Count(r => r.MarkedForGreatness) > 1)
                throw new InvalidOperationException("Only one unit per side can be Marked for Greatness.");

        var steps = new List<Step>();
        foreach (var r in battle.UnitResults)
        {
            var order = order1.Units.Any(u => u.Id == r.CardId) ? order1
                      : order2.Units.Any(u => u.Id == r.CardId) ? order2 : null;
            if (order is null)
            {
                logger.LogWarning("Crusade Card {CardId} ({Unit}) not found; skipped", r.CardId, r.UnitName);
                continue;
            }
            var card = order.Units.Single(u => u.Id == r.CardId);
            var name = string.IsNullOrEmpty(card.UnitName) ? r.UnitName : card.UnitName;

            var xp = XpRules.XpFor(r, card);
            var ranks = XpRules.RanksGained(card.Xp, card.Xp + xp, card.IsCharacter, card.LegendaryVeteransUsed);
            if (r.HonoursGained.Count != ranks)
                throw new InvalidOperationException($"{name} owes {ranks} Battle Honour(s) but {r.HonoursGained.Count} were picked.");

            if ((r.ModelsLostAfter is not null || r.WoundsLostAfter is not null) &&
                !UnitStrength.InRange(card, r.ModelsLostAfter ?? card.ModelsLost, r.WoundsLostAfter ?? card.WoundsLost))
                throw new InvalidOperationException($"{name}: the damage entered is more than the unit can take.");

            var remove = false;
            if (r.Destroyed)
            {
                if (r.OutOfActionPassed is null)
                    throw new InvalidOperationException($"{name} was destroyed: enter its Out of Action test result.");

                if (r.OutOfActionPassed == false)
                {
                    var options = OutOfActionRules.OptionsFor(card);
                    remove = options.WillBeRemoved;
                    switch (r.Penalty)
                    {
                        case OutOfActionPenalty.BattleScar when !options.CanTakeScar:
                            throw new InvalidOperationException($"{name} has {OutOfActionRules.MaxScars} Battle Scars and must suffer a Devastating Blow.");
                        case OutOfActionPenalty.BattleScar when r.ScarTaken is null:
                            throw new InvalidOperationException($"{name}: choose the Battle Scar taken.");
                        case OutOfActionPenalty.DevastatingBlow when options.CanTakeDevastatingBlow &&
                                                                     !card.BattleHonours.Any(h => h.Name == r.HonourLost):
                            throw new InvalidOperationException($"{name}: choose the Battle Honour lost.");
                        case OutOfActionPenalty.DevastatingBlow when !options.CanTakeDevastatingBlow && !options.WillBeRemoved:
                            throw new InvalidOperationException($"{name} has no Battle Honour to lose; take a Battle Scar.");
                        case OutOfActionPenalty.None when !options.WillBeRemoved:
                            throw new InvalidOperationException($"{name} failed its Out of Action test: choose a Battle Scar or a Devastating Blow.");
                    }
                }
            }

            steps.Add(new Step(r, order, card, xp, remove));
        }
        return steps;
    }

    private static void Apply(Step s)
    {
        var (r, order, card) = (s.Result, s.Order, s.Card);

        r.XpGained = s.Xp;
        card.Xp += s.Xp;
        card.BattlesPlayed++;
        if (!r.Destroyed) card.BattlesSurvived++;
        card.UnitsDestroyed += r.EnemyUnitsDestroyed;

        if (r.Destroyed && r.OutOfActionPassed == false)
        {
            if (r.Penalty == OutOfActionPenalty.BattleScar && r.ScarTaken is not null)
                card.BattleScars.Add(r.ScarTaken);
            else if (r.Penalty == OutOfActionPenalty.DevastatingBlow && r.HonourLost is not null)
            {
                var lost = card.BattleHonours.FindIndex(h => h.Name == r.HonourLost);
                if (lost >= 0) card.BattleHonours.RemoveAt(lost);
            }
        }

        card.BattleHonours.AddRange(r.HonoursGained);

        // Attrition carries into the next Battle; a value that was never entered leaves the card as it was.
        if (r.ModelsLostAfter is { } modelsLost) card.ModelsLost = modelsLost;
        if (r.WoundsLostAfter is { } woundsLost) card.WoundsLost = woundsLost;

        r.RemovedFromOrder = s.Remove;
        if (s.Remove) order.Units.Remove(card);
    }
}
