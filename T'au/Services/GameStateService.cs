namespace Warhammer.Services;

public class GameStateService
{
    private readonly Dictionary<string, int> _commandPoints = new();

    public int BattleRound { get; private set; } = 1;

    public event Action? OnChange;

    public int GetCP(string playerId) =>
        _commandPoints.TryGetValue(playerId, out var cp) ? cp : 0;

    public void AddCP(string playerId, int amount)
    {
        _commandPoints[playerId] = GetCP(playerId) + amount;
        OnChange?.Invoke();
    }

    public void SpendCP(string playerId, int amount)
    {
        _commandPoints[playerId] = Math.Max(0, GetCP(playerId) - amount);
        OnChange?.Invoke();
    }

    public void SetBattleRound(int round)
    {
        BattleRound = Math.Clamp(round, 1, 5);
        OnChange?.Invoke();
    }

    public void Reset()
    {
        foreach (var key in _commandPoints.Keys.ToList())
            _commandPoints[key] = 0;
        BattleRound = 1;
        OnChange?.Invoke();
    }
}
