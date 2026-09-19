using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-22 -- pure transform behind the relationship-scoped external-user
/// lookup. Given the denormalized party-email columns of the appointments a
/// caller can ALREADY see (the set computed by
/// <see cref="AppointmentVisibilityService"/>), produce the distinct co-parties
/// the caller may look up.
///
/// <para>Leak-safe by construction: it only ever emits parties named on
/// appointments the caller can already read, and never the caller themselves.
/// A co-party is keyed by (email, role-on-the-appointment), so a firm that is
/// the AA on one shared appointment and the DA on another yields two co-party
/// entries -- mirroring how the visibility rule and the picker treat roles.</para>
///
/// <para>Pure (no DI / no repos), mirroring <see cref="AppointmentAccessRules"/>.
/// The orchestrator resolves each co-party email to a registered account for the
/// final lookup DTO.</para>
/// </summary>
public static class ExternalCoPartyRules
{
    /// <summary>
    /// The four denormalized party-email columns of one appointment. Patient and
    /// Claim Examiner carry email only on the appointment; AA/DA names live in
    /// their own columns but are not needed here (the lookup resolves the account).
    /// </summary>
    public sealed record AppointmentParties(
        string? PatientEmail,
        string? ApplicantAttorneyEmail,
        string? DefenseAttorneyEmail,
        string? ClaimExaminerEmail);

    /// <summary>A co-party reference: the email named on a shared appointment and
    /// the role that column represents.</summary>
    public sealed record CoParty(string Email, string Role);

    /// <summary>
    /// Distinct co-parties across <paramref name="appointments"/>, excluding the
    /// caller's own email (case-insensitive). Blank columns are ignored. One entry
    /// per (email, role); the same email under two different roles yields two.
    /// </summary>
    public static IReadOnlyList<CoParty> CollectCoParties(
        string? callerEmail,
        IEnumerable<AppointmentParties> appointments)
    {
        if (appointments == null)
        {
            return Array.Empty<CoParty>();
        }

        var callerLower = string.IsNullOrWhiteSpace(callerEmail)
            ? null
            : callerEmail.Trim().ToLowerInvariant();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CoParty>();

        void Consider(string? column, string role)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                return;
            }

            var trimmed = column.Trim();
            var lower = trimmed.ToLowerInvariant();
            if (callerLower != null && string.Equals(lower, callerLower, StringComparison.Ordinal))
            {
                return;
            }

            // Key by role + normalized email so duplicates across appointments
            // collapse but the same email under two roles is kept distinct.
            if (seen.Add(role + "|" + lower))
            {
                result.Add(new CoParty(trimmed, role));
            }
        }

        foreach (var appt in appointments)
        {
            if (appt == null)
            {
                continue;
            }

            Consider(appt.PatientEmail, AppointmentAccessRules.PatientRole);
            Consider(appt.ApplicantAttorneyEmail, AppointmentAccessRules.ApplicantAttorneyRole);
            Consider(appt.DefenseAttorneyEmail, AppointmentAccessRules.DefenseAttorneyRole);
            Consider(appt.ClaimExaminerEmail, AppointmentAccessRules.ClaimExaminerRole);
        }

        return result;
    }
}
