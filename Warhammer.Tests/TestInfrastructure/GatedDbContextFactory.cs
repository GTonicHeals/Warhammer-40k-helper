using Microsoft.EntityFrameworkCore;
using Warhammer.Data;

namespace Warhammer.Tests.TestInfrastructure;

// Wraps a factory so CreateDbContextAsync can be held until the test opens the gate.
// SQLite in-memory I/O completes synchronously, which hides re-entrancy between two
// event handlers; holding the gate makes the first handler genuinely yield.
public sealed class GatedDbContextFactory(IDbContextFactory<AppDbContext> inner) : IDbContextFactory<AppDbContext>
{
    private TaskCompletionSource _gate = Open();

    private static TaskCompletionSource Open()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        tcs.SetResult();
        return tcs;
    }

    public void Close() => _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    public void Release() => _gate.TrySetResult();

    public AppDbContext CreateDbContext() => inner.CreateDbContext();

    public async Task<AppDbContext> CreateDbContextAsync(CancellationToken ct = default)
    {
        await _gate.Task;
        return inner.CreateDbContext();
    }
}
