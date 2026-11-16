using Warhammer.Helpers;
using Warhammer.Models.Campaign;
using Warhammer.Models.Crusade;

namespace Warhammer.Services.Crusade;

// A correction to a Completed Battle: the Territory it captured and what each unit lost.
public sealed record UnitDamageAmendment(string CardId, int ModelsLost, int WoundsLost);
public sealed record BattleAmendment(string? CapturedTerritoryId, IReadOnlyList<UnitDamageAmendment> Damage);

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

            foreach (var revealed in TerritoryReveal.RevealedByCapture(campaign.MapNodes, battle.CapturedTerritoryId))
                revealed.IsPublic = true;
        }

        battle.Status = BattleStatus.Completed;
        await campaigns.SaveCampaignAsync(campaign);
        logger.LogInformation("Completed Battle {Id}", battle.Id);
    }

    // The damage this Battle left the unit with, as recorded; the card's own value when none was entered.
    public static (int ModelsLost, int WoundsLost) RecordedDamage(BattleUnitResult r, CrusadeUnitCard card) =>
        (r.ModelsLostAfter ?? card.ModelsLost, r.WoundsLostAfter ?? card.WoundsLost);

    private sealed record DamageFix(BattleUnitResult Result, OrderOfBattle Order, CrusadeUnitCard Card, int Models, int Wounds);

    // Corrects a Completed Battle. Only the captured Territory and unit damage change; everything else
    // the Battle applied stays. Damage moves the card by the difference from what was recorded, so a
    // Repair or Reinforce bought since is kept. A previously captured Territory is left as it is.
    public async Task AmendAsync(string battleId, BattleAmendment amendment)
    {
        var campaign = await campaigns.LoadCampaignAsync();
        var battle = campaign.BattleHistory.FirstOrDefault(b => b.Id == battleId)
            ?? throw new InvalidOperationException($"Battle {battleId} not found.");
        if (battle.Status != BattleStatus.Completed)
            throw new InvalidOperationException("Only a completed Battle can be amended.");

        var territoryChanged = amendment.CapturedTerritoryId != battle.CapturedTerritoryId;
        Territory? captured = null;
        if (territoryChanged && amendment.CapturedTerritoryId is not null)
        {
            if (string.IsNullOrEmpty(battle.WinnerPlayerId))
                throw new InvalidOperationException("A Draw captures no territory.");
            captured = campaign.MapNodes.OfType<Territory>().FirstOrDefault(t => t.Id == amendment.CapturedTerritoryId)
                ?? throw new InvalidOperationException("That Territory no longer exists.");
        }

        await crusades.LoadAllCrusadesAsync();
        var orders = new[] { battle.Player1OrderId, battle.Player2OrderId }
            .Where(id => id is not null).Select(id => crusades.GetCrusade(id!)).OfType<OrderOfBattle>().ToList();

        var fixes = new List<DamageFix>();
        foreach (var d in amendment.Damage)
        {
            var r = battle.UnitResults.FirstOrDefault(x => x.CardId == d.CardId)
                ?? throw new InvalidOperationException($"Unit {d.CardId} was not in this Battle.");
            var order = r.RemovedFromOrder ? null : orders.FirstOrDefault(o => o.Units.Any(u => u.Id == r.CardId));
            if (order is null)
                throw new InvalidOperationException($"{r.UnitName} is no longer in its Order of Battle.");
            var card = order.Units.Single(u => u.Id == r.CardId);
            var name = string.IsNullOrEmpty(card.UnitName) ? r.UnitName : card.UnitName;
            if (!UnitStrength.InRange(card, d.ModelsLost, d.WoundsLost))
                throw new InvalidOperationException($"{name}: the damage entered is more than the unit can take.");
            fixes.Add(new DamageFix(r, order, card, d.ModelsLost, d.WoundsLost));
        }

        if (territoryChanged)
        {
            battle.CapturedTerritoryId = amendment.CapturedTerritoryId;
            if (captured is not null)
            {
                captured.ControllingPlayerId = battle.WinnerPlayerId;
                foreach (var revealed in TerritoryReveal.RevealedByCapture(campaign.MapNodes, captured.Id))
                    revealed.IsPublic = true;
            }
        }

        var changedOrders = new HashSet<OrderOfBattle>();
        foreach (var f in fixes)
        {
            var (models, wounds) = RecordedDamage(f.Result, f.Card);
            if (f.Models != models)
            {
                f.Card.ModelsLost += f.Models - models;
                f.Result.ModelsLostAfter = f.Models;
                changedOrders.Add(f.Order);
            }
            if (f.Wounds != wounds)
            {
                f.Card.WoundsLost += f.Wounds - wounds;
                f.Result.WoundsLostAfter = f.Wounds;
                changedOrders.Add(f.Order);
            }
            UnitStrength.Clamp(f.Card);
        }

        foreach (var order in changedOrders) await crusades.SaveCrusadeAsync(order);
        await campaigns.SaveCampaignAsync(campaign);
        logger.LogInformation("Amended Battle {Id}", battle.Id);
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
                !UnitStrength.InRange(card, ModelsLostTarget(r, card), WoundsLostTarget(r, card)))
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

    // Where the card's damage ends up. An entry made against a known starting value is applied as a
    // change, so a Repair or Reinforce bought between the draft and completion is not undone.
    public static int ModelsLostTarget(BattleUnitResult r, CrusadeUnitCard card) =>
        Target(card.ModelsLost, r.ModelsLostBefore, r.ModelsLostAfter);

    public static int WoundsLostTarget(BattleUnitResult r, CrusadeUnitCard card) =>
        Target(card.WoundsLost, r.WoundsLostBefore, r.WoundsLostAfter);

    private static int Target(int current, int? before, int? after) =>
          after is not { } a  ? current
        : before is { } b     ? Math.Max(0, current + (a - b))
        :                       a;

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
        (card.ModelsLost, card.WoundsLost) = (ModelsLostTarget(r, card), WoundsLostTarget(r, card));

        r.RemovedFromOrder = s.Remove;
        if (s.Remove) order.Units.Remove(card);
    }
}
