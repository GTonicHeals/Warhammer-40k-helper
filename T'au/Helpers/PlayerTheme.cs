namespace Warhammer.Helpers;

public static class PlayerTheme
{
    public static string GetColor(string playerId) => playerId.ToLowerInvariant() switch
    {
        "p1" => "#ffc107",
        "p2" => "#5dade2",
        "p3" => "#2ecc71",
        "p4" => "#a855f7",
        "p5" => "#e74c3c",
        _    => "#f39c12",
    };

    // CSS-variable form for use inside <style> blocks that consume design tokens
    public static string GetColorVar(string playerId) => playerId.ToLowerInvariant() switch
    {
        "p1" => "var(--c-p1, #ffc107)",
        "p2" => "var(--c-p2, #5dade2)",
        "p3" => "var(--c-p3, #2ecc71)",
        "p4" => "var(--c-p4, #a855f7)",
        "p5" => "var(--c-p5, #e74c3c)",
        _    => "var(--c-accent, #f39c12)",
    };

    public static string GetGradientColor(string playerId) => playerId.ToLowerInvariant() switch
    {
        "p1" => "darkred",
        "p2" => "#154360",
        "p3" => "#145a32",
        "p4" => "#4a235a",
        "p5" => "#641e16",
        _    => "#7e5109",
    };

    public static string GetLabel(string playerId)
    {
        if (int.TryParse(playerId.TrimStart('p', 'P'), out var n))
            return $"P{n}";
        return playerId.ToUpperInvariant();
    }
}
