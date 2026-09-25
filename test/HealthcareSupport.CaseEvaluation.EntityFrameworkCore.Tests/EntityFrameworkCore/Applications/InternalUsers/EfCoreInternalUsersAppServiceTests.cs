using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// Integration coverage for <see cref="InternalUsersAppService"/> -- the IT-Admin surface that
/// owns the whole internal-operator lifecycle: create a host operator, list the Staff table,
/// trigger a password reset on someone else's behalf, and populate the office picker.
///
/// <para><b>THE NOTIFICATION-TEMPLATE WALL DOES NOT APPLY TO THIS SERVICE, and that is the
/// fact the whole file turns on.</b> Both of its dispatches are HOST-scoped:
/// <c>CreateAsync</c> wraps its entire body in <c>CurrentTenant.Change(null)</c>, and
/// <c>SendPasswordResetEmailAsync</c> never changes tenant at all. The two codes involved --
/// <c>InternalUserCreated</c> and <c>ResetPassword</c> -- are both members of
/// <c>NotificationTemplateConsts.Codes.HostScoped</c>, which is exactly the set
/// <c>NotificationTemplateDataSeedContributor</c> seeds when the rig invokes
/// <c>IDataSeeder.SeedAsync()</c> with no tenant, and both have real resource-backed bodies on
/// disk. So the renders SUCCEED here and the resulting
/// <c>NotificationOutboxItem</c> rows are real, observable evidence -- no substitute
/// dispatcher, no seeded scaffolding. The one branch the wall does block is named in the
/// "not pinned" list below.</para>
///
/// <para><b>OUTBOX ASSERTIONS RUN IN A SECOND UNIT OF WORK, ALWAYS.</b> The row itself is
/// written inside the acting unit of work, but the drain enqueue is deferred to
/// <c>uow.OnCompleted</c>, and the test base runs <c>await action()</c> before
/// <c>uow.CompleteAsync()</c>. Asserting inside the acting lambda reads a half-finished
/// picture. Every Fact below therefore hoists the ids out and re-queries afterwards.</para>
///
/// <para><b>ISOLATION.</b> The rig is one SQLite connection for the whole run with no rollback
/// between tests, so nothing here asserts on a count of rows it did not create. Every Fact mints
/// its own <c>TEST-</c> token and either filters by it or queries the outbox by a context tag
/// carrying a user id created moments earlier.</para>
///
/// <para><b>NO IMPERSONATION IS NEEDED ANYWHERE IN THIS FILE.</b> The service reads
/// <c>CurrentTenant</c> and nothing else -- there is no <c>ICurrentUser</c> reference in it, so
/// no internal-role short-circuit exists to be taken vacuously by the ambient admin principal.
/// Authorization attributes are no-ops under <c>AddAlwaysAllowAuthorization()</c>, so no Fact
/// here asserts an authorization outcome.</para>
///
/// <para><b>WHAT THESE DO NOT PIN, and why each is unreachable rather than skipped:</b></para>
/// <list type="bullet">
///   <item><c>Check.NotNull(input, nameof(input))</c> in <c>CreateAsync</c> and
///     <c>GetInternalUsersAsync</c>. ABP's <c>MethodInvocationValidator</c> rejects a null
///     non-optional DTO argument with <c>AbpValidationException</c> before the body runs. A Fact
///     here would assert the framework validator while appearing to assert the service.</item>
///   <item><c>InternalUserRoleMissing</c> (step 4a). It needs a name that is in
///     <c>CreatableRoleNames</c> yet absent in host scope. Both allow-listed roles are seeded
///     host-scoped by <c>InternalUserRoleDataSeedContributor</c>, so forcing it means deleting a
///     seeded role out of a shared accumulating rig.</item>
///   <item><c>InternalUserCreateFailed</c> via the <c>updateResult</c> rollback (4e) and
///     <c>InternalUserRoleAssignFailed</c> (4f). Both need an <c>IdentityResult</c> failure on a
///     user that has just succeeded <c>CreateAsync</c> with a role verified present at 4a.</item>
///   <item><c>WelcomeEmailQueued == false</c> and the <c>catch</c> in
///     <c>TrySendWelcomeEmailAsync</c>. The natural way to make the dispatch throw is a missing
///     template, but the dispatch sits inside <c>CurrentTenant.Change(null)</c> where
///     <c>InternalUserCreated</c> is always seeded. Only the <c>true</c> branch is pinned.</item>
///   <item>The tenant branch of the reset-URL ternary
///     (<c>user.TenantId.HasValue -&gt; BuildPasswordResetUrlAsync</c>). Reaching it needs a
///     tenant-scoped user, which needs the call inside <c>CurrentTenant.Change(tenantId)</c>,
///     which puts the <c>ResetPassword</c> dispatch inside a tenant and raises
///     <c>BusinessException("CaseEvaluation:NotificationTemplate.NotFound")</c>. This is the
///     template wall, and it is left alone rather than papered over with a hand-seeded template.
///     The HOST branch is pinned, and it is the one Phase D actually broke.</item>
///   <item>The <c>user.TenantId != null</c> skip in <c>GetInternalUsersAsync</c>. It has NO
///     possible failing input in this rig, and saying so is more useful than a Fact that looks
///     like it covers it. The three internal roles exist only in host scope (the rig seeds with
///     <c>context.TenantId == null</c>, so the contributor's per-tenant branch never runs), and
///     the role query already runs inside <c>CurrentTenant.Change(null)</c> where ABP's
///     <c>IMultiTenant</c> filter excludes every tenant-scoped user before the check is reached.
///     Deleting the line would fail nothing.</item>
///   <item><c>Take(200)</c> in <c>GetTenantOptionsAsync</c>. Proving a cap needs 201 tenants;
///     creating them would slow every later Fact in a shared rig for one boundary.</item>
/// </list>
/// </summary>
public class EfCoreInternalUsersAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private const string IntakeStaff = InternalUserRoleDataSeedContributor.IntakeStaffRoleName;
    private const string StaffSupervisor = InternalUserRoleDataSeedContributor.StaffSupervisorRoleName;
    private const string ItAdmin = InternalUserRoleDataSeedContributor.ItAdminRoleName;

    /// <summary>The shared "Management" label CreateAsync stamps on every host operator.</summary>
    private const string ManagementLabel = "Management";

    /// <summary>
    /// Host portal root. App:AngularUrl is "http://localhost:4200" in the TestBase
    /// appsettings.json, and TenantNaming.ReservedSlug ("admin") is the label host scope
    /// composes, so BuildPortalRootUrlAsync(null) must land here.
    /// </summary>
    private const string HostPortalRoot = "http://admin.localhost:4200";

    /// <summary>
    /// Host AuthServer root. Same composition, sourced from AuthServer:Authority
    /// ("http://localhost:44368").
    /// </summary>
    private const string HostResetUrlPrefix = "http://admin.localhost:44368/Account/ResetPassword";

    /// <summary>
    /// Pulls the temporary password back out of the rendered welcome email. The shipped
    /// InternalUserCreated.html wraps ##Password## in a &lt;strong&gt; of its own, so an anchored
    /// "&gt;4 alphanumerics @ 4 alphanumerics&lt;" can only match the generated password: the only
    /// other "@" in the body is the login address, whose local part is longer than four
    /// characters and whose domain contains a dot.
    /// </summary>
    private static readonly Regex ParityPasswordInBody = new(
        ">([A-Za-z0-9]{4}@[A-Za-z0-9]{4})<",
        RegexOptions.None,
        TimeSpan.FromSeconds(1));

    private readonly IInternalUsersAppService _appService;
    private readonly IRepository<NotificationOutboxItem, Guid> _outboxRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreInternalUsersAppServiceTests()
    {
        _appService = GetRequiredService<IInternalUsersAppService>();
        _outboxRepository = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ------------------------------------------------------------------------
    // Fixture helpers.
    // ------------------------------------------------------------------------

    /// <summary>A per-Fact token; every row a Fact creates carries it so nothing else matches.</summary>
    private static string NewToken() => $"tk{Guid.NewGuid():N}";

    /// <summary>@test.local is RFC-reserved, so none of these can be routed to a real inbox.</summary>
    private static string EmailFor(string token, string suffix = "") =>
        $"TEST-{token}{suffix}@test.local";

    private static CreateInternalUserDto NewInput(
        string token,
        string roleName,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? phoneNumber = null,
        Guid? tenantId = null) =>
        new()
        {
            Email = email ?? EmailFor(token),
            FirstName = firstName ?? "TEST-first",
            LastName = lastName ?? $"TEST-last-{token}",
            RoleName = roleName,
            PhoneNumber = phoneNumber,
            TenantId = tenantId,
        };

    /// <summary>
    /// Creates a HOST IdentityUser (TenantId null) directly through the UserManager, optionally
    /// in one or more roles. Must be called inside a unit of work.
    /// </summary>
    private async Task<Guid> SeedHostUserAsync(
        string userName,
        string email,
        string? name,
        string? surname,
        bool isActive,
        string[] roleNames)
    {
        var userId = Guid.NewGuid();
        using (_currentTenant.Change(null))
        {
            var user = new IdentityUser(userId, userName, email, tenantId: null)
            {
                Name = name,
                Surname = surname,
            };
            if (!isActive)
            {
                user.SetIsActive(false);
            }

            var created = await _userManager.CreateAsync(user, IdentityUsersTestData.SeedPassword);
            created.Succeeded.ShouldBeTrue(
                "Fixture failed to create the host user, so every assertion below would be "
                + "asserting against a user that does not exist: "
                + string.Join("; ", created.Errors.Select(e => e.Description)));

            foreach (var roleName in roleNames)
            {
                var assigned = await _userManager.AddToRoleAsync(user, roleName);
                assigned.Succeeded.ShouldBeTrue(
                    $"Fixture failed to put the host user in '{roleName}': "
                    + string.Join("; ", assigned.Errors.Select(e => e.Description)));
            }
        }

        return userId;
    }

    private Task<PagedResultDtoShim> QueryInternalUsersAsync(
        string filter, string? sorting = null, int? maxResultCount = null, int? skipCount = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            var input = new GetInternalUsersInput { Filter = filter };
            if (sorting != null)
            {
                input.Sorting = sorting;
            }
            if (maxResultCount.HasValue)
            {
                input.MaxResultCount = maxResultCount.Value;
            }
            if (skipCount.HasValue)
            {
                input.SkipCount = skipCount.Value;
            }

            var page = await _appService.GetInternalUsersAsync(input);
            return new PagedResultDtoShim(page.TotalCount, page.Items.ToList());
        });

    /// <summary>
    /// A detached copy of the paged result. The DTOs survive the unit of work fine, but copying
    /// the list makes it explicit that every assertion happens after the UoW has completed.
    /// </summary>
    private sealed record PagedResultDtoShim(long TotalCount, List<InternalUserListDto> Items);

    /// <summary>
    /// Reads back the single outbox row for a context tag, in its OWN unit of work. See the class
    /// remarks: the row is written inside the acting UoW and this must not run there.
    /// </summary>
    private async Task<NotificationOutboxItem> SingleOutboxRowAsync(string contextTag)
    {
        var rows = await WithUnitOfWorkAsync(
            () => _outboxRepository.GetListAsync(x => x.Context == contextTag));

        rows.Count.ShouldBe(
            1,
            $"Expected exactly one outbox row tagged '{contextTag}'. The tag carries a user id "
            + "minted in this Fact, so anything other than 1 means the dispatch fanned out or "
            + "did not happen at all.");
        return rows[0];
    }

    // ------------------------------------------------------------------------
    // CreateAsync -- the refusals.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_RejectsARoleOutsideTheAllowList()
    {
        // "IT Admin" is the right probe precisely because it is a REAL host role -- seeded by
        // InternalUserRoleDataSeedContributor's host pass. So this is a genuine refusal, not a
        // lookup that happens to miss: delete the allow-list check and FindByNameAsync succeeds,
        // the create runs to completion, and an IT Admin mints another IT Admin.
        var token = NewToken();

        var ex = await Should.ThrowAsync<BusinessException>(
            () => WithUnitOfWorkAsync(() => _appService.CreateAsync(NewInput(token, ItAdmin))),
            "Creating an IT Admin through the staff form must be refused server-side. The SPA "
            + "dropdown offering only two roles is a convenience, not a control.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserInvalidRole);
        ex.Data["AttemptedRole"].ShouldBe(ItAdmin);
        ex.Data["AllowedRoles"].ShouldBe($"{IntakeStaff}, {StaffSupervisor}");

        // The refusal has to happen BEFORE any write. A code-correct exception thrown after the
        // user row landed would leave an orphan IT Admin behind.
        await WithUnitOfWorkAsync(async () =>
            (await _userManager.FindByEmailAsync(EmailFor(token))).ShouldBeNull(
                "The role was refused, so no IdentityUser may exist for that address."));
    }

    [Fact]
    public async Task CreateAsync_RejectsADuplicateEmailWithoutEchoingIt()
    {
        // The fixture is the seeded host admin: the duplicate must already EXIST for this to be a
        // negative guarantee rather than a not-found. Without a colliding row the guard is a
        // no-op and deleting it changes nothing.
        var duplicate = IdentityUsersTestData.HostAdminEmail;

        var ex = await Should.ThrowAsync<BusinessException>(
            () => WithUnitOfWorkAsync(() => _appService.CreateAsync(
                NewInput(NewToken(), IntakeStaff, email: duplicate))),
            "A second account on an address that already has one must be refused.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);

        // The other half, and the reason the service deliberately attaches NO data to this throw:
        // echoing the address turns the endpoint into an account-enumeration oracle. Adding a
        // .WithData("Email", normalizedEmail) breaks this half while leaving the code assertion
        // above green -- which is exactly the regression worth catching, because such a change
        // looks like a helpful error-message improvement.
        ex.Message.ShouldNotContain(duplicate, Case.Insensitive);
        foreach (var entryValue in ex.Data.Values)
        {
            var text = entryValue?.ToString();
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }
            text.Contains(duplicate, StringComparison.OrdinalIgnoreCase).ShouldBeFalse(
                "An entry in BusinessException.Data carries the submitted address.");
        }
    }

    // ------------------------------------------------------------------------
    // CreateAsync -- the happy path and what it persists.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsAHostOperatorIgnoringTheSuppliedTenantId()
    {
        // input.TenantId is deliberately set to a REAL office. Phase D (2026-06-25) made internal
        // operators host logins, and the DTO field survives only for proxy back-compat -- honouring
        // it would put the operator inside one office's database and break the office switcher.
        // The assertion that matters is TenantId == null DESPITE that value being supplied.
        var token = NewToken();
        var input = NewInput(
            token,
            IntakeStaff,
            phoneNumber: "2135550134",     // reserved-fiction range; exactly 10 digits per [PhoneNumber]
            tenantId: TenantsTestData.TenantARef);

        var dto = await WithUnitOfWorkAsync(() => _appService.CreateAsync(input));

        dto.UserId.ShouldNotBe(Guid.Empty);
        dto.Email.ShouldBe(EmailFor(token));
        dto.FirstName.ShouldBe("TEST-first");
        dto.LastName.ShouldBe($"TEST-last-{token}");
        dto.RoleName.ShouldBe(IntakeStaff);
        dto.TenantName.ShouldBe(
            ManagementLabel,
            "Host operators share one office label in the create-result UI.");
        dto.WelcomeEmailQueued.ShouldBeTrue(
            "The InternalUserCreated template is seeded host-scoped and the dispatch runs in host "
            + "scope, so the queue attempt must succeed here.");

        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(dto.UserId);

            user.TenantId.ShouldBeNull(
                "input.TenantId named TenantA. Honouring it -- or dropping the enclosing "
                + "CurrentTenant.Change(null) -- puts the operator inside an office.");
            user.UserName.ShouldBe(
                EmailFor(token),
                "The address doubles as the login name; the SPA has no separate username field.");
            user.EmailConfirmed.ShouldBeTrue(
                "OLD parity: an IT-Admin-added account is auto-verified so it can sign in at once.");
            user.ShouldChangePasswordOnNextLogin.ShouldBeTrue(
                "The generated password is single-use by design (Adrian decision 2026-05-15).");
            user.PhoneNumber.ShouldBe("2135550134");
            user.PhoneNumberConfirmed.ShouldBeFalse(
                "SetPhoneNumber is called with confirmed:false -- nothing has verified the number.");

            (await _userManager.GetRolesAsync(user)).ShouldContain(
                IntakeStaff,
                "A role-less account cannot do anything; the assignment is part of the create.");
        });
    }

    [Fact]
    public async Task CreateAsync_AcceptsStaffSupervisorAsWellAsIntakeStaff()
    {
        // CreatableRoleNames is a LIST. Narrowing it to a single value would leave the Intake
        // Staff path green and silently break the other half of the form.
        var token = NewToken();

        var dto = await WithUnitOfWorkAsync(
            () => _appService.CreateAsync(NewInput(token, StaffSupervisor)));

        dto.RoleName.ShouldBe(StaffSupervisor);

        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(dto.UserId);
            (await _userManager.GetRolesAsync(user)).ShouldContain(StaffSupervisor);
            user.TenantId.ShouldBeNull(
                "Phase D moved Staff Supervisor host-side; a tenant-scoped one cannot switch "
                + "between offices.");
        });
    }

    [Fact]
    public async Task CreateAsync_TrimsTheNameAndPhoneFieldsBeforePersisting()
    {
        // Padding survives an SPA copy-paste and a stale bundle. The service trims name, surname
        // and phone; the email cannot be padded here because [EmailAddress] rejects it before the
        // body runs, which is itself worth knowing -- the Email.Trim() at step 4b is therefore
        // defence in depth rather than a reachable behaviour through this interface.
        var token = NewToken();
        var input = NewInput(
            token,
            IntakeStaff,
            firstName: "  TEST-first  ",
            lastName: $"  TEST-last-{token}  ",
            phoneNumber: "  2135550134  ");

        var dto = await WithUnitOfWorkAsync(() => _appService.CreateAsync(input));

        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(dto.UserId);
            user.Name.ShouldBe("TEST-first", "Leading/trailing space must not reach the column.");
            user.Surname.ShouldBe($"TEST-last-{token}");
            user.PhoneNumber.ShouldBe(
                "2135550134",
                "A padded number stored verbatim breaks every exact-match lookup on the column.");
        });
    }

    [Fact]
    public async Task CreateAsync_WithNoPhoneNumber_LeavesThePhoneUnset()
    {
        // The phone block is guarded by IsNullOrWhiteSpace precisely because the field is
        // optional. Remove the guard and the very next line dereferences a null.
        var token = NewToken();

        var dto = await WithUnitOfWorkAsync(
            () => _appService.CreateAsync(NewInput(token, IntakeStaff, phoneNumber: null)));

        await WithUnitOfWorkAsync(async () =>
        {
            var user = await _userManager.GetByIdAsync(dto.UserId);
            user.PhoneNumber.ShouldBeNull(
                "No number was submitted, so nothing may be written to the column.");
        });
    }

    // ------------------------------------------------------------------------
    // CreateAsync -- the welcome email. Real render, real outbox row.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_QueuesTheWelcomeEmailWithTheHostPortalUrl()
    {
        var token = NewToken();

        // Hoisted out of the lambda on purpose -- see the class remarks on UoW-deferred work.
        var dto = await WithUnitOfWorkAsync(
            () => _appService.CreateAsync(NewInput(token, IntakeStaff)));

        var row = await SingleOutboxRowAsync($"InternalUserCreated/{dto.UserId}");

        row.To.ShouldBe(EmailFor(token), "The welcome email goes to the new operator, nobody else.");
        row.IsBodyHtml.ShouldBeTrue();
        row.Status.ShouldBe(
            NotificationOutboxStatus.Pending,
            "A freshly enqueued row is Pending until the drain confirms SMTP delivery.");
        row.TenantId.ShouldBeNull(
            "The dispatch runs inside CurrentTenant.Change(null), so the ledger row belongs to "
            + "the host database, not an office one.");
        // Shouldly has no string-plus-custom-message ShouldContain overload in this version -- the
        // compiler falls through to the IEnumerable<char> overloads and fails (CS1503). Contains()
        // plus ShouldBeTrue keeps the explanation, which is the part worth keeping.
        row.Body.Contains(IntakeStaff).ShouldBeTrue(
            "##RoleName## must render; the operator is told what they were granted.");
        row.Body.Contains(HostPortalRoot).ShouldBeTrue(
            "##PortalUrl## must be the HOST portal root. A bare base-domain link 404s, and a "
            + "tenant-prefixed one sends a host operator to an office they may not hold.");
    }

    [Fact]
    public async Task CreateAsync_SendsTheTemporaryPasswordOnlyByEmail()
    {
        // Two halves, and both are needed. First: the password genuinely reaches the email, in the
        // parity shape -- drop ["Password"] from the variable bag and ##Password## renders
        // literally, so the extraction below finds nothing. Second: it reaches NOTHING ELSE.
        var token = NewToken();

        var dto = await WithUnitOfWorkAsync(
            () => _appService.CreateAsync(NewInput(token, IntakeStaff)));

        var row = await SingleOutboxRowAsync($"InternalUserCreated/{dto.UserId}");

        var match = ParityPasswordInBody.Match(row.Body);
        match.Success.ShouldBeTrue(
            "The rendered welcome email carries no {4}@{4} value, so either ##Password## was not "
            + "substituted or the generator stopped producing the parity shape. The email is the "
            + "ONLY channel this password leaves the server through -- if it is not here, the "
            + "account is unusable.");
        var generatedPassword = match.Groups[1].Value;
        generatedPassword.ShouldNotBe(
            dto.Email,
            "The extraction matched the login address instead of the password.");

        // Reflection rather than a property-by-property list, so a field added to the DTO later is
        // covered the day it appears rather than the day someone remembers to extend this Fact.
        foreach (var property in typeof(InternalUserCreatedDto)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = property.GetValue(dto)?.ToString();
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }
            value.Contains(generatedPassword, StringComparison.Ordinal).ShouldBeFalse(
                $"InternalUserCreatedDto.{property.Name} carries the temporary password. The "
                + "response DTO is logged, cached and rendered by the SPA; the password must "
                + "travel by email alone.");
        }
    }

    // ------------------------------------------------------------------------
    // SendPasswordResetEmailAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task SendPasswordResetEmailAsync_ThrowsNotFoundForAnUnknownUserId()
    {
        var ex = await Should.ThrowAsync<BusinessException>(
            () => WithUnitOfWorkAsync(
                () => _appService.SendPasswordResetEmailAsync(Guid.NewGuid())),
            "An id that resolves to nobody must come back as a 400-mapped business error, not a "
            + "raw 500 from dereferencing null.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_QueuesAHostScopedResetLinkForAHostOperator()
    {
        // THIS IS THE REGRESSION PIN FOR THE PHASE D BUG the method's own docstring names: the
        // prior guard threw InternalUserTenantRequired for any user with a null TenantId, which
        // after Phase D is EVERY internal operator -- admin-triggered reset was dead for all of
        // them. Restore that guard, or flip the ternary so a null-tenant user takes
        // BuildPasswordResetUrlAsync(user.TenantId.Value, ...), and this Fact throws.
        var token = NewToken();
        var userId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}",
            email: EmailFor(token),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IntakeStaff }));

        await WithUnitOfWorkAsync(() => _appService.SendPasswordResetEmailAsync(userId));

        var row = await SingleOutboxRowAsync($"PasswordReset/AdminTriggered/{userId}");

        row.To.ShouldBe(EmailFor(token));
        // See the note above on Shouldly's missing string-plus-message ShouldContain overload.
        row.Body.Contains(HostResetUrlPrefix).ShouldBeTrue(
            "A host operator must be sent to the HOST AuthServer reset page. An office-prefixed "
            + "host would resolve to a database the operator does not live in.");
        row.Body.Contains($"userId={userId}").ShouldBeTrue(
            "The reset link must carry the target user id, not the caller's.");
    }

    // ------------------------------------------------------------------------
    // GetTenantOptionsAsync -- the office picker.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetTenantOptionsAsync_FiltersByNameAndExcludesNonMatches()
    {
        // TenantB is the decoy this negative guarantee needs: without a second seeded office,
        // "the filter excludes non-matches" has nothing to exclude and deleting the WhereIf
        // changes no observable result.
        var result = await WithUnitOfWorkAsync(
            () => _appService.GetTenantOptionsAsync(TenantsTestData.TenantAName));

        var ids = result.Items.Select(x => x.Id).ToList();
        ids.ShouldContain(TenantsTestData.TenantARef);
        ids.ShouldNotContain(
            TenantsTestData.TenantBRef,
            $"'{TenantsTestData.TenantBName}' does not contain '{TenantsTestData.TenantAName}', "
            + "so the filter must exclude it.");

        var tenantA = result.Items.Single(x => x.Id == TenantsTestData.TenantARef);
        tenantA.DisplayName.ShouldBe(
            TenantsTestData.TenantAName,
            "The dropdown label is the office name; anything else renders a GUID to the operator.");
    }

    [Fact]
    public async Task GetTenantOptionsAsync_WithNoFilter_ReturnsEverySeededOffice()
    {
        // The WhereIf condition is the thing under test. Make it unconditional and a null filter
        // is passed to string.Contains, which is not a silent no-op -- it throws.
        var result = await WithUnitOfWorkAsync(() => _appService.GetTenantOptionsAsync(null));

        var ids = result.Items.Select(x => x.Id).ToList();
        ids.ShouldContain(TenantsTestData.TenantARef);
        ids.ShouldContain(
            TenantsTestData.TenantBRef,
            "An absent filter must not filter. Both seeded offices belong in the picker.");
    }

    [Fact]
    public async Task GetTenantOptionsAsync_OrdersOptionsByNameAscending()
    {
        // Asserted as a RELATIVE position rather than an index, so other Facts adding offices to
        // this shared rig cannot make it flap.
        var result = await WithUnitOfWorkAsync(
            () => _appService.GetTenantOptionsAsync("TEST-tenant-"));

        var ids = result.Items.Select(x => x.Id).ToList();
        var indexOfA = ids.IndexOf(TenantsTestData.TenantARef);
        var indexOfB = ids.IndexOf(TenantsTestData.TenantBRef);

        indexOfA.ShouldBeGreaterThanOrEqualTo(0, "TenantA must be in the filtered picker.");
        indexOfB.ShouldBeGreaterThanOrEqualTo(0, "TenantB must be in the filtered picker.");
        indexOfA.ShouldBeLessThan(
            indexOfB,
            $"'{TenantsTestData.TenantAName}' sorts before '{TenantsTestData.TenantBName}'. The "
            + "picker is alphabetical so an operator can find an office by scanning.");
    }

    // ------------------------------------------------------------------------
    // GetInternalUsersAsync -- the Staff table.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetInternalUsersAsync_ReturnsOnlyInternalRoleMembers()
    {
        // The decoy holds the host "admin" role. That choice is load-bearing: "admin" is a real,
        // seeded, host-scoped role that is NOT one of the three internal tiers, so this is a
        // negative guarantee with something present to exclude. Adding "admin" to
        // InternalRoleNamesByPrecedence makes the decoy appear and fails the Fact.
        var token = NewToken();
        var internalUserId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-in",
            email: EmailFor(token, "-in"),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IntakeStaff }));
        var decoyUserId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-out",
            email: EmailFor(token, "-out"),
            name: "TEST-decoy",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IdentityUsersTestData.HostAdminRoleName }));

        var page = await QueryInternalUsersAsync(token);

        page.TotalCount.ShouldBe(
            1,
            "Both users match the filter; only the one in an internal role belongs in the Staff "
            + "table.");
        page.Items.Single().Id.ShouldBe(internalUserId);
        page.Items.Select(x => x.Id).ShouldNotContain(
            decoyUserId,
            "A host 'admin' is not internal staff. Widening the role union past the three internal "
            + "tiers leaks unrelated accounts into the Staff screen.");
    }

    [Fact]
    public async Task GetInternalUsersAsync_ResolvesTheHighestPrecedenceRole()
    {
        // One user, two internal roles. Without the dedup the row would be emitted twice; without
        // the precedence order it would surface as the lower tier, understating what the account
        // can actually do.
        var token = NewToken();
        var userId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}",
            email: EmailFor(token),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IntakeStaff, StaffSupervisor }));

        var page = await QueryInternalUsersAsync(token);

        page.TotalCount.ShouldBe(
            1,
            "A user holding two internal roles is one person and must produce one row.");
        var row = page.Items.Single();
        row.Id.ShouldBe(userId);
        row.Role.ShouldBe(
            StaffSupervisor,
            "Precedence is IT Admin > Staff Supervisor > Intake Staff. Reversing the array, or "
            + "dropping the byUserId.ContainsKey guard so the later pass overwrites, both land on "
            + "Intake Staff and understate the account.");
    }

    [Fact]
    public async Task GetInternalUsersAsync_SortsAndPagesWithinTheFilteredSet()
    {
        var token = NewToken();
        foreach (var suffix in new[] { "a", "b", "c" })
        {
            await WithUnitOfWorkAsync(() => SeedHostUserAsync(
                userName: $"TEST-{token}-{suffix}",
                email: EmailFor(token, $"-{suffix}"),
                name: "TEST-first",
                surname: $"TEST-ln-{token}-{suffix}",
                isActive: true,
                roleNames: new[] { IntakeStaff }));
        }

        var firstPage = await QueryInternalUsersAsync(
            token, sorting: "lastname desc", maxResultCount: 2);

        firstPage.TotalCount.ShouldBe(
            3,
            "TotalCount is the size of the FILTERED set, not of the page. Returning page.Count "
            + "instead makes the pager show one page however many rows exist.");
        firstPage.Items.Count.ShouldBe(2);
        firstPage.Items[0].LastName.ShouldBe($"TEST-ln-{token}-c");
        firstPage.Items[1].LastName.ShouldBe($"TEST-ln-{token}-b");

        var secondPage = await QueryInternalUsersAsync(
            token, sorting: "lastname desc", maxResultCount: 2, skipCount: 2);

        secondPage.TotalCount.ShouldBe(3, "The total does not shrink as the operator pages.");
        secondPage.Items.Count.ShouldBe(1);
        secondPage.Items[0].LastName.ShouldBe(
            $"TEST-ln-{token}-a",
            "Skip/Take must be applied AFTER the filter and the sort. Applying them first pages "
            + "over the whole role union and drops matching rows.");
    }

    [Fact]
    public async Task GetInternalUsersAsync_MatchesTheFilterCaseInsensitively()
    {
        // The token is prefixed with letters by NewToken(), so the upper-cased form below is
        // guaranteed to differ from what is stored even if the GUID half happened to be digits.
        var token = NewToken();
        var userId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}",
            email: EmailFor(token),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IntakeStaff }));

        var page = await QueryInternalUsersAsync(token.ToUpperInvariant());

        page.TotalCount.ShouldBe(
            1,
            "An operator typing a name in a different case must still find the row. Switching the "
            + "Contains comparison to Ordinal makes the search silently return nothing.");
        page.Items.Single().Id.ShouldBe(userId);
    }

    [Fact]
    public async Task GetInternalUsersAsync_SortsByEmailWhenAsked()
    {
        // The fixture is built so email order and full-name order DISAGREE -- otherwise "sorted by
        // email" is indistinguishable from "sorted by anything else" and the switch arm is not
        // actually pinned.
        var token = NewToken();
        var (byEmailFirstId, byEmailSecondId) = await SeedDivergentSortFixtureAsync(token);

        var page = await QueryInternalUsersAsync(token, sorting: "email");

        page.TotalCount.ShouldBe(2);
        page.Items[0].Id.ShouldBe(
            byEmailFirstId,
            "Sorting=email must order on the Email column. The seeded pair's full names run the "
            + "other way, so any other selector fails here.");
        page.Items[1].Id.ShouldBe(byEmailSecondId);
    }

    [Fact]
    public async Task GetInternalUsersAsync_FallsBackToFullNameOrderForAnUnknownSortField()
    {
        // A column the table does not know about must not throw and must not scramble the order;
        // it falls through to the FullName default. Same divergent fixture, opposite expectation.
        var token = NewToken();
        var (byEmailFirstId, byEmailSecondId) = await SeedDivergentSortFixtureAsync(token);

        var page = await QueryInternalUsersAsync(token, sorting: "TEST-not-a-column");

        page.TotalCount.ShouldBe(2);
        page.Items[0].Id.ShouldBe(
            byEmailSecondId,
            "The default arm orders by FullName, and the seeded pair's full names run opposite to "
            + "their addresses. Pointing the default at any other column flips this pair.");
        page.Items[1].Id.ShouldBe(byEmailFirstId);
    }

    /// <summary>
    /// Seeds two internal-role host users whose EMAIL order is the reverse of their FULL NAME
    /// order, so a Fact can tell the two sort arms apart. Returns the ids in email-ascending
    /// order.
    /// </summary>
    private async Task<(Guid ByEmailFirst, Guid ByEmailSecond)> SeedDivergentSortFixtureAsync(
        string token)
    {
        // Email "...-a@..." sorts first, but FullName "TEST-zz ..." sorts last.
        var emailFirst = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-a",
            email: EmailFor(token, "-a"),
            name: "TEST-zz",
            surname: $"TEST-ln-{token}-a",
            isActive: true,
            roleNames: new[] { IntakeStaff }));

        var emailSecond = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-b",
            email: EmailFor(token, "-b"),
            name: "TEST-aa",
            surname: $"TEST-ln-{token}-b",
            isActive: true,
            roleNames: new[] { IntakeStaff }));

        return (emailFirst, emailSecond);
    }

    [Fact]
    public async Task GetInternalUsersAsync_FallsBackToTheUserNameWhenNoNameIsStored()
    {
        // ABP does not require a name, and an account created outside this form can have neither.
        // A blank cell in the Staff table gives the operator nothing to click on, so the row
        // composer falls back to the login name.
        var token = NewToken();
        var userName = $"TEST-{token}-nameless";
        var userId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: userName,
            email: EmailFor(token, "-nameless"),
            name: null,
            surname: null,
            isActive: true,
            roleNames: new[] { IntakeStaff }));

        var page = await QueryInternalUsersAsync(token);

        var row = page.Items.Single(x => x.Id == userId);
        row.FirstName.ShouldBe(string.Empty, "A null name maps to empty, never to null.");
        row.LastName.ShouldBe(string.Empty);
        row.FullName.ShouldBe(
            userName,
            "With no name stored the row falls back to the user name. Dropping the fallback "
            + "renders a blank cell the operator cannot identify.");
    }

    [Fact]
    public async Task GetInternalUsersAsync_ReportsTheStoredActiveFlag()
    {
        // Seeding BOTH states is what makes this falsifiable: hardcoding IsActive to true passes
        // against an all-active fixture.
        var token = NewToken();
        var activeId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-on",
            email: EmailFor(token, "-on"),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: true,
            roleNames: new[] { IntakeStaff }));
        var inactiveId = await WithUnitOfWorkAsync(() => SeedHostUserAsync(
            userName: $"TEST-{token}-off",
            email: EmailFor(token, "-off"),
            name: "TEST-first",
            surname: $"TEST-last-{token}",
            isActive: false,
            roleNames: new[] { IntakeStaff }));

        var page = await QueryInternalUsersAsync(token);

        page.TotalCount.ShouldBe(
            2,
            "A deactivated operator is still staff and must stay visible -- the table shows the "
            + "state, it does not hide the row.");
        page.Items.Single(x => x.Id == activeId).IsActive.ShouldBeTrue();
        page.Items.Single(x => x.Id == inactiveId).IsActive.ShouldBeFalse(
            "The flag is read off the IdentityUser. Hardcoding it hides a disabled account behind "
            + "an active-looking row.");
    }
}
