using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace HealthcareSupport.CaseEvaluation.Logging;

/// <summary>
/// Test logger that keeps every rendered message and every structured property value as text. A value
/// can reach a sink as a property even when the rendered message omits it, so a "this value is not
/// logged" assertion has to look at both.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = new();

    public List<string> PropertyValues { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Messages.Add(formatter(state, exception));
        if (state is IEnumerable<KeyValuePair<string, object?>> properties)
        {
            PropertyValues.AddRange(properties.Select(p => p.Value?.ToString() ?? string.Empty));
        }
    }

    public bool Mentions(string value) =>
        Messages.Concat(PropertyValues).Any(text => text.Contains(value, StringComparison.OrdinalIgnoreCase));
}
