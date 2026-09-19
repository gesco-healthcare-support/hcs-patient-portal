using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// One appointment a dashboard test created. The confirmation number is carried
/// back because two dashboard sections (the deadline list and the activity feed)
/// echo it, so a test can identify its OWN rows in a shared, accumulating rig
/// instead of asserting on counts of rows it did not create.
/// </summary>
public sealed record DashboardFixtureRow(Guid Id, string ConfirmationNumber);

/// <summary>
/// Shared fixture plumbing for the <see cref="IDashboardAppService"/> tests.
///
/// <para>Shape B (abstract body here, thin concrete runner in
/// EntityFrameworkCore.Tests) is not stylistic: the donut assertions read
/// <c>StatusPillPolicy</c>, which is <c>internal</c> to the Application assembly
/// and therefore visible ONLY to this test assembly.</para>
///
/// <para>Every helper writes through a repository inside an explicit unit of work
/// with the office scope nested inside, mirroring
/// <c>AppointmentReadAccessGuardTests</c>. Confirmation numbers are unique per
/// call because <c>IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber</c>
/// is a real unique index and rows accumulate.</para>
///
/// <para>NOT pinned anywhere in this family: the two
/// <c>AbpAuthorizationException</c> arms on the four gated members. The test
/// module calls <c>AddAlwaysAllowAuthorization()</c>, so a "Forbidden" assertion
/// has no failing input and would read as coverage it is not. The
/// <c>Check.NotNull(input, ...)</c> guards on <c>GetOfficesAsync</c> /
/// <c>GetTenantBreakdownAsync</c> are likewise unreachable through the public
/// surface: the service is resolved through DI, so ABP's method-invocation
/// validator rejects a null reference-type argument before the body runs.</para>
/// </summary>
public abstract class DashboardTestsBase<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>
    /// Default appointment DATE for fixture rows: deliberately a fixed past date,
    /// never "today". Today's schedule is a calendar-date window, so a fixture row
    /// dated today would leak into the schedule test's Take(8) and could crowd out
    /// the row that test actually asserts on.
    /// </summary>
    protected static readonly DateTime FixtureAppointmentDate =
        new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    protected IDashboardAppService Dashboard { get; }
    protected ICurrentTenant Tenancy { get; }
    protected IRepository<Appointment, Guid> AppointmentRepository { get; }
    protected IRepository<AppointmentChangeRequest, Guid> ChangeRequestRepository { get; }
    protected IRepository<Location, Guid> LocationRepository { get; }
    protected IRepository<AppointmentType, Guid> AppointmentTypeRepository { get; }
    protected IRepository<Doctor, Guid> DoctorRepository { get; }
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> OfficeRepository { get; }
    protected ISystemParameterRepository SystemParameterRepository { get; }

    protected DashboardTestsBase()
    {
        Dashboard = GetRequiredService<IDashboardAppService>();
        Tenancy = GetRequiredService<ICurrentTenant>();
        AppointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        ChangeRequestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        LocationRepository = GetRequiredService<IRepository<Location, Guid>>();
        AppointmentTypeRepository = GetRequiredService<IRepository<AppointmentType, Guid>>();
        DoctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
        OfficeRepository = GetRequiredService<IRepository<Volo.Saas.Tenants.Tenant, Guid>>();
        SystemParameterRepository = GetRequiredService<ISystemParameterRepository>();
    }

    /// <summary>
    /// Calls the legacy nav-badge endpoint in an explicit scope. Host scope is
    /// passed as an explicit <c>Change(null)</c> rather than relied on ambiently,
    /// so the branch under test is chosen by the test and not by whatever the
    /// default principal happens to carry.
    /// </summary>
    protected async Task<DashboardCountersDto> GetCountersAsync(Guid? officeId)
    {
        using (Tenancy.Change(officeId))
        {
            return await Dashboard.GetAsync();
        }
    }

    /// <summary>Calls the composite endpoint in an explicit scope. See <see cref="GetCountersAsync"/>.</summary>
    protected async Task<DashboardDto> GetDashboardAsync(Guid? officeId, DashboardRange range)
    {
        using (Tenancy.Change(officeId))
        {
            return await Dashboard.GetDashboardAsync(range);
        }
    }

    /// <summary>
    /// Inserts one synthetic appointment into <paramref name="officeId"/>.
    ///
    /// <para><paramref name="createdAtUtc"/> backdates <c>CreationTime</c>. ABP's
    /// audit setter only stamps a creation time that is still <c>default</c>, so a
    /// value assigned before the insert survives -- but that is exactly the kind of
    /// assumption that mis-bins every window test silently if it is wrong, so the
    /// helper RE-READS the row in a second unit of work and asserts the persisted
    /// value. A broken assumption fails here, by name, instead of quietly moving
    /// rows between buckets.</para>
    /// </summary>
    /// <param name="token">Short (max 8 chars) label woven into the confirmation
    /// number so a failure names the row. The number must stay under 50 chars.</param>
    protected async Task<DashboardFixtureRow> InsertAppointmentAsync(
        Guid officeId,
        string token,
        AppointmentStatusType status,
        DateTime? createdAtUtc = null,
        DateTime? approveDateUtc = null,
        DateTime? appointmentDate = null,
        Guid? appointmentTypeId = null,
        Guid? locationId = null)
    {
        var id = Guid.NewGuid();
        var confirmationNumber = $"TEST-{token}-{Guid.NewGuid():N}";

        // Patient and slot must be the ones seeded INTO this office: Patient is
        // IMultiTenant and the deadline list inner-joins it, so a patient belonging
        // to the other office would silently drop the row from that section.
        var isOfficeB = officeId == TenantsTestData.TenantBRef;
        var patientId = isOfficeB ? PatientsTestData.Patient2Id : PatientsTestData.Patient1Id;
        var slotId = isOfficeB
            ? DoctorAvailabilitiesTestData.Slot3Id
            : DoctorAvailabilitiesTestData.Slot1Id;

        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                var appointment = new Appointment(
                    id: id,
                    patientId: patientId,
                    // Optional FK; left null so the row needs no seeded login.
                    identityUserId: null,
                    appointmentTypeId: appointmentTypeId ?? LocationsTestData.AppointmentType1Id,
                    locationId: locationId ?? LocationsTestData.Location1Id,
                    doctorAvailabilityId: slotId,
                    appointmentDate: appointmentDate ?? FixtureAppointmentDate,
                    requestConfirmationNumber: confirmationNumber,
                    appointmentStatus: status)
                {
                    TenantId = officeId,
                    AppointmentApproveDate = approveDateUtc,
                };

                if (createdAtUtc.HasValue)
                {
                    SetCreationTime(appointment, createdAtUtc.Value);
                }

                await AppointmentRepository.InsertAsync(appointment, autoSave: true);
            }
        });

        if (createdAtUtc.HasValue)
        {
            var persisted = await WithUnitOfWorkAsync(async () =>
            {
                using (Tenancy.Change(officeId))
                {
                    return await AppointmentRepository.GetAsync(id);
                }
            });

            (persisted.CreationTime - createdAtUtc.Value).Duration()
                .ShouldBeLessThan(
                    TimeSpan.FromSeconds(1),
                    $"Backdating CreationTime did not survive the insert for {confirmationNumber}. " +
                    "Every window assertion in the dashboard tests depends on it.");
        }

        return new DashboardFixtureRow(id, confirmationNumber);
    }

    /// <summary>
    /// Inserts one cancel change request against <paramref name="appointmentId"/>.
    /// A non-Pending status is reached through <c>MarkDecided</c> -- the only way in
    /// since the status setter became protected -- which requires a UTC instant.
    /// </summary>
    protected async Task<Guid> InsertChangeRequestAsync(
        Guid officeId,
        Guid appointmentId,
        RequestStatusType status)
    {
        var id = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                var request = new AppointmentChangeRequest(
                    id: id,
                    tenantId: officeId,
                    appointmentId: appointmentId,
                    changeRequestType: ChangeRequestType.Cancel,
                    cancellationReason: "TEST-DSH synthetic cancel reason",
                    reScheduleReason: null,
                    newDoctorAvailabilityId: null);

                if (status != RequestStatusType.Pending)
                {
                    request.MarkDecided(status, null, DateTime.UtcNow);
                }

                await ChangeRequestRepository.InsertAsync(request, autoSave: true);
            }
        });

        return id;
    }

    /// <summary>
    /// Inserts a clinic location OWNED BY <paramref name="officeId"/>.
    ///
    /// <para>The integration seed inserts its three locations inside
    /// <c>Change(null)</c>, so they carry a null TenantId and belong to no office --
    /// the seed contributor's own comment calling them "not IMultiTenant" is stale.
    /// A location that an office can actually see has to be created here.
    /// <c>Location.TenantId</c> is <c>protected set</c>, so the tenant scope at
    /// insert time is the only way to stamp it.</para>
    /// </summary>
    protected async Task<Guid> InsertOfficeLocationAsync(Guid officeId, string name)
    {
        var id = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                await LocationRepository.InsertAsync(
                    new Location(id: id, stateId: null, name: name, parkingFee: 0m, isActive: true),
                    autoSave: true);
            }
        });

        return id;
    }

    /// <summary>
    /// Inserts an appointment type OWNED BY <paramref name="officeId"/>. Same
    /// reasoning as <see cref="InsertOfficeLocationAsync"/>: the seeded catalog is
    /// host-scoped and invisible from inside an office.
    /// </summary>
    protected async Task<Guid> InsertOfficeAppointmentTypeAsync(Guid officeId, string name)
    {
        var id = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                await AppointmentTypeRepository.InsertAsync(
                    new AppointmentType(id: id, name: name),
                    autoSave: true);
            }
        });

        return id;
    }

    /// <summary>All appointments visible inside one office, counted the way the service counts them.</summary>
    protected Task<int> CountAppointmentsAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                return await AppointmentRepository.CountAsync();
            }
        });

    /// <summary>Pending appointments visible inside one office.</summary>
    protected Task<int> CountPendingAppointmentsAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                return await AppointmentRepository.CountAsync(
                    a => a.AppointmentStatus == AppointmentStatusType.Pending);
            }
        });

    /// <summary>Doctors visible inside one office (one per office, by unique index).</summary>
    protected Task<int> CountDoctorsAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                return await DoctorRepository.CountAsync();
            }
        });

    /// <summary>Rows in the office registry, read host-scoped.</summary>
    protected Task<int> CountOfficesAsync() =>
        WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(null))
            {
                return await OfficeRepository.CountAsync();
            }
        });

    /// <summary>
    /// The office's configured decision window. Read rather than hardcoded so a
    /// changed seed fails the precondition instead of silently re-binning the
    /// deadline band.
    /// </summary>
    protected Task<int> GetDecisionDueDaysAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (Tenancy.Change(officeId))
            {
                var parameter = await SystemParameterRepository.GetCurrentTenantAsync();
                parameter.ShouldNotBeNull("The office has no seeded SystemParameter row.");
                return parameter!.PendingAppointmentOverDueNotificationDays;
            }
        });

    /// <summary>
    /// Assigns <c>CreationTime</c> through its non-public setter. The repo already
    /// does this in ChangeRequestListFilterUnitTests; the difference here is that
    /// the value then has to survive a real insert, which is why every caller that
    /// backdates also verifies the persisted value.
    /// </summary>
    private static void SetCreationTime(object entity, DateTime creationTimeUtc)
    {
        var property = entity.GetType().GetProperty(
            nameof(Appointment.CreationTime),
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        var setter = property?.GetSetMethod(nonPublic: true);
        setter.ShouldNotBeNull("CreationTime no longer exposes a non-public setter.");
        setter!.Invoke(entity, new object[] { creationTimeUtc });
    }
}
