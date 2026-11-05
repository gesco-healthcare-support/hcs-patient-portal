using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>The outcome of <see cref="AccessorManagementRules.Decide"/>.</summary>
public enum AccessorManageDecision
{
    /// <summary>The caller may add / edit / remove this Authorized User.</summary>
    Allowed = 1,

    /// <summary>The caller is not a law-office party of the side the Authorized User belongs to (400).</summary>
    OtherSide = 2,

    /// <summary>The caller is not entitled to manage Authorized Users on this case at all (403-style).</summary>
    Denied = 3,
}

/// <summary>The facts about the caller that the rule needs. The guard hydrates them.</summary>
/// <param name="IsInternal">An internal (staff) caller.</param>
/// <param name="Roles">The caller's role names.</param>
/// <param name="VerifiedEmail">The caller's VERIFIED email, never the raw claim.</param>
/// <param name="IsBooker">The caller is the booker (CreatorId, else BookedByUserId) of this appointment.</param>
/// <param name="IsAuthorizedUser">The caller has an accessor (Authorized User) row on this appointment.</param>
/// <param name="IsExternalAccessorManager">The caller holds a law-office role (today's creator-only gate).</param>
public sealed record AccessorCaller(
    bool IsInternal,
    IReadOnlyCollection<string?> Roles,
    string? VerifiedEmail,
    bool IsBooker,
    bool IsAuthorizedUser,
    bool IsExternalAccessorManager);

/// <summary>The appointment's attorney and claim examiner email columns.</summary>
public sealed record AccessorCase(
    string? ApplicantAttorneyEmail,
    string? DefenseAttorneyEmail,
    string? ClaimExaminerEmail = null);

/// <summary>What the Authorized User being managed is: the law-office sides and/or the Claim Examiner role.</summary>
/// <param name="Sides">The law-office sides their roles carry (none for a Patient or Claim Examiner).</param>
/// <param name="IsClaimExaminer">Their roles include Claim Examiner.</param>
public sealed record AccessorTarget(IReadOnlyCollection<ChangeRequestSide> Sides, bool IsClaimExaminer)
{
    /// <summary>The target an account with these role names is.</summary>
    public static AccessorTarget Of(IEnumerable<string?>? roleNames)
    {
        var roles = (roleNames ?? Array.Empty<string?>()).ToList();
        return new AccessorTarget(AccessorManagementRules.SidesOf(roles), AccessorManagementRules.HoldsClaimExaminer(roles));
    }
}

