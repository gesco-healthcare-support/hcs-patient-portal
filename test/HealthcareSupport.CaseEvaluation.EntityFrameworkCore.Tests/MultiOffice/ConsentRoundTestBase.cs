using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Shared setup for the phase 4c consent-round suites (2026-08-05). Every one of them needs the
/// same three things -- run inside an office's tenant context, seed a reschedule change request,
/// and build a round on a seeded slot -- so they live here rather than being copy-pasted four
/// times.
/// </summary>
public abstract class ConsentRoundTestBase : CaseEvaluationMultiOfficeTestBase
{
    protected IRepository<AppointmentChangeRequest, Guid> ChangeRequestRepository =>
        GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();

    protected IChangeRequestConsentRoundRepository RoundRepository =>
        GetRequiredService<IChangeRequestConsentRoundRepository>();

    private ICurrentTenant CurrentTenant => GetRequiredService<ICurrentTenant>();

    /// <summary>
    /// Runs <paramref name="action"/> inside the office's tenant context in its OWN unit of work.
    /// <c>requiresNew</c> matters: crossing into a different tenant only re-resolves the office
    /// connection on a fresh unit of work (ABP #16357 / this repo's B9 finding).
    /// </summary>
    protected Task InOfficeAsync(SeededOffice office, Func<Task> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (CurrentTenant.Change(office.OfficeId))
            {
                await action();
            }
        }, requiresNew: true);

    /// <summary>A pending reschedule request against the office's appointment, with no proposed slot.</summary>
    protected Task SeedRescheduleRequestAsync(SeededOffice office, Guid changeRequestId) =>
        SeedRescheduleRequestAsync(office, changeRequestId, office.AppointmentId);

    /// <summary>A pending reschedule request against a caller-supplied appointment.</summary>
    protected async Task SeedRescheduleRequestAsync(
        SeededOffice office, Guid changeRequestId, Guid appointmentId)
    {
        await ChangeRequestRepository.InsertAsync(
            new AppointmentChangeRequest(
                id: changeRequestId,
                tenantId: office.OfficeId,
                appointmentId: appointmentId,
                changeRequestType: ChangeRequestType.Reschedule,
                cancellationReason: null,
                reScheduleReason: "TEST-reschedule-reason",
                newDoctorAvailabilityId: null),
            autoSave: true);
    }

    /// <summary>An un-sent round on the office's seeded slot; the caller issues consent as needed.</summary>
    protected static ChangeRequestConsentRound NewRound(
        SeededOffice office,
        Guid changeRequestId,
        Guid roundId,
        int roundNumber = 1,
        Guid? proposedSlotId = null) =>
        new(
            id: roundId,
            tenantId: office.OfficeId,
            appointmentChangeRequestId: changeRequestId,
            roundNumber: roundNumber,
            proposedDoctorAvailabilityId: proposedSlotId ?? office.DoctorAvailabilityId,
            proposedByUserId: office.BookerUserId);
}
