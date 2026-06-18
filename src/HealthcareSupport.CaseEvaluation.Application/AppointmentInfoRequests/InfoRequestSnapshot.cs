using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Captures a per-round before/after snapshot of the flagged SCALAR field values
/// for the Send Back staff diff (Branch 2). Pure (no DI) so the masking +
/// formatting rules are unit-tested directly; the app service reads the field homes
/// and calls <see cref="Capture"/> at send-back (the "before") and resubmit (the
/// "after").
///
/// HIPAA: the SSN is masked HERE, at capture, so the AppointmentInfoRequest table
/// never stores a second raw copy -- the diff shows the masked form on both sides.
/// Keys mirror the frontend send-back-fields registry; <c>documents</c> is
/// intentionally absent (excluded from the diff), and insurance name + phone share
/// the single "appointmentInsuranceName" key (the name is the snapshotted value).
/// </summary>
internal static class InfoRequestSnapshot
{
    /// <summary>Raw current values of the flaggable scalar fields, read from their homes.</summary>
    internal sealed class FieldValues
    {
        public DateTime? DateOfBirth { get; init; }
        public string? SocialSecurityNumber { get; init; }
        public string? Address { get; init; }
        public string? CellPhoneNumber { get; init; }
        public string? AppointmentLanguageName { get; init; }
        public string? ApplicantAttorneyEmail { get; init; }
        public string? ClaimExaminerEmail { get; init; }
        public string? InsuranceName { get; init; }
        public string? DefenseAttorneyFirmName { get; init; }
    }

    /// <summary>
    /// Builds a flagged-key -&gt; display-value map for the flagged scalar fields. A
    /// flagged field with no current value maps to "" so the diff can show
    /// "(empty) -&gt; new". Non-scalar / unknown keys (e.g. "documents") are dropped.
    /// </summary>
    public static Dictionary<string, string> Capture(FieldValues values, ISet<string> flaggedKeys)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void Add(string key, string? display)
        {
            if (flaggedKeys.Contains(key))
            {
                map[key] = display ?? string.Empty;
            }
        }

        Add("dateOfBirth", values.DateOfBirth?.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
        Add("socialSecurityNumber", MaskSsn(values.SocialSecurityNumber));
        Add("address", values.Address);
        Add("cellPhoneNumber", values.CellPhoneNumber);
        Add("appointmentLanguageId", values.AppointmentLanguageName);
        Add("applicantAttorneyEmail", values.ApplicantAttorneyEmail);
        Add("appointmentClaimExaminerEmail", values.ClaimExaminerEmail);
        Add("appointmentInsuranceName", values.InsuranceName);
        Add("defenseAttorneyFirmName", values.DefenseAttorneyFirmName);

        return map;
    }

    /// <summary>Registry order of the scalar flaggable keys (documents/panel/date excluded).</summary>
    private static readonly string[] OrderedScalarKeys =
    {
        "dateOfBirth", "socialSecurityNumber", "address", "cellPhoneNumber", "appointmentLanguageId",
        "applicantAttorneyEmail", "defenseAttorneyFirmName", "appointmentInsuranceName",
        "appointmentClaimExaminerEmail",
    };

    /// <summary>
    /// Builds the per-field old-&gt;new diff for one round, in registry order, for the
    /// flagged scalar fields. A field is "Changed" only when an AFTER snapshot exists
    /// and differs from BEFORE, so open rounds and no-op resubmits read as unchanged.
    /// </summary>
    public static List<InfoRequestFieldDiffDto> BuildDiff(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        ISet<string> flaggedKeys)
    {
        var diffs = new List<InfoRequestFieldDiffDto>();
        foreach (var key in OrderedScalarKeys)
        {
            if (!flaggedKeys.Contains(key))
            {
                continue;
            }
            var hasOld = before.TryGetValue(key, out var oldValue);
            var hasNew = after.TryGetValue(key, out var newValue);
            diffs.Add(new InfoRequestFieldDiffDto
            {
                Key = key,
                OldValue = hasOld ? oldValue : null,
                NewValue = hasNew ? newValue : null,
                Changed = hasNew &&
                    !string.Equals(oldValue ?? string.Empty, newValue ?? string.Empty, StringComparison.Ordinal),
            });
        }
        return diffs;
    }

    public static string Serialize(Dictionary<string, string> map)
    {
        return JsonSerializer.Serialize(map);
    }

    public static Dictionary<string, string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Masks a raw SSN to the last four digits ("***-**-1234"). Returns "" for a
    /// null/empty value. Storing only this form keeps raw SSNs out of the snapshot.
    /// </summary>
    private static string MaskSsn(string? ssn)
    {
        if (string.IsNullOrWhiteSpace(ssn))
        {
            return string.Empty;
        }
        var digits = new string(ssn.Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return "***-**-" + last4;
    }
}
