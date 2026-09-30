using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentDocumentTypeManagerTests()
    {
        _manager = GetRequiredService<AppointmentDocumentTypeManager>();
        _repository = GetRequiredService<IAppointmentDocumentTypeRepository>();
        _documentRepository = GetRequiredService<IRepository<AppointmentDocument, Guid>>();
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
                    appointmentTypeIds: new List<Guid>(),
                    appliesToAll: true,
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

    [Fact]
    public async Task Manager_DeleteAsync_WhenReferencedByADocument_ThrowsInUse()
    {
        // Regression guard for the PR2 in-use-before-delete rule: a category
        // still referenced by an AppointmentDocument cannot be deleted (staff
        // retire it instead), so the type label on existing documents is kept.
        var typeId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var type = await _manager.CreateAsync(
                    name: "Referenced Category",
                    appointmentTypeIds: new List<Guid>(),
                    appliesToAll: true,
                    isActive: true);
                typeId = type.Id;

                var document = new AppointmentDocument(
                    id: Guid.NewGuid(),
                    tenantId: TenantsTestData.TenantARef,
                    appointmentId: AppointmentsTestData.Appointment1Id,
                    documentName: "Tagged Document",
                    fileName: "scan.pdf",
                    blobName: "blob-key",
                    contentType: "application/pdf",
                    fileSize: 1024,
                    uploadedByUserId: Guid.NewGuid(),
                    appointmentDocumentTypeId: typeId);
                await _documentRepository.InsertAsync(document, autoSave: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var ex = await Should.ThrowAsync<BusinessException>(
                    async () => await _manager.DeleteAsync(typeId));
                ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeInUse);
            }
        });
    }

    [Fact]
    public async Task Manager_UpdateAsync_ReconcilesAppointmentTypeSet()
    {
        // #4: one record offered to a SET of appointment types. An update must
        // reconcile (add new, drop removed), not append -- starting [Ame, Ime]
        // and saving [Ime, PanelQme] must end exactly [Ime, PanelQme].
        var ame = CaseEvaluationSeedIds.AppointmentTypes.Ame;
        var ime = CaseEvaluationSeedIds.AppointmentTypes.Ime;
        var pqme = CaseEvaluationSeedIds.AppointmentTypes.PanelQme;
        var typeId = Guid.Empty;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var created = await _manager.CreateAsync(
                    name: "Reconciled Category",
                    appointmentTypeIds: new List<Guid> { ame, ime },
                    appliesToAll: false,
                    isActive: true);
                typeId = created.Id;
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _manager.UpdateAsync(
                    id: typeId,
                    name: "Reconciled Category",
                    appointmentTypeIds: new List<Guid> { ime, pqme },
                    appliesToAll: false,
                    isActive: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var persisted = await _repository.GetWithAppointmentTypesAsync(typeId);
                var ids = persisted.AppointmentTypes.Select(j => j.AppointmentTypeId).ToList();
                ids.Count.ShouldBe(2);
                ids.ShouldContain(ime);
                ids.ShouldContain(pqme);
                ids.ShouldNotContain(ame);
            }
        });
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNameExistsInTenant_ThrowsDuplicate()
    {
        // #4: uniqueness is now per-tenant (a name is curated once), no longer
        // per appointment type -- so a second active row with the same name in
        // the same tenant is rejected regardless of its type set. The first row
        // is created (and flushed) in its own UoW: InsertAsync without autoSave
        // is not yet queryable in the same UoW, so the duplicate check must run
        // against a committed row (each CRUD call is its own UoW in the app).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _manager.CreateAsync(
                    name: "Unique Per Tenant",
                    appointmentTypeIds: new List<Guid> { CaseEvaluationSeedIds.AppointmentTypes.Ame },
                    appliesToAll: false,
                    isActive: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var ex = await Should.ThrowAsync<BusinessException>(async () =>
                    await _manager.CreateAsync(
                        name: "Unique Per Tenant",
                        appointmentTypeIds: new List<Guid> { CaseEvaluationSeedIds.AppointmentTypes.Ime },
                        appliesToAll: false,
                        isActive: true));
                ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeNameAlreadyExists);
            }
        });
    }
}
