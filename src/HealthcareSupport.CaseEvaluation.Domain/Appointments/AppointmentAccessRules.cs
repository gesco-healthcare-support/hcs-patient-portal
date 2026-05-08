using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13 (2026-05-04) -- pure access predicates for the
/// "view appointment" + "edit appointment" gates.
///
/// Mirrors OLD's behavior at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:62
/// (stored proc <c>spm.spAppointmentRequestList</c> filters to creator
/// OR accessor) and the per-appointment <c>Get(int id)</c> path which
/// includes <c>AppointmentAccessors</c> and is paired with a UI-level
/// access check.
///
/// NEW models the rule as:
///   - Internal users (admin / Clinic Staff / Staff Supervisor / IT
///     Admin / Doctor) can read+edit any appointment in their tenant.
///     ABP's automatic IMultiTenant filter still scopes them to their
///     own tenant; this rule decides cross-role access INSIDE a
///     tenant.
///   - External users can read iff they are the creator OR have an
///     <see cref="AppointmentAccessor"/> row pointing at them.
///   - External users can edit iff they are the creator OR have an
///     accessor row with <see cref="AccessType.Edit"/> (24). View
///     (23) accessors are read-only.
///
/// Pure (no DI / no repos): the orchestrator passes in the live
/// access state. Lives in Domain so the AppService can compose it.
/// </summary>
public static class AppointmentAccessRules
{
    /// <summary>
    /// Returns true when <paramref name="callerUserId"/> can read the
    /// appointment whose <paramref name="appointmentCreatorId"/> and
    /// <paramref name="accessorEntries"/> are supplied. Internal
    /// callers (per <see cref="BookingFlowRoles.IsInternalUserCaller"/>
    /// applied upstream by the AppService) bypass the per-row check.
    /// </summary>
    /// <param name="callerUserId">The current user's IdentityUser id.</param>
    /// <param name="callerIsInternalUser">
    /// True when the caller holds at least one internal role (admin /
    /// Clinic Staff / Staff Supervisor / IT Admin / Doctor). The caller
    /// computes this once and passes it down so the predicate stays
    /// pure.
    /// </param>
    /// <param name="appointmentCreatorId">
    /// The appointment's <c>CreatorId</c>. May be null on legacy /
    /// system-created rows; treated as "no creator match".
    /// </param>
    /// <param name="accessorEntries">
    /// Every <see cref="AppointmentAccessor"/> row for the target
    /// appointment, projected to (IdentityUserId, AccessType). The
    /// caller has already loaded these from the
    /// <see cref="AppointmentAccessors.IAppointmentAccessorRepository"/>.
    /// </param>
    public static bool CanRead(
        Guid? callerUserId,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        IEnumerable<AccessorEntry>? accessorEntries)
    {
        if (callerIsInternalUser)
        {
            return true;
        }
        if (!callerUserId.HasValue)
        {
            return false;
        }
        if (appointmentCreatorId.HasValue && appointmentCreatorId.Value == callerUserId.Value)
        {
            return true;
        }
        if (accessorEntries == null)
        {
            return false;
        }
        return accessorEntries.Any(a => a.IdentityUserId == callerUserId.Value);
    }

    /// <summary>
    /// Returns true when the caller can EDIT the appointment.
    /// Internal callers bypass; external callers must be the creator
    /// OR hold an accessor row with <see cref="AccessType.Edit"/>.
    /// View (23) accessors return false.
    /// </summary>
    public static bool CanEdit(
        Guid? callerUserId,
        bool callerIsInternalUser,
        Guid? appointmentCreatorId,
        IEnumerable<AccessorEntry>? accessorEntries)
    {
        if (callerIsInternalUser)
        {
            return true;
        }
        if (!callerUserId.HasValue)
        {
            return false;
        }
        if (appointmentCreatorId.HasValue && appointmentCreatorId.Value == callerUserId.Value)
        {
            return true;
        }
        if (accessorEntries == null)
        {
            return false;
        }
        return accessorEntries.Any(a => a.IdentityUserId == callerUserId.Value
                                        && a.AccessType == AccessType.Edit);
    }

    /// <summary>
    /// Lightweight projection of <see cref="AppointmentAccessor"/>
    /// rows for predicate consumption. Keeps the rule pure (no
    /// dependency on EF Core / ABP entity types beyond the
    /// <see cref="AccessType"/> enum already in Domain.Shared).
    /// </summary>
    public sealed record AccessorEntry(Guid IdentityUserId, AccessType AccessType);
}
