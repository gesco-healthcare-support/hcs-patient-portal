using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Gates a write to an appointment CHILD row on the caller being a party to the row's parent
/// appointment, and refuses an attempt to move the row to a different parent.
///
/// <para><b>Why this exists.</b> Seven appointment child-resource application services accepted an
/// update by row id and took the parent <c>AppointmentId</c> from the request body, with no check
/// that the caller was a party to that appointment and no comparison against the row's existing
/// parent. Every external role holds the <c>.Edit</c> permission those services gate on, so an
/// authenticated external user could rewrite another patient's claim data inside the same office --
/// including the opposing party's representative, on the record a Workers' Compensation Appeals
/// Board decision turns on.</para>
///
/// <para><b>Why the read rule rather than the edit rule.</b> This calls
/// <see cref="AppointmentReadAccessGuard.EnsureCanReadAsync(Guid)"/>, whose rule is "the caller is a
/// party to this appointment". That is the same gate <c>AppointmentDocumentsAppService</c> already
/// applies to read, upload AND delete, adopted there in 2026-05-13 after the identical defect: it
/// "previously gated only by permission + tenant and so let any same-tenant external party
/// read/upload/delete documents on an appointment they were not a party to". Same defect, same
/// codebase, same fix, so this follows the established precedent rather than inventing a second
/// standard.</para>
///
/// <para>The slimmer <see cref="AppointmentReadAccessGuard.CanEditAsync(Guid)"/> rule -- internal
/// user, creator or Edit-accessor only -- would refuse a party who did not personally book the
/// appointment. Tightening to it is defensible and is a SEPARATE decision, because it changes
/// behaviour for legitimate parties rather than only closing this hole.</para>
/// </summary>
public class AppointmentChildOwnershipGuard : ITransientDependency
{
    private readonly AppointmentReadAccessGuard _readAccessGuard;

    public AppointmentChildOwnershipGuard(AppointmentReadAccessGuard readAccessGuard)
    {
        _readAccessGuard = readAccessGuard;
    }

    /// <summary>
    /// Refuses unless the caller is a party to <paramref name="existingAppointmentId"/>, and unless
    /// <paramref name="suppliedAppointmentId"/> names that same appointment.
    ///
    /// <para>The parent is taken from the ROW, never from the request. Checking the supplied id
    /// instead would let a caller nominate an appointment they are a party to and still write to a
    /// row belonging to someone else's.</para>
    ///
    /// <para>Both refusals raise the same <see cref="AbpAuthorizationException"/> with the same
    /// message, deliberately. Distinguishing "you are not a party" from "that is not this row's
    /// appointment" would confirm to a caller that a row id exists under a different parent.</para>
    /// </summary>
    public virtual async Task EnsureCanWriteChildAsync(Guid existingAppointmentId, Guid suppliedAppointmentId)
    {
        EnsureSameParent(existingAppointmentId, suppliedAppointmentId);
        await EnsureIsPartyAsync(existingAppointmentId);
    }

    /// <summary>
    /// Refuses a write that would move a row to a different parent. Split out from
    /// <see cref="EnsureCanWriteChildAsync"/> for the GRANDCHILD case: an appointment body part
    /// hangs off an injury detail rather than an appointment, so its parent check and its party
    /// check are against different ids and cannot be one call.
    /// </summary>
    public virtual void EnsureSameParent(Guid existingParentId, Guid suppliedParentId)
    {
        if (suppliedParentId != existingParentId)
        {
            throw new AbpAuthorizationException(
                "You are not authorised to modify this record.");
        }
    }

    /// <summary>Refuses unless the caller is a party to the appointment.</summary>
    public virtual async Task EnsureIsPartyAsync(Guid appointmentId)
    {
        await _readAccessGuard.EnsureCanReadAsync(appointmentId);
    }
}
