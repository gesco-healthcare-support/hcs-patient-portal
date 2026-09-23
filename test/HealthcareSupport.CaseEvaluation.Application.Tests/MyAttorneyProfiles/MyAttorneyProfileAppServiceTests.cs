using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
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

    // The defense-attorney half. No defense master is seeded, so each fact inserts one in office B
    // for the seeded DefenseAttorney1 identity.

    private async Task<Guid> InsertDefenseMasterAsync()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            return await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<DefenseAttorney, Guid>>()
                .InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, IdentityUsersTestData.DefenseAttorney1UserId,
                    firmName: "Synthetic Defense Firm", email: "defense@example.test"), autoSave: true)).Id);
        }
    }

    [Fact]
    public async Task GetAsync_resolves_the_callers_own_defense_master()
    {
        await InsertDefenseMasterAsync();

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.DefenseAttorney1UserId, IdentityUsersTestData.DefenseAttorneyRoleName))
        {
            var dto = await _service.GetAsync();

            dto.Kind.ShouldBe("defense");
            dto.FirmName.ShouldBe("Synthetic Defense Firm");
            dto.Email.ShouldBe("defense@example.test");
        }
    }

    [Fact]
    public async Task UpdateAsync_updates_the_callers_own_defense_master_and_preserves_identity()
    {
        var defenseId = await InsertDefenseMasterAsync();

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.DefenseAttorney1UserId, IdentityUsersTestData.DefenseAttorneyRoleName))
        {
            var dto = await _service.UpdateAsync(new UpdateMyAttorneyProfileInput
            {
                FirstName = "Synthetic-First",
                LastName = "Synthetic-Last",
                FirmName = "Synthetic Renamed Firm",
                City = "Synthetic City",
            });

            dto.Kind.ShouldBe("defense");
            var saved = await GetRequiredService<IRepository<DefenseAttorney, Guid>>().GetAsync(defenseId);
            saved.FirmName.ShouldBe("Synthetic Renamed Firm");
            saved.FirstName.ShouldBe("Synthetic-First");
            saved.City.ShouldBe("Synthetic City");
            saved.IdentityUserId.ShouldBe(IdentityUsersTestData.DefenseAttorney1UserId);
            saved.Email.ShouldBe("defense@example.test");
        }
    }

    [Fact]
    public async Task GetAsync_denies_a_defense_caller_with_no_defense_master()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        using (WithCurrentUser.Run(_principal, Guid.NewGuid(), IdentityUsersTestData.DefenseAttorneyRoleName))
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => _service.GetAsync()))
                .Message.ShouldContain("No attorney profile is linked");
        }
    }

    [Fact]
    public async Task GetAsync_denies_a_caller_in_neither_attorney_role()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.ApplicantAttorney1UserId, IdentityUsersTestData.PatientRoleName))
        {
            (await Should.ThrowAsync<UserFriendlyException>(() => _service.GetAsync()))
                .Message.ShouldContain("not registered as an applicant or defense attorney");
        }
    }
}
