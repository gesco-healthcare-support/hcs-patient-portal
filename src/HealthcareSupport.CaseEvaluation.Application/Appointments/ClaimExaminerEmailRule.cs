using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Validation;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-13 -- when the claim examiner EMAIL may be left blank at booking. Only a law-office booker
/// (Applicant / Defense Attorney or Paralegal, never staff) who has also given the OTHER side's attorney
/// a real email: that attorney is then the other side's contact, so the examiner address is not needed to
/// reach anyone. Everyone else keeps the email required. The booker side is read from the caller's roles,
/// never from anything the client sends.
/// </summary>
public static class ClaimExaminerEmailRule
{
    /// <summary>The booker's side from their law-office role; null for staff, other roles, or both sides at once.</summary>
    public static ChangeRequestSide? LawOfficeSideOf(IEnumerable<string?>? callerRoles)
    {
        var roles = (callerRoles ?? Array.Empty<string?>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        if (BookingFlowRoles.IsInternalUserCaller(roles))
        {
            return null;
        }

        var applicant = HoldsAny(roles, ExternalRoleConsts.ApplicantAttorney, ExternalRoleConsts.ApplicantParalegal);
        var defense = HoldsAny(roles, ExternalRoleConsts.DefenseAttorney, ExternalRoleConsts.DefenseParalegal);
        if (applicant == defense)
        {
            return null;
        }

        return applicant ? ChangeRequestSide.SideA : ChangeRequestSide.SideB;
    }

    /// <summary>True when the caller may leave the claim examiner email blank (see the type summary).</summary>
    public static bool MayLeaveBlank(
        IEnumerable<string?>? callerRoles, string? applicantAttorneyEmail, string? defenseAttorneyEmail)
    {
        var side = LawOfficeSideOf(callerRoles);
        if (side == null)
        {
            return false;
        }

        var otherSide = side == ChangeRequestSide.SideA ? defenseAttorneyEmail : applicantAttorneyEmail;
        return IsRealEmail(otherSide);
    }

    private static bool IsRealEmail(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed) || PlaceholderEmail.IsPlaceholder(trimmed))
        {
            return false;
        }

        try
        {
            return new MailAddress(trimmed).Address == trimmed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool HoldsAny(IReadOnlyCollection<string> roles, params string[] names) =>
        roles.Any(r => names.Any(n => string.Equals(r, n, StringComparison.OrdinalIgnoreCase)));
}
