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
/// <c>GET /api/app/internal-users/tenants</c> returns every office's id and name. It used to be
/// readable without signing in, from every host. These pin who is ADMITTED and who is REFUSED.
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
/// <para><b>The callers, established from the code:</b></para>
/// <list type="bullet">
///   <item>the internal-users form (<c>internal-users-form.component.ts</c>), used by holders of
///   <c>InternalUsers.Create</c>;</item>
///   <item>the internal shell's office switcher (<c>internal-shell-layout.component.ts</c>,
///   <c>loadSwitchTargets</c>), from host scope for any operator who is not Intake;</item>
///   <item>the same switcher from INSIDE an office while switched in. That session is not the host
///   operator: <c>HostIntakeImpersonationExtensionGrant</c> signs them in as their own per-office
///   shadow user, holding the office <c>admin</c> role (IT Admin) or the office Staff Supervisor role
///   (anyone else holding <c>Saas.Tenants.Impersonation</c>). A host-only permission would refuse
///   exactly this caller.</item>
/// </list>
/// <para>An in-office Intake operator is NOT a caller: the switcher sends them to
/// <c>IntakeAssignments.GetSwitchableOffices</c>. They are refused here, which is recorded below.</para>
///
/// <para>A refusal cannot be seen in the UI: the switcher's error handler shows an empty office list
/// with no message. That is why the in-office caller is asserted here.</para>
/// </summary>
public sealed class InternalUsersTenantOptionsGateTests : IAsyncLifetime
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    private static readonly Guid HostOperator = Guid.Parse("00000000-0000-0000-0000-00000000b001");

    private IAbpApplicationWithInternalServiceProvider _app = null!;

    public async Task InitializeAsync()
    {
        _app = await AbpApplicationFactory.CreateAsync<TenantOptionsGateModule>();
        await _app.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.ShutdownAsync();
        _app.Dispose();
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_on_the_host()
    {
        (await AdmitsAsync(Anonymous(), tenantId: null)).ShouldBeFalse();
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_on_an_office_host()
    {
        (await AdmitsAsync(Anonymous(), tenantId: Office)).ShouldBeFalse();
    }

    /// <summary>Caller kind 1: a host-scope operator who is not Intake.</summary>
    [Theory]
    [InlineData("IT Admin")]
    [InlineData("Staff Supervisor")]
    [InlineData("admin")]
    public async Task A_host_operator_is_admitted(string hostRole)
    {
        (await AdmitsAsync(SignedIn(hostRole, tenantId: null), tenantId: null)).ShouldBeTrue();
    }

    /// <summary>
    /// Caller kind 2: a host operator switched into an office, as the shadow user the grant signs
    /// them in as. The session carries the SHADOW's office role, not the operator's host role.
    /// </summary>
    [Theory]
    [InlineData("admin")]
    [InlineData("Staff Supervisor")]
    public async Task A_host_operator_switched_into_an_office_is_admitted(string shadowOfficeRole)
    {
        var principal = SignedIn(shadowOfficeRole, tenantId: Office, impersonatedBy: HostOperator);

        (await AdmitsAsync(principal, tenantId: Office)).ShouldBeTrue();
    }

    /// <summary>
    /// Not a caller of this route (see the class summary), and the office list is not theirs to read.
    /// Includes an Intake operator switched in, whose shadow holds the office Intake Staff role.
    /// </summary>
    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Patient")]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    public async Task Any_other_office_role_is_refused(string officeRole)
    {
        (await AdmitsAsync(SignedIn(officeRole, tenantId: Office), tenantId: Office)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_intake_operator_is_refused()
    {
        // The switcher does not offer them an office list at host scope (canSwitch is false).
        (await AdmitsAsync(SignedIn("Intake Staff", tenantId: null), tenantId: null)).ShouldBeFalse();
    }

    /// <summary>
    /// The HTTP action cannot be booted here. The app service check above is the one that decides
    /// on every call; this keeps MVC from also treating the route as anonymous.
    /// </summary>
    [Fact]
    public void The_controller_action_is_not_anonymous()
    {
        var action = typeof(InternalUsersController).GetMethod(nameof(InternalUsersController.GetTenantOptionsAsync))!;

        action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).ShouldBeEmpty();
        action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ShouldNotBeEmpty();
    }

    private async Task<bool> AdmitsAsync(ClaimsPrincipal principal, Guid? tenantId)
    {
        var method = typeof(InternalUsersAppService).GetMethod(nameof(InternalUsersAppService.GetTenantOptionsAsync))!;
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
    public sealed class TenantOptionsGateModule : AbpModule
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
    public sealed class SeededRoleGrantStore : IPermissionStore
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

        private static Dictionary<string, (HashSet<string> Host, HashSet<string> Office)> Load()
        {
            var path = Path.Combine(ThisDirectory(), "..", "Authorization", "role-permission-surface.approved.txt");
            var line = new Regex("^(?<name>\\S+) side=\\S+ host=(?<host>.*) office=(?<office>.*)$");
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
