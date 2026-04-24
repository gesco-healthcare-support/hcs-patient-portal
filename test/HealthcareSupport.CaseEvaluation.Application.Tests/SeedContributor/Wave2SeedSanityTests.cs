using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Testing;

/// <summary>
/// Sanity tests for the Wave-2 seed additions introduced in Phase B-6 Tier-2
/// PR-2A. These do NOT exercise entity business logic -- they just confirm
/// the orchestrator actually inserts the rows the Tier-2 entity tests will
/// FK into.
///
/// Both tests run under <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c> so the
/// host-admin cross-tenant view returns every seeded row regardless of
/// CurrentTenant context. If a future session adds seeds but the orchestrator
/// call-order regresses, these tests fail loudly at the seed step instead of
/// manifesting as "TotalCount=0" in the downstream entity tests.
/// </summary>
public abstract class Wave2SeedSanityTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IRepository<DoctorAvailability, System.Guid> _doctorAvailabilityRepository;
    private readonly IRepository<Appointment, System.Guid> _appointmentRepository;
    private readonly IDataFilter _dataFilter;

    protected Wave2SeedSanityTests()
    {
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, System.Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, System.Guid>>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task SeedSanity_DoctorAvailabilities_ThreeSlotsPresent()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var all = await _doctorAvailabilityRepository.GetListAsync();

            all.Count.ShouldBeGreaterThanOrEqualTo(3);

            var slot1 = all.SingleOrDefault(x => x.Id == DoctorAvailabilitiesTestData.Slot1Id);
            var slot2 = all.SingleOrDefault(x => x.Id == DoctorAvailabilitiesTestData.Slot2Id);
            var slot3 = all.SingleOrDefault(x => x.Id == DoctorAvailabilitiesTestData.Slot3Id);

            slot1.ShouldNotBeNull();
            slot1!.BookingStatusId.ShouldBe(DoctorAvailabilitiesTestData.Slot1BookingStatus);
            slot1.LocationId.ShouldBe(LocationsTestData.Location1Id);
            slot1.TenantId.ShouldBe(TenantsTestData.TenantARef);

            slot2.ShouldNotBeNull();
            slot2!.BookingStatusId.ShouldBe(DoctorAvailabilitiesTestData.Slot2BookingStatus);
            slot2.LocationId.ShouldBe(LocationsTestData.Location2Id);
            slot2.TenantId.ShouldBe(TenantsTestData.TenantARef);

            slot3.ShouldNotBeNull();
            slot3!.BookingStatusId.ShouldBe(DoctorAvailabilitiesTestData.Slot3BookingStatus);
            slot3.LocationId.ShouldBe(LocationsTestData.Location1Id);
            slot3.TenantId.ShouldBe(TenantsTestData.TenantBRef);
        }
    }

    [Fact]
    public async Task SeedSanity_Appointments_TwoAppointmentsPresent()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var all = await _appointmentRepository.GetListAsync();

            all.Count.ShouldBeGreaterThanOrEqualTo(2);

            var appt1 = all.SingleOrDefault(x => x.Id == AppointmentsTestData.Appointment1Id);
            var appt2 = all.SingleOrDefault(x => x.Id == AppointmentsTestData.Appointment2Id);

            appt1.ShouldNotBeNull();
            appt1!.PatientId.ShouldBe(PatientsTestData.Patient1Id);
            appt1.DoctorAvailabilityId.ShouldBe(DoctorAvailabilitiesTestData.Slot1Id);
            appt1.AppointmentStatus.ShouldBe(AppointmentsTestData.Appointment1Status);
            appt1.RequestConfirmationNumber.ShouldBe(AppointmentsTestData.Appointment1RequestConfirmationNumber);
            appt1.TenantId.ShouldBe(TenantsTestData.TenantARef);

            appt2.ShouldNotBeNull();
            appt2!.PatientId.ShouldBe(PatientsTestData.Patient2Id);
            appt2.DoctorAvailabilityId.ShouldBe(DoctorAvailabilitiesTestData.Slot3Id);
            appt2.AppointmentStatus.ShouldBe(AppointmentsTestData.Appointment2Status);
            appt2.RequestConfirmationNumber.ShouldBe(AppointmentsTestData.Appointment2RequestConfirmationNumber);
            appt2.TenantId.ShouldBe(TenantsTestData.TenantBRef);
        }
    }
}
