using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Applications.Integration;

/// <summary>
/// A post-approval edit to a row the appointment OWNS (employer, injury, primary insurance, claim examiner) re-sends
/// the appointment's full snapshot to the Case Tracker, through the real child app services the edit screens call,
/// on the real provider (2026-11-23).
///
/// <para><b>Both ways round.</b> With ABP's <c>PublishEntityUpdatedEventWhenNavigationChanges</c> OFF, the explicit
/// <see cref="Integration.CaseTracker.Handlers.AppointmentChildChangedHandler{TChild}"/> subclasses are the only
/// trigger, so these tests prove them. With it ON (the default), the framework ALSO raises the appointment's update
/// event, and the two builds must collapse: still exactly ONE new row per edit. Measured before the handlers existed:
/// injury, insurance and examiner edits were re-sent only with the option on, and the employer was in no payload.</para>
///
/// <para>Appointments the automatic path does not publish, and ones whose packet set has not settled, get nothing.
/// All data synthetic.</para>
/// </summary>
public class EfCoreChildRePushTests : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IBackgroundJobManager _jobs = Substitute.For<IBackgroundJobManager>();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton(_jobs));
    }

    private ICurrentTenant Tenant => GetRequiredService<ICurrentTenant>();

    public static IEnumerable<object[]> ChildTypesBothWays() =>
        from child in new[] { "employer", "injury", "insurance", "examiner" }
        from navigationEvents in new[] { true, false }
        select new object[] { child, navigationEvents };

    [Theory]
    [MemberData(nameof(ChildTypesBothWays))]
    public async Task Each_create_update_and_delete_on_an_approved_appointment_queues_exactly_one_row(
        string child, bool navigationEvents)
    {
        await WithNavigationEventsAsync(navigationEvents, async () =>
        {
            var id = await AppointmentAsync(AppointmentStatusType.Approved);
            // With navigation events on, seeding the packets already re-sent the appointment once, exactly as in
            // production; count each edit's rows from here.
            var expected = (await IntakesAsync(id)).Count;

            await EditThroughTheServiceAsync(child, id, async () =>
            {
                expected++;
                (await IntakesAsync(id)).Count.ShouldBe(expected, $"one new row per {child} edit");
            });
        });
    }

    [Fact]
    public async Task The_employer_edits_carry_the_current_employer_and_a_delete_carries_null()
    {
        await WithNavigationEventsAsync(false, async () =>
        {
            var id = await AppointmentAsync(AppointmentStatusType.Approved);
            var employers = GetRequiredService<IAppointmentEmployerDetailsAppService>();

            var created = await InOfficeAsync(() => employers.CreateAsync(NewEmployer(id, "TEST Acme Logistics")));
            var employer = EmployerOf((await IntakesAsync(id)).Last());
            employer.GetProperty("name").GetString().ShouldBe("TEST Acme Logistics");
            employer.GetProperty("state").GetString().ShouldBe(StatesTestData.State1Name, "the State NAME");

            var updated = await InOfficeAsync(() => employers.UpdateAsync(created.Id, EmployerEdit(created, "TEST Acme Freight")));
            EmployerOf((await IntakesAsync(id)).Last()).GetProperty("name").GetString().ShouldBe("TEST Acme Freight");

            await InOfficeAsync(async () => { await employers.DeleteAsync(updated.Id); return 0; });
            EmployerOf((await IntakesAsync(id)).Last()).ValueKind.ShouldBe(JsonValueKind.Null, "a delete sends employer: null");
        });
    }

    [Fact]
    public async Task A_save_that_changes_nothing_queues_nothing_new()
    {
        var id = await AppointmentAsync(AppointmentStatusType.Approved);
        var employers = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        var created = await InOfficeAsync(() => employers.CreateAsync(NewEmployer(id, "TEST Acme Logistics")));
        var afterCreate = (await IntakesAsync(id)).Count;

        await InOfficeAsync(() => employers.UpdateAsync(created.Id, EmployerEdit(created, "TEST Acme Logistics")));

        (await IntakesAsync(id)).Count.ShouldBe(afterCreate, "identical data collapses onto the existing row");
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public async Task An_appointment_that_is_not_published_gets_nothing(AppointmentStatusType status)
    {
        var id = await AppointmentAsync(status);

        foreach (var child in new[] { "employer", "injury", "insurance", "examiner" })
        {
            await EditThroughTheServiceAsync(child, id, () => Task.CompletedTask);
        }

        (await IntakesAsync(id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_approved_appointment_whose_packets_are_still_rendering_gets_nothing()
    {
        var id = await AppointmentAsync(AppointmentStatusType.Approved, packets: PacketGenerationStatus.Generating);

        await EditThroughTheServiceAsync("employer", id, () => Task.CompletedTask);

        (await IntakesAsync(id)).ShouldBeEmpty("the settle path sends the complete body once the packets render");
    }

    /// <summary>Create, update, then delete one child row through its real app service; <paramref name="afterEachStep"/> runs after each.</summary>
    private Task EditThroughTheServiceAsync(string child, Guid id, Func<Task> afterEachStep) => child switch
    {
        "employer" => EmployerCrudAsync(id, afterEachStep),
        "injury" => InjuryCrudAsync(id, afterEachStep),
        "insurance" => InsuranceCrudAsync(id, afterEachStep),
        "examiner" => ExaminerCrudAsync(id, afterEachStep),
        _ => throw new ArgumentOutOfRangeException(nameof(child), child, null),
    };

    private async Task EmployerCrudAsync(Guid id, Func<Task> afterEachStep)
    {
        var service = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        var row = await InOfficeAsync(() => service.CreateAsync(NewEmployer(id, "TEST Acme Logistics")));
        await afterEachStep();
        row = await InOfficeAsync(() => service.UpdateAsync(row.Id, EmployerEdit(row, "TEST Acme Freight")));
        await afterEachStep();
        await InOfficeAsync(async () => { await service.DeleteAsync(row.Id); return 0; });
        await afterEachStep();
    }

    private async Task InjuryCrudAsync(Guid id, Func<Task> afterEachStep)
    {
        var service = GetRequiredService<IAppointmentInjuryDetailsAppService>();
        var row = await InOfficeAsync(() => service.CreateAsync(new AppointmentInjuryDetailCreateDto
        {
            AppointmentId = id,
            DateOfInjury = new DateTime(2025, 1, 2),
            ClaimNumber = "TEST-CLM-1",
            WcabAdj = "TEST-ADJ1",
            BodyPartsSummary = "TEST knee",
        }));
        await afterEachStep();
        row = await InOfficeAsync(() => service.UpdateAsync(row.Id, new AppointmentInjuryDetailUpdateDto
        {
            AppointmentId = id,
            DateOfInjury = new DateTime(2025, 1, 2),
            ClaimNumber = "TEST-CLM-2",
            WcabAdj = "TEST-ADJ1",
            BodyPartsSummary = "TEST knee",
            ConcurrencyStamp = row.ConcurrencyStamp,
        }));
        await afterEachStep();
        await InOfficeAsync(async () => { await service.DeleteAsync(row.Id); return 0; });
        await afterEachStep();
    }

    private async Task InsuranceCrudAsync(Guid id, Func<Task> afterEachStep)
    {
        var service = GetRequiredService<IAppointmentPrimaryInsurancesAppService>();
        var row = await InOfficeAsync(() => service.CreateAsync(new AppointmentPrimaryInsuranceCreateDto
        {
            AppointmentId = id,
            Name = "TEST Carrier One",
            IsActive = true,
        }));
        await afterEachStep();
        row = await InOfficeAsync(() => service.UpdateAsync(row.Id, new AppointmentPrimaryInsuranceUpdateDto
        {
            AppointmentId = id,
            Name = "TEST Carrier Two",
            IsActive = true,
            ConcurrencyStamp = row.ConcurrencyStamp,
        }));
        await afterEachStep();
        await InOfficeAsync(async () => { await service.DeleteAsync(row.Id); return 0; });
        await afterEachStep();
    }

    private async Task ExaminerCrudAsync(Guid id, Func<Task> afterEachStep)
    {
        var service = GetRequiredService<IAppointmentClaimExaminersAppService>();
        var row = await InOfficeAsync(() => service.CreateAsync(new AppointmentClaimExaminerCreateDto
        {
            AppointmentId = id,
            Name = "TEST Examiner One",
            Email = "ce-rp@example.test",
            IsActive = true,
        }));
        await afterEachStep();
        row = await InOfficeAsync(() => service.UpdateAsync(row.Id, new AppointmentClaimExaminerUpdateDto
        {
            AppointmentId = id,
            Name = "TEST Examiner Two",
            Email = "ce-rp@example.test",
            IsActive = true,
            ConcurrencyStamp = row.ConcurrencyStamp,
        }));
        await afterEachStep();
        await InOfficeAsync(async () => { await service.DeleteAsync(row.Id); return 0; });
        await afterEachStep();
    }

    private static JsonElement EmployerOf(IntegrationOutboxItem row)
    {
        using var json = JsonDocument.Parse(row.Payload);
        return json.RootElement.GetProperty("data").GetProperty("employer").Clone();
    }

    private static AppointmentEmployerDetailCreateDto NewEmployer(Guid appointmentId, string name) => new()
    {
        AppointmentId = appointmentId,
        EmployerName = name,
        Occupation = "TEST Warehouse Lead",
        PhoneNumber = "555-010-0142",
        City = "Encino",
        StateId = StatesTestData.State1Id,
    };

    private static AppointmentEmployerDetailUpdateDto EmployerEdit(AppointmentEmployerDetailDto current, string name) => new()
    {
        AppointmentId = current.AppointmentId,
        EmployerName = name,
        Occupation = current.Occupation,
        PhoneNumber = current.PhoneNumber,
        Street = current.Street,
        City = current.City,
        ZipCode = current.ZipCode,
        StateId = current.StateId,
        ConcurrencyStamp = current.ConcurrencyStamp,
    };

    private async Task WithNavigationEventsAsync(bool on, Func<Task> body)
    {
        var options = GetRequiredService<IOptions<AbpEntityChangeOptions>>().Value;
        options.PublishEntityUpdatedEventWhenNavigationChanges = on;
        try
        {
            await body();
        }
        finally
        {
            options.PublishEntityUpdatedEventWhenNavigationChanges = true;
        }
    }

    private async Task<T> InOfficeAsync<T>(Func<Task<T>> call)
    {
        using (Tenant.Change(TenantsTestData.TenantARef))
        {
            return await call();
        }
    }

    private Task<List<IntegrationOutboxItem>> IntakesAsync(Guid id) => WithUnitOfWorkAsync(async () =>
    {
        using (Tenant.Change(TenantsTestData.TenantARef))
        {
            var outbox = GetRequiredService<IIntegrationOutboxRepository>();
            return (await outbox.GetListAsync())
                .Where(r => r.AppointmentId == id && r.MessageType == IntegrationMessageType.Intake)
                .OrderBy(r => r.CreationTime)
                .ToList();
        }
    });

    private async Task<Guid> AppointmentAsync(
        AppointmentStatusType status, PacketGenerationStatus packets = PacketGenerationStatus.Generated)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N")[..8];
        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenant.Change(TenantsTestData.TenantARef))
            {
                await GetRequiredService<IRepository<Patient, Guid>>().InsertAsync(new Patient(
                    id: patientId, stateId: null, appointmentLanguageId: null, identityUserId: null,
                    tenantId: TenantsTestData.TenantARef, firstName: $"TEST-{token}", lastName: $"TEST-{token}",
                    email: $"pt-{token}@example.test", genderId: (Gender)PatientsTestData.PatientGenderIdValue,
                    dateOfBirth: PatientsTestData.FixedDateOfBirth,
                    phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue), autoSave: true);
                var appointment = new Appointment(
                    id: appointmentId, patientId: patientId, identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id, locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: $"A9-RP-{token}", appointmentStatus: status)
                {
                    TenantId = TenantsTestData.TenantARef,
                    PatientEmail = $"pt-{token}@example.test",
                };
                await GetRequiredService<IAppointmentRepository>().InsertAsync(appointment, autoSave: true);
                var packetRepository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
                foreach (var kind in new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer })
                {
                    await packetRepository.InsertAsync(new AppointmentPacket(
                        Guid.NewGuid(), TenantsTestData.TenantARef, appointmentId, kind,
                        $"TEST-{appointmentId:N}-{kind}.pdf", packets), autoSave: true);
                }
            }
        });
        return appointmentId;
    }
}
