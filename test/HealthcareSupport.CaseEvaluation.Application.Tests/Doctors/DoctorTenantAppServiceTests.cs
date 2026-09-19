using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Saas.Host.Dtos;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

/// <summary>
/// Seam: <see cref="DoctorTenantAppService"/> -- the office (tenant) creation surface.
/// Two public entry points share one naming guard: the New Practice form
/// (<c>CreatePracticeAsync</c>) and the inherited Volo SaaS create
/// (<c>CreateAsync</c>, POST /api/app/doctor-tenant). Because an office name IS its
/// subdomain and its "CaseEvaluation_{slug}" database token, both must reject the
/// reserved host slug and anything that is not a DNS label. These tests pin that
/// guard, its case-insensitive trimmed comparison, and the translation of the
/// domain's <c>ArgumentException</c> into a user-facing <c>UserFriendlyException</c>
/// the SPA can render as a 400.
///
/// WHAT THESE TESTS DO NOT PIN, and why:
///
/// 1. Everything from <c>CreateTenantWithOfficeDatabaseAsync</c>'s second statement
///    onward -- the connectivity precheck, the single host transaction that writes
///    the tenant row plus its connection string, the out-of-band office-database
///    provisioning, the compensation message on a provisioning failure, and the
///    host-scope <c>OfficeBranding</c> display-name upsert (including the
///    "Dr. {First} {Last}" default from <c>PracticeNaming</c>). This harness
///    configures neither "App:TenantDbTemplate" nor ConnectionStrings:Default
///    (see TestBase/appsettings.json and CaseEvaluationTestBase.BeforeAddApplication;
///    the DbContext is bound to a SqliteConnection object, not a named string), so
///    the FIRST statement of that method -- TenantConnectionStringProvider
///    .BuildConnectionString -- throws AbpException("No base connection string is
///    configured...") and the rest is unreachable. Reaching it honestly needs a
///    dedicated startup module with its own database, as CaseEvaluationMultiOffice
///    TestModule does; mutating IConfiguration at runtime instead would be global
///    state in a rig with no rollback, and could fall through into real migrators
///    against the shared SQLite connection. Deliberately out of scope here.
///    A welcome side effect: this file writes NOTHING, so it cannot disturb the
///    shared accumulating test database.
///
/// 2. The seven <c>Check.NotNull</c> / <c>Check.NotNullOrWhiteSpace</c> guards.
///    Whether they are reachable through DI depends on whether ABP's class
///    interceptor wraps the member, which validates the DataAnnotations on the DTO
///    first -- and <c>CreatePracticeAsync</c> is NOT declared virtual, unlike its
///    siblings elsewhere in this codebase, so interception of that member cannot be
///    settled by reading. A test there would assert the interceptor while appearing
///    to assert the service. Every input below is DataAnnotations-valid, so each
///    test's outcome is the same under either answer.
///
/// 3. Authorization. The test module installs AddAlwaysAllowAuthorization(), so
///    [Authorize(SaasHostPermissions.Tenants.Create)] is a no-op and an
///    authorization assertion here could not fail.
///
/// All data is synthetic: TEST- prefixed names and an @test.local address
/// (RFC 6761 reserved), never a real-looking identifier.
/// </summary>
public abstract class DoctorTenantAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    // Spelled literally rather than read off either constant, so these tests stay
    // independent of the constants that ReservedTenantName_IsInSyncWith... guards.
    private const string ReservedOfficeName = "admin";

    private const string SyntheticDoctorFirstName = "TEST-Ada";
    private const string SyntheticDoctorLastName = "TEST-Falken";
    private const string SyntheticDoctorEmail = "owner@test.local";

    // Shaped to the identity policy (upper/lower/digit/symbol) so a policy check
    // cannot be what rejects the input; 12 chars, well inside every length bound.
    private const string SyntheticAdminPassword = "TEST-Aa1!zzq";

    // Resolved as the CONCRETE type on purpose. ABP's default-interface convention
    // exposes ITenantAppService for BOTH this class ("DoctorTenantAppService" ends
    // with "TenantAppService") and Volo's own TenantAppService, so resolving the
    // interface would be registration-order roulette. There is no
    // IDoctorTenantAppService. ABP's conventional registrar registers the
    // implementation type itself, which is how domain services are resolved
    // elsewhere in this suite.
    private readonly DoctorTenantAppService _service;

    protected DoctorTenantAppServiceTests()
    {
        _service = GetRequiredService<DoctorTenantAppService>();
    }

    // --- The cross-layer reserved-name constant ---

    /// <summary>
    /// ADR-006 duplicates the reserved slug "admin" in three layers on purpose so
    /// the Domain and Application can validate without referencing the host
    /// projects. Duplicated knowledge with no compiler link between the copies can
    /// drift silently, and the drift would only surface as a tenant that resolves
    /// to the host-context surface. This is the cheap guard against that.
    /// </summary>
    [Fact]
    public void ReservedTenantName_IsInSyncWithTheDomainAndRequestBoundaryCopies()
    {
        DoctorTenantAppService.ReservedTenantNameAdmin.ShouldBe(
            TenantNaming.ReservedSlug,
            "ADR-006: the Application copy of the reserved slug must match the Domain copy.");

        DoctorTenantAppService.ReservedTenantNameAdmin.ShouldBe(
            HostAwareDomainTenantResolveContributor.ReservedHostSlug,
            "ADR-006: the Application copy must match the request-boundary copy, "
            + "or a tenant could be created that the resolver sends to host context.");
    }

    // --- New Practice flow: the naming guard ---

    /// <summary>
    /// "admin" can never become an office, and it is THIS service's own guard that
    /// says so. Note that the exception TYPE alone cannot fail this test: deleting
    /// the guard block still yields a UserFriendlyException, because TenantNaming
    /// .DeriveSlug rejects the reserved slug too and the catch below translates it.
    /// The MESSAGE is therefore the only mutation-sensitive assertion -- the service
    /// guard says "Tenant name ... cannot be used", the domain says "Office name ...
    /// is reserved". The service guard is defence in depth, not the only barrier.
    /// </summary>
    [Fact]
    public async Task CreatePracticeAsync_WhenSlugIsReserved_IsRejectedByTheServicesOwnGuard()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _service.CreatePracticeAsync(BuildPracticeInput(ReservedOfficeName)));

        ex.Message.ShouldContain("Tenant name");
        ex.Message.ShouldContain("cannot be used");
    }

    /// <summary>
    /// The guard's comment claims the match is "case-insensitive on the trimmed
    /// name". Pins exactly that: a padded, mixed-case "admin" is still caught by the
    /// service guard rather than falling through to the domain. Without the message
    /// assertion this test would be vacuous, since the domain lowercases and trims
    /// on its own and would reject the same input one layer down.
    /// </summary>
    [Fact]
    public async Task CreatePracticeAsync_WhenReservedSlugIsPaddedAndMixedCase_IsStillRejectedByTheServicesOwnGuard()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _service.CreatePracticeAsync(BuildPracticeInput("  Admin  ")));

        ex.Message.ShouldContain("Tenant name");
        ex.Message.ShouldContain("cannot be used");
    }

    /// <summary>
    /// The catch block that turns the domain's ArgumentException into a
    /// UserFriendlyException is the only thing standing between a bad office name
    /// and a raw 500. A space is not a DNS label character, and nothing upstream
    /// checks the charset -- [Required] and [StringLength(63)] both pass here -- so
    /// TenantNaming is what rejects it and the service is what makes it renderable.
    /// The message assertion additionally pins that the domain's explanation is
    /// forwarded rather than swallowed and replaced.
    /// </summary>
    [Fact]
    public async Task CreatePracticeAsync_WhenSlugIsNotDnsSafe_TranslatesTheDomainFailureToAUserFacingError()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _service.CreatePracticeAsync(BuildPracticeInput("TEST practice")));

        ex.Message.ShouldContain("DNS-safe");
    }

    /// <summary>
    /// The negative guarantee: a legal DNS label must NOT be rejected. A guard that
    /// only ever rejects is indistinguishable from a broken one, so this pins that
    /// a valid slug passes the naming guard and the flow proceeds into
    /// CreateTenantWithOfficeDatabaseAsync.
    ///
    /// The terminal exception is a property of THIS HARNESS, not of the service:
    /// with no "App:TenantDbTemplate" and no ConnectionStrings:Default configured,
    /// TenantConnectionStringProvider.BuildConnectionString throws AbpException on
    /// the first line of that method. If either key is ever added to
    /// TestBase/appsettings.json this test MUST be revisited -- the call would then
    /// run on into base.CreateAsync and start writing tenant rows into the shared,
    /// non-rolled-back test database.
    ///
    /// CORRECTED 2026-09-17. This docstring previously claimed "UserFriendlyException derives from
    /// AbpException, so ThrowAsync&lt;AbpException&gt; alone would accept a rejection by the naming
    /// guard", and added an explicit is-check to compensate. **That hierarchy claim is false**, and
    /// the compiler proves it: the is-check raised CS0184, "the given expression is never of the
    /// provided type". In ABP, BusinessException -- and therefore UserFriendlyException -- derives
    /// from Exception directly, NOT from AbpException; they are unrelated branches.
    ///
    /// So <c>ThrowAsync&lt;AbpException&gt;</c> ALREADY excludes a naming-guard rejection: had the
    /// guard rejected this slug, a UserFriendlyException would have been thrown and the ThrowAsync
    /// itself would have failed. The is-check was not merely wrong, it was redundant, and the
    /// negative guarantee is carried by the exception type plus the message assertion below.
    /// </summary>
    [Fact]
    public async Task CreatePracticeAsync_WhenSlugIsAValidDnsLabel_PassesTheNamingGuard()
    {
        var ex = await Should.ThrowAsync<AbpException>(
            async () => await _service.CreatePracticeAsync(BuildPracticeInput("test-practice-zzq")));

        // Reaching the connection-string stage IS the proof that the naming guard passed: the guard
        // runs first and would have thrown a UserFriendlyException instead.
        ex.Message.ShouldContain("TenantDbTemplate");
    }

    /// <summary>
    /// The office name is case-NORMALISED, not case-rejected. TenantNaming.DeriveSlug
    /// lowercases before validating, so a surname typed with a capital letter on the
    /// New Practice form is accepted. Hardening DeriveSlugOrThrow into a
    /// validate-only check (IsValidSlug, which requires an already-lowercase slug)
    /// would silently start rejecting ordinary input; this is what would catch it.
    /// Same harness-terminal AbpException as the test above -- see its remarks.
    /// </summary>
    [Fact]
    public async Task CreatePracticeAsync_WhenSlugIsUppercase_IsNormalisedRatherThanRejected()
    {
        var ex = await Should.ThrowAsync<AbpException>(
            async () => await _service.CreatePracticeAsync(BuildPracticeInput("TEST-Upper-ZZQ")));

        // Same correction as the Fact above: the is-check here raised CS0184 because
        // UserFriendlyException is not an AbpException. Reaching TenantDbTemplate is itself the
        // proof that the uppercase name was NORMALISED rather than rejected -- a rejection would
        // have thrown a UserFriendlyException, which ThrowAsync<AbpException> would not have caught.
        ex.Message.ShouldContain("TenantDbTemplate");
    }

    // --- Inherited Volo SaaS create: the same guard on the stock endpoint ---

    /// <summary>
    /// The security-relevant half. CreateAsync is the INHERITED SaaS create surface
    /// (POST /api/app/doctor-tenant, generated into the SPA proxy), and Volo's own
    /// implementation knows nothing about the reserved host slug. Only this
    /// override's DeriveSlugOrThrow call stops an operator creating an office named
    /// "admin" whose subdomain would collide with the host-context surface.
    /// Unlike CreatePracticeAsync, the exception type IS load-bearing here: with the
    /// guard removed the call falls through to the connection-string resolution and
    /// throws a bare AbpException instead.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenTenantNameIsReserved_IsGuardedLikeThePracticeFlow()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _service.CreateAsync(BuildSaasInput(ReservedOfficeName)));

        ex.Message.ShouldContain("Tenant name");
        ex.Message.ShouldContain("cannot be used");
    }

    /// <summary>
    /// The stock SaaS endpoint is DNS-guarded too, not merely reserved-name guarded.
    /// Volo's TenantAppService accepts any non-empty name; here the name becomes a
    /// subdomain and a database token, so the charset restriction has to hold on
    /// this surface as well. With the DeriveSlugOrThrow call removed the name flows
    /// straight through and the failure becomes a bare AbpException from the
    /// connection-string resolution rather than a user-facing 400.
    /// </summary>
    [Fact]
    public async Task CreateAsync_WhenTenantNameIsNotDnsSafe_IsGuardedLikeThePracticeFlow()
    {
        var ex = await Should.ThrowAsync<UserFriendlyException>(
            async () => await _service.CreateAsync(BuildSaasInput("TEST Practice")));

        ex.Message.ShouldContain("DNS-safe");
    }

    // --- Builders. Every field is DataAnnotations-valid so that DTO validation,
    // whether or not the interceptor runs it, can never be what fails a test. ---

    private static CreatePracticeInput BuildPracticeInput(string slug)
    {
        return new CreatePracticeInput
        {
            Slug = slug,
            DoctorFirstName = SyntheticDoctorFirstName,
            DoctorLastName = SyntheticDoctorLastName,
            DoctorEmail = SyntheticDoctorEmail,

            // Left null deliberately: the "Dr. {First} {Last}" default it triggers
            // lives past the unreachable line, so a value here would assert nothing.
            DisplayName = null,
        };
    }

    private static SaasTenantCreateDto BuildSaasInput(string name)
    {
        return new SaasTenantCreateDto
        {
            Name = name,
            AdminEmailAddress = SyntheticDoctorEmail,
            AdminPassword = SyntheticAdminPassword,
        };
    }
}
