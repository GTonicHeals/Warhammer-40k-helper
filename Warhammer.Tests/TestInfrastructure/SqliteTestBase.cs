using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Warhammer.Data;

namespace Warhammer.Tests.TestInfrastructure;

// Base class for tests that need a real EF Core context against an in-memory SQLite database.
//
// xUnit instantiates the test class once per test method, so each test gets its own fresh
// SqliteConnection, its own schema, and its own change-tracker — perfect isolation with
// no reset coordination. The in-memory DB lives only as long as _connection stays open;
// Dispose() drops it.
//
// EnsureCreated runs OnModelCreating, which is what wires up the HasConversion +
// ValueComparer for Node.ConnectedNodeIds — the production JSON list mapping.
public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection                _connection;
    private readonly DbContextOptions<AppDbContext>  _options;

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var ctx = new AppDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    // Exposed for tests that need to register their own IDbContextFactory<AppDbContext>
    // against this fixture's in-memory database via DI (e.g. SeedRunnerTests).
    protected SqliteConnection Connection => _connection;

    // Hands out a fresh AppDbContext on each call. Sharing the underlying connection
    // means every context sees the same in-memory DB, but each has its own ChangeTracker —
    // letting tests simulate "save in one unit-of-work, read in another" without disposing
    // the database itself.
    protected AppDbContext CreateContext() => new(_options);

    // CampaignManagerService takes IDbContextFactory<AppDbContext>, not AppDbContext directly.
    // This factory hands out contexts bound to the same shared connection.
    protected IDbContextFactory<AppDbContext> CreateFactory() => new TestDbContextFactory(_options);

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
