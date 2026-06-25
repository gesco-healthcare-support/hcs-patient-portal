using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// G-03-01 (2026-06-03): seeds the one reserved <c>IsSystem</c> document
/// category ("Generated Packet") per tenant. Generated/queued packet documents
/// are auto-tagged with this category (wired in a later slice) so they read
/// distinctly from manually-uploaded documents; the row is hidden from the
/// upload picker and is not editable or deletable by admins (enforced in
/// <see cref="AppointmentDocumentTypeManager"/>).
///
/// Per-tenant, idempotent, and skips the host scope -- generated documents are
/// always tenant-scoped, matching <c>SystemParameterDataSeedContributor</c>.
/// </summary>
public class AppointmentDocumentTypeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IAppointmentDocumentTypeRepository _repository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentDocumentTypeDataSeedContributor(
        IAppointmentDocumentTypeRepository repository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _repository = repository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId == null)
        {
            // Host scope: skip; the system category is per-tenant.
            return;
        }

        using (_currentTenant.Change(context.TenantId))
        {
            // Reserved system row (idempotent). AppliesToAll = it is offered for
            // every appointment type.
            var systemRow = await _repository.FindAsync(
                x => x.IsSystem && x.Name == AppointmentDocumentTypeConsts.GeneratedPacketName);
            if (systemRow == null)
            {
                await _repository.InsertAsync(
                    new AppointmentDocumentType(
                        id: _guidGenerator.Create(),
                        name: AppointmentDocumentTypeConsts.GeneratedPacketName,
                        appliesToAll: true,
                        isActive: true,
                        isSystem: true,
                        tenantId: context.TenantId),
                    autoSave: true);
            }

            // #4 (2026-06-19): one record per name, offered to a SET of appointment
            // types (inverted from the old per-type duplicate rows). Idempotent per
            // name so admin edits + re-seeds are preserved. The "Panel Strike List"
            // PQME label drives the strike-list flag (upload path) and the PQME
            // approval gate.
            var ame = CaseEvaluationSeedIds.AppointmentTypes.Ame;
            var ime = CaseEvaluationSeedIds.AppointmentTypes.Ime;
            var pqme = CaseEvaluationSeedIds.AppointmentTypes.PanelQme;
            await SeedLabelAsync(context.TenantId, "Joint Letter", ame);
            await SeedLabelAsync(context.TenantId, "Medical Records", ame, ime, pqme);
            await SeedLabelAsync(context.TenantId, "Advocacy Letter", ime, pqme);
            await SeedLabelAsync(context.TenantId, "Cover Letter", pqme);
            await SeedLabelAsync(context.TenantId, AppointmentDocumentTypeConsts.PanelStrikeListName, pqme);
        }
    }

    private async Task SeedLabelAsync(System.Guid? tenantId, string name, params System.Guid[] appointmentTypeIds)
    {
        var exists = await _repository.FindAsync(x => !x.IsSystem && x.Name == name);
        if (exists != null)
        {
            return;
        }

        var entity = new AppointmentDocumentType(
            id: _guidGenerator.Create(),
            name: name,
            appliesToAll: false,
            isActive: true,
            isSystem: false,
            tenantId: tenantId);
        entity.SetAppointmentTypes(new List<System.Guid>(appointmentTypeIds));
        await _repository.InsertAsync(entity, autoSave: true);
    }
}
