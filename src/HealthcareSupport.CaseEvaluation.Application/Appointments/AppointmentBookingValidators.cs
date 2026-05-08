using HealthcareSupport.CaseEvaluation.SystemParameters;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11a (2026-05-04) -- pure validators and helpers for the
/// AppointmentManager booking flow. Mirrors the OLD-parity rules from
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// and <c>P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\ApplicationUtility.cs</c>.
///
/// Extracted as <c>internal static</c> for unit-testability via the
/// existing <c>InternalsVisibleTo</c> wiring (matches the Phase 3 / 5 / 6
/// pattern). The full <c>AppointmentManager.CreateAsync</c> rewrite is
/// Phase 11b: it consumes these helpers instead of inlining them so the
/// manager method stays focused on orchestration.
/// </summary>
internal static class AppointmentBookingValidators
{
    /// <summary>
    /// OLD's confirmation-number formatter from
    /// <c>ApplicationUtility.GenerateConfirmationNumber</c>:
    /// <c>"A" + AppointmentId.ToString("D5")</c>. NEW uses Guid PKs so the
    /// raw int is supplied by a per-tenant counter (Phase 11b decision).
    /// Strict parity: zero-padded to 5 digits, single 'A' prefix.
    /// </summary>
    /// <param name="sequenceNumber">The tenant-scoped sequential int.</param>
    /// <returns>String of the form <c>A00001</c>.</returns>
    internal static string FormatConfirmationNumber(int sequenceNumber)
    {
        return "A" + sequenceNumber.ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// OLD's lead-time gate from <c>AppointmentDomain.cs</c> Add path:
    /// reject when the slot date is sooner than today + lead-time days.
    /// Returns <c>true</c> when the slot is far enough out to be bookable.
    /// </summary>
    internal static bool IsSlotWithinLeadTime(DateTime slotDate, DateTime today, int leadTimeDays)
    {
        var earliest = today.Date.AddDays(leadTimeDays);
        return slotDate.Date >= earliest;
    }

    /// <summary>
    /// OLD's per-type max-horizon gate: reject when the slot date is too
    /// far out for the appointment type. Returns <c>true</c> when the
    /// slot is within the horizon.
    /// </summary>
    internal static bool IsSlotWithinMaxTime(DateTime slotDate, DateTime today, int maxTimeDays)
    {
        var latest = today.Date.AddDays(maxTimeDays);
        return slotDate.Date <= latest;
    }

    /// <summary>
    /// OLD's per-type max-time resolver. PQME / PQME-REVAL use
    /// <c>AppointmentMaxTimePQME</c>; AME / AME-REVAL use
    /// <c>AppointmentMaxTimeAME</c>; everything else uses
    /// <c>AppointmentMaxTimeOTHER</c>. Match is name-substring + uppercase
    /// invariant so seeded names like "PQME-REVAL" or "AME" route correctly.
    /// </summary>
    internal static int ResolveMaxTimeDaysForType(string? appointmentTypeName, SystemParameter systemParameter)
    {
        if (systemParameter == null)
        {
            throw new ArgumentNullException(nameof(systemParameter));
        }

        var name = (appointmentTypeName ?? string.Empty).Trim().ToUpperInvariant();

        // AME and AME-REVAL share the AME horizon. PQME and PQME-REVAL share
        // the PQME horizon. We check the more-specific AME first because
        // "AME-REVAL" contains both substrings on the OLD-side; a literal
        // name comparison against "PQME" then "AME" would mis-route AME-REVAL.
        if (name.Contains("AME"))
        {
            // AME or AME-REVAL.
            if (!name.StartsWith("PQME"))
            {
                return systemParameter.AppointmentMaxTimeAME;
            }
        }
        if (name.Contains("PQME"))
        {
            return systemParameter.AppointmentMaxTimePQME;
        }
        return systemParameter.AppointmentMaxTimeOTHER;
    }

    /// <summary>
    /// OLD's patient-deduplication 3-of-6 rule from
    /// <c>AppointmentDomain.cs:736-776</c> (<c>IsPatientRegistered</c>):
    /// counts how many of LastName / DOB / Phone / Email / SSN /
    /// ClaimNumber match between an incoming intake and an existing
    /// candidate. The caller decides the threshold via
    /// <see cref="IsPatientDuplicate"/>.
    ///
    /// All comparisons are case-insensitive trimmed string matches; null
    /// or whitespace fields on either side count as "no match" for that
    /// field (cannot match an empty value).
    /// </summary>
    internal static int CountMatchingDeduplicationFields(
        PatientDeduplicationCandidate incoming,
        PatientDeduplicationCandidate existing)
    {
        if (incoming == null) throw new ArgumentNullException(nameof(incoming));
        if (existing == null) throw new ArgumentNullException(nameof(existing));

        var matches = 0;
        if (StringMatches(incoming.LastName, existing.LastName)) matches++;
        if (incoming.DateOfBirth.HasValue && existing.DateOfBirth.HasValue
            && incoming.DateOfBirth.Value.Date == existing.DateOfBirth.Value.Date) matches++;
        if (StringMatches(incoming.PhoneNumber, existing.PhoneNumber)) matches++;
        if (StringMatches(incoming.Email, existing.Email)) matches++;
        if (StringMatches(incoming.SocialSecurityNumber, existing.SocialSecurityNumber)) matches++;
        if (StringMatches(incoming.ClaimNumber, existing.ClaimNumber)) matches++;
        return matches;
    }

    /// <summary>
    /// Threshold check against <see cref="CountMatchingDeduplicationFields"/>.
    /// OLD uses 3 (line 770). Strict parity preserves the threshold;
    /// extracting as a parameter lets callers tune it for legacy data
    /// imports if needed.
    /// </summary>
    internal const int DefaultDuplicateThreshold = 3;

    internal static bool IsPatientDuplicate(
        PatientDeduplicationCandidate incoming,
        PatientDeduplicationCandidate existing,
        int threshold = DefaultDuplicateThreshold)
    {
        return CountMatchingDeduplicationFields(incoming, existing) >= threshold;
    }

    private static bool StringMatches(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Field bag used by the dedup helpers. Internal so it stays
/// uncoupled from any DTO layer; the AppointmentManager will pack a
/// <see cref="PatientDeduplicationCandidate"/> from the booking input
/// at call time.
/// </summary>
internal class PatientDeduplicationCandidate
{
    public string? LastName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string? SocialSecurityNumber { get; init; }
    public string? ClaimNumber { get; init; }
}
