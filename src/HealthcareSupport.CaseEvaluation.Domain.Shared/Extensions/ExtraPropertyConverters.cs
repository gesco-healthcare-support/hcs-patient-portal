using System.Text.Json;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Extensions;

/// <summary>
/// Helpers for reading ABP <see cref="IHasExtraProperties"/> values that
/// may surface as <c>System.Text.Json.JsonElement</c> after a round-trip
/// through the entity's JSON column.
///
/// <para>
/// ABP's typed <c>entity.GetProperty&lt;T&gt;(string, T)</c> routes through
/// <c>TypeHelper.ChangeTypePrimitiveExtended&lt;T&gt;</c>. That helper
/// only handles primitive <c>T</c>s and explicitly throws for
/// <c>object?</c>, <c>JsonElement</c>, and arbitrary reference types --
/// so calling <c>GetProperty&lt;bool&gt;("flag")</c> against a freshly
/// reloaded entity throws even when the stored value is a JSON
/// <c>true</c>/<c>false</c>.
/// </para>
///
/// <para>
/// The workaround used across the codebase: read the raw value via the
/// non-generic <c>GetProperty(string)</c> overload, then coerce here.
/// Open ABP issues tracking the asymmetry:
/// <list type="bullet">
///   <item>https://github.com/abpframework/abp/issues/12547</item>
///   <item>https://github.com/abpframework/abp/issues/19430</item>
///   <item>https://github.com/abpframework/abp/issues/23546</item>
/// </list>
/// </para>
///
/// <para>B3 (2026-05-06): promoted from
/// <c>ExternalSignupAppService.ReadBoolExtensionProperty</c> /
/// <c>CoerceBool</c> so any future feature that needs a typed read of an
/// extension property has a single, tested helper to call.</para>
/// </summary>
public static class ExtraPropertyConverters
{
    /// <summary>
    /// Reads a <see cref="bool"/> extension property tolerantly. Returns
    /// <paramref name="defaultValue"/> when the property is missing,
    /// null, an unrecognized string, or any value type that cannot be
    /// parsed as a bool. Recognizes:
    /// <list type="bullet">
    ///   <item>native <see cref="bool"/></item>
    ///   <item>"True" / "False" strings (case-insensitive)</item>
    ///   <item><see cref="JsonElement"/> with kind <c>True</c> / <c>False</c></item>
    ///   <item>any other type whose <c>ToString()</c> parses as a bool</item>
    /// </list>
    /// </summary>
    public static bool GetBoolOrDefault(
        IHasExtraProperties? source,
        string propertyName,
        bool defaultValue = false)
    {
        if (source == null || string.IsNullOrEmpty(propertyName))
        {
            return defaultValue;
        }
        var raw = source.GetProperty(propertyName);
        return CoerceBool(raw, defaultValue);
    }

    /// <summary>
    /// Coerces an arbitrary boxed value into a bool. Public so callers
    /// that already have the raw <c>object?</c> (e.g. from a custom
    /// dictionary lookup) can reuse the same coercion ladder without
    /// going through <see cref="IHasExtraProperties"/>.
    /// </summary>
    public static bool CoerceBool(object? raw, bool defaultValue = false)
    {
        if (raw is null)
        {
            return defaultValue;
        }
        if (raw is bool b)
        {
            return b;
        }
        if (raw is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(json.GetString(), out var parsed) && parsed,
                _ => defaultValue,
            };
        }
        if (raw is string s)
        {
            return bool.TryParse(s, out var parsed) && parsed;
        }
        return bool.TryParse(raw.ToString(), out var parsedFallback) && parsedFallback;
    }

    /// <summary>
    /// Reads an <see cref="int"/> extension property tolerantly. Item D (2026-08-22): added for the
    /// progressive-lockout cycle counter, which lives in <c>AbpUsers.ExtraProperties</c> so it needs
    /// no migration.
    ///
    /// <para>Returns <paramref name="defaultValue"/> when the property is missing, null, or holds
    /// something that cannot be read as a whole number. Recognizes native <see cref="int"/> and
    /// <see cref="long"/>, a <see cref="JsonElement"/> of kind <c>Number</c> or a numeric
    /// <c>String</c>, a numeric string, and anything else whose <c>ToString()</c> parses.</para>
    /// </summary>
    public static int GetIntOrDefault(
        IHasExtraProperties? source,
        string propertyName,
        int defaultValue = 0)
    {
        if (source == null || string.IsNullOrEmpty(propertyName))
        {
            return defaultValue;
        }
        var raw = source.GetProperty(propertyName);
        return CoerceInt(raw, defaultValue);
    }

    /// <summary>
    /// Coerces an arbitrary boxed value into an int. Public for the same reason as
    /// <see cref="CoerceBool"/>: callers holding a raw <c>object?</c> can reuse the ladder.
    ///
    /// <para><c>long</c> is handled explicitly because a JSON round-trip widens small integers, and
    /// a value outside int range is treated as absent rather than wrapped -- a silently truncated
    /// lockout cycle would pick the wrong backoff rung.</para>
    /// </summary>
    public static int CoerceInt(object? raw, int defaultValue = 0)
    {
        if (raw is null)
        {
            return defaultValue;
        }
        if (raw is int i)
        {
            return i;
        }
        if (raw is long l)
        {
            return l >= int.MinValue && l <= int.MaxValue ? (int)l : defaultValue;
        }
        if (raw is JsonElement json)
        {
            return json.ValueKind switch
            {
                JsonValueKind.Number => json.TryGetInt32(out var fromNumber) ? fromNumber : defaultValue,
                JsonValueKind.String => int.TryParse(json.GetString(), out var fromString)
                    ? fromString
                    : defaultValue,
                _ => defaultValue,
            };
        }
        if (raw is string s)
        {
            return int.TryParse(s, out var parsed) ? parsed : defaultValue;
        }
        return int.TryParse(raw.ToString(), out var parsedFallback) ? parsedFallback : defaultValue;
    }
}
