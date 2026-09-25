using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Records every dispatch instead of rendering and enqueueing email, so a test can assert exactly
/// what WOULD be sent and nothing can reach a mail server. One instance per test application.
/// Separate from the notification handlers' <c>RecordingNotificationDispatcher</c> because Send
/// Test's contract includes the context tag and the recipient's flags, which that one drops.
/// </summary>
public sealed class ContextTagRecordingDispatcher : INotificationDispatcher
{
    public List<RecordedDispatch> Dispatches { get; } = new();

    public Task DispatchAsync(string templateCode, IReadOnlyCollection<NotificationRecipient> recipients,
        IReadOnlyDictionary<string, object?> variables, string contextTag,
        PacketAttachmentRef? packetRef = null, CancellationToken cancellationToken = default)
    {
        Dispatches.Add(new RecordedDispatch(templateCode, recipients.ToList(), variables, contextTag));
        return Task.CompletedTask;
    }

    public Task DispatchToWithCcAsync(string templateCode, NotificationRecipient to, IReadOnlyCollection<NotificationRecipient> cc,
        IReadOnlyDictionary<string, object?> variables, string contextTag,
        PacketAttachmentRef? packetRef = null, CancellationToken cancellationToken = default)
    {
        Dispatches.Add(new RecordedDispatch(templateCode, new[] { to }.Concat(cc).ToList(), variables, contextTag));
        return Task.CompletedTask;
    }

    public sealed record RecordedDispatch(
        string TemplateCode, List<NotificationRecipient> Recipients, IReadOnlyDictionary<string, object?> Variables, string ContextTag);
}

/// <summary>The EF Core test graph with <see cref="INotificationDispatcher"/> replaced by a recorder.</summary>
[DependsOn(typeof(CaseEvaluationEntityFrameworkCoreTestModule))]
public class RecordingDispatcherTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<ContextTagRecordingDispatcher>();
        context.Services.Replace(ServiceDescriptor.Transient<INotificationDispatcher>(sp => sp.GetRequiredService<ContextTagRecordingDispatcher>()));
    }
}

/// <summary>
/// <see cref="NotificationTemplatesAppService"/> paths no other test reaches: the variable-chip
/// palette (<c>GetVariablesAsync</c>), a missing template on <c>GetAsync</c>, and the Send Test
/// action for an inactive template, an active one, and ANOTHER OFFICE's template. (A caller with
/// no email address is already covered by <c>NotificationTemplatesAppServiceTests</c>.)
///
/// <para>Templates are office-scoped, so Send Test carries an office decoy: a template that
/// exists only in TenantB must be "not found" from TenantA, and nothing may be dispatched.</para>
/// </summary>
public class NotificationTemplatesSendTestAndVariablesTests : CaseEvaluationTestBase<RecordingDispatcherTestModule>
{
    private const string Code = NotificationTemplateConsts.Codes.InviteExternalUser;

    private readonly INotificationTemplatesAppService _service;
    private readonly INotificationTemplateRepository _templates;
    private readonly INotificationTemplateTypeRepository _types;
    private readonly ContextTagRecordingDispatcher _dispatcher;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    public NotificationTemplatesSendTestAndVariablesTests()
    {
        _service = GetRequiredService<INotificationTemplatesAppService>();
        _templates = GetRequiredService<INotificationTemplateRepository>();
        _types = GetRequiredService<INotificationTemplateTypeRepository>();
        _dispatcher = GetRequiredService<ContextTagRecordingDispatcher>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task GetVariables_ForACodeWithTokens_ReturnsEachTokenWithAHumanLabel()
    {
        var result = await _service.GetVariablesAsync(Code);

        var expected = NotificationTemplateVariableCatalog.GetVariablesForCode(Code);
        result.Items.Select(i => i.Token).ShouldBe(expected);
        result.Items.Single(i => i.Token == "TenantName").Label.ShouldBe("Tenant Name");
    }

    [Fact]
    public async Task GetVariables_ForAnUnknownCode_IsNotFound()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _service.GetVariablesAsync("TEST-NoSuchTemplateCode"));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
        ex.Data["templateCode"].ShouldBe("TEST-NoSuchTemplateCode");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetVariables_ForABlankCode_IsRejected(string code)
    {
        var ex = await Should.ThrowAsync<ArgumentException>(() => _service.GetVariablesAsync(code));

        ex.ParamName.ShouldBe("templateCode");
    }

