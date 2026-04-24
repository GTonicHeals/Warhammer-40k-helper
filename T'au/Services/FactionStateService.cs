namespace Warhammer.Services;

public class FactionStateService
{
    private readonly Dictionary<string, string> _factions = new();

    public IReadOnlyDictionary<string, string> PlayerFactions => _factions;

    public event Action? OnChange;

    public void SetFaction(string playerId, string factionName)
    {
        if (string.IsNullOrEmpty(factionName)) return;
        _factions[playerId] = factionName;
        OnChange?.Invoke();
    }
}
