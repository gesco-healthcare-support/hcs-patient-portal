using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Testing;

/// <summary>
/// Single orchestrator for integration-test data seeding. Replaces per-entity
/// IDataSeedContributor implementations because ABP does not guarantee the
/// execution order of multiple contributors (see ABP forum #571).
///
/// Tier-1 PRs extend this class with additional private SeedXAsync methods
/// and add them to SeedAsync's await chain in strict FK-dependency order
/// (IdentityUser -&gt; Doctor -&gt; Patient -&gt; ... etc).
///
/// Tenant rows are seeded via reflection because Volo.Saas.Tenants.Tenant exposes
/// only a non-public (Guid id, string name) constructor. ITenantManager would work
/// but is heavier (async, validates name uniqueness against the full host context)
/// than a test seed needs. Patient has a FK to Tenant (SetNull on delete); without
/// these rows, seeded patients fail the FK constraint. Future PRs can migrate this
/// to ITenantManager if richer tenant state is needed.
/// </summary>
public class CaseEvaluationIntegrationTestSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool _isSeeded;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IPatientRepository _patientRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IdentityUsersDataSeedContributor _identityUsersSeeder;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CaseEvaluationIntegrationTestSeedContributor(
        IDoctorRepository doctorRepository,
        IPatientRepository patientRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IdentityUsersDataSeedContributor identityUsersSeeder,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _doctorRepository = doctorRepository;
        _patientRepository = patientRepository;
        _tenantRepository = tenantRepository;
        _identityUsersSeeder = identityUsersSeeder;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (_isSeeded)
        {
            return;
        }

        await SeedTenantsAsync();
        await _unitOfWorkManager.Current!.SaveChangesAsync();
        await _identityUsersSeeder.SeedAsync(context);
        await _unitOfWorkManager.Current!.SaveChangesAsync();
        await SeedDoctorsAsync();
        await SeedPatientsAsync();

        await _unitOfWorkManager.Current!.SaveChangesAsync();
        _isSeeded = true;
    }

    private async Task SeedTenantsAsync()
    {
        using (_currentTenant.Change(null))
        {
            if (await _tenantRepository.FindAsync(PatientsTestData.TenantAId) == null)
            {
                await _tenantRepository.InsertAsync(CreateTenant(PatientsTestData.TenantAId, PatientsTestData.TenantAName));
            }

            if (await _tenantRepository.FindAsync(PatientsTestData.TenantBId) == null)
            {
                await _tenantRepository.InsertAsync(CreateTenant(PatientsTestData.TenantBId, PatientsTestData.TenantBName));
            }
        }
    }

    /// <summary>
    /// Instantiates a Volo.Saas.Tenants.Tenant via its non-public (Guid, string)
    /// constructor. Direct `new Tenant(id, name)` will not compile because the
    /// constructor is protected/internal to the Saas assembly. Using ITenantManager
    /// instead would also work but is heavier for test seeding.
    /// </summary>
    /// <summary>
    /// Constructs a Volo.Saas.Tenants.Tenant via reflection. The public signature
    /// we rely on is (Guid id, string name, string normalizedName) -- the Tenant
    /// constructor is internal/protected so direct `new Tenant(...)` does not
    /// compile from outside the Saas assembly. If this constructor signature
    /// changes upstream, the fallback enumerates the available constructors and
    /// tries the first one that starts with (Guid, string, ...).
    /// </summary>
    private static Tenant CreateTenant(Guid id, string name)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var normalizedName = name.ToUpperInvariant();

        var ctor = typeof(Tenant).GetConstructor(flags, binder: null,
            types: new[] { typeof(Guid), typeof(string), typeof(string) }, modifiers: null);
        if (ctor != null)
        {
            return (Tenant)ctor.Invoke(new object[] { id, name, normalizedName });
        }

        foreach (var candidate in typeof(Tenant).GetConstructors(flags))
        {
            var ps = candidate.GetParameters();
            if (ps.Length >= 2
                && ps[0].ParameterType == typeof(Guid)
                && ps[1].ParameterType == typeof(string))
            {
                var args = new object?[ps.Length];
                args[0] = id;
                args[1] = name;
                for (var i = 2; i < ps.Length; i++)
                {
                    args[i] = ps[i].ParameterType == typeof(string) && !ps[i].HasDefaultValue
                        ? normalizedName
                        : ps[i].HasDefaultValue
                            ? ps[i].DefaultValue
                            : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                }
                return (Tenant)candidate.Invoke(args);
            }
        }

        var sigs = string.Join("; ", typeof(Tenant).GetConstructors(flags)
            .Select(c => "(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name)) + ")"));
        throw new InvalidOperationException(
            $"No Volo.Saas.Tenants.Tenant constructor matches (Guid, string, ...). Available: {sigs}");
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

    private async Task SeedPatientsAsync()
    {
        await _patientRepository.InsertAsync(new Patient(
            id: PatientsTestData.Patient1Id,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: IdentityUsersTestData.PatientUserId,
            tenantId: PatientsTestData.TenantAId,
            firstName: PatientsTestData.Patient1FirstName,
            lastName: PatientsTestData.Patient1LastName,
            email: PatientsTestData.Patient1Email,
            genderId: (Gender)PatientsTestData.PatientGenderIdValue,
            dateOfBirth: PatientsTestData.FixedDateOfBirth,
            phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue,
            address: PatientsTestData.Patient1Address,
            city: PatientsTestData.Patient1City,
            zipCode: PatientsTestData.Patient1ZipCode,
            socialSecurityNumber: PatientsTestData.Patient1SocialSecurityNumber));

        await _patientRepository.InsertAsync(new Patient(
            id: PatientsTestData.Patient2Id,
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: IdentityUsersTestData.Patient2UserId,
            tenantId: PatientsTestData.TenantBId,
            firstName: PatientsTestData.Patient2FirstName,
            lastName: PatientsTestData.Patient2LastName,
            email: PatientsTestData.Patient2Email,
            genderId: (Gender)PatientsTestData.PatientGenderIdValue,
            dateOfBirth: PatientsTestData.FixedDateOfBirth,
            phoneNumberTypeId: (PhoneNumberType)PatientsTestData.PatientPhoneNumberTypeIdValue));
    }
}
