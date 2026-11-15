using System;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <c>data.office</c> on the REAL rig (2026-11-15): the intake queued for an appointment carries
/// that office's letterhead, read from the HOST branding row while the enqueue runs inside the
/// office, in one transactional unit of work, which is how an approval enqueues it.
///
/// <para>Real queue, real builder, real letterhead resolver, real <see cref="ICurrentTenant"/>:
/// nothing is substituted. The OFFICE DECOY is real too: office B has its own letterhead, and office
/// A's intake must carry A's and never B's. A lookup that ignored the current office would fail.</para>
///
/// <para>What this rig cannot show: it keeps every office in one database, so the host read here
/// shares a connection with the office's. In production the host database is a separate connection
/// inside the same unit of work, the same thing the packet job already does through the same
/// resolver.</para>
/// </summary>
public class EfCoreIntakeOfficeSectionTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string PracticeA = "TEST Office A Spine Center";
    private const string PracticeB = "TEST Office B Pain Clinic";

    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly IRepository<OfficeBranding, Guid> _brandings;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreIntakeOfficeSectionTests()
    {
        _intakeQueue = GetRequiredService<ICaseTrackerIntakeQueue>();
        _brandings = GetRequiredService<IRepository<OfficeBranding, Guid>>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task The_queued_intake_carries_its_own_offices_letterhead_read_from_the_host()
    {
        await SetLetterheadAsync(TenantsTestData.TenantARef, new OfficeLetterheadValues
        {
            PracticeName = PracticeA,
            Phone = "(555) 010-0100",
            RecordsReleaseAddress = "TEST RECORDS DEPT.\r\n100 SAMPLE WAY\r\nSAMPLETOWN, CA 90000",
        });
        await SetLetterheadAsync(TenantsTestData.TenantBRef, new OfficeLetterheadValues
        {
            PracticeName = PracticeB,
            Phone = "(555) 010-0199",
            Fax = "(555) 010-0198",
        });
        var appointmentId = await CreateApprovedAppointmentInOfficeAAsync();

        var payload = await WithUnitOfWorkAsync(new AbpUnitOfWorkOptions { IsTransactional = true }, async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var row = await _intakeQueue.EnqueueIntakeAsync(appointmentId, TenantsTestData.TenantARef);
                return row.Payload;
            }
        });

        using var json = JsonDocument.Parse(payload);
        var data = json.RootElement.GetProperty("data");
        data.GetProperty("tenant").GetProperty("tenantId").GetGuid().ShouldBe(TenantsTestData.TenantARef);

        var office = data.GetProperty("office");
        office.GetProperty("practiceName").GetString().ShouldBe(PracticeA);
        office.GetProperty("phone").GetString().ShouldBe("(555) 010-0100");
        office.GetProperty("fax").ValueKind.ShouldBe(JsonValueKind.Null, "office A stores no fax; office B's must not leak in");
        office.GetProperty("recordsReleaseAddress").GetString()
            .ShouldBe("TEST RECORDS DEPT.\n100 SAMPLE WAY\nSAMPLETOWN, CA 90000");
        payload.ShouldNotContain(PracticeB);
    }

    /// <summary>Writes an office's letterhead to its HOST branding row, creating the row if needed.</summary>
    private Task SetLetterheadAsync(Guid officeId, OfficeLetterheadValues values) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var branding = await _brandings.FirstOrDefaultAsync(b => b.OfficeId == officeId);
                if (branding == null)
                {
                    branding = new OfficeBranding(Guid.NewGuid(), officeId);
                    branding.SetLetterhead(values);
                    await _brandings.InsertAsync(branding, autoSave: true);
                    return;
                }

                branding.SetLetterhead(values);
                await _brandings.UpdateAsync(branding, autoSave: true);
            }
        });

    private async Task<Guid> CreateApprovedAppointmentInOfficeAAsync()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var email = $"pt-{token}@example.test";

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _patientRepository.InsertAsync(new Patient(
                    id: patientId,
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: null,
                    tenantId: TenantsTestData.TenantARef,
                    firstName: $"TEST-{token}",
                    lastName: $"TEST-{token}",
                    email: email,
                    genderId: (Gender)PatientsTestData.PatientGenderIdValue,
                    dateOfBirth: PatientsTestData.FixedDateOfBirth,
                    phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue), autoSave: true);

                await _appointmentRepository.InsertAsync(new Appointment(
                    id: appointmentId,
                    patientId: patientId,
                    identityUserId: null,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: $"A9-OF-{token}",
                    appointmentStatus: AppointmentStatusType.Approved)
                {
                    TenantId = TenantsTestData.TenantARef,
                    PatientEmail = email,
                }, autoSave: true);
            }
        });

        return appointmentId;
    }
}
