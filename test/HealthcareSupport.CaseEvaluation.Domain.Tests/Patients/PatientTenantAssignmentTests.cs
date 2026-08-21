using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// 2026-08-20 -- a patient must belong to a practice, and the failure to do so is silent.
///
/// <para><b>The defect.</b> Two patients reached production with a null TenantId. Nothing threw.
/// The rows were inserted and their appointments pointed at them correctly, but the
/// multi-tenancy filter hid them from every tenant-scoped read: no patient name in the
/// appointment list, blank demographics, staff unable to edit, and -- worst -- the duplicate
/// search could not see them either, so re-booking the same person created a second record. The
/// rows had to be repaired directly in the database because the UI cannot reach them.</para>
///
/// <para><b>Why a guard is needed here specifically.</b> <see cref="Patient"/> is
/// <see cref="IMultiTenant"/>, but its TenantId comes from a caller ARGUMENT rather than from
/// ABP, so ABP's usual guarantee never applied: whatever the caller passes is written.</para>
///
/// <para>The manager refuses rather than inferring the practice from request context. Every
/// caller already passes a value explicitly, so a null is the caller's bug, and inferring one
/// would mask it -- or attach the patient to whichever practice happened to be in scope.</para>
/// </summary>
public abstract class PatientTenantAssignmentTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly PatientManager _patientManager;
    private readonly ICurrentTenant _currentTenant;

    protected PatientTenantAssignmentTests()
    {
        _patientManager = GetRequiredService<PatientManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// The load-bearing case: no practice supplied means refuse, not write an ownerless row.
    /// Nothing is inserted, so this needs no tenant row of its own.
    /// </summary>
    [Fact]
    public async Task Refuses_to_create_a_patient_with_no_practice()
    {
        var thrown = await Should.ThrowAsync<BusinessException>(() => CreateAsync(tenantId: null));

        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PatientTenantRequired);
    }

    /// <summary>
    /// A practice in request context does NOT satisfy the guard. This pins the deliberate choice
    /// to refuse rather than fall back to <c>CurrentTenant.Id</c>: if this ever starts passing,
    /// someone has reintroduced the inference and a caller's null will be silently papered over.
    /// </summary>
    [Fact]
    public async Task Refuses_even_when_a_practice_is_in_context()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var thrown = await Should.ThrowAsync<BusinessException>(() => CreateAsync(tenantId: null));

            thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.PatientTenantRequired);
        }
    }

    /// <summary>
    /// The happy path, against the SEEDED practice rather than a fresh Guid: the row is really
    /// inserted here, and an unseeded tenant id would fail the foreign key rather than the
    /// assertion.
    /// </summary>
    [Fact]
    public async Task Uses_the_practice_the_caller_supplied()
    {
        var patient = await CreateAsync(tenantId: TenantsTestData.TenantARef);

        patient.TenantId.ShouldBe(TenantsTestData.TenantARef);
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
