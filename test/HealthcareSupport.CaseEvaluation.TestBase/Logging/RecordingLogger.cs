using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace HealthcareSupport.CaseEvaluation.Logging;

/// <summary>One recorded log call: the level the call site chose, and the rendered message.</summary>
public sealed record LogEntry(LogLevel Level, string Message);

/// <summary>
/// A test logger that records every call, for asserting on what a class logs.
///
/// WHY EVERY LEVEL IS ENABLED BY DEFAULT. <see cref="IsEnabled"/> returns true for every level
/// unless a minimum is given, so a test observes the level the CALL SITE chose rather than
/// whatever minimum a configuration happens to set. A test that cares about the production
/// minimum asserts it explicitly against <see cref="LogEntry.Level"/>.
///
/// THE OPTIONAL MINIMUM answers <see cref="IsEnabled"/> only; <see cref="Log{TState}"/> still
/// records every call that reaches it. That is exactly how the private copy it replaced in the
/// account page tests behaved: the minimum decides what a call site guarded by IsEnabled does,
/// and an unguarded call is still observed.
///
/// WHY IT IS SHARED. Four test classes each carried a private copy of this; one shared type keeps
/// them recording the same way.
///
/// The message is rendered with the call's own formatter, so it is exactly the text a real sink
/// would write for the message template. The exception passed alongside is not recorded.
/// </summary>
/// <typeparam name="T">The category, matching the ILogger&lt;T&gt; the class under test takes.</typeparam>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly LogEntryRecorder _recorder = new();
    private readonly LogLevel _minimumLevel;

    /// <summary>Creates a logger; <paramref name="minimumLevel"/> is what IsEnabled answers from.</summary>
    /// <param name="minimumLevel">The lowest level IsEnabled reports as enabled; every level by default.</param>
    public RecordingLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    /// <summary>A snapshot of every call recorded so far, in order.</summary>
    public IReadOnlyList<LogEntry> Entries => _recorder.Snapshot();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _recorder.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// The provider form of <see cref="RecordingLogger{T}"/>, for a class whose logger comes from the
/// container, such as ABP's DomainService.Logger. Attach it with ILoggerFactory.AddProvider in the
/// one test that needs it, so the capture stays scoped to that test. Every category it creates
/// records into the same <see cref="Entries"/>, with every level enabled.
/// </summary>
public sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly LogEntryRecorder _recorder = new();

    /// <summary>A snapshot of every call recorded so far, across all categories, in order.</summary>
    public IReadOnlyList<LogEntry> Entries => _recorder.Snapshot();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CategoryLogger(_recorder);

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release: the entries stay readable after the factory disposes its providers.
    }

    private sealed class CategoryLogger : ILogger
    {
        private readonly LogEntryRecorder _recorder;

        public CategoryLogger(LogEntryRecorder recorder) => _recorder = recorder;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            _recorder.Add(new LogEntry(logLevel, formatter(state, exception)));
        }
    }
}

/// <summary>
/// The entry list both loggers write to. Locked because a logger taken from the container can be
/// called from more than one thread; reads return a copy so a test never enumerates a list that
/// is still growing.
/// </summary>
internal sealed class LogEntryRecorder
{
    private readonly object _gate = new();
    private readonly List<LogEntry> _entries = new();

    public void Add(LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToList();
        }
    }
}
