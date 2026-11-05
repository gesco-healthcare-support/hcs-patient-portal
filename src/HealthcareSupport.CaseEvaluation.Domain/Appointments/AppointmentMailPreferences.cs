using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Identity;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The booker's per-appointment mail choices (office decisions of 2026-11-04), stored in the
/// appointment's <c>ExtraProperties</c> so no migration is needed. A missing key means today's
/// behaviour: the booker follows the case and no mail is redirected.
/// </summary>
/// <remarks>
/// <para><b>Decision 1, booker follows the case.</b> Unticked, the external booker gets only the request
/// receipt; creator access is unchanged.</para>
/// <para><b>Decision 2, the lawyer's mail goes to the paralegal INSTEAD.</b> Stored as the side and the
/// lawyer's address (the side's attorney-of-record email at booking). It applies only while BOTH still
/// hold: the side's attorney email column still names that address (so a lawyer staff later put on the
/// case is mailed), and at least one ACTIVE paralegal Authorized User of that side remains (so nobody is
/// left unreachable). See <see cref="AttorneyMailRedirect.AppliesTo"/>.</para>
/// </remarks>
public static class AppointmentMailPreferences
{
    public const string BookerFollowsCaseKey = "BookerFollowsCase";
    public const string AttorneyMailRedirectSideKey = "AttorneyMailRedirectSide";
    public const string AttorneyMailRedirectEmailKey = "AttorneyMailRedirectEmail";

    /// <summary>True unless the booker opted out (a missing key is the default, true).</summary>
    public static bool BookerFollowsCase(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        return appointment.GetProperty<bool?>(BookerFollowsCaseKey) ?? true;
    }

    /// <summary>Stores the choice; the default (true) is stored as the absence of the key.</summary>
    public static void SetBookerFollowsCase(Appointment appointment, bool follows)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (follows)
        {
            appointment.RemoveProperty(BookerFollowsCaseKey);
        }
        else
        {
            appointment.SetProperty(BookerFollowsCaseKey, false);
        }
    }

    /// <summary>The stored redirect, or null when the booker did not ask for one.</summary>
    public static AttorneyMailRedirect? GetAttorneyMailRedirect(Appointment appointment)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        var side = appointment.GetProperty<int?>(AttorneyMailRedirectSideKey);
        var email = appointment.GetProperty<string?>(AttorneyMailRedirectEmailKey);
        if (side is not ((int)ChangeRequestSide.SideA or (int)ChangeRequestSide.SideB)
            || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        return new AttorneyMailRedirect((ChangeRequestSide)side.Value, email.Trim());
    }

    public static void SetAttorneyMailRedirect(Appointment appointment, ChangeRequestSide side, string lawyerEmail)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (string.IsNullOrWhiteSpace(lawyerEmail))
        {
            throw new ArgumentException("The lawyer's address is required.", nameof(lawyerEmail));
        }
        appointment.SetProperty(AttorneyMailRedirectSideKey, (int)side);
        appointment.SetProperty(AttorneyMailRedirectEmailKey, lawyerEmail.Trim());
    }

    /// <summary>
    /// The law-office side of an account's roles: Side A for the applicant attorney or paralegal, Side B for
    /// the defense attorney or paralegal, null for anything else (applicant side wins when both are held, as
    /// in <c>AttorneyRecipientPromotion</c>).
    /// </summary>
    public static ChangeRequestSide? LawOfficeSideOf(IEnumerable<string?>? roleNames)
    {
        var roles = (roleNames ?? Array.Empty<string?>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        if (roles.Any(r => ExternalRoleConsts.ApplicantLawOffice.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            return ChangeRequestSide.SideA;
        }
        if (roles.Any(r => ExternalRoleConsts.DefenseLawOffice.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            return ChangeRequestSide.SideB;
        }
        return null;
    }

    /// <summary>The paralegal role of a side.</summary>
    public static string ParalegalRoleOf(ChangeRequestSide side) =>
        side == ChangeRequestSide.SideA ? ExternalRoleConsts.ApplicantParalegal : ExternalRoleConsts.DefenseParalegal;

    /// <summary>The attorney role of a side.</summary>
    public static string AttorneyRoleOf(ChangeRequestSide side) =>
        side == ChangeRequestSide.SideA ? ExternalRoleConsts.ApplicantAttorney : ExternalRoleConsts.DefenseAttorney;
}

/// <summary>
/// Office decision 3 (2026-11-04): who besides the side's representative may answer the consent request
/// for side S -- a case follower holding a law-office role of side S. For an Authorized User the row must
/// also have been added by staff or by an account holding a side-S law-office role, so one side can never
/// plant someone who answers for the other (rows from before the same-side rule are judged the same way).
/// Used by the consent-link mail and by the in-portal Agree/Decline card alike.
/// </summary>
public static class ConsentSideFollowerRule
{
    /// <param name="side">The side being asked to consent.</param>
    /// <param name="followerRoles">The follower's role names.</param>
    /// <param name="isBooker">True for the booker (who follows the case); the adder check does not apply.</param>
    /// <param name="adderRoles">The roles of the account that added the Authorized User row; null when unknown,
    /// which refuses.</param>
    public static bool MaySpeakForSide(
        ChangeRequestSide side,
        IEnumerable<string?>? followerRoles,
        bool isBooker,
        IEnumerable<string?>? adderRoles)
    {
        if (AppointmentMailPreferences.LawOfficeSideOf(followerRoles) != side)
        {
            return false;
        }
        if (isBooker)
        {
            return true;
        }
        if (adderRoles == null)
        {
            return false;
        }
        var adder = adderRoles.ToList();
        return Notifications.AppointmentSubmitter.HoldsStaffRole(adder)
            || AppointmentMailPreferences.LawOfficeSideOf(adder) == side;
    }
}

/// <summary>A stored "send the lawyer's mail to the paralegal instead" choice (decision 2).</summary>
public sealed record AttorneyMailRedirect(ChangeRequestSide Side, string LawyerEmail)
{
    /// <summary>
    /// True when the redirect is in force for <paramref name="appointment"/>: the side's attorney email
    /// column still holds the stored address, and <paramref name="activeParalegalOfSideExists"/> (an ACTIVE
    /// Authorized User holding the side's paralegal role) is true. Otherwise the lawyer is mailed again.
    /// </summary>
    public bool AppliesTo(Appointment appointment, bool activeParalegalOfSideExists)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        var column = Side == ChangeRequestSide.SideA
            ? appointment.ApplicantAttorneyEmail
            : appointment.DefenseAttorneyEmail;
        return activeParalegalOfSideExists
            && !string.IsNullOrWhiteSpace(column)
            && string.Equals(column.Trim(), LawyerEmail, StringComparison.OrdinalIgnoreCase);
    }
}
