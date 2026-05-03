using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Seeds 2 host-scoped <c>NotificationTemplateType</c> rows (Email + SMS),
/// then per-tenant inserts a placeholder <c>NotificationTemplate</c> row for
/// every TemplateCode listed in <see cref="NotificationTemplateConsts.Codes"/>.
///
/// Bodies are stub strings -- parity-correct subject + body content lands in
/// per-feature phases (registration, login, approval, document review,
/// change-request approval, reminders). Phase 1.3 just guarantees row
/// existence so notification handlers can resolve templates by code.
///
/// Idempotent: skips rows that already exist.
/// </summary>
public class NotificationTemplateDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private static readonly Guid EmailTypeId = new("c0000001-0000-4000-9000-000000000001");
    private static readonly Guid SmsTypeId = new("c0000001-0000-4000-9000-000000000002");

    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _typeRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public NotificationTemplateDataSeedContributor(
        INotificationTemplateRepository templateRepository,
        INotificationTemplateTypeRepository typeRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _templateRepository = templateRepository;
        _typeRepository = typeRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId == null)
        {
            // Host pass: seed the template-type rows.
            await SeedTypesAsync();
            return;
        }

        // Tenant pass: stub a row per template code.
        using (_currentTenant.Change(context.TenantId))
        {
            await SeedTemplatesAsync(context.TenantId);
        }
    }

    private async Task SeedTypesAsync()
    {
        await EnsureTypeAsync(EmailTypeId, NotificationTemplateTypeConsts.Names.Email);
        await EnsureTypeAsync(SmsTypeId, NotificationTemplateTypeConsts.Names.Sms);
    }

    private async Task EnsureTypeAsync(Guid id, string name)
    {
        var existing = await _typeRepository.FindAsync(id);
        if (existing != null)
        {
            return;
        }
        await _typeRepository.InsertAsync(new NotificationTemplateType(id, name), autoSave: true);
    }

    private async Task SeedTemplatesAsync(Guid? tenantId)
    {
        // All 59 codes verified against OLD source 2026-05-03 (Phase 4):
        //   - 16 from OLD's `TemplateCode` int enum (DB-managed in OLD)
        //   - 43 from OLD's `EmailTemplate` static class (on-disk HTML in OLD)
        // NEW unifies both into this single table; all become IT-Admin
        // editable. Per-feature phases replace stub bodies as their
        // notification handlers wire variable substitution.
        var queryable = await _templateRepository.GetQueryableAsync();
        var existingCodes = queryable.Select(x => x.TemplateCode).ToList();

        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existingCodes.Contains(c)))
        {
            var entity = new NotificationTemplate(
                id: _guidGenerator.Create(),
                tenantId: tenantId,
                templateCode: code,
                templateTypeId: EmailTypeId,
                subject: $"[{code}] -- TODO: parity-correct subject",
                bodyEmail: $"<p>Stub body for {code}. Per-feature phases will replace with parity-correct content.</p>",
                bodySms: $"Stub SMS for {code}.",
                description: null,
                isActive: true);
            await _templateRepository.InsertAsync(entity, autoSave: false);
        }
    }
}
