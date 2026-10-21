using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// The appointment party-email columns are the access key: <c>AppointmentAccessRules</c> lets a
/// party open a case when its verified email matches the column for its role. So whoever can write
/// a column can grant access to any address. Staff may; an external party must not, not even one
/// that is ON the case (2026-10-21). Under REAL authorization, not the always-allow rig.
///
/// <para>Each refusal is asserted twice: the call is refused, AND the columns are read back
/// unchanged. The read-back is what makes the upsert fact meaningful: that endpoint is plain
/// <c>[Authorize]</c> plus the read guard, so an external party on the case IS allowed to call it.
/// What must hold is that it never writes a column.</para>
///
/// <para>The appointment written to is created per fact, booked by a fresh external user, so the
/// harness's shared office appointment is never changed.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class AppointmentPartiesAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string ExternalRole = "Applicant Attorney";

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentPartiesAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task An_external_party_on_the_case_is_refused_the_staff_parties_endpoint_and_nothing_is_written()
    {
        var (officeId, appointmentId, partyId) = await CreatePartyAppointmentAsync();

        var outcome = await CallAsync(officeId, partyId, ExternalRole, sp =>
            sp.GetRequiredService<IAppointmentPartiesAppService>().UpdateAsync(appointmentId, new AppointmentPartiesUpdateDto
            {
                DefenseAttorney = new StaffAttorneyChangeDto
                {
                    Email = "TEST-planted-da@example.test",
                    FirstName = "TEST",
                    LastName = "Planted",
                    FirmName = "TEST-Firm",
                },
                PatientEmail = "TEST-planted-patient@example.test",
                ClaimExaminer = new StaffClaimExaminerChangeDto { Email = "TEST-planted-ce@example.test" },
            }));

        outcome.ShouldBeOfType<AbpAuthorizationException>();
        await ColumnsShouldBeUntouchedAsync(officeId, appointmentId);
    }

    [Fact]
    public async Task An_external_party_on_the_case_is_refused_the_appointment_update()
    {
        var (officeId, appointmentId, partyId) = await CreatePartyAppointmentAsync();

        var outcome = await CallAsync(officeId, partyId, ExternalRole, sp =>
            sp.GetRequiredService<IAppointmentsAppService>().UpdateAsync(appointmentId, new AppointmentUpdateDto
            {
                PatientId = Guid.NewGuid(),
                AppointmentTypeId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
                DoctorAvailabilityId = Guid.NewGuid(),
                AppointmentDate = DateTime.UtcNow,
                RequestConfirmationNumber = "TEST-X",
                ConcurrencyStamp = "TEST-stamp",
                DefenseAttorneyEmail = "TEST-planted-da@example.test",
            }));

        outcome.ShouldBeOfType<AbpAuthorizationException>();
        await ColumnsShouldBeUntouchedAsync(officeId, appointmentId);
    }

    [Fact]
    public async Task The_attorney_upsert_called_by_an_external_party_on_the_case_never_writes_an_email_column()
    {
        var (officeId, appointmentId, partyId) = await CreatePartyAppointmentAsync();

        // This call is ALLOWED (the caller booked the case). It may write the attorney master and
        // link rows, exactly as booking does; it must not write the access key.
        var defenseOutcome = await CallAsync(officeId, partyId, ExternalRole, sp =>
            sp.GetRequiredService<IAppointmentsAppService>().UpsertDefenseAttorneyForAppointmentAsync(appointmentId, new DefenseAttorneyDetailsDto
            {
                Email = "TEST-planted-da@example.test",
                FirstName = "TEST",
                LastName = "Planted",
                FirmName = "TEST-Firm",
            }));
        var applicantOutcome = await CallAsync(officeId, partyId, ExternalRole, sp =>
            sp.GetRequiredService<IAppointmentsAppService>().UpsertApplicantAttorneyForAppointmentAsync(appointmentId, new ApplicantAttorneyDetailsDto
            {
                Email = "TEST-planted-aa@example.test",
                FirstName = "TEST",
                LastName = "Planted",
                FirmName = "TEST-Firm",
            }));

        // Both calls went through: without this, "no column written" would also pass on a refusal.
        defenseOutcome.ShouldBeNull();
        applicantOutcome.ShouldBeNull();
        await ColumnsShouldBeUntouchedAsync(officeId, appointmentId);
    }

    [Fact]
    public async Task Intake_staff_pass_the_parties_permission()
    {
        // Positive control for the first fact: production Intake Staff reach the service body. An
        // empty change writes nothing, so the shared fixture is untouched.
        var fixture = await GetFixtureAsync();

        var outcome = await CallAsync(fixture.Office.OfficeId, Guid.NewGuid(), InternalUserRoleDataSeedContributor.IntakeStaffRoleName, sp =>
            sp.GetRequiredService<IAppointmentPartiesAppService>().UpdateAsync(fixture.Office.AppointmentId, new AppointmentPartiesUpdateDto()));

        outcome.ShouldBeNull("Intake Staff hold Appointments.Edit in production and must be able to save parties");
    }

    private async Task<Exception?> CallAsync(Guid officeId, Guid userId, string role, Func<IServiceProvider, Task> call)
    {
        Exception? caught = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            using (WithCurrentUser.Run(_principalAccessor, userId, role))
            {
                try
                {
                    await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);
        return caught;
    }

    /// <summary>An appointment in the harness office, booked by a fresh external user (its creator).</summary>
    private async Task<(Guid officeId, Guid appointmentId, Guid partyId)> CreatePartyAppointmentAsync()
    {
        var fixture = await GetFixtureAsync();
        var office = fixture.Office;
        var partyId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, partyId, ExternalRole))
            {
                var appointment = new Appointment(
                    id: appointmentId,
                    patientId: office.PatientId,
                    // Null: that column is an FK to AbpUsers. The party is the booker (BookedByUserId)
                    // and the creator (this insert runs as them), which is what the read guard reads.
                    identityUserId: null,
                    appointmentTypeId: office.AppointmentTypeId,
                    locationId: office.LocationId,
                    doctorAvailabilityId: office.DoctorAvailabilityId,
                    appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: $"A9-AZ-{appointmentId:N}"[..16],
                    appointmentStatus: AppointmentStatusType.Approved)
                {
                    TenantId = office.OfficeId,
                    PatientEmail = office.PatientEmail,
                };
                appointment.RecordBookedBy(partyId);
                await ServiceProvider.GetRequiredService<IAppointmentRepository>().InsertAsync(appointment, autoSave: true);
            }
        }, requiresNew: true);
        return (office.OfficeId, appointmentId, partyId);
    }

    private async Task ColumnsShouldBeUntouchedAsync(Guid officeId, Guid appointmentId)
    {
        var fixture = await GetFixtureAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var appointment = await ServiceProvider.GetRequiredService<IAppointmentRepository>().GetAsync(appointmentId);
                appointment.DefenseAttorneyEmail.ShouldBeNull();
                appointment.ApplicantAttorneyEmail.ShouldBeNull();
                appointment.ClaimExaminerEmail.ShouldBeNull();
                appointment.PatientEmail.ShouldBe(fixture.Office.PatientEmail);
            }
        }, requiresNew: true);
    }
}
