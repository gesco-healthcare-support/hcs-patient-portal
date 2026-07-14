using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- DI-resolved integration tests for
/// <see cref="IExternalSignupAppService"/>. The pure-helper tests in
/// <see cref="ExternalSignupValidatorUnitTests"/> exercise the static
/// validator directly; this file exercises the full AppService instance
/// resolved through ABP's DI graph so the constructor's
/// <c>IStringLocalizer&lt;CaseEvaluationResource&gt;</c> wiring + the
/// caller-line <c>ValidateRegistrationInput(input, _localizer)</c>
/// invocation get coverage credit.
///
/// <para>The test deliberately submits a DTO that fails validation at
/// the first gate (Password/ConfirmPassword mismatch). This proves the
/// AppService is constructible via DI and that <c>RegisterAsync</c>
/// reaches the validator before any expensive DB / tenant / email
/// work -- a single short-circuit covers all the constructor +
/// caller lines without standing up tenant context.</para>
/// </summary>
public abstract class ExternalSignupAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IExternalSignupAppService _appService;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupAppServiceTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _applicantAttorneyRepository = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _defenseAttorneyRepository = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task RegisterAsync_PasswordMismatch_ThrowsLocalizedConfirmPasswordMismatch()
    {
        // Building a valid-shape DTO that breaks the FIRST validator gate
        // (password mismatch). Anything reaching downstream (tenant lookup,
        // email check, role assignment) would have side effects; the
        // validator must throw before that. The throw + the caller's pass
        // of `_localizer` is what we are covering here.
        var dto = new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = "bug012-sub1-di@example.test",
            Password = "Test1234!",
            ConfirmPassword = "Mismatch99!",
            FirstName = "Sub1",
            LastName = "DI",
            TenantId = Guid.NewGuid(),
        };

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => _appService.RegisterAsync(dto));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        // The localized message reached the consumer via the DI'd
        // IStringLocalizer -- the proof that line 489's
        // ValidateRegistrationInput(input, _localizer) call wired
        // through correctly.
        ex.Message.ShouldBe("Password and confirm password do not match.");
    }

    // =====================================================================
    // F-H01 (2026-06-25): register-after-booking for attorneys. When a booking
    // named an attorney's email before they had an account, it created an
    // unclaimed master (IdentityUserId NULL) keyed by (TenantId, Email).
    // Registration used to dedup only by the new user's id, miss that row, and
    // INSERT a duplicate -> unique-index violation -> HTTP 500. The fix ADOPTS
    // the unclaimed master (claims the login + backfills the typed firm).
    // =====================================================================

    [Fact]
    public async Task RegisterAsync_ApplicantAttorney_AdoptsUnclaimedEmailMaster_NoDuplicate()
    {
        const string email = "fh01.aa.adopt@test.example";
        const string firmName = "TEST-Adopt-AA-Firm";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _applicantAttorneyRepository.InsertAsync(
                new ApplicantAttorney(Guid.NewGuid(), stateId: null, identityUserId: null, email: email),
                autoSave: true);
        }

        var dto = new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.ApplicantAttorney,
            Email = email,
            Password = "Test1234!",
            ConfirmPassword = "Test1234!",
            FirmName = firmName,
            TenantId = TenantsTestData.TenantARef,
        };

        // Pre-fix this threw (duplicate-key 500); post-fix it adopts cleanly.
        await _appService.RegisterAsync(dto);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var masters = await _applicantAttorneyRepository.GetListAsync(
                a => a.Email != null && a.Email == email);
            masters.Count.ShouldBe(1);                   // adopted, not duplicated
            masters[0].IdentityUserId.ShouldNotBeNull(); // claimed by the new login
            masters[0].FirmName.ShouldBe(firmName);      // firm persisted (Adrian: store the firm)
        }
    }

    [Fact]
    public async Task RegisterAsync_DefenseAttorney_AdoptsUnclaimedEmailMaster_NoDuplicate()
    {
        const string email = "fh01.da.adopt@test.example";
        const string firmName = "TEST-Adopt-DA-Firm";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await _defenseAttorneyRepository.InsertAsync(
                new DefenseAttorney(Guid.NewGuid(), stateId: null, identityUserId: null, email: email),
                autoSave: true);
        }

        var dto = new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.DefenseAttorney,
            Email = email,
            Password = "Test1234!",
            ConfirmPassword = "Test1234!",
            FirmName = firmName,
            TenantId = TenantsTestData.TenantARef,
        };

        await _appService.RegisterAsync(dto);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var masters = await _defenseAttorneyRepository.GetListAsync(
                a => a.Email != null && a.Email == email);
            masters.Count.ShouldBe(1);
            masters[0].IdentityUserId.ShouldNotBeNull();
            masters[0].FirmName.ShouldBe(firmName);
        }
    }

    [Fact]
    public async Task RegisterAsync_DefenseAttorney_NoPriorBooking_CreatesSingleMaster()
    {
        // Registration-first (no booking placeholder) must still create exactly
        // one master with the typed firm -- the adopt change must not regress it.
        const string email = "fh01.da.first@test.example";
        const string firmName = "TEST-First-DA-Firm";

        var dto = new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.DefenseAttorney,
            Email = email,
            Password = "Test1234!",
            ConfirmPassword = "Test1234!",
            FirmName = firmName,
            TenantId = TenantsTestData.TenantARef,
        };

        await _appService.RegisterAsync(dto);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var masters = await _defenseAttorneyRepository.GetListAsync(
                a => a.Email != null && a.Email == email);
            masters.Count.ShouldBe(1);
            masters[0].IdentityUserId.ShouldNotBeNull();
            masters[0].FirmName.ShouldBe(firmName);
        }
    }
}
