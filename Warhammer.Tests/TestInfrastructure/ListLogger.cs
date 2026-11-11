using Microsoft.Extensions.Logging;

namespace Warhammer.Tests.TestInfrastructure;

// Captures formatted log messages so a test can count how often a code path ran.
public sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (Messages) Messages.Add(formatter(state, exception));
    }
}
