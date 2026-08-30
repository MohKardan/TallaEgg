using Microsoft.Extensions.Logging;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>
/// Records every log call instead of writing it anywhere, so a test can assert on the level
/// and exception a piece of code chose, not just that "something" got logged.
/// </summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public sealed record Entry(LogLevel Level, Exception? Exception, string Message);

    public List<Entry> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new Entry(logLevel, exception, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
