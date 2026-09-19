using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: the two Development-only helpers on <c>ExternalSignupAppService</c> --
/// <c>MarkEmailConfirmedAsync</c> and <c>DeleteTestUsersAsync</c>. Both exist so a demo can be
/// re-run without an inbox round trip, and both have to work ACROSS OFFICES, because under
/// database-per-office the caller has no idea which office holds the address.
///
/// <para>The rule that actually matters in <c>DeleteTestUsersAsync</c> is that the dependent
/// masters are HARD-deleted. A soft delete leaves the row physically present, and the filtered
/// unique index on (TenantId, Email) counts it, so a soft delete would silently block re-using
/// the address -- the exact thing the helper exists to allow.</para>
///
/// <para>NOT pinned here: the <c>EnsureDevelopmentOnly</c> throw. The test module registers a
/// Development <c>IHostEnvironment</c> singleton, so no call through this surface can reach the
/// non-Development branch; there is no failing input for it in this rig.</para>
///
/// <para>Also not pinned: that re-registering the deleted address succeeds. That would make the
/// test depend on SQLite honouring a filtered unique index, which is a database-provider
/// behaviour rather than this service's rule. The assertion is on the row's physical absence.</para>
/// </summary>
public abstract class ExternalSignupDevHelperTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _appService;
    private readonly IdentityUserManager _userManager;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupDevHelperTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The cross-office scan is the whole point: the account is in office B and the call is made
    /// from host scope, where a single ambient-scope query would find nothing.
    /// </summary>
    [Fact]
    public async Task MarkEmailConfirmedAsync_FindsTheUserInWhicheverOfficeHoldsThem()
    {
        var token = NewToken();
        var email = $"mec-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantBRef,
        });

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var before = await _userManager.FindByEmailAsync(email);
            before.ShouldNotBeNull();
            // The anonymous register path does not confirm the address, so there is a real
            // false-to-true transition for the helper to make.
            before!.EmailConfirmed.ShouldBeFalse();
        }

        // Host scope on purpose. No CurrentTenant.Change around this call.
        await _appService.MarkEmailConfirmedAsync(email);

        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var after = await _userManager.FindByEmailAsync(email);
            after.ShouldNotBeNull();
            after!.EmailConfirmed.ShouldBeTrue();
        }
    }

    /// <summary>
    /// An address no office holds is reported, not silently accepted. A quiet success here would
    /// send the operator hunting for a confirmation that never happened.
    /// </summary>
    [Fact]
    public async Task MarkEmailConfirmedAsync_UnknownEmail_ReportsNotFound()
    {
        var token = NewToken();
        var email = $"mnf-{token}@test.local";

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => _appService.MarkEmailConfirmedAsync(email));

        ex.Message.ShouldContain("not found");
    }

    /// <summary>
    /// HARD delete, not soft: with the soft-delete filter switched off the dependent master must
    /// be physically gone. A soft-deleted row would still sit under the filtered unique index and
    /// block the re-registration this helper exists to enable.
    /// </summary>
    [Fact]
    public async Task DeleteTestUsersAsync_HardDeletesDependentMastersSoTheEmailIsReusable()
    {
        var token = NewToken();
        var email = $"del-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        // The master the delete must remove IS present first.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var seeded = await _patientRepository.GetListAsync(p => p.Email == email);
            seeded.Count.ShouldBe(1);
        }

        var result = await _appService.DeleteTestUsersAsync(new List<string> { email });
        result.Deleted.ShouldContain(email);
        result.NotFound.ShouldNotContain(email);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            (await _userManager.FindByEmailAsync(email)).ShouldBeNull();

            // The filter is disabled deliberately: with it ON a soft-deleted row is invisible and
            // this assertion would pass against exactly the bug it is meant to catch.
            using (_dataFilter.Disable<ISoftDelete>())
            {
                var survivors = await _patientRepository.GetListAsync(p => p.Email == email);
                survivors.ShouldBeEmpty();
            }
        }
    }

    /// <summary>
    /// A blank entry in the list is skipped, not reported as a missing account. The caller pastes
    /// a list from a scratch file; a trailing blank line is noise, and reporting it as NotFound
    /// makes the operator hunt for an address that was never asked for.
    /// </summary>
    [Fact]
    public async Task DeleteTestUsersAsync_BlankEntriesAreSkippedNotReportedNotFound()
    {
        var token = NewToken();
        var email = $"blk-{token}@test.local";

        await _appService.RegisterAsync(new ExternalUserSignUpDto
        {
            UserType = ExternalUserType.Patient,
            Email = email,
            Password = Password,
            ConfirmPassword = Password,
            TenantId = TenantsTestData.TenantARef,
        });

        // The blank entry is PRESENT in the input; the real address rides alongside it so the
        // call is not a no-op overall.
        var result = await _appService.DeleteTestUsersAsync(
            new List<string> { "   ", email, string.Empty });

        result.Deleted.ShouldContain(email);
        result.NotFound.ShouldBeEmpty();
    }
}
