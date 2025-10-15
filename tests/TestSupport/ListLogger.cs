using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FlowTime.Tests.Support;

public sealed class ListLogger<T> : ILogger<T>
{
    private static readonly IDisposable NullScope = new NullDisposable();
    private readonly ConcurrentQueue<LogEntry> entries = new();

    public IReadOnlyCollection<LogEntry> Entries => entries.ToArray();

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        IReadOnlyDictionary<string, object?> properties = ExtractProperties(state);
        entries.Enqueue(new LogEntry(logLevel, eventId, message, exception, properties));
    }

    private static IReadOnlyDictionary<string, object?> ExtractProperties<TState>(TState state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
        {
            return kvps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return new Dictionary<string, object?>();
    }

    private sealed class NullDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }

    public sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties);
}
