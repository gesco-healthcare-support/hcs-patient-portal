using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Office-targeted branding calls are host-only (2026-10-21). Branding is a both-sides permission,
/// so an office's <c>admin</c> role holds it, and the branding rows live host-side keyed by office id.
/// A call naming another office's id, and the every-office lists, are therefore refused to any caller
/// inside an office, while the office's own branding page (no id, or its own id) keeps working and
/// the host operators who run the branding page are admitted.
///
/// <para>THE OFFICE ADMIN HOLDS THE GRANTS A DEPLOYED OFFICE HAS (Branding and Branding.Edit, written
/// straight into the grant table the way ABP gives them to the office admin role). So it gets past the
/// permission interceptor, and a refusal here comes from the method body: the guard under test. The
/// interceptor's refusal carries an ABP error code; the body's carries none.</para>
///
/// <para>Data-light: the other office is an id no office has, so nothing is ever written for it; an
/// admitted host call edits the fixture office's own row with an empty change.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class BrandingOfficeTargetAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string RoleProviderName = "R";
    private const string InterceptorCodePrefix = "Volo.Authorization:";
    private const string OfficeAdminRole = "admin";

    private static readonly Guid OtherOffice = Guid.Parse("7e57d000-0000-4000-9000-0000000000b0");

    // A PNG signature, so the upload's own checks pass and only the target decides the outcome.
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, Task>> CallsNamingAnotherOffice =
        new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.Ordinal)
        {
            ["SetDisplayName"] = sp => Branding(sp).SetDisplayNameAsync(OtherOffice, "TEST Hijacked"),
            ["UploadLogo"] = sp => Branding(sp).UploadLogoAsync(OtherOffice, "logo.png", "image/png", PngBytes.Length, new MemoryStream(PngBytes)),
            ["RemoveLogo"] = sp => Branding(sp).RemoveLogoAsync(OtherOffice),
            ["DownloadLogoForOffice"] = sp => Branding(sp).DownloadLogoForOfficeAsync(OtherOffice),
            ["GetOfficeBrandings"] = sp => Branding(sp).GetOfficeBrandingsAsync(),
            ["GetPagedOfficeBrandings"] = sp => Branding(sp).GetPagedOfficeBrandingsAsync(new GetOfficeBrandingInput { MaxResultCount = 10 }),
            ["GetLetterheadForOffice"] = sp => Letterhead(sp).GetForOfficeAsync(OtherOffice),
            ["UpdateLetterheadForOffice"] = sp => Letterhead(sp).UpdateForOfficeAsync(OtherOffice, new UpdateOfficeLetterheadInput()),
        };

    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static bool _seeded;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public BrandingOfficeTargetAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    public static TheoryData<string> CallNames()
    {
        var data = new TheoryData<string>();
        foreach (var call in CallsNamingAnotherOffice.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            data.Add(call);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CallNames))]
    public async Task An_office_admin_cannot_reach_another_offices_branding(string call)
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);

        var outcome = await InvokeAsync(fixture.Office.OfficeId, OfficeAdminRole, CallsNamingAnotherOffice[call]);

        (outcome is AbpAuthorizationException).ShouldBeTrue($"{call} must be refused. Got: {Describe(outcome)}.");
        IsInterceptorRefusal(outcome).ShouldBeFalse(
            $"The office admin holds Branding, so {call} must be refused by the method body, not the interceptor. " +
            $"Got: {Describe(outcome)}.");
    }

    /// <summary>Positive control: the office's own branding page still works for its admin.</summary>
    [Fact]
    public async Task The_office_admin_keeps_its_own_branding_page()
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);
        var own = fixture.Office.OfficeId;

        var calls = new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.Ordinal)
        {
            ["SetDisplayName(current)"] = sp => Branding(sp).SetDisplayNameAsync(null, null),
            ["SetDisplayName(own id)"] = sp => Branding(sp).SetDisplayNameAsync(own, null),
            ["DownloadLogoForOffice(own id)"] = sp => Branding(sp).DownloadLogoForOfficeAsync(own),
            ["Letterhead.Get"] = sp => Letterhead(sp).GetAsync(),
            ["Letterhead.Update"] = sp => Letterhead(sp).UpdateAsync(new UpdateOfficeLetterheadInput()),
        };
        foreach (var (name, call) in calls)
        {
            var outcome = await InvokeAsync(own, OfficeAdminRole, call);
            outcome.ShouldBeNull($"{name} must work for the office's own admin. Got: {Describe(outcome)}.");
        }
    }

    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Patient")]
    [InlineData("Applicant Attorney")]
    public async Task Office_staff_and_parties_without_branding_are_refused_the_letterhead(string role)
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);

        foreach (var call in new Func<IServiceProvider, Task>[]
                 {
                     sp => Letterhead(sp).GetAsync(),
                     sp => Letterhead(sp).GetForOfficeAsync(fixture.Office.OfficeId),
                     sp => Letterhead(sp).UpdateForOfficeAsync(fixture.Office.OfficeId, new UpdateOfficeLetterheadInput()),
                 })
        {
            var outcome = await InvokeAsync(fixture.Office.OfficeId, role, call);
            IsInterceptorRefusal(outcome).ShouldBeTrue($"{role} holds no Branding and must be refused. Got: {Describe(outcome)}.");
        }
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.ItAdminRoleName)]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)]
    public async Task The_host_operators_read_and_edit_an_offices_letterhead_and_branding(string hostRole)
    {
        var fixture = await GetFixtureAsync();
        await SeedAsync(fixture.Office.OfficeId);
        var office = fixture.Office.OfficeId;

        var calls = new Dictionary<string, Func<IServiceProvider, Task>>(StringComparer.Ordinal)
        {
            ["GetLetterheadForOffice"] = sp => Letterhead(sp).GetForOfficeAsync(office),
            ["UpdateLetterheadForOffice"] = sp => Letterhead(sp).UpdateForOfficeAsync(office, new UpdateOfficeLetterheadInput()),
            ["GetPagedOfficeBrandings"] = sp => Branding(sp).GetPagedOfficeBrandingsAsync(new GetOfficeBrandingInput { MaxResultCount = 10 }),
            ["SetDisplayName"] = sp => Branding(sp).SetDisplayNameAsync(office, null),
        };
        foreach (var (name, call) in calls)
        {
            var outcome = await InvokeAsync(null, hostRole, call);
            outcome.ShouldBeNull($"The host {hostRole} runs the branding page and must be admitted to {name}. Got: {Describe(outcome)}.");
        }
    }

    // ------------------------------------------------------------------------

    private static IBrandingAppService Branding(IServiceProvider sp) => sp.GetRequiredService<IBrandingAppService>();

    private static IOfficeLetterheadAppService Letterhead(IServiceProvider sp) => sp.GetRequiredService<IOfficeLetterheadAppService>();

    /// <summary>An office caller carries its office id as a tenant claim, as every office session's token does.</summary>
    private static ClaimsPrincipal Principal(string role, Guid? officeId)
    {
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, userId.ToString()),
            new(AbpClaimTypes.UserName, $"test-user-{userId:N}"),
            new(AbpClaimTypes.Role, role),
        };
        if (officeId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, officeId.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    private static bool IsInterceptorRefusal(Exception? outcome) =>
        outcome is AbpAuthorizationException refusal
        && refusal.Code != null
        && refusal.Code.StartsWith(InterceptorCodePrefix, StringComparison.Ordinal);

    private static string Describe(Exception? outcome) =>
        outcome == null
            ? "no exception"
            : outcome.GetType().Name + " (code " + (outcome as AbpAuthorizationException)?.Code + "): " + outcome.Message;

    private async Task<Exception?> InvokeAsync(Guid? scope, string role, Func<IServiceProvider, Task> call)
    {
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(scope))
            using (_principalAccessor.Change(Principal(role, scope)))
            {
                try
                {
                    await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        return caught;
    }

    /// <summary>
    /// Once per process: the production host role pass, and the Branding rows a deployed office's admin
    /// role holds.
    /// </summary>
    private async Task SeedAsync(Guid officeId)
    {
        if (_seeded)
        {
            return;
        }

        await SeedLock.WaitAsync();
        try
        {
            if (_seeded)
            {
                return;
            }

            await WithUnitOfWorkAsync(
                () => GetRequiredService<InternalUserRoleDataSeedContributor>().SeedAsync(new DataSeedContext(null)),
                requiresNew: true);

            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(officeId))
                {
                    var grants = GetRequiredService<IPermissionGrantRepository>();
                    foreach (var permission in new[] { CaseEvaluationPermissions.Branding.Default, CaseEvaluationPermissions.Branding.Edit })
                    {
                        if (await grants.FindAsync(permission, RoleProviderName, OfficeAdminRole) == null)
                        {
                            await grants.InsertAsync(
                                new PermissionGrant(Guid.NewGuid(), permission, RoleProviderName, OfficeAdminRole, officeId),
                                autoSave: true);
                        }
                    }
                }
            }, requiresNew: true);

            _seeded = true;
        }
        finally
        {
            SeedLock.Release();
        }
    }
}
