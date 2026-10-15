using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Pins that <see cref="AccountUrlBuilder.BuildChangeRequestConsentUrlAsync"/> refuses an empty tenant
/// id. A consent link is tenant-specific: built against no tenant it would point a party at the wrong
/// office, so the builder fails loudly before reading any setting. The result asserted is the thrown
/// error, and that no setting was read.
/// </summary>
public class AccountUrlBuilderConsentGuardTests
{
    [Fact]
    public async Task BuildChangeRequestConsentUrlAsync_AnEmptyTenantId_IsRefusedBeforeAnySettingIsRead()
    {
        var settings = Substitute.For<ISettingProvider>();
        var builder = new AccountUrlBuilder(
            settings,
            Substitute.For<IRepository<Tenant, Guid>>(),
            Substitute.For<ICurrentTenant>(),
            NullLogger<AccountUrlBuilder>.Instance);

        var thrown = await Should.ThrowAsync<ArgumentException>(
            () => builder.BuildChangeRequestConsentUrlAsync(Guid.Empty, "TEST-raw-token"));

        thrown.ParamName.ShouldBe("tenantId");
        await settings.DidNotReceiveWithAnyArgs().GetOrNullAsync(default!);
    }
}
