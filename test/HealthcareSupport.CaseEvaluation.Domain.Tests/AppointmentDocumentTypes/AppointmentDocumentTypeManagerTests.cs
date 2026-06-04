using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Domain-service tests for <see cref="AppointmentDocumentTypeManager"/>.
/// Mirrors the abstract+concrete split used by DoctorAvailabilityManagerTests
/// (abstract here in Domain.Tests; EF-backed concrete in EntityFrameworkCore.Tests).
/// </summary>
public abstract class AppointmentDocumentTypeManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentDocumentTypeManager _manager;
    private readonly IAppointmentDocumentTypeRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentDocumentTypeManagerTests()
    {
        _manager = GetRequiredService<AppointmentDocumentTypeManager>();
        _repository = GetRequiredService<IAppointmentDocumentTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    [Fact]
    public async Task Manager_CreateAsync_StampsCurrentTenantOnTheRow()
    {
        // Regression guard for the silent null-tenant bug. The entity
        // constructor assigns TenantId, which suppresses ABP's auto-stamp on
        // insert, so the manager MUST pass CurrentTenant.Id explicitly. Without
        // it the row persists with a null TenantId and disappears from the
        // tenant-scoped list (and the null tenant also suppresses CreatorId via
        // ABP's cross-tenant audit guard). Create without autoSave is not
        // visible to a FindAsync in the same UoW, so -- as in
        // DoctorAvailabilityManagerTests -- create inside the UoW and read back
        // after it commits. CreatorId is not asserted: the harness runs without
        // a current user, so it is legitimately null.
        var createdId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var created = await _manager.CreateAsync(
                    name: "Regression Document Category",
                    appointmentTypeId: null,
                    isActive: true);

                created.ShouldNotBeNull();
                created.Id.ShouldNotBe(Guid.Empty);
                createdId = created.Id;
            }
        });

        // Read with the multi-tenant filter disabled so the row is found
        // regardless of its TenantId -- this lets the assertion target the
        // stamped value directly (a null-tenant regression reports "TenantId was
        // null", not a misleading "not found"). Disabling IDataFilter<IMultiTenant>
        // is the project's documented cross-tenant read pattern.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var persisted = await _repository.FindAsync(createdId);
                persisted.ShouldNotBeNull();
                persisted!.TenantId.ShouldBe(TenantsTestData.TenantARef);
            }
        });
    }
}
