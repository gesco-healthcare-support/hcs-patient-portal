using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// The two tenant lookups (<c>api/app/patients/tenant-lookup</c>, <c>api/app/doctors/tenant-lookup</c>)
/// return only the caller's own office when the caller is inside an office (2026-10-22). The office
/// registry is host-side and the Saas repository reads it from any scope, so before this an office's
/// staff got every office's id and name. The host still gets every office.
///
/// <para>A SECOND office is registered here, so "only its own" is measured against a registry that
/// holds more than one office. The callers are the production roles with their seeded grants: office
/// Intake Staff and Staff Supervisor (both hold Patients and Doctors), and host IT Admin.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class TenantLookupOfficeScopeAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    // The cleanup prefix maps the office to the harness's shared cleanup database; nothing is ever
    // written there, the office only has to exist in the host registry.
    private const string OtherOfficeName = "f2-cleanup-tenant-lookup";

    private static readonly SemaphoreSlim SeedLock = new(1, 1);
    private static Guid? _otherOffice;

    private static readonly IReadOnlyDictionary<string, Func<IServiceProvider, LookupRequestDto, Task<PagedResultDto<LookupDto<Guid>>>>> Lookups =
        new Dictionary<string, Func<IServiceProvider, LookupRequestDto, Task<PagedResultDto<LookupDto<Guid>>>>>(StringComparer.Ordinal)
        {
            ["patients"] = (sp, input) => sp.GetRequiredService<IPatientsAppService>().GetTenantLookupAsync(input),
            ["doctors"] = (sp, input) => sp.GetRequiredService<IDoctorsAppService>().GetTenantLookupAsync(input),
        };

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public TenantLookupOfficeScopeAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Theory]
    [InlineData(InternalUserRoleDataSeedContributor.IntakeStaffRoleName, "patients")]
    [InlineData(InternalUserRoleDataSeedContributor.IntakeStaffRoleName, "doctors")]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, "patients")]
    [InlineData(InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, "doctors")]
    public async Task An_office_caller_sees_only_its_own_office(string role, string lookup)
    {
        var fixture = await GetFixtureAsync();
        var other = await EnsureOtherOfficeAsync();
        var own = fixture.Office.OfficeId;

        var all = await InvokeAsync(own, role, lookup, new LookupRequestDto { MaxResultCount = 100 });

        all.TotalCount.ShouldBe(1);
        all.Items.Select(i => i.Id).ShouldBe(new[] { own });

        var byName = await InvokeAsync(own, role, lookup, new LookupRequestDto { MaxResultCount = 100, Filter = OtherOfficeName });
        byName.TotalCount.ShouldBe(0, $"the {lookup} lookup must not find office {other} by name from inside another office");
        byName.Items.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("patients")]
    [InlineData("doctors")]
    public async Task The_host_still_sees_every_office(string lookup)
    {
        var fixture = await GetFixtureAsync();
        var other = await EnsureOtherOfficeAsync();

        var all = await InvokeAsync(null, InternalUserRoleDataSeedContributor.ItAdminRoleName, lookup, new LookupRequestDto { MaxResultCount = 100 });

        all.Items.Select(i => i.Id).ShouldContain(fixture.Office.OfficeId);
        all.Items.Select(i => i.Id).ShouldContain(other);
        all.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    // ------------------------------------------------------------------------

    private async Task<PagedResultDto<LookupDto<Guid>>> InvokeAsync(Guid? scope, string role, string lookup, LookupRequestDto input)
    {
        PagedResultDto<LookupDto<Guid>>? result = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(scope))
            using (_principalAccessor.Change(Principal(role, scope)))
            {
                result = await Lookups[lookup](ServiceProvider, input);
            }
        }, requiresNew: true);
        return result!;
    }

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

    /// <summary>Once per process: the host role pass (IT Admin) and a second office in the registry.</summary>
    private async Task<Guid> EnsureOtherOfficeAsync()
    {
        if (_otherOffice.HasValue)
        {
            return _otherOffice.Value;
        }

        await SeedLock.WaitAsync();
        try
        {
            if (_otherOffice.HasValue)
            {
                return _otherOffice.Value;
            }

            await WithUnitOfWorkAsync(
                () => GetRequiredService<InternalUserRoleDataSeedContributor>().SeedAsync(new DataSeedContext(null)),
                requiresNew: true);

            Guid id = Guid.Empty;
            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(null))
                {
                    var office = await GetRequiredService<ITenantManager>().CreateAsync(OtherOfficeName);
                    await GetRequiredService<IRepository<Tenant, Guid>>().InsertAsync(office, autoSave: true);
                    id = office.Id;
                }
            }, requiresNew: true);

            _otherOffice = id;
            return id;
        }
        finally
        {
            SeedLock.Release();
        }
    }
}
