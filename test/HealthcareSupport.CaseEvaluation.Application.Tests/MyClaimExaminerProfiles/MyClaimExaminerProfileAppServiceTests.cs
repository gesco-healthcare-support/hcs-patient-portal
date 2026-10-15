using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;

/// <summary>
/// Covers <see cref="IMyClaimExaminerProfileAppService"/>, which reported 0.0% before this file --
/// nothing at all, not a partial figure.
///
/// <para>MIRRORS <c>MyAttorneyProfileAppServiceTests</c> BY DESIGN, because the SERVICE says it
/// mirrors <c>MyAttorneyProfileAppService</c> (its class docstring, R2-4 2026-06-22). Where the two
/// tests diverge it is because the services diverge, and each divergence is called out below rather
/// than left for a reader to spot.</para>
///
/// <para>WHY EVERY FACT SEEDS ITS OWN MASTER INSTEAD OF USING A SEEDED ONE. There IS a seeded
/// <c>ClaimExaminer1UserId</c> identity user, but the seed contributor creates NO
/// <c>ClaimExaminer</c> master row linked to it -- grep the contributor for "ClaimExaminer" and it
/// returns nothing. The attorney mirror can lean on <c>Attorney1Id</c>; this one cannot. So each
/// Fact creates its own master against a FRESH identity id. That is not merely convenient: the rig
/// runs one SQLite connection for the whole collection with no rollback, and the service resolves
/// by <c>FirstOrDefaultAsync(x =&gt; x.IdentityUserId == userId)</c> -- a shared id would make a
/// Fact's answer depend on what ran before it.</para>
///
/// <para>ROLE CLAIMS ARE NOT NEUTERED BY THE RIG, AND THAT IS THE POINT OF THIS FILE.
/// <c>AddAlwaysAllowAuthorization()</c> makes every <c>[Authorize]</c> attribute inert, so no Fact
/// here asserts an attribute. But <c>ResolveOwnMasterAsync</c> calls
/// <c>CurrentUser.IsInRole("Claim Examiner")</c>, which reads the PRINCIPAL'S CLAIMS -- ordinary
/// service code that always-allow does not touch. <c>WithCurrentUser.Run</c> emits role claims, so
/// that guard is genuinely testable while the attributes above it are not.</para>
///
/// <para><c>ClaimExaminer</c> is <c>IMultiTenant</c>, so every Fact runs inside TenantA.</para>
/// </summary>
public abstract class MyClaimExaminerProfileAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    /// <summary>
    /// The literal the SERVICE compares against (<c>MyClaimExaminerProfileAppService.cs:22</c>,
    /// a <c>private const</c>). <c>IdentityUsersTestData.ClaimExaminerRoleName</c> carries the same
    /// string and is used here, so if the production constant and the seeded role name ever drift
    /// apart these Facts fail rather than silently testing a role nobody holds.
    /// </summary>
    private const string ClaimExaminerRole = IdentityUsersTestData.ClaimExaminerRoleName;

    private readonly IMyClaimExaminerProfileAppService _profile;
    private readonly IClaimExaminersAppService _claimExaminers;
    private readonly IRepository<ClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected MyClaimExaminerProfileAppServiceTests()
    {
        _profile = GetRequiredService<IMyClaimExaminerProfileAppService>();
        _claimExaminers = GetRequiredService<IClaimExaminersAppService>();
        _claimExaminerRepository = GetRequiredService<IRepository<ClaimExaminer, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>A seeded claim examiner: the login it is resolved by, and the master row itself.</summary>
    private sealed record SeededExaminer(Guid IdentityUserId, Guid MasterId, string Email);

    // The create DTO's own rules constrain what a token may look like, and they bit on the first
    // run of the tranche-1 ClaimExaminers file: Email carries [EmailAddress], PhoneNumber carries
    // the repo's [PhoneNumber] (EXACTLY ten digits after punctuation is stripped), and the string
    // columns are length-capped. ABP's validator runs BEFORE the method body, so an invalid token
    // fails inside the interceptor and the Fact never reaches the service it claims to test.
    private const string TenDigitPhone = "2135550134";

    /// <summary>
    /// Creates a REAL identity user and returns its id.
    ///
    /// <para>MEASURED, NOT ASSUMED: the first run of this file failed six Facts with
    /// <c>DbUpdateException</c> on save because <c>SeedOwnMasterAsync</c> was handed a freshly
    /// invented <c>Guid</c>. <c>ClaimExaminer.IdentityUserId</c> is a real foreign key to
    /// <c>AbpUsers</c>, so an id that points at no row cannot be persisted. Inventing a Guid is the
    /// obvious shortcut and it does not work here.</para>
    ///
    /// <para>The user is created in the AMBIENT tenant, which every Fact sets to TenantA, so it is
    /// visible to the tenant-scoped master it will own. The <c>Succeeded</c> assertion is
    /// load-bearing: a rejected create would otherwise surface much later as a confusing
    /// "no profile linked" refusal in a Fact that is testing something else entirely.</para>
    /// </summary>
    private async Task<Guid> SeedIdentityUserAsync(string token)
    {
        var userId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            var user = new IdentityUser(
                userId,
                $"TEST-mce-{token}",
                $"TEST-mce-{token}@test.local",
                tenantId: _currentTenant.Id);

            var created = await _userManager.CreateAsync(user);
            created.Succeeded.ShouldBeTrue(
                "FIXTURE PRECONDITION FAILED: the identity user was not created, so the master "
                + "below cannot reference it and every later assertion would be testing the "
                + "wrong refusal: "
                + string.Join("; ", created.Errors.Select(e => e.Description)));
        });

        return userId;
    }

    /// <summary>
    /// Creates a real login AND the claim-examiner master that belongs to it, and returns both.
    /// Uses the admin app service for the master rather than the repository, so the row is built
    /// the way production builds it.
    ///
    /// <para>Must be called INSIDE the Fact's tenant scope: both rows are tenant-scoped and the
    /// service resolves them from inside a tenant.</para>
    /// </summary>
    private async Task<SeededExaminer> SeedOwnMasterAsync(string label)
    {
        var token = $"{label}-{Guid.NewGuid():N}"[..24];
        var identityUserId = await SeedIdentityUserAsync(token);
        var email = $"TEST-mce-{token}@test.local";

        var created = await _claimExaminers.CreateAsync(new ClaimExaminerCreateDto
        {
            FirstName = "TEST-First",
            LastName = "TEST-Last",
            Email = email,
            PhoneNumber = TenDigitPhone,
            City = "TEST-City",
            IdentityUserId = identityUserId,
        });

        return new SeededExaminer(identityUserId, created.Id, email);
    }

    // ------------------------------------------------------------------------
    // ResolveOwnMasterAsync -- the three refusals.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_WithNoUserIdOnThePrincipal_IsRefused()
    {
        // WithCurrentUser.Run is deliberately NOT used: it ALWAYS emits a UserId claim, so it
        // cannot produce the state this guard exists for. An empty ClaimsIdentity can.
        // This pins `CurrentUser.Id ?? throw new AbpAuthorizationException()` -- ordinary service
        // code that happens to throw an authorization type, NOT the inert [Authorize] attribute.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (_principal.Change(new ClaimsPrincipal(new ClaimsIdentity())))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(
                async () => await _profile.GetAsync(),
                "A principal carrying no user id must be refused before any repository read.");
        }
    }

    [Fact]
    public async Task GetAsync_ForACallerWhoDoesNotHoldTheClaimExaminerRole_IsRefused()
    {
        // THE MASTER EXISTS AND IS OWNED BY THIS CALLER. That is load-bearing, not setup noise:
        // against a caller with no master the next guard down would refuse too, and this Fact would
        // pass with the role check deleted. Seeding the master isolates the ROLE check as the only
        // thing that can produce this refusal.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var owner = await SeedOwnMasterAsync("norole");

            using (WithCurrentUser.Run(_principal, owner.IdentityUserId))
            {
                var ex = await Should.ThrowAsync<UserFriendlyException>(
                    async () => await _profile.GetAsync());

                ex.Message.ShouldContain(
                    "not registered",
                    Case.Insensitive,
                    "This must be the NOT-A-CLAIM-EXAMINER refusal. If it is the no-profile-linked "
                    + "message instead, the role check did not fire and the master lookup did.");
            }
        }
    }

    [Fact]
    public async Task GetAsync_ForARoleHolderWithNoLinkedMaster_IsRefusedWithADifferentMessage()
    {
        // The role IS held and NO master is seeded, so only the second guard can refuse. Asserting
        // a DIFFERENT message from the Fact above is the whole point -- both guards throw
        // UserFriendlyException, so asserting the TYPE alone would let either one be deleted.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, Guid.NewGuid(), ClaimExaminerRole))
        {
            var ex = await Should.ThrowAsync<UserFriendlyException>(
                async () => await _profile.GetAsync());

            ex.Message.ShouldContain(
                "No claim examiner profile",
                Case.Insensitive,
                "A role holder with no master must get the no-profile-linked refusal, not the "
                + "not-registered one. These two messages are the only way a caller -- or this "
                + "test -- can tell which guard fired.");
        }
    }

    [Fact]
    public async Task TheTwoRefusals_DoNotShareAMessage()
    {
        // Pins the DISCRIMINATION ITSELF rather than either guard. If someone ever collapses the
        // two messages into one shared string, the two Facts above would still pass individually
        // (each substring would match the merged text only by luck) -- this one fails immediately.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var owner = await SeedOwnMasterAsync("distinct");

            UserFriendlyException notInRole;
            using (WithCurrentUser.Run(_principal, owner.IdentityUserId))
            {
                notInRole = await Should.ThrowAsync<UserFriendlyException>(
                    async () => await _profile.GetAsync());
            }

            UserFriendlyException noMaster;
            using (WithCurrentUser.Run(_principal, Guid.NewGuid(), ClaimExaminerRole))
            {
                noMaster = await Should.ThrowAsync<UserFriendlyException>(
                    async () => await _profile.GetAsync());
            }

            noMaster.Message.ShouldNotBe(
                notInRole.Message,
                "The two refusals must stay distinguishable. A caller who is not a claim examiner "
                + "and a claim examiner with no profile need different remedies.");
        }
    }

    // ------------------------------------------------------------------------
    // GetAsync -- the happy path and the projection.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ResolvesTheCallersOwnMaster()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var owner = await SeedOwnMasterAsync("get");

            using (WithCurrentUser.Run(_principal, owner.IdentityUserId, ClaimExaminerRole))
            {
                var dto = await _profile.GetAsync();

                dto.ShouldNotBeNull();
                dto.FirstName.ShouldBe("TEST-First");
                dto.LastName.ShouldBe("TEST-Last");
                dto.Email.ShouldBe(owner.Email);
                dto.ConcurrencyStamp.ShouldNotBeNullOrWhiteSpace(
                    "Map must carry the stamp through, or the caller cannot round-trip an update.");
            }
        }
    }

    [Fact]
    public async Task GetAsync_DoesNotResolveSomebodyElsesMaster()
    {
        // The API accepts no target id, so cross-party reads are meant to be structurally
        // impossible. This asserts the RESOLUTION is actually keyed on the caller: two masters
        // exist, and the caller must get their own rather than simply the first row.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await SeedOwnMasterAsync("theirs");
            var mine = await SeedOwnMasterAsync("mine");

            using (WithCurrentUser.Run(_principal, mine.IdentityUserId, ClaimExaminerRole))
            {
                var dto = await _profile.GetAsync();

                dto.Email.ShouldBe(
                    mine.Email,
                    "Resolution must be keyed on CurrentUser.Id. Getting the other master back "
                    + "means the predicate is not filtering on IdentityUserId.");
            }
        }
    }

    // ------------------------------------------------------------------------
    // UpdateAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PersistsTheEditableFields()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var owner = await SeedOwnMasterAsync("update");

            using (WithCurrentUser.Run(_principal, owner.IdentityUserId, ClaimExaminerRole))
            {
                var before = await _profile.GetAsync();

                var updated = await _profile.UpdateAsync(new UpdateMyClaimExaminerProfileInput
                {
                    FirstName = "TEST-Updated-First",
                    LastName = "TEST-Updated-Last",
                    City = "TEST-Updated-City",
                    PhoneNumber = TenDigitPhone,
                    ConcurrencyStamp = before.ConcurrencyStamp,
                });

                updated.FirstName.ShouldBe("TEST-Updated-First");
                updated.LastName.ShouldBe("TEST-Updated-Last");
                updated.City.ShouldBe("TEST-Updated-City");
            }

            var saved = await _claimExaminerRepository.GetAsync(owner.MasterId);
            saved.FirstName.ShouldBe(
                "TEST-Updated-First",
                "Read back from the repository, not from the returned DTO -- otherwise this asserts "
                + "the mapper rather than the write.");
        }
    }

    [Fact]
    public async Task UpdateAsync_PreservesEmailAndIdentityFromTheExistingMaster()
    {
        // THE HIGHEST-VALUE FACT IN THIS FILE. UpdateAsync passes `examiner.Email` and
        // `examiner.IdentityUserId` -- values read from the PERSISTED ROW -- into the manager, and
        // NOT anything from the input. The service comment calls that deliberate ("email + identity
        // are preserved (not self-editable here)"), and nothing pinned it before this Fact.
        //
        // It matters beyond tidiness: IdentityUserId is what the caller is resolved BY. If a
        // self-edit could move it, a claim examiner could re-home their own master onto another
        // login, and the "structurally impossible to reach another party's record" guarantee in the
        // class docstring would stop being true.
        //
        // The input DTO has no Email or IdentityUserId field at all, so the mutation this Fact
        // catches is somebody ADDING one and wiring it through -- which is exactly the change that
        // would look like a harmless feature.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var owner = await SeedOwnMasterAsync("preserve");

            using (WithCurrentUser.Run(_principal, owner.IdentityUserId, ClaimExaminerRole))
            {
                var before = await _profile.GetAsync();

                var updated = await _profile.UpdateAsync(new UpdateMyClaimExaminerProfileInput
                {
                    FirstName = "TEST-Renamed",
                    LastName = "TEST-Renamed",
                    ConcurrencyStamp = before.ConcurrencyStamp,
                });

                updated.Email.ShouldBe(
                    owner.Email,
                    "Email must survive a self-edit: the service reads it off the existing master "
                    + "rather than the input.");
            }

            var saved = await _claimExaminerRepository.GetAsync(owner.MasterId);
            saved.Email.ShouldBe(owner.Email);
            saved.IdentityUserId.ShouldBe(
                owner.IdentityUserId,
                "IdentityUserId is the key the caller is resolved BY. If a self-edit can change it, "
                + "a claim examiner can re-home their master onto another login.");
        }
    }

    [Fact]
    public async Task UpdateAsync_ForACallerWithNoMaster_IsRefusedBeforeAnyWrite()
    {
        // The write path resolves the master FIRST, so a caller with none is refused rather than
        // silently creating one. Mirrors the attorney file's third Fact.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, Guid.NewGuid(), ClaimExaminerRole))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _profile.UpdateAsync(new UpdateMyClaimExaminerProfileInput
                {
                    FirstName = "TEST-NoMaster",
                }));
        }
    }
}
