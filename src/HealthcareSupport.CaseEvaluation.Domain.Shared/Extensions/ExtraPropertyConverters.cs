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
}
