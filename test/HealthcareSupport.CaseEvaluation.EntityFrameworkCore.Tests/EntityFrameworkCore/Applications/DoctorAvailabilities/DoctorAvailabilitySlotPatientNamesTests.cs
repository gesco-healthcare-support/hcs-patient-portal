using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// <c>GetSlotPatientNamesAsync</c>, the internal week view's "who holds this slot" chips. The
/// repository query behind it is tested elsewhere; this pins the service's contract: an empty id
/// list asks nothing, and a list is answered with names for the office's OWN slots.
///
/// <para>The lookup is office-scoped, so it carries an OFFICE decoy: office B's seeded slot 3
/// (held by office B's patient) is asked for from office A and must not come back.</para>
/// </summary>
public class DoctorAvailabilitySlotPatientNamesTests : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IDoctorAvailabilitiesAppService _service;
    private readonly ICurrentTenant _currentTenant;

    public DoctorAvailabilitySlotPatientNamesTests()
    {
        _service = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task SlotPatientNames_NamesTheHolderOfThisOfficesSlot_AndNotAnotherOfficesSlot()
    {
        var result = await InOfficeAAsync(() => _service.GetSlotPatientNamesAsync(
            new List<Guid> { DoctorAvailabilitiesTestData.Slot1Id, DoctorAvailabilitiesTestData.Slot3Id }));

        var slot = result.ShouldHaveSingleItem();
        slot.SlotId.ShouldBe(DoctorAvailabilitiesTestData.Slot1Id);
        slot.Names.ShouldBe(new[] { $"{PatientsTestData.Patient1FirstName} {PatientsTestData.Patient1LastName}" });
    }

    [Fact]
    public async Task SlotPatientNames_ForAnEmptyList_IsEmpty()
    {
        // Decoy: office A's slot 1 IS held, so an implementation that ignored the list would name its patient.
        (await InOfficeAAsync(() => _service.GetSlotPatientNamesAsync(new List<Guid>()))).ShouldBeEmpty();
    }

    private Task<T> InOfficeAAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });
}
