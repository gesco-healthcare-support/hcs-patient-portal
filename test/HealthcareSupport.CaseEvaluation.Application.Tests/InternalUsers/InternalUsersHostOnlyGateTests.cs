using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Controllers.InternalUsers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// Internal users are HOST operators, and the four internal-users methods (create, list, password
/// reset and the office list) are managed from the host alone. These pin who the authorization
/// check ADMITS and who it REFUSES, for every method.
///
/// <para><b>Why not the usual harness.</b> Both test harnesses call
/// <c>AddAlwaysAllowAuthorization()</c>, so a call through the app service there is admitted whatever
/// the attributes say. This class instead boots ABP's own authorization module on its own and runs
/// <see cref="IMethodInvocationAuthorizationService.CheckAsync"/> for the real method: the check
/// ABP's authorization interceptor makes before the method body runs. The attribute resolution
/// (a method-level <c>[AllowAnonymous]</c> beats the class attribute), the policy, the permission
/// definition and its host/office side, and the role check are all ABP's real code.</para>
///
/// <para><b>Only the stored grants are substituted,</b> and they come from
/// <c>Authorization/role-permission-surface.approved.txt</c>: what seeding writes for each role on the
/// host and in an office. <c>RolePermissionSurfaceSnapshotTests</c> keeps that file equal to a real
/// seeding run, so a grant change reaches these tests without anyone copying it here.</para>
///
/// <para><b>The callers.</b> At the host: IT Admin, Staff Supervisor and the host <c>admin</c>, who
/// run the internal-users screen and the shell's office switcher at host scope. Inside an office:
/// the office's own <c>admin</c>, and a host operator switched in, who is signed in as their own
/// per-office shadow user holding the office <c>admin</c> role (IT Admin) or the office Staff
/// Supervisor role (anyone else holding <c>Saas.Tenants.Impersonation</c>). Every caller inside an
/// office is refused; the in-office switcher asks <c>IntakeAssignments.GetSwitchableOffices</c>
/// instead. The method body also refuses an office caller on its own
/// (<c>InternalUsersHostCallerGuardTests</c>), for the day a permission's side is widened.</para>
/// </summary>
public sealed partial class InternalUsersHostOnlyGateTests : IAsyncLifetime
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    private static readonly Guid HostOperator = Guid.Parse("00000000-0000-0000-0000-00000000b001");

    private static readonly string[] Methods =
    {
        nameof(InternalUsersAppService.CreateAsync),
        nameof(InternalUsersAppService.GetInternalUsersAsync),
        nameof(InternalUsersAppService.SendPasswordResetEmailAsync),
        nameof(InternalUsersAppService.GetTenantOptionsAsync),
    };

    private IAbpApplicationWithInternalServiceProvider _app = null!;

    public async Task InitializeAsync()
    {
        _app = await AbpApplicationFactory.CreateAsync<HostOnlyGateModule>();
        await _app.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.ShutdownAsync();
        _app.Dispose();
    }

    public static TheoryData<string> EveryMethod()
    {
        var data = new TheoryData<string>();
        foreach (var method in Methods)
        {
            data.Add(method);
        }
        return data;
    }

    public static TheoryData<string, string> HostOperatorRoles() =>
        EveryMethodWith("IT Admin", "Staff Supervisor", "admin");

    public static TheoryData<string, string> ShadowOfficeRoles() =>
        EveryMethodWith("admin", "Staff Supervisor", "Intake Staff");

    public static TheoryData<string, string> OfficeRoles() =>
        EveryMethodWith(
            "admin", "Staff Supervisor", "Intake Staff",
            "Patient", "Applicant Attorney", "Defense Attorney", "Claim Examiner");

    [Theory]
    [MemberData(nameof(EveryMethod))]
    public async Task An_anonymous_caller_is_refused_on_the_host(string method)
    {
        (await AdmitsAsync(method, Anonymous(), tenantId: null)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(EveryMethod))]
    public async Task An_anonymous_caller_is_refused_on_an_office_host(string method)
    {
        (await AdmitsAsync(method, Anonymous(), tenantId: Office)).ShouldBeFalse();
    }

    /// <summary>
    /// The host operators who run the screen. Without this positive control, a harness that refused
    /// everyone would make every refusal below pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostOperatorRoles))]
    public async Task A_host_operator_is_admitted(string method, string hostRole)
    {
        (await AdmitsAsync(method, SignedIn(hostRole, tenantId: null), tenantId: null)).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(EveryMethod))]
    public async Task A_host_intake_operator_is_refused(string method)
    {
        // The switcher does not offer them an office list at host scope (canSwitch is false).
        (await AdmitsAsync(method, SignedIn("Intake Staff", tenantId: null), tenantId: null)).ShouldBeFalse();
    }

    /// <summary>
    /// Every role an office session can hold, the office's own <c>admin</c> and Staff Supervisor
    /// included. Until 2026-10-20 those two were admitted.
    /// </summary>
    [Theory]
    [MemberData(nameof(OfficeRoles))]
    public async Task Every_office_role_is_refused(string method, string officeRole)
    {
        (await AdmitsAsync(method, SignedIn(officeRole, tenantId: Office), tenantId: Office)).ShouldBeFalse();
    }

    /// <summary>
    /// A host operator switched into an office, as the shadow user the grant signs them in as. The
    /// session carries the SHADOW's office role, not the operator's host role, so it is refused too.
    /// </summary>
    [Theory]
    [MemberData(nameof(ShadowOfficeRoles))]
    public async Task A_host_operator_switched_into_an_office_is_refused(string method, string shadowOfficeRole)
    {
        var principal = SignedIn(shadowOfficeRole, tenantId: Office, impersonatedBy: HostOperator);

        (await AdmitsAsync(method, principal, tenantId: Office)).ShouldBeFalse();
    }

    /// <summary>
    /// The HTTP actions cannot be booted here. The app service check above is the one that decides
    /// on every call; this keeps MVC from also treating a route as anonymous.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryMethod))]
    public void The_controller_action_is_not_anonymous(string method)
    {
        var action = typeof(InternalUsersController).GetMethod(method)!;

        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).ShouldBeEmpty();
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ShouldNotBeEmpty();
    }

    private static TheoryData<string, string> EveryMethodWith(params string[] roles)
    {
        var data = new TheoryData<string, string>();
        foreach (var method in Methods)
        {
            foreach (var role in roles)
            {
                data.Add(method, role);
            }
        }
        return data;
    }

    private async Task<bool> AdmitsAsync(string methodName, ClaimsPrincipal principal, Guid? tenantId)
    {
        var method = typeof(InternalUsersAppService).GetMethod(methodName)!;
        using var scope = _app.ServiceProvider.CreateScope();
        var principalAccessor = scope.ServiceProvider.GetRequiredService<ICurrentPrincipalAccessor>();
        var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var gate = scope.ServiceProvider.GetRequiredService<IMethodInvocationAuthorizationService>();

        using (principalAccessor.Change(principal))
        using (currentTenant.Change(tenantId))
        {
            try
            {
                await gate.CheckAsync(new MethodInvocationAuthorizationContext(method));
                return true;
            }
            catch (AbpAuthorizationException)
            {
                return false;
            }
        }
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal SignedIn(string role, Guid? tenantId, Guid? impersonatedBy = null)
    {
        var claims = new List<Claim>
        {
            new(AbpClaimTypes.UserId, Guid.NewGuid().ToString()),
            new(AbpClaimTypes.Role, role),
        };
        if (tenantId.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.TenantId, tenantId.Value.ToString()));
        }
        if (impersonatedBy.HasValue)
        {
            claims.Add(new Claim(AbpClaimTypes.ImpersonatorUserId, impersonatedBy.Value.ToString()));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    [DependsOn(typeof(AbpAuthorizationModule))]
    public sealed class HostOnlyGateModule : AbpModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddTransient<CaseEvaluationPermissionDefinitionProvider>();
            context.Services.Configure<AbpPermissionOptions>(options =>
            {
                if (!options.DefinitionProviders.Contains<CaseEvaluationPermissionDefinitionProvider>())
                {
                    options.DefinitionProviders.Add<CaseEvaluationPermissionDefinitionProvider>();
                }
            });
            context.Services.Replace(ServiceDescriptor.Transient<IPermissionStore, SeededRoleGrantStore>());
        }
    }

    /// <summary>
    /// Answers role grants from role-permission-surface.approved.txt: the host column when there is
    /// no current office, the office column inside one. Grants to anything other than a role are
    /// undefined, as they are for these callers after seeding.
    /// </summary>
    public sealed partial class SeededRoleGrantStore : IPermissionStore
    {
        private static readonly Lazy<Dictionary<string, (HashSet<string> Host, HashSet<string> Office)>> Grants =
            new(Load);

        private readonly ICurrentTenant _currentTenant;

        public SeededRoleGrantStore(ICurrentTenant currentTenant)
        {
            _currentTenant = currentTenant;
        }

        public Task<bool> IsGrantedAsync(string name, string? providerName, string? providerKey)
        {
            return Task.FromResult(IsGranted(name, providerName, providerKey));
        }

        public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names, string? providerName, string? providerKey)
        {
            var result = new MultiplePermissionGrantResult();
            foreach (var name in names)
            {
                result.Result[name] = IsGranted(name, providerName, providerKey)
                    ? PermissionGrantResult.Granted
                    : PermissionGrantResult.Undefined;
            }
            return Task.FromResult(result);
        }

        private bool IsGranted(string name, string? providerName, string? providerKey)
        {
            if (providerName != RolePermissionValueProvider.ProviderName || providerKey == null)
            {
                return false;
            }
            if (!Grants.Value.TryGetValue(name, out var roles))
            {
                return false;
            }
            return (_currentTenant.Id == null ? roles.Host : roles.Office).Contains(providerKey);
        }

        [GeneratedRegex("^(?<name>\\S+) side=\\S+ host=(?<host>.*) office=(?<office>.*)$")]
        private static partial Regex SurfaceLine();

        private static Dictionary<string, (HashSet<string> Host, HashSet<string> Office)> Load()
        {
            var path = Path.Combine(ThisDirectory(), "..", "Authorization", "role-permission-surface.approved.txt");
            var line = SurfaceLine();
            var grants = new Dictionary<string, (HashSet<string>, HashSet<string>)>(StringComparer.Ordinal);
            foreach (var text in File.ReadAllLines(path))
            {
                var match = line.Match(text);
                if (match.Success)
                {
                    grants[match.Groups["name"].Value] = (Roles(match.Groups["host"].Value), Roles(match.Groups["office"].Value));
                }
            }
            // An empty table would refuse everyone, and every "refused" test would pass for that reason.
            grants.ShouldContainKey(CaseEvaluationPermissions.InternalUsers.Default);
            return grants;
        }

        private static HashSet<string> Roles(string list)
        {
            return list == "-"
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(list.Split(", "), StringComparer.Ordinal);
        }

        private static string ThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
    }
}
