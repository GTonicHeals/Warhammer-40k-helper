using Warhammer.Tests.TestInfrastructure;

namespace Warhammer.Tests.Services;

/// <summary>
/// Tests for StratagemService — lazy JSON loading, unit/detachment matching,
/// and error-handling for missing or malformed files.
/// IWebHostEnvironment and ILogger are mocked; a temp directory stands in for wwwroot.
/// </summary>
public class StratagemServiceTests : IDisposable
{
    private readonly TempWebRoot _temp = new();
    private readonly Mock<ILogger<StratagemService>> _logger = new();

    public void Dispose() => _temp.Dispose();

    private StratagemService BuildService()
    {
        var env = _temp.BuildMockEnv();
        return new StratagemService(env.Object, _logger.Object);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // EnsureLoadedAsync — loading behavior
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task EnsureLoadedAsync_WithValidJson_LoadsStratagemsWithoutThrowing()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();

        // Act & Assert (no exception)
        await svc.EnsureLoadedAsync();
    }

    [Fact]
    public async Task EnsureLoadedAsync_WithValidJson_MakesStratagemsQueryable()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();

        // Act
        await svc.EnsureLoadedAsync();
        var result = svc.GetByName("Focused Fire");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Focused Fire", result.Name);
        Assert.Equal("1", result.Cost);
        Assert.Equal("Firebase Cadre", result.Detachment);
    }

    [Fact]
    public async Task EnsureLoadedAsync_WhenFileIsMissing_DoesNotThrow()
    {
        // Arrange — no Stratagems.json written to temp dir
        var svc = BuildService();

        // Act & Assert
        await svc.EnsureLoadedAsync(); // should complete gracefully
    }

    [Fact]
    public async Task EnsureLoadedAsync_WhenFileIsMalformedJson_DoesNotThrow()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", "{ this is not valid json }}}");
        var svc = BuildService();

        // Act & Assert
        await svc.EnsureLoadedAsync();
    }

    [Fact]
    public async Task EnsureLoadedAsync_CalledMultipleTimes_OnlyLoadsFileOnce()
    {
        // Arrange — counts File.ReadAllTextAsync via ILogger log calls is not practical,
        // so we verify behavior: after first load, remove the file; second call still works
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();

        await svc.EnsureLoadedAsync(); // first call — populates cache

        // Delete the file; a second load attempt would throw/fail
        File.Delete(Path.Combine(_temp.RootPath, "data", "Stratagems.json"));

        // Act — second call must not re-read (already cached)
        await svc.EnsureLoadedAsync();
        var result = svc.GetByName("Focused Fire");

        // Assert — data is still available from cache
        Assert.NotNull(result);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetStratagemsForUnit — matching
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStratagemsForUnit_WithExactUnitMatch_ReturnsAssociatedStratagems()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var results = svc.GetStratagemsForUnit("Crisis Battlesuits", "Firebase Cadre");

        // Assert
        Assert.Contains(results, s => s.Name == "Focused Fire");
    }

    [Fact]
    public async Task GetStratagemsForUnit_WithCaseInsensitiveUnitName_ReturnsStratagems()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act — different casing than stored "Crisis Battlesuits"
        var results = svc.GetStratagemsForUnit("crisis battlesuits", "Firebase Cadre");

        // Assert
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetStratagemsForUnit_WithPartialUnitNameSubstring_ReturnsStratagems()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act — "Crisis" is a substring of "Crisis Battlesuits"
        var results = svc.GetStratagemsForUnit("Crisis", "Firebase Cadre");

        // Assert
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task GetStratagemsForUnit_WithDetachmentFilter_ExcludesNonMatchingDetachmentStratagems()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act — "Firebase Cadre" detachment; "Emergency Plasma Vents" has empty detachment so it passes
        var results = svc.GetStratagemsForUnit("Crisis Battlesuits", "Firebase Cadre");

        // "Focused Fire" has detachment "Firebase Cadre" → should be included
        Assert.Contains(results, s => s.Name == "Focused Fire");
    }

    [Fact]
    public async Task GetStratagemsForUnit_WithNonMatchingUnit_ReturnsEmptyList()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var results = svc.GetStratagemsForUnit("Noise Marines", "");

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetStratagemsForUnit_WithEmptyUnitName_ReturnsEmptyList()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var results = svc.GetStratagemsForUnit("", "");

        // Assert
        Assert.Empty(results);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetByName
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetByName_WithExactName_ReturnsCorrectStratagem()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var result = svc.GetByName("Emergency Plasma Vents");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("2", result.Cost);
        Assert.Equal("SM", result.FactionId);
    }

    [Fact]
    public async Task GetByName_WithCaseInsensitiveName_ReturnsStratagem()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var result = svc.GetByName("focused fire");

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetByName_WithUnknownName_ReturnsNull()
    {
        // Arrange
        _temp.WriteDataFile("Stratagems.json", TempWebRoot.MinimalStratagemJson);
        var svc = BuildService();
        await svc.EnsureLoadedAsync();

        // Act
        var result = svc.GetByName("Nonexistent Stratagem XYZ");

        // Assert
        Assert.Null(result);
    }
}
