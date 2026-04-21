using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Testing;

/// <summary>
/// Single orchestrator for integration-test data seeding. Replaces per-entity
/// IDataSeedContributor implementations because ABP does not guarantee the
/// execution order of multiple contributors (see ABP forum #571).
///
/// Tier-1 PRs extend this class with additional private SeedXAsync methods
/// and add them to SeedAsync's await chain in strict FK-dependency order
/// (State -&gt; AppointmentLanguage -&gt; AppointmentType -&gt; Location -&gt;
/// IdentityUser -&gt; Doctor -&gt; DoctorAvailability -&gt; Patient -&gt; Appointment -&gt;
/// ApplicantAttorney -&gt; AppointmentAccessor etc).
/// </summary>
public class CaseEvaluationIntegrationTestSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool _isSeeded;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IdentityUsersDataSeedContributor _identityUsersSeeder;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CaseEvaluationIntegrationTestSeedContributor(
        IDoctorRepository doctorRepository,
        IdentityUsersDataSeedContributor identityUsersSeeder,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _doctorRepository = doctorRepository;
        _identityUsersSeeder = identityUsersSeeder;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (_isSeeded)
        {
            return;
        }

        await _identityUsersSeeder.SeedAsync(context);
        await SeedDoctorsAsync();

        await _unitOfWorkManager.Current!.SaveChangesAsync();
        _isSeeded = true;
    }

    private async Task SeedDoctorsAsync()
    {
        await _doctorRepository.InsertAsync(new Doctor(
            id: DoctorsTestData.Doctor1Id,
            firstName: DoctorsTestData.Doctor1FirstName,
            lastName: DoctorsTestData.Doctor1LastName,
            email: DoctorsTestData.Doctor1Email,
            gender: default,
            identityUserId: null));

        await _doctorRepository.InsertAsync(new Doctor(
            id: DoctorsTestData.Doctor2Id,
            firstName: DoctorsTestData.Doctor2FirstName,
            lastName: DoctorsTestData.Doctor2LastName,
            email: DoctorsTestData.Doctor2Email,
            gender: default,
            identityUserId: null));
    }
}
