using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation;
using Volo.Abp;
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
    /// <para><b>ORDER IS WHAT HIDES THE DIFFERENCE, not the message.</b> The party check runs
    /// FIRST, against the row's stored parent. A caller who is not a party is therefore refused
    /// identically whatever appointment id they supply, and never learns whether their guess matched
    /// the row's real parent. Only a caller who can already read that appointment can reach the
    /// second check, so the second refusal tells them nothing they did not already have.</para>
    ///
    /// <para>An earlier version ran the same-parent check first and claimed the two refusals were
    /// indistinguishable because they shared an exception. They did not: this class threw
    /// <c>AbpAuthorizationException</c> while the real read gate throws
    /// <c>BusinessException(AppointmentAccessDenied)</c>, so a non-party got one error when their
    /// supplied id was wrong and the other when it happened to be right -- exactly the distinction
    /// the comment set out to deny. The tests agreed with the comment only because they stubbed the
    /// read gate to throw the wrong type. Both are fixed: the order, and the exception.</para>
    /// </summary>
    public virtual async Task EnsureCanWriteChildAsync(Guid existingAppointmentId, Guid suppliedAppointmentId)
    {
        await EnsureIsPartyAsync(existingAppointmentId);
        EnsureSameParent(existingAppointmentId, suppliedAppointmentId);
    }

    /// <summary>
    /// Refuses a write that would move a row to a different parent. Split out from
    /// <see cref="EnsureCanWriteChildAsync"/> for the GRANDCHILD case: an appointment body part
    /// hangs off an injury detail rather than an appointment, so its parent check and its party
    /// check are against different ids and cannot be one call.
    ///
    /// <para>Raises the SAME exception the read gate raises, so a caller cannot separate the two
    /// refusals by type either. Callers must run <see cref="EnsureIsPartyAsync"/> BEFORE this, or a
    /// non-party can distinguish a correct parent guess from a wrong one by which refusal arrives.</para>
    /// </summary>
    public virtual void EnsureSameParent(Guid existingParentId, Guid suppliedParentId)
    {
        if (suppliedParentId != existingParentId)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
    }

    /// <summary>
    /// Refuses unless the caller is a party to the appointment. The update path calls this against a
    /// row's stored parent; the create path calls it against the appointment a new row will hang off.
    ///
    /// <para><b>Who passes it when a booking writes its own child rows.</b> The booker, through
    /// <c>AppointmentAccessRules</c> pathway 2, <c>AccessPathway.Creator</c>: the appointment's
    /// <c>CreatorId</c> (or <c>BookedByUserId</c>) equals the caller. <c>SubmitAsync</c> flushes the new
    /// appointment before writing any child group, which is what stamps <c>CreatorId</c> in time; an
    /// internal booker passes earlier, through pathway 1.</para>
    ///
    /// <para><b>That grant is PERMANENT, and deliberately so.</b> Whoever booked an appointment may go
    /// on adding and editing its child records for as long as it exists, because it is their booking;
    /// the view page's employer-detail add relies on exactly this. <c>CreatorId</c> does not change after
    /// the booking, so nothing ever revokes it. If the booker should ever lose that right (for example
    /// after handing a case over), this is the rule to change, and <c>CanReadAsync</c> is where.</para>
    /// </summary>
    public virtual async Task EnsureIsPartyAsync(Guid appointmentId)
    {
        await _readAccessGuard.EnsureCanReadAsync(appointmentId);
    }
}
