namespace Warhammer.Helpers;

// Reading a whole number typed into a number box.
//
// Pair it with @bind:get / @bind:set (string-typed) rather than value="" + @onchange: a @bind
// tells the renderer what the browser is showing, so when the stored value comes out the same
// as before (a clamped 15 that was already 10, a blank that keeps the old value) the box is
// still redrawn instead of keeping the text that was typed.
public static class WholeNumber
{
    // Clamped into [min, max]; blank or anything that is not a whole number keeps the previous value.
    public static int Parse(string? typed, int previous, int min = 0, int max = int.MaxValue) =>
        int.TryParse(typed, out var n) ? Math.Clamp(n, min, max) : previous;
}
