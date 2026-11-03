using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// Who submitted an appointment request, for email routing (2026-11-03). The "booker" an email is
/// addressed to is the account that actually submitted the request, not the patient's login
/// (<c>Appointment.IdentityUserId</c>, which on a new-patient staff booking is the staff member's own
/// id). The submitter is <c>CreatorId</c>, with <c>BookedByUserId</c> as the stamped fallback for rows
/// whose creator was not recorded -- the same coalesce the read-access guard and visibility service use.
/// A submitter who holds one of the office-side roles is staff, and staff are never mailed as the
/// booker: the office hears through the inbox and the Intake Staff.
/// </summary>
public static class AppointmentSubmitter
{
    /// <summary>The roles that make an account office staff. Compared case-insensitively.</summary>
    public static readonly IReadOnlyList<string> StaffRoleNames = new[]
    {
        "admin",
        "Intake Staff",
        "Staff Supervisor",
        "IT Admin",
    };

    /// <summary>The submitting account's id, or null when neither stamp was recorded.</summary>
    public static Guid? IdOf(Appointment appointment) =>
        appointment.CreatorId ?? appointment.BookedByUserId;

    /// <summary>True when <paramref name="roleNames"/> contains an office-side staff role.</summary>
    public static bool HoldsStaffRole(IEnumerable<string?>? roleNames)
    {
        if (roleNames == null)
        {
            return false;
        }
        return roleNames.Any(role => !string.IsNullOrWhiteSpace(role)
            && StaffRoleNames.Any(staff => string.Equals(role.Trim(), staff, StringComparison.OrdinalIgnoreCase)));
    }
}
