using System.Text.Json;

namespace Warhammer.Services;

public class ObjectiveService
{
    private readonly List<Objective> _defaultDeck;

    public PlayerState P1State { get; } = new();
    public PlayerState P2State { get; } = new();

    public ObjectiveService(IWebHostEnvironment env, ILogger<ObjectiveService> logger)
    {
        _defaultDeck = LoadDeck(env, logger);
        P1State.DefaultDeck = _defaultDeck;
        P2State.DefaultDeck = _defaultDeck;
    }

    public PlayerState GetState(string side) => side == "p2" ? P2State : P1State;

    private static List<Objective> LoadDeck(IWebHostEnvironment env, ILogger<ObjectiveService> logger)
    {
        var path = Path.Combine(env.WebRootPath, "data", "TacticalObjectives.json");
        if (!File.Exists(path))
        {
            logger.LogWarning("TacticalObjectives.json not found at {Path}", path);
            return new();
        }
        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<Objective>>(json, options) ?? new();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load TacticalObjectives.json");
            return new();
        }
    }
}

public class PlayerState
{
    internal List<Objective> DefaultDeck { get; set; } = new();

    public List<Objective> Deck { get; set; } = new();
    public List<Objective> ActiveMissions { get; set; } = new();
    public List<Objective> ScoredMissions { get; set; } = new();

    public int[] PrimaryScores { get; set; } = new int[4];
    public int SecondaryVP { get; set; } = 0;

    public int PrimaryTotal   => Math.Min(PrimaryScores.Sum(), 50);
    public int SecondaryTotal => Math.Min(SecondaryVP, 40);
    public int TotalScore     => PrimaryTotal + SecondaryTotal;

    public bool IsInitialized => Deck.Count > 0 || ActiveMissions.Count > 0 || ScoredMissions.Count > 0;

    public void Reset()
    {
        PrimaryScores = new int[4];
        SecondaryVP = 0;
        ActiveMissions.Clear();
        ScoredMissions.Clear();
        Deck = DefaultDeck
            .Select(o => new Objective(o.Name, o.Type, o.Description, o.Reward))
            .ToList();
    }
}

public class Objective
{
    public string Name        { get; set; } = "";
    public string Type        { get; set; } = "";
    public string Description { get; set; } = "";
    public string Reward      { get; set; } = "";

    public Objective() { }
    public Objective(string name, string type, string description, string reward)
    {
        Name = name; Type = type; Description = description; Reward = reward;
    }
}
