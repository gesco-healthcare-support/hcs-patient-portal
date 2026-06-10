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
            // Reserved system row (idempotent).
            var systemRow = await _repository.FindAsync(
                x => x.IsSystem && x.Name == AppointmentDocumentTypeConsts.GeneratedPacketName);
            if (systemRow == null)
            {
                await _repository.InsertAsync(
                    new AppointmentDocumentType(
                        id: _guidGenerator.Create(),
                        name: AppointmentDocumentTypeConsts.GeneratedPacketName,
                        appointmentTypeId: null,
                        isActive: true,
                        isSystem: true,
                        tenantId: context.TenantId),
                    autoSave: true);
            }

            // I15 (2026-06-08): per-appointment-type document-category label sets.
            // Idempotent per (name, appointmentTypeId) so admin edits + re-seeds are
            // preserved. The "Panel Strike List" PQME label drives the strike-list
            // flag (upload path) and the PQME approval gate.
            await SeedLabelsAsync(context.TenantId, CaseEvaluationSeedIds.AppointmentTypes.Ame,
                "Joint Letter", "Medical Records");
            await SeedLabelsAsync(context.TenantId, CaseEvaluationSeedIds.AppointmentTypes.Ime,
                "Advocacy Letter", "Medical Records");
            await SeedLabelsAsync(context.TenantId, CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
                AppointmentDocumentTypeConsts.PanelStrikeListName, "Advocacy Letter", "Cover Letter", "Medical Records");
        }
    }

    private async Task SeedLabelsAsync(System.Guid? tenantId, System.Guid appointmentTypeId, params string[] names)
    {
        foreach (var name in names)
        {
            var exists = await _repository.FindAsync(
                x => !x.IsSystem && x.AppointmentTypeId == appointmentTypeId && x.Name == name);
            if (exists != null)
            {
                continue;
            }

            await _repository.InsertAsync(
                new AppointmentDocumentType(
                    id: _guidGenerator.Create(),
                    name: name,
                    appointmentTypeId: appointmentTypeId,
                    isActive: true,
                    isSystem: false,
                    tenantId: tenantId),
                autoSave: true);
        }
    }
}
