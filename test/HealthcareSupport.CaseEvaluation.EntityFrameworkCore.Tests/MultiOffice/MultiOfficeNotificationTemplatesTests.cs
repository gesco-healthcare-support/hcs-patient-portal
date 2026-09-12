using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Restores the NotificationTemplates catalog skips. NotificationTemplateType became
/// IMultiTenant per office (B4); the single-shared-SQLite rig seeded the types
/// host-scoped (TenantId null), so a tenant-context lookup saw nothing -- and seeding
/// them per tenant collided on the fixed type GUIDs in one database. On the multi-office
/// harness each office owns its database, so the fixed-GUID types + per-code templates
/// are seeded under the office's tenant context and resolve correctly.
///
/// Ports NotificationTemplatesAppServiceTests.{GetListAsync_ReturnsAllSeededCodes,
/// GetByCodeAsync_KnownCode_ReturnsTemplate, GetTypeLookupAsync_ReturnsEmailAndSmsRows}.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeNotificationTemplatesTests : CaseEvaluationMultiOfficeTestBase
{
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");
    private static readonly Guid SmsTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000002");

    private readonly INotificationTemplatesAppService _appService;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _typeRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public MultiOfficeNotificationTemplatesTests()
    {
        _appService = GetRequiredService<INotificationTemplatesAppService>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _typeRepository = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededCodes()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                await SeedTemplatesAsync(officeA.OfficeId);

                var result = await _appService.GetListAsync(new GetNotificationTemplatesInput
                {
                    MaxResultCount = 200,
                });

                var expectedCount = NotificationTemplateConsts.Codes.All.Length;
                result.TotalCount.ShouldBe(expectedCount);
                result.Items.Count.ShouldBe(expectedCount);
                result.Items.ShouldAllBe(x => x.NotificationTemplateType != null);
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task GetByCodeAsync_KnownCode_ReturnsTemplate()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                await SeedTemplatesAsync(officeA.OfficeId);

                var dto = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);

                dto.NotificationTemplate.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentApproved);
                dto.NotificationTemplate.IsActive.ShouldBeTrue();
                dto.NotificationTemplateType.ShouldNotBeNull();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task GetTypeLookupAsync_ReturnsEmailAndSmsRows()
    {
        var (officeA, _) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                await SeedTemplatesAsync(officeA.OfficeId);

                var result = await _appService.GetTypeLookupAsync();

                result.Items.Count.ShouldBe(2);
                result.Items.Any(x => x.Name == "Email").ShouldBeTrue();
                result.Items.Any(x => x.Name == "SMS").ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Idempotently seeds the two notification types (Email/SMS) and one template per
    /// known code into the CURRENT office's database. Caller has already entered the
    /// office tenant context + a unit of work.
    /// </summary>
    private async Task SeedTemplatesAsync(Guid officeId)
    {
        await EnsureTypeAsync(EmailTypeId, "Email");
        await EnsureTypeAsync(SmsTypeId, "SMS");

        var queryable = await _templateRepository.GetQueryableAsync();
        var existing = queryable.Select(x => x.TemplateCode).ToList();

        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
        {
            await _templateRepository.InsertAsync(new NotificationTemplate(
                id: _guidGenerator.Create(),
                tenantId: officeId,
                templateCode: code,
                templateTypeId: EmailTypeId,
                subject: $"[{code}] -- TODO",
                bodyEmail: $"<p>Stub for {code}</p>",
                bodySms: $"Stub for {code}",
                description: null,
                isActive: true), autoSave: true);
        }
    }

    private async Task EnsureTypeAsync(Guid id, string name)
    {
        if (await _typeRepository.FindAsync(id) != null)
        {
            return;
        }
        await _typeRepository.InsertAsync(new NotificationTemplateType(id, name), autoSave: true);
    }
}
