using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace FlowTime.Api.Tests.Infrastructure;

public sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<TestLogEntry> entries = new();

    public IReadOnlyList<TestLogEntry> Entries => entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CollectorLogger(categoryName, this);

    public void Clear()
    {
        while (entries.TryDequeue(out _))
        {
        }
    }

    public void Dispose()
    {
    }

    internal void Add(TestLogEntry entry) => entries.Enqueue(entry);

    private sealed class CollectorLogger : ILogger
    {
        private readonly string category;
        private readonly TestLogCollector owner;

        public CollectorLogger(string category, TestLogCollector owner)
        {
            this.category = category;
            this.owner = owner;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var stateValues = state as IEnumerable<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            owner.Add(new TestLogEntry(category, logLevel, eventId, message, stateValues.ToArray(), exception));
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose()
        {
        }
    }
}

public sealed record TestLogEntry(
    string Category,
    LogLevel Level,
    EventId EventId,
    string Message,
    IReadOnlyList<KeyValuePair<string, object?>> State,
    Exception? Exception);
