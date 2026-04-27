using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentApplicantAttorneyRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentApplicantAttorneyRepository _joinRepository;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentApplicantAttorneyRepositoryTests()
    {
        _joinRepository = GetRequiredService<IAppointmentApplicantAttorneyRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListWithNavigationPropertiesAsync_FiltersByAppointmentId()
    {
        // Seeded Join1 lives in TenantA; a nav-props lookup with AppointmentId=Appointment1
        // inside TenantA context must return Join1 with its navigation properties
        // populated (Appointment + ApplicantAttorney + IdentityUser).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var results = await _joinRepository.GetListWithNavigationPropertiesAsync(
                    appointmentId: AppointmentsTestData.Appointment1Id);

                results.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join1Id).ShouldBeTrue();
                var join = results.Single(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join1Id);
                join.Appointment.ShouldNotBeNull();
                join.ApplicantAttorney.ShouldNotBeNull();
                join.IdentityUser.ShouldNotBeNull();
            }
        });
    }
}
