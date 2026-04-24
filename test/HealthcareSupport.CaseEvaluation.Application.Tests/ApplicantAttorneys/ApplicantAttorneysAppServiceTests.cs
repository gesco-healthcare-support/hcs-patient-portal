using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public abstract class ApplicantAttorneysAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IApplicantAttorneysAppService _attorneysAppService;
    private readonly ApplicantAttorneyManager _attorneyManager;
    private readonly IApplicantAttorneyRepository _attorneyRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected ApplicantAttorneysAppServiceTests()
    {
        _attorneysAppService = GetRequiredService<IApplicantAttorneysAppService>();
        _attorneyManager = GetRequiredService<ApplicantAttorneyManager>();
        _attorneyRepository = GetRequiredService<IApplicantAttorneyRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path. Seeded Attorney1 lives in TenantA; tests that fetch it
    // by ID must run under `CurrentTenant.Change(TenantARef)` so ABP's
    // IMultiTenant auto-filter matches the row's TenantId.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededAttorney()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _attorneysAppService.GetAsync(ApplicantAttorneysTestData.Attorney1Id);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(ApplicantAttorneysTestData.Attorney1Id);
            result.FirmName.ShouldBe(ApplicantAttorneysTestData.Attorney1FirmName);
            result.FirmAddress.ShouldBe(ApplicantAttorneysTestData.Attorney1FirmAddress);
            result.PhoneNumber.ShouldBe(ApplicantAttorneysTestData.Attorney1PhoneNumber);
            result.WebAddress.ShouldBe(ApplicantAttorneysTestData.Attorney1WebAddress);
            result.FaxNumber.ShouldBe(ApplicantAttorneysTestData.Attorney1FaxNumber);
            result.Street.ShouldBe(ApplicantAttorneysTestData.Attorney1Street);
            result.City.ShouldBe(ApplicantAttorneysTestData.Attorney1City);
            result.ZipCode.ShouldBe(ApplicantAttorneysTestData.Attorney1ZipCode);
            result.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        }
    }

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsAttorneysFromBothTenants()
    {
        // Host-admin cross-tenant access pattern. ApplicantAttorney is IMultiTenant,
        // so the default behaviour in host context is to hide tenant-scoped rows.
        // Disabling IMultiTenant via IDataFilter is the ABP-standard way host-admin
        // surfaces both tenants -- same pattern used in DoctorsAppService.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _attorneysAppService.GetListAsync(new GetApplicantAttorneysInput());

            result.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney1Id).ShouldBeTrue();
            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney2Id).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task CreateAsync_SetsAllEightStringFields_AndBothFks()
    {
        var input = new ApplicantAttorneyCreateDto
        {
            FirmName = "TEST-CreateFirm",
            FirmAddress = "TEST-123 Create Ln",
            PhoneNumber = "5551234567",
            WebAddress = "https://TEST-create.test.local",
            FaxNumber = "5557654321",
            Street = "TEST-456 Create St",
            City = "TEST-CreateCity",
            ZipCode = "90001",
            StateId = null,
            IdentityUserId = IdentityUsersTestData.ApplicantAttorney1UserId
        };

        var created = await _attorneysAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        // Ctor-set fields (via ApplicantAttorney(id, stateId, identityUserId, firmName, firmAddress, phoneNumber)).
        created.FirmName.ShouldBe(input.FirmName);
        created.FirmAddress.ShouldBe(input.FirmAddress);
        created.PhoneNumber.ShouldBe(input.PhoneNumber);
        // Manager-post-construction fields (assigned after ctor in ApplicantAttorneyManager.CreateAsync).
        created.WebAddress.ShouldBe(input.WebAddress);
        created.FaxNumber.ShouldBe(input.FaxNumber);
        created.Street.ShouldBe(input.Street);
        created.City.ShouldBe(input.City);
        created.ZipCode.ShouldBe(input.ZipCode);
        // FKs.
        created.IdentityUserId.ShouldBe(input.IdentityUserId);

        // Re-fetch via the repository to prove the persistence layer accepted
        // the manager-post-ctor assignments. This test runs in host context so
        // the new attorney inherits TenantId = null (host-scoped), which is
        // visible to FindAsync without a tenant filter.
        var persisted = await _attorneyRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
        persisted!.WebAddress.ShouldBe(input.WebAddress);
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var existing = await _attorneyRepository.GetAsync(ApplicantAttorneysTestData.Attorney1Id);
            var update = new ApplicantAttorneyUpdateDto
            {
                FirmName = "TEST-UpdatedFirm",
                FirmAddress = existing.FirmAddress,
                PhoneNumber = existing.PhoneNumber,
                WebAddress = "https://TEST-updated.test.local",
                FaxNumber = existing.FaxNumber,
                Street = existing.Street,
                City = existing.City,
                ZipCode = existing.ZipCode,
                StateId = existing.StateId,
                IdentityUserId = existing.IdentityUserId,
                ConcurrencyStamp = existing.ConcurrencyStamp
            };

            var result = await _attorneysAppService.UpdateAsync(ApplicantAttorneysTestData.Attorney1Id, update);

            result.FirmName.ShouldBe("TEST-UpdatedFirm");
            result.WebAddress.ShouldBe("https://TEST-updated.test.local");

            // Restore seed values so sibling tests still see canonical Attorney1.
            var current = await _attorneyRepository.GetAsync(ApplicantAttorneysTestData.Attorney1Id);
            current.FirmName = ApplicantAttorneysTestData.Attorney1FirmName;
            current.WebAddress = ApplicantAttorneysTestData.Attorney1WebAddress;
            await _attorneyRepository.UpdateAsync(current);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesAttorney()
    {
        var created = await _attorneysAppService.CreateAsync(new ApplicantAttorneyCreateDto
        {
            FirmName = "TEST-DelFirm",
            IdentityUserId = IdentityUsersTestData.ApplicantAttorney1UserId
        });

        await _attorneysAppService.DeleteAsync(created.Id);

        (await _attorneyRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Validation guards (AppService + Manager)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException()
    {
        var input = new ApplicantAttorneyCreateDto
        {
            FirmName = "TEST-Whatever",
            IdentityUserId = Guid.Empty
        };

        await Should.ThrowAsync<UserFriendlyException>(async () =>
            await _attorneysAppService.CreateAsync(input));
    }

    [Fact]
    public async Task UpdateAsync_WhenIdentityUserIdIsEmpty_ThrowsUserFriendlyException()
    {
        // The AppService guard fires BEFORE the entity is fetched, so we do not
        // need to resolve Attorney1 first. An arbitrary Guid + empty
        // IdentityUserId is enough to trigger the UserFriendlyException path at
        // ApplicantAttorneysAppService line 103-106. No tenant wrap required
        // because the code path short-circuits before any repository call.
        var update = new ApplicantAttorneyUpdateDto
        {
            FirmName = "TEST-Whatever",
            IdentityUserId = Guid.Empty,
            ConcurrencyStamp = "dummy-stamp"
        };

        await Should.ThrowAsync<UserFriendlyException>(async () =>
            await _attorneysAppService.UpdateAsync(ApplicantAttorneysTestData.Attorney1Id, update));
    }

    [Fact]
    public async Task ApplicantAttorneyManager_CreateAsync_WhenFirmNameExceedsMax_ThrowsArgumentException()
    {
        // FirmName is a constructor-path field -- Check.Length fires inside the
        // ApplicantAttorney ctor (and is also pre-validated in the manager).
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _attorneyManager.CreateAsync(
                stateId: null,
                identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                firmName: new string('x', ApplicantAttorneyConsts.FirmNameMaxLength + 1)));
    }

    [Fact]
    public async Task ApplicantAttorneyManager_CreateAsync_WhenWebAddressExceedsMax_ThrowsArgumentException()
    {
        // WebAddress is a manager-post-construction field. Check.Length runs in
        // the manager BEFORE the ctor, so this asserts the manager's own
        // validation path (separate from the entity ctor's validation).
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _attorneyManager.CreateAsync(
                stateId: null,
                identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                firmName: "TEST-ValidFirm",
                webAddress: new string('x', ApplicantAttorneyConsts.WebAddressMaxLength + 1)));
    }

    // ------------------------------------------------------------------------
    // IMultiTenant isolation (contrast vs Patient FEAT-09 leak).
    // ApplicantAttorney IS IMultiTenant, so ABP's automatic tenant filter works.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsOnlyAttorney1()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _attorneysAppService.GetListAsync(new GetApplicantAttorneysInput());

            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney1Id).ShouldBeTrue();
            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantBContext_ReturnsOnlyAttorney2()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var result = await _attorneysAppService.GetListAsync(new GetApplicantAttorneysInput());

            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney2Id).ShouldBeTrue();
            result.Items.Any(x => x.ApplicantAttorney.Id == ApplicantAttorneysTestData.Attorney1Id).ShouldBeFalse();
        }
    }

    // ------------------------------------------------------------------------
    // IdentityUser lookup: filters by Email.Contains(filter), NOT by Username.
    // Run under TenantA context so ApplicantAttorney1UserId (a TenantA-scoped
    // user) is visible to the underlying IdentityUser query's tenant filter.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetIdentityUserLookupAsync_FiltersByEmail_NotByUsername()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Seeded users have emails like "TEST-x-1@test.local" and usernames
            // like "TEST-x-1" (no "@" in username). Filtering by "@test.local"
            // is unique to the email column -- proves the filter runs on Email,
            // not Username/Name.
            var result = await _attorneysAppService.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                Filter = "@test.local",
                MaxResultCount = 100
            });

            result.Items.ShouldNotBeEmpty();
            result.Items.Any(x => x.Id == IdentityUsersTestData.ApplicantAttorney1UserId).ShouldBeTrue();
        }
    }
}
