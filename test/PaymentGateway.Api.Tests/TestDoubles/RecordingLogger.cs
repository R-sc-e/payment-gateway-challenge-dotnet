using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests.TestDoubles;

internal sealed class RecordedLog
{
    public RecordedLog(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception,
        IReadOnlyDictionary<string, object?> properties)
    {
        Level = level;
        EventId = eventId;
        Message = message;
        Exception = exception;
        Properties = properties;
    }

    public LogLevel Level { get; }

    public EventId EventId { get; }

    public string Message { get; }

    public Exception? Exception { get; }

    public IReadOnlyDictionary<string, object?> Properties { get; }
}

internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<RecordedLog> _entries = [];

    public IReadOnlyList<RecordedLog> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = state is IEnumerable<KeyValuePair<string, object?>> values
            ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>();

        _entries.Add(new RecordedLog(
            logLevel,
            eventId,
            formatter(state, exception),
            exception,
            properties));
    }
}