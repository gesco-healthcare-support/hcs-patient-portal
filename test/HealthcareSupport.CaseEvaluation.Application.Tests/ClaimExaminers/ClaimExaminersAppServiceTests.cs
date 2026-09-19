using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

/// <summary>
/// Covers <see cref="IClaimExaminersAppService"/>, which reported 0.0% before this tranche -- not a
/// partial figure, nothing at all. It is one of the two largest single wins in the group.
///
/// THE APP SERVICE HAS NO GUARDS OF ITS OWN. CreateAsync and UpdateAsync delegate straight to
/// ClaimExaminerManager, whose Check.Length calls are the only validation, and those are covered by
/// ClaimExaminerManagerTests rather than here. So every Fact below asserts a round trip or a
/// projection, and none pretends to assert a rule this class does not enforce.
///
/// ClaimExaminer is IMultiTenant, so Facts run inside TenantA. Every Fact creates its own rows and
/// filters by a unique token; the rig shares one SQLite connection and never rolls back, so a Fact
/// asserting TotalCount would be decided by whatever ran before it.
///
/// NO AUTHORIZATION FACTS -- AddAlwaysAllowAuthorization() makes every [Authorize] here a no-op.
/// </summary>
public abstract class ClaimExaminersAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IClaimExaminersAppService _claimExaminersAppService;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected ClaimExaminersAppServiceTests()
    {
        _claimExaminersAppService = GetRequiredService<IClaimExaminersAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // THE DTO'S OWN RULES CONSTRAIN WHAT A TOKEN MAY LOOK LIKE, and all three bit on the first
    // run: Email carries [EmailAddress] so an arbitrary token is rejected; PhoneNumber carries the
    // repo's [PhoneNumber], which requires EXACTLY ten digits after punctuation is stripped; and
    // the string columns are length-capped. A token here is unique AND valid, or the Fact fails
    // inside ABP's validator and never reaches the service it claims to test.
    // Every token in this class is used as an Email, so the token IS an address: unique for
    // filtering, and valid for [EmailAddress]. @test.local per the synthetic-data rule.
    private static string Token(string label) => $"ce-{label}-{Guid.NewGuid():N}@test.local";

    private const string TenDigitPhone = "2135550134";

    private async Task<ClaimExaminerDto> CreateAsync(string email, Guid? stateId = null)
    {
        return await _claimExaminersAppService.CreateAsync(new ClaimExaminerCreateDto
        {
            FirstName = "TEST-First",
            LastName = "TEST-Last",
            Email = email,
            PhoneNumber = TenDigitPhone,
            City = "TEST-City",
            StateId = stateId
        });
    }

    [Fact]
    public async Task CreateAsync_PersistsTheExaminer()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var email = Token("create");

            var created = await CreateAsync(email);

            created.ShouldNotBeNull();
            created.Id.ShouldNotBe(Guid.Empty);
            created.Email.ShouldBe(email);
            created.FirstName.ShouldBe("TEST-First");
            created.LastName.ShouldBe("TEST-Last");
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsTheExaminerThatWasCreated()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var email = Token("get");
            var created = await CreateAsync(email);

            var fetched = await _claimExaminersAppService.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.Email.ShouldBe(email);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByEmail_ReturnsOnlyTheMatchingExaminer()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var email = Token("list");
            var created = await CreateAsync(email);
            await CreateAsync(Token("list-other"));

            var result = await _claimExaminersAppService.GetListAsync(new GetClaimExaminersInput
            {
                Email = email,
                MaxResultCount = 1000
            });

            result.Items.Any(x => x.ClaimExaminer.Id == created.Id).ShouldBeTrue(
                "the examiner matching the email filter must be returned.");
            result.Items.ShouldAllBe(
                x => x.ClaimExaminer.Email == email,
                "the Email filter must exclude every non-matching examiner; a second examiner was "
                + "created in this Fact specifically so the filter has something to exclude.");
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_HydratesTheStateJoin()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("nav"), LocationsTestData.State1Id);

            // STATE IS IMultiTenant AND IS SEEDED AS A HOST ROW (TenantId null), so from inside
            // TenantA the multi-tenancy filter hides it and the join hydrates as null. The seed
            // contributor's own comment claims "State is host-only (NOT IMultiTenant)" -- that is
            // WRONG; State.cs:13 declares IMultiTenant. Logged to the backlog. Disabling the
            // filter here is the same device AppointmentEmployerDetailsAppServiceTests uses.
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var result = await _claimExaminersAppService.GetWithNavigationPropertiesAsync(created.Id);

                result.ClaimExaminer.Id.ShouldBe(created.Id);
                result.State.ShouldNotBeNull(
                    "the examiner was created WITH a StateId, so the navigation property must "
                    + "hydrate; a null here means the join is not being loaded.");
                result.State!.Id.ShouldBe(LocationsTestData.State1Id);
            }
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheStoredFields()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-before"));
            var after = Token("update-after");

            var updated = await _claimExaminersAppService.UpdateAsync(created.Id, new ClaimExaminerUpdateDto
            {
                FirstName = "TEST-Changed",
                LastName = created.LastName,
                Email = after,
                PhoneNumber = created.PhoneNumber,
                City = created.City,
                StateId = created.StateId,
                ConcurrencyStamp = created.ConcurrencyStamp
            });

            updated.Email.ShouldBe(after);
            updated.FirstName.ShouldBe("TEST-Changed");

            var refetched = await _claimExaminersAppService.GetAsync(created.Id);
            refetched.Email.ShouldBe(
                after,
                "the update must be persisted, not merely reflected in the returned DTO.");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheExaminer()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete"));

            await _claimExaminersAppService.DeleteAsync(created.Id);

            await Should.ThrowAsync<EntityNotFoundException>(
                () => _claimExaminersAppService.GetAsync(created.Id));
        }
    }

    [Fact]
    public async Task GetStateLookupAsync_FilteredByName_ReturnsTheMatchingState()
    {
        // No tenant wrap: the lookup reads host reference data. State is IMultiTenant and is
        // seeded as a host row, so querying it from inside a tenant returns nothing at all --
        // which would make this Fact fail for a reason that has nothing to do with the lookup.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _claimExaminersAppService.GetStateLookupAsync(new LookupRequestDto
            {
                Filter = StatesTestData.State1Name,
                MaxResultCount = 1000
            });

            result.Items.ShouldContain(
                x => x.Id == StatesTestData.State1Id,
                "the seeded state must be reachable through the lookup the booking form uses.");
        }
    }

    [Fact]
    public async Task GetIdentityUserLookupAsync_ReturnsAPageOfUsers()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _claimExaminersAppService.GetIdentityUserLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 1000
            });

            // Asserts the lookup PROJECTS rather than that it returns a particular population --
            // identity users are seeded by a different contributor and this Fact must not depend on
            // how many of them exist.
            result.Items.ShouldAllBe(
                x => x.Id != Guid.Empty,
                "every lookup row must carry a real id; an empty id means the projection is wrong.");
        }
    }
}