/// <summary>
/// Office decisions 3 + 4 (2026-11-05): who may add, edit or remove Authorized Users after booking.
/// Staff may manage anyone on any side. An external caller may manage only the Authorized Users of a
/// law-office side S on which the caller is a party of THIS case: the named attorney of S (verified email
/// equals that side's attorney column and the caller holds the attorney role of S), or the booker / an
/// Authorized User of the case who holds a law-office role of S. Patient and Claim Examiner callers carry
/// no side and manage nothing. A firm account can hold the roles of both sides, so every side is judged on
/// its own; <see cref="AppointmentMailPreferences.LawOfficeSideOf"/> (which answers side A first) is not used.
/// Pure: the guard supplies the facts.
/// </summary>
public static class AccessorManagementRules
{
    /// <summary>The law-office sides the role names carry (zero, one, or both for a firm account).</summary>
    public static IReadOnlyCollection<ChangeRequestSide> SidesOf(IEnumerable<string?>? roleNames)
    {
        var roles = (roleNames ?? Array.Empty<string?>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        var sides = new List<ChangeRequestSide>(2);
        if (roles.Any(r => ExternalRoleConsts.ApplicantLawOffice.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            sides.Add(ChangeRequestSide.SideA);
        }
        if (roles.Any(r => ExternalRoleConsts.DefenseLawOffice.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            sides.Add(ChangeRequestSide.SideB);
        }
        return sides;
    }

    /// <summary>Do the role names include Claim Examiner?</summary>
    public static bool HoldsClaimExaminer(IEnumerable<string?>? roleNames) =>
        (roleNames ?? Array.Empty<string?>()).Any(r =>
            !string.IsNullOrWhiteSpace(r)
            && string.Equals(r.Trim(), ExternalRoleConsts.ClaimExaminer, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Office decision 5 (2026-11-05): is the caller a claim examiner on THIS case -- the named one (verified
    /// email equals the case's claim examiner column, with the Claim Examiner role), or the booker / an
    /// Authorized User of the case holding the Claim Examiner role?
    /// </summary>
    public static bool IsClaimExaminerParty(AccessorCaller caller, AccessorCase theCase)
    {
        if (!HoldsClaimExaminer(caller.Roles))
        {
            return false;
        }
        var named = !string.IsNullOrWhiteSpace(caller.VerifiedEmail)
            && !string.IsNullOrWhiteSpace(theCase.ClaimExaminerEmail)
            && string.Equals(theCase.ClaimExaminerEmail.Trim(), caller.VerifiedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
        return named || caller.IsBooker || caller.IsAuthorizedUser;
    }

    /// <summary>Is the caller a law-office party of <paramref name="side"/> on this case?</summary>
    public static bool IsPartyOfSide(ChangeRequestSide side, AccessorCaller caller, AccessorCase theCase)
    {
        if (!SidesOf(caller.Roles).Contains(side))
        {
            return false;
        }

        // The named attorney of the side: verified email == the side's column AND the side's attorney role.
        var column = side == ChangeRequestSide.SideA
            ? theCase.ApplicantAttorneyEmail
            : theCase.DefenseAttorneyEmail;
        var attorneyRole = AppointmentMailPreferences.AttorneyRoleOf(side);
        var holdsAttorneyRole = caller.Roles.Any(r =>
            !string.IsNullOrWhiteSpace(r)
            && string.Equals(r.Trim(), attorneyRole, StringComparison.OrdinalIgnoreCase));
        var named = holdsAttorneyRole
            && !string.IsNullOrWhiteSpace(caller.VerifiedEmail)
            && !string.IsNullOrWhiteSpace(column)
            && string.Equals(column.Trim(), caller.VerifiedEmail.Trim(), StringComparison.OrdinalIgnoreCase);

        // Or the booker / an Authorized User of the case, holding a law-office role of the side.
        return named || caller.IsBooker || caller.IsAuthorizedUser;
    }

    /// <summary>
    /// May this caller manage an Authorized User who belongs to <paramref name="accessorSides"/>?
    /// A target with no law-office side (a Patient or Claim Examiner accessor) may be managed by a claim
    /// examiner party only when it is a Claim Examiner helper, or by a law-office booker when
    /// <paramref name="allowNonLawOffice"/> (adding one). A target with several sides needs the caller to
    /// be a party of every one of them.
    /// </summary>
    public static AccessorManageDecision Decide(
        AccessorCaller caller,
        AccessorCase theCase,
        AccessorTarget target,
        bool allowNonLawOffice)
    {
        if (caller.IsInternal)
        {
            return AccessorManageDecision.Allowed;
        }

        var accessorSides = target.Sides;
        if (accessorSides.Count == 0)
        {
            // A Claim Examiner helper may be managed by a claim examiner party of the case (decision 5);
            // today's creator rule (a law-office booker) is kept for adding a non-law-office role.
            var byClaimExaminer = target.IsClaimExaminer && IsClaimExaminerParty(caller, theCase);
            var byBooker = allowNonLawOffice && caller.IsBooker && caller.IsExternalAccessorManager;
            return byClaimExaminer || byBooker
                ? AccessorManageDecision.Allowed
                : AccessorManageDecision.Denied;
        }

        if (accessorSides.All(s => IsPartyOfSide(s, caller, theCase)))
        {
            return AccessorManageDecision.Allowed;
        }

        // Refused. Told "wrong side" when the caller is a party of some side, or has no law-office role at
        // all (Patient / Claim Examiner), and "denied" when the caller is a law-office account that is not
        // on this case.
        var party = IsPartyOfSide(ChangeRequestSide.SideA, caller, theCase)
            || IsPartyOfSide(ChangeRequestSide.SideB, caller, theCase);
        return party || SidesOf(caller.Roles).Count == 0
            ? AccessorManageDecision.OtherSide
            : AccessorManageDecision.Denied;
    }
}
