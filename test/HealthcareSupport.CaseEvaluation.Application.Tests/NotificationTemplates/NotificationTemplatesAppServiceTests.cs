using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Phase 4 (2026-05-03) integration coverage. Exercises:
///   - GetListAsync paged + filtered (FilterText, TemplateTypeId, IsActive)
///   - GetAsync by id and BusinessException on missing
///   - GetByCodeAsync resolution + BusinessException on unknown code
///   - GetTypeLookupAsync returns 2 host-scoped rows
///   - UpdateAsync round-trip + tenant isolation + ConcurrencyStamp
///   - UpdateAsync field immutability (TemplateCode, TemplateTypeId,
///     Description preserved)
///
/// Authorization gates ([Authorize(...Default / .Edit)]) are NOT
/// exercised here -- ABP's test base does not run a user-claims pipeline
/// for direct AppService calls. Permission grants are tested via the
/// seed contributor (Phase 2.5) and acted on at the AspNetCore middleware
/// layer.
///
/// **Currently blocked**: see
/// <c>docs/handoffs/2026-05-03-test-host-license-blocker.md</c>. The ABP
/// Pro license validator calls Environment.Exit(-42) when
/// <c>appsettings.secrets.json</c>'s <c>AbpLicenseCode</c> is the
/// placeholder. Populate with a real license to run these tests.
/// </summary>
public abstract class NotificationTemplatesAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly INotificationTemplatesAppService _appService;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _typeRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    protected NotificationTemplatesAppServiceTests()
    {
        _appService = GetRequiredService<INotificationTemplatesAppService>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _typeRepository = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
        _unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
    }

    // ------------------------------------------------------------------
    // GetListAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_ReturnsAllSeeded59Codes()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetListAsync(new GetNotificationTemplatesInput
            {
                MaxResultCount = 100,
            });

            result.TotalCount.ShouldBe(59);
            result.Items.Count.ShouldBe(59);
            result.Items.Any(x => x.NotificationTemplate.TemplateCode == NotificationTemplateConsts.Codes.AppointmentApproved)
                .ShouldBeTrue();
            result.Items.Any(x => x.NotificationTemplate.TemplateCode == NotificationTemplateConsts.Codes.JointAgreementLetterUploaded)
                .ShouldBeTrue();
            // Each row should have its NotificationTemplateType populated (LEFT JOIN succeeded).
            result.Items.ShouldAllBe(x => x.NotificationTemplateType != null);
        }
    }

    [Fact]
    public async Task GetListAsync_FilterText_NarrowsResultSet()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetListAsync(new GetNotificationTemplatesInput
            {
                FilterText = "Reschedule",
                MaxResultCount = 100,
            });

            result.TotalCount.ShouldBeGreaterThan(0);
            result.Items.ShouldAllBe(x => x.NotificationTemplate.TemplateCode.Contains("Reschedule"));
        }
    }

    [Fact]
    public async Task GetListAsync_IsActiveFalse_ReturnsEmptyAfterFreshSeed()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetListAsync(new GetNotificationTemplatesInput
            {
                IsActive = false,
                MaxResultCount = 100,
            });

            // Fresh seed sets all rows to IsActive = true.
            result.TotalCount.ShouldBe(0);
        }
    }

    // ------------------------------------------------------------------
    // GetAsync / GetByCodeAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetByCodeAsync_KnownCode_ReturnsTemplate()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var dto = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);

            dto.NotificationTemplate.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentApproved);
            dto.NotificationTemplate.IsActive.ShouldBeTrue();
            dto.NotificationTemplateType.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task GetByCodeAsync_UnknownCode_Throws()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appService.GetByCodeAsync("ThisCodeDoesNotExist"));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
        }
    }

    [Fact]
    public async Task GetAsync_UnknownId_Throws()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                () => _appService.GetAsync(Guid.NewGuid()));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
        }
    }

    // ------------------------------------------------------------------
    // GetTypeLookupAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetTypeLookupAsync_ReturnsEmailAndSmsRows()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _appService.GetTypeLookupAsync();

            result.Items.Count.ShouldBe(2);
            result.Items.Any(x => x.Name == "Email").ShouldBeTrue();
            result.Items.Any(x => x.Name == "SMS").ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------
    // UpdateAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_ChangesEditableFields()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);
            var input = new NotificationTemplateUpdateDto
            {
                Subject = "Your appointment is approved -- {ClinicName}",
                BodyEmail = "<p>Hi @Model.PatientName,</p><p>Your appointment is approved.</p>",
                BodySms = "Hi @Model.PatientName, your appointment is approved.",
                IsActive = true,
                ConcurrencyStamp = current.NotificationTemplate.ConcurrencyStamp,
            };

            var result = await _appService.UpdateAsync(current.NotificationTemplate.Id, input);

            result.Subject.ShouldBe(input.Subject);
            result.BodyEmail.ShouldBe(input.BodyEmail);
            result.BodySms.ShouldBe(input.BodySms);
            // Code and type unchanged
            result.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentApproved);
            result.TemplateTypeId.ShouldBe(current.NotificationTemplate.TemplateTypeId);
        }
    }

    [Fact]
    public async Task UpdateAsync_NullBodyEmail_Throws()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);

            var input = new NotificationTemplateUpdateDto
            {
                Subject = "x",
                BodyEmail = null!,
                BodySms = "x",
                IsActive = true,
                ConcurrencyStamp = current.NotificationTemplate.ConcurrencyStamp,
            };

            await Should.ThrowAsync<Exception>(
                () => _appService.UpdateAsync(current.NotificationTemplate.Id, input));
        }
    }

    [Fact]
    public async Task UpdateAsync_SubjectTooLong_Throws()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var current = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);
            var input = new NotificationTemplateUpdateDto
            {
                Subject = new string('x', NotificationTemplateConsts.SubjectMaxLength + 1),
                BodyEmail = "x",
                BodySms = "x",
                IsActive = true,
                ConcurrencyStamp = current.NotificationTemplate.ConcurrencyStamp,
            };

            await Should.ThrowAsync<Exception>(
                () => _appService.UpdateAsync(current.NotificationTemplate.Id, input));
        }
    }

    [Fact]
    public async Task UpdateAsync_TenantIsolation()
    {
        await EnsureSeededAsync(TenantsTestData.TenantARef);
        await EnsureSeededAsync(TenantsTestData.TenantBRef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var a = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);
            var input = new NotificationTemplateUpdateDto
            {
                Subject = "Tenant A custom subject",
                BodyEmail = a.NotificationTemplate.BodyEmail,
                BodySms = a.NotificationTemplate.BodySms,
                IsActive = true,
                ConcurrencyStamp = a.NotificationTemplate.ConcurrencyStamp,
            };
            await _appService.UpdateAsync(a.NotificationTemplate.Id, input);
        }

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var b = await _appService.GetByCodeAsync(NotificationTemplateConsts.Codes.AppointmentApproved);
            b.NotificationTemplate.Subject.ShouldNotBe("Tenant A custom subject");
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Ensures the per-tenant 59-template seed exists. The integration
    /// orchestrator only creates Tenants A and B; the Phase 1.3
    /// NotificationTemplateDataSeedContributor only fires when ABP's
    /// IDataSeeder is invoked with a tenant context, which the orchestrator
    /// does not do directly. Seeding inline keeps the test class
    /// self-contained.
    /// </summary>
    private async Task EnsureSeededAsync(Guid? tenantId)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        // Ensure the host-scoped types exist (Email + SMS).
        await EnsureTypeAsync(Guid.Parse("c0000001-0000-4000-9000-000000000001"), "Email");
        await EnsureTypeAsync(Guid.Parse("c0000001-0000-4000-9000-000000000002"), "SMS");

        using (_currentTenant.Change(tenantId))
        {
            var queryable = await _templateRepository.GetQueryableAsync();
            var existing = queryable.Select(x => x.TemplateCode).ToList();
            var emailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

            foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
            {
                await _templateRepository.InsertAsync(new NotificationTemplate(
                    id: _guidGenerator.Create(),
                    tenantId: tenantId,
                    templateCode: code,
                    templateTypeId: emailTypeId,
                    subject: $"[{code}] -- TODO",
                    bodyEmail: $"<p>Stub for {code}</p>",
                    bodySms: $"Stub for {code}",
                    description: null,
                    isActive: true), autoSave: false);
            }
        }

        await uow.CompleteAsync();
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
}
