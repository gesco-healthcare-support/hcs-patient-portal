using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// A patient must belong to a practice.
///
/// <para>Found live 2026-08-19: two patients booked through the wizard were written with
/// <c>TenantId = NULL</c>. Nothing failed and nothing logged. The appointments pointed at them
/// correctly, but the multi-tenancy filter hid the rows from every tenant-scoped read, so the
/// list showed no patient name, the detail view showed blank demographics, staff could not edit
/// them, and the duplicate search could not see them -- which is why re-booking the same person
/// silently produced a second record instead of reusing the first.</para>
///
/// <para>The trigger was never reproduced: the code passes <c>CurrentTenant.Id</c>, the audit log
/// recorded the correct tenant for those very requests, and the deployed build did not change
/// that path. That is precisely why the guard is at the domain boundary rather than at whichever
/// call site was blamed -- it holds regardless of which caller, or which ambient state, produced
/// the null.</para>
/// </summary>
public abstract class PatientTenantAssignmentTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : Volo.Abp.Modularity.IAbpModule
{
    private readonly PatientManager _patientManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public PatientTenantAssignmentTests()
    {
        _patientManager = GetRequiredService<PatientManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    [Fact]
    public async Task Refuses_to_create_a_patient_with_no_practice()
    {
        using (_currentTenant.Change(null))
        {
            var thrown = await Should.ThrowAsync<BusinessException>(
                () => CreateAsync(tenantId: null));

            thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PatientTenantRequired);
        }
    }

    [Fact]
    public async Task Falls_back_to_the_ambient_practice_when_the_caller_passes_none()
    {
        var tenantId = Guid.NewGuid();

        using (_currentTenant.Change(tenantId))
        {
            // The booking path passes CurrentTenant.Id, and PatientsAppService.CreateAsync takes
            // the value straight off a client DTO -- so "caller supplied nothing" has to resolve
            // to the practice actually in context rather than to null.
            // Deliberately never completed: the assertion is about the value the manager RESOLVES,
            // and committing would need a real tenant row purely to satisfy a foreign key.
            using var uow = _unitOfWorkManager.Begin(requiresNew: true);
            var patient = await CreateAsync(tenantId: null);

            patient.TenantId.ShouldBe(tenantId);
        }
    }

    [Fact]
    public async Task Keeps_an_explicit_practice_over_the_ambient_one()
    {
        var explicitTenant = Guid.NewGuid();
        var ambientTenant = Guid.NewGuid();

        using (_currentTenant.Change(ambientTenant))
        {
            // Seeders create patients for a practice other than the one in context; the argument
            // has to win, or seeding would silently write rows into the wrong practice.
            using var uow = _unitOfWorkManager.Begin(requiresNew: true);
            var patient = await CreateAsync(tenantId: explicitTenant);

            patient.TenantId.ShouldBe(explicitTenant);
        }
    }

    private Task<Patient> CreateAsync(Guid? tenantId)
    {
        return _patientManager.CreateAsync(
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: null,
            tenantId: tenantId,
            firstName: "Testcase",
            lastName: "Tenantguard",
            email: "testcase.tenantguard@example.test",
            genderId: Gender.Male,
            dateOfBirth: new DateTime(1980, 1, 15),
            phoneNumberTypeId: PhoneNumberType.Home);
    }
}
