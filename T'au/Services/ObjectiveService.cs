using System;
using System.Collections.Generic;


public class ObjectiveService
{
    // We store two separate states: one for Player 1, one for Player 2
    public PlayerState P1State { get; set; } = new PlayerState();
    public PlayerState P2State { get; set; } = new PlayerState();

    public PlayerState GetState(string side)
    {
        return side == "p2" ? P2State : P1State;
    }
}

public class PlayerState
{
    public List<Objective> Deck { get; set; } = new();
    public List<Objective> ActiveMissions { get; set; } = new();
    public List<Objective> ScoredMissions { get; set; } = new();

    // Check if initialized so we don't reset every time
    public bool IsInitialized => Deck.Count > 0 || ActiveMissions.Count > 0 || ScoredMissions.Count > 0;

    public void Reset()
    {
        ActiveMissions.Clear();
        ScoredMissions.Clear();
        Deck = new List<Objective>
        {
            new Objective("Assassination", "Purge the Enemy", "Score 5VP if one or more enemy CHARACTER models are destroyed this turn. Score 5VP if the enemy Warlord is destroyed.", "5VP"),
            new Objective("Bring It Down", "Purge the Enemy", "Score 2VP for each enemy MONSTER or VEHICLE destroyed. Score 5VP if the target had Wounds 10+.", "2VP / 5VP"),
            new Objective("Cleanse", "No Man's Land", "Select up to two units eligible to shoot. They cannot shoot or charge. At end of turn, score VP for each objective they control that is not in your deployment zone.", "2VP / 4VP"),
            new Objective("Deploy Teleport Homers", "Battlefield Supremacy", "Select units in opponent's deployment zone or center. They cannot shoot/charge. Score VP at end of turn.", "2VP / 4VP"),
            new Objective("Engage On All Fronts", "Battlefield Supremacy", "Score VP if you have units wholly within 3 or 4 different table quarters, and more than 3\" from center.", "2VP / 4VP"),
            new Objective("Extend Battle Lines", "No Man's Land", "Score 5VP if you control your home objective and one or more No Man's Land objectives.", "5VP"),
            new Objective("Investigate Signals", "No Man's Land", "Select units within 9\" of corners. They cannot shoot/charge. Score 2VP for each corner secured.", "2VP per corner"),
            new Objective("No Prisoners", "Purge the Enemy", "Score 2VP if one enemy unit is destroyed. Score 5VP if two or more are destroyed.", "2VP / 5VP"),
            new Objective("Overwhelming Force", "Purge the Enemy", "Score 3VP if an enemy unit on an objective is destroyed. Score 5VP if two or more are destroyed.", "3VP / 5VP"),
            new Objective("Secure No Man's Land", "Battlefield Supremacy", "Score 2VP if you control 1 objective in No Man's Land. Score 5VP if you control 2+.", "2VP / 5VP"),
            new Objective("Storm Hostile Objective", "Take and Hold", "Score 5VP if you control an objective that the enemy controlled at the start of the turn.", "5VP"),
            new Objective("Area Denial", "Battlefield Supremacy", "Score 5VP if there are no enemy units wholly within 6\" of the center of the battlefield.", "5VP"),
            new Objective("Capture Enemy Outpost", "Take and Hold", "Score 8VP if you control the objective marker in your opponent's deployment zone.", "8VP"),
            new Objective("Defend Stronghold", "Take and Hold", "Score 3VP if you control your home objective. Score more if you hold more.", "3VP+"),
            new Objective("A Tempting Target", "Take and Hold", "Opponent chooses one No Man's Land objective. Score 5VP if you control it at end of turn.", "5VP")
        };
    }
}

public class Objective
{
    public string Name { get; set; }
    public string Type { get; set; }
    public string Description { get; set; }
    public string Reward { get; set; }
    public Objective(string n, string t, string d, string r) { Name = n; Type = t; Description = d; Reward = r; }
}