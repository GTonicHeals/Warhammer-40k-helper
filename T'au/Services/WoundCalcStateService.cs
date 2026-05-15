namespace Warhammer.Services;

public class WoundCalcStateService
{
    public string? AttackerFilter { get; set; }
    public string? DefenderFilter { get; set; }
    public string? AttackerUnitKey { get; set; }
    public string? DefenderUnitKey { get; set; }
    public string? WeaponName { get; set; }

    public void Clear()
    {
        AttackerFilter = null;
        DefenderFilter = null;
        AttackerUnitKey = null;
        DefenderUnitKey = null;
        WeaponName = null;
    }
}
