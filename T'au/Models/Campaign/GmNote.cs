namespace Warhammer.Models.Campaign;

public class GmNote
{
    public string   Id        { get; set; } = Guid.NewGuid().ToString();
    public string   Title     { get; set; } = "New Note";
    public string   Content   { get; set; } = "";
    public string   Tags      { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double   X         { get; set; } = -1;
    public double   Y         { get; set; } = -1;
    public bool     IsPublic  { get; set; } = false;
}
