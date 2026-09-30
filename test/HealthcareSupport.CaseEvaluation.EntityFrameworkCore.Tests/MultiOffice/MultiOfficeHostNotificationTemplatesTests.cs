using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// task_4c0f6fe9 (2026-07-21) -- verifies <see cref="NotificationTemplateDataSeedContributor"/>
/// seeds ONLY the host-scoped account-lifecycle subset
/// (<see cref="NotificationTemplateConsts.Codes.HostScoped"/>) into the host database, not the
/// full appointment-lifecycle catalog. The host database needs these rows so account emails
/// (welcome / reset / password-change / registration) dispatched at host scope -- internal
/// operators are Phase D host logins -- resolve their template instead of throwing
/// NotificationTemplateNotFound.
///
/// <para>The contributor inserts with <c>autoSave:false</c> (batched for the outer unit of
/// work, matching the tenant pass), so each seed runs in its own committed UoW here -- exactly
/// as the real <c>DataSeeder</c> drives it -- before a fresh UoW reads the persisted rows.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeHostNotificationTemplatesTests : CaseEvaluationMultiOfficeTestBase
{
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");
    private static readonly Guid SmsTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000002");

    private readonly NotificationTemplateDataSeedContributor _seedContributor;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _typeRepository;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeHostNotificationTemplatesTests()
    {
        _seedContributor = GetRequiredService<NotificationTemplateDataSeedContributor>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _typeRepository = GetRequiredService<INotificationTemplateTypeRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task SeedAsync_HostScope_SeedsOnlyTheHostScopedSubset()
    {
        await GetSeededOfficesAsync();
        await SeedHostAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var queryable = await _templateRepository.GetQueryableAsync();
                var hostCodes = queryable.Select(x => x.TemplateCode).ToList();

                // Every host-scoped code is present at host scope.
                foreach (var code in NotificationTemplateConsts.Codes.HostScoped)
                {
                    hostCodes.ShouldContain(code);
                }

                // The subset is narrow: appointment-lifecycle and invite codes stay per-office.
                hostCodes.ShouldNotContain(NotificationTemplateConsts.Codes.AppointmentApproved);
                hostCodes.ShouldNotContain(NotificationTemplateConsts.Codes.InviteExternalUser);

                // Exactly the four host-scoped codes (idempotent across module-init + reseeds).
                hostCodes.Count.ShouldBe(NotificationTemplateConsts.Codes.HostScoped.Length);

                // The two template types the templates FK to exist at host too.
                (await _typeRepository.FindAsync(EmailTypeId)).ShouldNotBeNull();
                (await _typeRepository.FindAsync(SmsTypeId)).ShouldNotBeNull();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task SeedAsync_HostScope_IsIdempotent()
    {
        await GetSeededOfficesAsync();

        // Two independent committed seeds -- the second must not duplicate rows.
        await SeedHostAsync();
        await SeedHostAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var queryable = await _templateRepository.GetQueryableAsync();
                var hostCount = queryable.Count(x =>
                    NotificationTemplateConsts.Codes.HostScoped.Contains(x.TemplateCode));

                hostCount.ShouldBe(NotificationTemplateConsts.Codes.HostScoped.Length);
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Runs the real host seed in its own committed unit of work so the contributor's
    /// autoSave:false inserts are persisted before the assertions read them.
    /// </summary>
    private Task SeedHostAsync() =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await _seedContributor.SeedAsync(new DataSeedContext(tenantId: null));
            }
        }, requiresNew: true);
}