    [Fact]
    public async Task Get_UnknownId_IsNotFound_WhileTheOfficeHasATemplate()
    {
        await InsertTemplateAsync(TenantsTestData.TenantARef, isActive: true); // decoy: a template does exist

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAsync(TenantsTestData.TenantARef, () => _service.GetAsync(Guid.NewGuid())));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
    }

    [Fact]
    public async Task SendTest_OfAnInactiveTemplate_IsRefused_AndDispatchesNothing()
    {
        var id = await InsertTemplateAsync(TenantsTestData.TenantARef, isActive: false);

        using (WithCurrentUser.RunWithEmail(_principal, Guid.NewGuid(), "test.sender@test.local"))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAsync(TenantsTestData.TenantARef, () => SendTestAsync(id)));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
            ex.Data["templateCode"].ShouldBe(Code);
        }

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendTest_OfAnActiveTemplate_SendsSampleVariablesToTheCallersOwnAddress()
    {
        var id = await InsertTemplateAsync(TenantsTestData.TenantARef, isActive: true);
        var userId = Guid.NewGuid();

        using (WithCurrentUser.RunWithEmail(_principal, userId, "test.sender@test.local"))
        {
            await InTenantAsync(TenantsTestData.TenantARef, () => SendTestAsync(id));
        }

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(Code);
        var recipient = sent.Recipients.ShouldHaveSingleItem();
        recipient.Email.ShouldBe("test.sender@test.local");
        recipient.IsRegistered.ShouldBeTrue();
        recipient.PhoneNumber.ShouldBeNull(); // email only: a test must never fire an SMS
        sent.Variables.Keys.OrderBy(k => k).ShouldBe(NotificationTemplateVariableCatalog.BuildSampleVariables(Code).Keys.OrderBy(k => k));
        sent.ContextTag.ShouldBe($"SendTest/{Code}/{userId}");
    }

    [Fact]
    public async Task SendTest_OfAnotherOfficesTemplate_IsNotFound_AndDispatchesNothing()
    {
        // Office decoy: the template exists only in TenantB.
        var idInOfficeB = await InsertTemplateAsync(TenantsTestData.TenantBRef, isActive: true);

        using (WithCurrentUser.RunWithEmail(_principal, Guid.NewGuid(), "test.sender@test.local"))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAsync(TenantsTestData.TenantARef, () => SendTestAsync(idInOfficeB)));
            // Positive control: TenantB's own Send Test of the same template goes out.
            await InTenantAsync(TenantsTestData.TenantBRef, () => SendTestAsync(idInOfficeB));
        }

        _dispatcher.Dispatches.ShouldHaveSingleItem().TemplateCode.ShouldBe(Code);
    }

    private async Task<bool> SendTestAsync(Guid id)
    {
        await _service.SendTestAsync(id);
        return true;
    }

    private async Task<Guid> InsertTemplateAsync(Guid tenantId, bool isActive)
    {
        var typeId = await WithUnitOfWorkAsync(async () =>
        {
            var type = new NotificationTemplateType(Guid.NewGuid(), "TEST-Email-" + Guid.NewGuid().ToString("N")[..6]);
            await _types.InsertAsync(type, autoSave: true);
            return type.Id;
        });

        return await InTenantAsync(tenantId, async () =>
        {
            var id = Guid.NewGuid();
            await _templates.InsertAsync(new NotificationTemplate(
                id: id, tenantId: tenantId, templateCode: Code, templateTypeId: typeId,
                subject: "TEST-Subject ##TenantName##", bodyEmail: "<p>TEST ##URL##</p>", bodySms: "TEST sms",
                description: null, isActive: isActive), autoSave: true);
            return id;
        });
    }

    private Task<T> InTenantAsync<T>(Guid tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return await action();
            }
        });
}
