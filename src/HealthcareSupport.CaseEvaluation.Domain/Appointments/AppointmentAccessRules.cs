using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13 (2026-05-04) -- pure access predicates for the
/// "view appointment" + "edit appointment" gates.
///
/// 2026-05-12 expansion (2.5/2.6 fix): widened from 2 pathways to 7 to
/// match the list endpoint's <c>ComputeExternalPartyVisibilityAsync</c>
/// rule set. Prior shape only checked Creator + Accessor, which caused
/// AA/DA/CE/Patient to see appointments in their list but get 403 on
/// click. New rule set mirrors the list rules exactly.
///
/// Pathways (checked in order, first match wins):
///   1. Internal user bypass (admin / Intake Staff / Staff Supervisor /
///      IT Admin / Doctor) -- always allowed.
///   2. Creator -- <c>Appointment.CreatorId == CurrentUser.Id</c>.
///   3. Patient -- <c>Patient.IdentityUserId == CurrentUser.Id</c>
///      (caller is the patient on the appointment).
///   4. Applicant Attorney -- caller's IdentityUserId is on any
///      <c>AppointmentApplicantAttorney</c> link row.
///   5. Defense Attorney -- caller's IdentityUserId is on any
///      <c>AppointmentDefenseAttorney</c> link row.
///   5b. Paralegal (2026-06-10, Phase 1) -- caller's IdentityUserId is a
///      paralegal delegate on any AA/DA link row. CanRead ONLY: CanEdit is
///      intentionally NOT widened, because the only Phase-1 paralegal is the
///      booking-side creator (already edit-capable via pathway 2); the
///      non-creator opposing-side paralegal edit pathway is Phase 2.
///   6. Claim Examiner -- caller's email matches any
///      <c>AppointmentClaimExaminer.Email</c> (case-insensitive).
///   7. Appointment Accessor -- caller's IdentityUserId is on any
///      <c>AppointmentAccessor</c> row (with appropriate AccessType
///      for CanEdit).
///
/// Pure (no DI / no repos): the orchestrator passes in the live access
/// state. Lives in Domain so the AppService can compose it.
/// </summary>
public static class AppointmentAccessRules
{
    /// <summary>
    /// Returns true when the caller can read the appointment, with the
    /// matched pathway for telemetry. See class-level docs for the 7
    /// pathways. Empty / null collections are treated as "no match" for
    /// that pathway only.
    /// </summary>
    public static (bool allowed, AccessPathway? pathway) CanRead(
        Guid? callerUserId,
        string? callerEmail,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        Guid? patientIdentityUserId,
        IEnumerable<Guid>? applicantAttorneyIdentityUserIds,
        IEnumerable<Guid>? defenseAttorneyIdentityUserIds,
        IEnumerable<string>? claimExaminerEmails,
        IEnumerable<AccessorEntry>? accessorEntries,
        // Paralegal delegate ids (2026-06-10, Phase 1). Trailing + optional so the
        // pre-existing full-overload callers compile unchanged (omitted -> no
        // paralegal pathway). Only AppointmentReadAccessGuard.EnsureCanReadAsync
        // passes it. The CHECK is still placed after the DA branch (see below).
        IEnumerable<Guid>? paralegalIdentityUserIds = null)
    {
        if (callerIsInternalUser)
        {
            return (true, AccessPathway.InternalUser);
        }
        if (!callerUserId.HasValue)
        {
            return (false, null);
        }

        var userId = callerUserId.Value;

        if (appointmentCreatorId.HasValue && appointmentCreatorId.Value == userId)
        {
            return (true, AccessPathway.Creator);
        }
        if (patientIdentityUserId.HasValue && patientIdentityUserId.Value == userId)
        {
            return (true, AccessPathway.Patient);
        }
        if (applicantAttorneyIdentityUserIds != null
            && applicantAttorneyIdentityUserIds.Any(id => id == userId))
        {
            return (true, AccessPathway.ApplicantAttorney);
        }
        if (defenseAttorneyIdentityUserIds != null
            && defenseAttorneyIdentityUserIds.Any(id => id == userId))
        {
            return (true, AccessPathway.DefenseAttorney);
        }
        // Paralegal delegate (2026-06-10, Phase 1): the caller's IdentityUserId
        // is recorded as a paralegal on an AA/DA link row. Read-only pathway --
        // the booking paralegal is also the Creator (covered above for edit).
        if (paralegalIdentityUserIds != null
            && paralegalIdentityUserIds.Any(id => id == userId))
        {
            return (true, AccessPathway.Paralegal);
        }
        if (!string.IsNullOrWhiteSpace(callerEmail)
            && claimExaminerEmails != null
            && claimExaminerEmails.Any(e =>
                !string.IsNullOrWhiteSpace(e)
                && string.Equals(e.Trim(), callerEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return (true, AccessPathway.ClaimExaminer);
        }
        if (accessorEntries != null
            && accessorEntries.Any(a => a.IdentityUserId == userId))
        {
            return (true, AccessPathway.AppointmentAccessor);
        }
        return (false, null);
    }

    /// <summary>
    /// Backward-compatible overload used by existing tests + any caller
    /// that has not yet been widened. Defers to the new 7-pathway form
    /// with null for the new fields, so external callers fall back to
    /// the old Creator + Accessor check unless the caller widens the
    /// inputs. Returns only the boolean for ergonomic call sites.
    /// </summary>
    public static bool CanRead(
        Guid? callerUserId,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        IEnumerable<AccessorEntry>? accessorEntries)
    {
        return CanRead(
            callerUserId: callerUserId,
            callerEmail: null,
            callerIsInternalUser: callerIsInternalUser,
            appointmentCreatorId: appointmentCreatorId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: accessorEntries).allowed;
    }

    /// <summary>
    /// Edit predicate. Same 7-pathway expansion, but the
    /// <see cref="AccessPathway.AppointmentAccessor"/> branch requires
    /// <see cref="AccessType.Edit"/> -- View-only accessors return
    /// false on this gate. Patient/AA/DA/CE who would be allowed to
    /// read are also allowed to edit per OLD parity (the OLD app
    /// treated "named on the appointment" as edit-capable for any
    /// external party that wasn't an explicit View-only accessor).
    /// </summary>
    public static (bool allowed, AccessPathway? pathway) CanEdit(
        Guid? callerUserId,
        string? callerEmail,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        Guid? patientIdentityUserId,
        IEnumerable<Guid>? applicantAttorneyIdentityUserIds,
        IEnumerable<Guid>? defenseAttorneyIdentityUserIds,
        IEnumerable<string>? claimExaminerEmails,
        IEnumerable<AccessorEntry>? accessorEntries)
    {
        if (callerIsInternalUser)
        {
            return (true, AccessPathway.InternalUser);
        }
        if (!callerUserId.HasValue)
        {
            return (false, null);
        }

        var userId = callerUserId.Value;

        if (appointmentCreatorId.HasValue && appointmentCreatorId.Value == userId)
        {
            return (true, AccessPathway.Creator);
        }
        if (patientIdentityUserId.HasValue && patientIdentityUserId.Value == userId)
        {
            return (true, AccessPathway.Patient);
        }
        if (applicantAttorneyIdentityUserIds != null
            && applicantAttorneyIdentityUserIds.Any(id => id == userId))
        {
            return (true, AccessPathway.ApplicantAttorney);
        }
        if (defenseAttorneyIdentityUserIds != null
            && defenseAttorneyIdentityUserIds.Any(id => id == userId))
        {
            return (true, AccessPathway.DefenseAttorney);
        }
        if (!string.IsNullOrWhiteSpace(callerEmail)
            && claimExaminerEmails != null
            && claimExaminerEmails.Any(e =>
                !string.IsNullOrWhiteSpace(e)
                && string.Equals(e.Trim(), callerEmail.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return (true, AccessPathway.ClaimExaminer);
        }
        if (accessorEntries != null
            && accessorEntries.Any(a => a.IdentityUserId == userId
                                        && a.AccessType == AccessType.Edit))
        {
            return (true, AccessPathway.AppointmentAccessor);
        }
        return (false, null);
    }

    /// <summary>
    /// Backward-compatible overload for the legacy 2-pathway CanEdit
    /// (creator + accessor.Edit). Kept so existing tests + callers
    /// compile unchanged; new callers should prefer the 7-pathway form.
    /// </summary>
    public static bool CanEdit(
        Guid? callerUserId,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        IEnumerable<AccessorEntry>? accessorEntries)
    {
        return CanEdit(
            callerUserId: callerUserId,
            callerEmail: null,
            callerIsInternalUser: callerIsInternalUser,
            appointmentCreatorId: appointmentCreatorId,
            patientIdentityUserId: null,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: accessorEntries).allowed;
    }

    /// <summary>
    /// Accessor-management gate (2026-06-10, Workstream B). Stricter than
    /// <see cref="CanEdit(Guid?, bool, Guid?, IEnumerable{AccessorEntry})"/>: an
    /// external caller must be BOTH the appointment creator AND hold an authorized
    /// accessor-managing external role (today: Applicant Attorney / Defense
    /// Attorney; the paralegal feature appends Paralegal as a one-line set
    /// extension in <c>BookingFlowRoles</c>, with no change to this rule). The
    /// Edit-accessor pathway is intentionally NOT admitted here -- Edit-accessors
    /// may complete/edit the appointment form (CanEdit) but may not add accessors.
    /// Pure: the guard computes the role bool from <c>ICurrentUser.Roles</c>, so
    /// this rule stays string-free (no accessor hydration needed either).
    /// </summary>
    public static bool CanManageAccessors(
        Guid? callerUserId,
        bool callerIsInternalUser,
        bool callerIsAuthorizedExternalAccessorManager,
        Guid? appointmentCreatorId)
    {
        if (callerIsInternalUser)
        {
            return true;
        }
        if (!callerUserId.HasValue)
        {
            return false;
        }
        return appointmentCreatorId.HasValue
            && appointmentCreatorId.Value == callerUserId.Value
            && callerIsAuthorizedExternalAccessorManager;
    }

    /// <summary>
    /// Lightweight projection of <see cref="AppointmentAccessor"/>
    /// rows for predicate consumption. Keeps the rule pure (no
    /// dependency on EF Core / ABP entity types beyond the
    /// <see cref="AccessType"/> enum already in Domain.Shared).
    /// </summary>
    public sealed record AccessorEntry(Guid IdentityUserId, AccessType AccessType);

    /// <summary>
    /// Which of the 7 pathways granted access on a positive result.
    /// Returned alongside the bool for telemetry / debugging / future
    /// audit logging. Null when access was denied.
    /// </summary>
    public enum AccessPathway
    {
        InternalUser,
        Creator,
        Patient,
        ApplicantAttorney,
        DefenseAttorney,
        Paralegal,
        ClaimExaminer,
        AppointmentAccessor,
    }
}
