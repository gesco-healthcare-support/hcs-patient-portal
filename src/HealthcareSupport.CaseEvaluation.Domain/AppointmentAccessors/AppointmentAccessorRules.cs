using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// Phase 11i (2026-05-04) -- pure rules backing the accessor link /
/// invite flow. Mirrors OLD
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
/// lines 186-196 (validation) and 222 / 290+ (Add path).
///
/// Extracted as <c>public static</c> so the orchestrating
/// <see cref="AppointmentAccessorManager"/> can compose them and so
/// they remain unit-testable without standing up an Identity stack.
/// </summary>
public static class AppointmentAccessorRules
{
    /// <summary>
    /// Canonical external-party role names used by the accessor flow.
    /// Mirror the seeds in
    /// <c>ExternalUserRoleDataSeedContributor</c>; if a role is
    /// renamed there, this list must be updated in lock-step.
    ///
    /// OLD has 4 external roles total (verified at
    /// <c>P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs</c>:
    /// Patient=4, Adjuster=5, PatientAttorney=6, DefenseAttorney=7).
    /// NEW renamed for clarity:
    ///   OLD Adjuster        -> NEW Claim Examiner
    ///   OLD PatientAttorney -> NEW Applicant Attorney
    /// "Adjuster" and "Claim Examiner" are the SAME role with different
    /// labels; NEW's canonical name is "Claim Examiner". Earlier
    /// audits mistakenly listed both -- reconciled.
    /// </summary>
    public static readonly IReadOnlyList<string> RecognizedExternalRoles = new[]
    {
        "Patient",
        "Applicant Attorney",
        "Defense Attorney",
        "Claim Examiner",
    };

    /// <summary>
    /// Returns <c>true</c> when the user already holds the requested
    /// role (case-insensitive trimmed match). Used by the manager to
    /// decide whether to short-circuit user creation and just link the
    /// accessor row.
    /// </summary>
    public static bool HoldsRequestedRole(IEnumerable<string>? userRoles, string requestedRole)
    {
        if (userRoles == null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(requestedRole))
        {
            return false;
        }
        var target = requestedRole.Trim();
        return userRoles.Any(r => string.Equals(
            (r ?? string.Empty).Trim(),
            target,
            StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns <c>true</c> when the user already holds at least one
    /// recognised external-party role that is NOT the requested role.
    /// This is the OLD-parity "different user type" check at
    /// <c>AppointmentDomain.cs:191-193</c>: an existing user whose
    /// role differs from the booking-flow's requested role triggers
    /// the role-mismatch BusinessException.
    ///
    /// Internal-only roles (admin, Intake Staff, Staff Supervisor, IT
    /// Admin, Doctor, etc.) are NOT in <see cref="RecognizedExternalRoles"/>;
    /// holding only those roles does NOT trigger a mismatch (those
    /// users can be added as accessors as a side-band grant on top of
    /// their internal role).
    /// </summary>
    public static bool HasConflictingExternalRole(IEnumerable<string>? userRoles, string requestedRole)
    {
        if (userRoles == null)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(requestedRole))
        {
            return false;
        }
        var target = requestedRole.Trim();
        var roles = userRoles.Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .ToList();
        var holdsTarget = roles.Any(r => string.Equals(r, target, StringComparison.OrdinalIgnoreCase));
        if (holdsTarget)
        {
            // No conflict: requested role is one of the user's roles.
            return false;
        }

        return roles.Any(r => RecognizedExternalRoles.Any(er =>
            string.Equals(er, r, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Decides which step the accessor flow should take for a given
    /// (existing-or-not, role-set) combination. Returns the OLD-parity
    /// outcome the manager then translates into a side effect.
    /// </summary>
    public static AccessorLinkOutcome ResolveOutcome(bool userExists, IEnumerable<string>? userRoles, string requestedRole)
    {
        if (!userExists)
        {
            return AccessorLinkOutcome.CreateUserAndLink;
        }

        if (HoldsRequestedRole(userRoles, requestedRole))
        {
            return AccessorLinkOutcome.LinkExisting;
        }

        // D9 (firm-based AA/DA, 2026-06-12): an existing user is granted the
        // requested role (idempotently) and linked -- WHETHER OR NOT they already
        // hold a different recognised external role. The firm model lets one
        // account accumulate roles (e.g. an Applicant Attorney firm invited as a
        // Defense Attorney accessor on the opposing side gains the Defense Attorney
        // role on top). The accessor invite is already authorized upstream by
        // AppointmentAccessRules.CanManageAccessors (internal staff OR the creator
        // who holds AA/DA), so granting the invited role is the intended outcome.
        //
        // This reverses the earlier OLD-parity "do not soften RoleMismatch" stance
        // per the locked D9 decision. Granting the role is SAFE because visibility
        // is gated independently by role (see IsAppointmentEmailRoleVisible) -- the
        // user only sees appointments where their email is a party under a role
        // they hold, so accumulating Defense Attorney reveals only DA-side
        // appointments, never the AA side they did not already have.
        //
        // HasConflictingExternalRole / AccessorLinkOutcome.RoleMismatch are retained
        // (predicate + enum value still referenced by the manager's defensive
        // switch) but ResolveOutcome no longer returns RoleMismatch.
        return AccessorLinkOutcome.GrantRoleAndLink;
    }
}

/// <summary>
/// Discriminator returned by
/// <see cref="AppointmentAccessorRules.ResolveOutcome"/>; drives the
/// side-effect branch in <see cref="AppointmentAccessorManager.CreateOrLinkAsync"/>.
/// </summary>
public enum AccessorLinkOutcome
{
    /// <summary>Email is unknown -- create a new IdentityUser, grant role, link.</summary>
    CreateUserAndLink = 1,

    /// <summary>User exists with the requested role -- just link.</summary>
    LinkExisting = 2,

    /// <summary>User exists with a different recognised external role -- throw BusinessException.</summary>
    RoleMismatch = 3,

    /// <summary>
    /// User exists with no recognised external role (e.g. internal
    /// staff user being added as an accessor) -- grant the requested
    /// role (idempotently) and link.
    /// </summary>
    GrantRoleAndLink = 4,
}
