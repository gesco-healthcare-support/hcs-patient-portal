using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;

/// <summary>
/// #9: pins the self-scoped attorney profile. The endpoint takes no target id -- it resolves
/// the caller's own master from CurrentUser.Id -- so a caller can only read/edit their own
/// record, and a caller with no master is denied. Uses the seeded Attorney1 (TenantA,
/// identity ApplicantAttorney1UserId). Tests are order-independent (no cross-test coupling
/// on seed mutations).
/// </summary>
public abstract class MyAttorneyProfileAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IMyAttorneyProfileAppService _service;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected MyAttorneyProfileAppServiceTests()
    {
        _service = GetRequiredService<IMyAttorneyProfileAppService>();
        _applicantRepository = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task GetAsync_resolves_the_callers_own_applicant_master()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorneyRoleName))
        {
            var dto = await _service.GetAsync();

            dto.ShouldNotBeNull();
            dto.Kind.ShouldBe("applicant");
            dto.FirmName.ShouldNotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public async Task UpdateAsync_updates_the_callers_own_master_and_preserves_identity()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.ApplicantAttorneyRoleName))
        {
            await _service.UpdateAsync(new UpdateMyAttorneyProfileInput
            {
                FirstName = "TEST-Updated-First",
                LastName = "TEST-Updated-Last",
                FirmName = "TEST-Updated-Firm",
            });

            var saved = await _applicantRepository.GetAsync(ApplicantAttorneysTestData.Attorney1Id);
            saved.FirmName.ShouldBe("TEST-Updated-Firm");
            saved.FirstName.ShouldBe("TEST-Updated-First");
            saved.LastName.ShouldBe("TEST-Updated-Last");
            // Identity is preserved -- self-edit never re-homes the master.
            saved.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        }
    }

    [Fact]
    public async Task UpdateAsync_denies_a_caller_with_no_attorney_master()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, Guid.NewGuid(), IdentityUsersTestData.ApplicantAttorneyRoleName))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                () => _service.UpdateAsync(new UpdateMyAttorneyProfileInput { FirmName = "x" }));
        }
    }
}
