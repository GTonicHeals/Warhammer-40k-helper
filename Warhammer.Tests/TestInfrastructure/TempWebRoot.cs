namespace Warhammer.Tests.TestInfrastructure;

/// <summary>
/// Creates an isolated temp directory that mimics wwwroot so services that
/// read from IWebHostEnvironment.WebRootPath can be tested without touching
/// the real project files. Disposed after each test class that uses it.
/// </summary>
public sealed class TempWebRoot : IDisposable
{
    public string RootPath { get; } =
        Path.Combine(Path.GetTempPath(), "WH40k_Tests_" + Guid.NewGuid().ToString("N"));

    public TempWebRoot()
    {
        Directory.CreateDirectory(Path.Combine(RootPath, "data"));
    }

    public void WriteDataFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(RootPath, "data", fileName), content);

    public void WriteRosterFile(string playerId, string json) =>
        File.WriteAllText(Path.Combine(RootPath, $"{playerId}.json"), json);

    public Mock<IWebHostEnvironment> BuildMockEnv()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(e => e.WebRootPath).Returns(RootPath);
        return mock;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
            Directory.Delete(RootPath, recursive: true);
    }

    // ── Canonical JSON payloads used across multiple test classes ─────────

    public const string MinimalStratagemJson = """
        {
          "data": {
            "stratagems": [
              {
                "name": "Focused Fire",
                "cp_cost": "1",
                "type": "Battle Tactic",
                "turn": "Your Turn",
                "phase": "Shooting Phase",
                "description": "Target enemy unit suffers -1 to its saving throws.",
                "faction_id": "TAU",
                "detachment": "Firebase Cadre"
              },
              {
                "name": "Emergency Plasma Vents",
                "cp_cost": "2",
                "type": "Strategic Ploy",
                "turn": "Opponent's Turn",
                "phase": "Any Phase",
                "description": "Prevent Hazardous damage.",
                "faction_id": "SM",
                "detachment": ""
              }
            ],
            "units": [
              {
                "name": "Crisis Battlesuits",
                "stratagems": "Focused Fire|Emergency Plasma Vents"
              },
              {
                "name": "Intercessors",
                "stratagems": "Emergency Plasma Vents"
              }
            ]
          }
        }
        """;

    public const string MinimalObjectivesJson = """
        [
          { "Name": "Assassination",   "Type": "Purge the Enemy", "Description": "Score 5VP.", "Reward": "5VP" },
          { "Name": "Bring It Down",   "Type": "Purge the Enemy", "Description": "Score 2VP.", "Reward": "2VP" },
          { "Name": "Cleanse",         "Type": "No Man's Land",   "Description": "Score VP.",  "Reward": "2VP" }
        ]
        """;

    public const string MinimalRosterJson = """
        {
          "roster": {
            "forces": [
              {
                "catalogueName": "Xenos - T'au Empire",
                "selections": [
                  { "name": "Crisis Battlesuits", "number": 1, "profiles": [], "rules": [], "categories": [], "costs": [], "selections": [] },
                  { "name": "Detachment",         "number": 1, "profiles": [], "rules": [], "categories": [], "costs": [], "selections": [] }
                ],
                "rules": []
              }
            ],
            "costs": [],
            "costLimits": []
          }
        }
        """;
}
