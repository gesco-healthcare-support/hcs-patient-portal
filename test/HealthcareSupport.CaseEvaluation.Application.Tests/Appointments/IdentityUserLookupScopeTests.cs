using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Every <c>GetIdentityUserLookupAsync</c> in the application layer, called as each kind of
/// caller. These lookups once returned every account in the office to anyone holding the owning
/// service's <c>.Default</c>, which the external roles hold -- a searchable directory of patient
/// and attorney logins.
/// </summary>
/// <remarks>
/// No filter is passed, so the assertion does not depend on which column a lookup filters on
/// (Patients filters on Name, the rest on Email): an external caller must get back their own
/// account and nothing else, however many accounts the office holds. The test harness allows
/// every permission, so this pins the narrowing itself, not the permission in front of it.
/// </remarks>
public abstract class IdentityUserLookupScopeTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected IdentityUserLookupScopeTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    public static IEnumerable<object[]> Lookups() =>
        new[]
        {
            "ApplicantAttorneys", "AppointmentAccessors", "AppointmentApplicantAttorneys",
            "AppointmentDefenseAttorneys", "ClaimExaminers", "DefenseAttorneys", "Patients", "Appointments",
        }.Select(name => new object[] { name });

    public static IEnumerable<object[]> ExternalCallers()
    {
        var roles = new[]
        {
            IdentityUsersTestData.PatientRoleName,
            IdentityUsersTestData.ClaimExaminerRoleName,
            IdentityUsersTestData.ApplicantAttorneyRoleName,
            IdentityUsersTestData.DefenseAttorneyRoleName,
            "", // a self-registered account with no role at all
        };
        return from lookup in Lookups() from role in roles select new[] { lookup[0], role };
    }

    private Task<PagedResultDto<LookupDto<Guid>>> CallLookup(string lookup)
    {
        var input = new LookupRequestDto { MaxResultCount = 1000 };
        return lookup switch
        {
            "ApplicantAttorneys" => GetRequiredService<IApplicantAttorneysAppService>().GetIdentityUserLookupAsync(input),
            "AppointmentAccessors" => GetRequiredService<IAppointmentAccessorsAppService>().GetIdentityUserLookupAsync(input),
            "AppointmentApplicantAttorneys" => GetRequiredService<IAppointmentApplicantAttorneysAppService>().GetIdentityUserLookupAsync(input),
            "AppointmentDefenseAttorneys" => GetRequiredService<IAppointmentDefenseAttorneysAppService>().GetIdentityUserLookupAsync(input),
            "ClaimExaminers" => GetRequiredService<IClaimExaminersAppService>().GetIdentityUserLookupAsync(input),
            "DefenseAttorneys" => GetRequiredService<IDefenseAttorneysAppService>().GetIdentityUserLookupAsync(input),
            "Patients" => GetRequiredService<IPatientsAppService>().GetIdentityUserLookupAsync(input),
            "Appointments" => GetRequiredService<IAppointmentsAppService>().GetIdentityUserLookupAsync(input),
            _ => throw new ArgumentOutOfRangeException(nameof(lookup), lookup, "Unknown lookup."),
        };
    }

    private async Task<PagedResultDto<LookupDto<Guid>>> LookupInOfficeA(string lookup, Guid callerId, string role)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(async () =>
            {
                var callerRoles = role.Length == 0 ? Array.Empty<string>() : new[] { role };
                using (WithCurrentUser.Run(_currentPrincipalAccessor, callerId, callerRoles))
                {
                    return await CallLookup(lookup);
                }
            });
        }
    }

    private async Task<Guid> SeedOfficeAUserAsync()
    {
        var userId = Guid.NewGuid();
        var token = userId.ToString("N")[..8];
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await WithUnitOfWorkAsync(async () =>
            {
                var user = new IdentityUser(userId, $"TEST-lookup-{token}", $"TEST-lookup-{token}@test.local", TenantsTestData.TenantARef);
                (await GetRequiredService<IdentityUserManager>().CreateAsync(user, IdentityUsersTestData.SeedPassword))
                    .Succeeded.ShouldBeTrue();
            });
        }
        return userId;
    }

    [Theory]
    [MemberData(nameof(ExternalCallers))]
    public async Task An_external_caller_sees_only_their_own_account(string lookup, string role)
    {
        var callerId = await SeedOfficeAUserAsync();
        var otherId = await SeedOfficeAUserAsync();

        var result = await LookupInOfficeA(lookup, callerId, role);

        result.Items.Select(x => x.Id).ShouldBe(
            new[] { callerId },
            $"{lookup} returned accounts other than the caller's own to a '{role}' caller. Another "
            + $"seeded account ({otherId}) is in the same office, so anything beyond the caller is "
            + "the office directory leaking.");
        result.TotalCount.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Lookups))]
    public async Task Internal_staff_still_see_the_office(string lookup)
    {
        var callerId = await SeedOfficeAUserAsync();
        var otherId = await SeedOfficeAUserAsync();

        var result = await LookupInOfficeA(lookup, callerId, "Intake Staff");

        result.Items.ShouldContain(x => x.Id == otherId,
            $"{lookup} hid another account from internal staff; the booking screens rely on it.");
    }
}
