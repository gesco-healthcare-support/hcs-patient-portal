using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Identity;
using Microsoft.Extensions.Logging;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// task_2e8e4dc2 (2026-07-21) -- verifies the two per-office building blocks that let a host
/// operator switch into an office as their OWN shadow user (not the shared office admin):
///  1. the re-introduced per-tenant Staff Supervisor role is seeded with its operational grants
///     (and is narrower than admin -- no framework powers);
///  2. the generalized shadow provisioner creates a per-office shadow holding a caller-specified
///     role (here Staff Supervisor).
/// The grant wiring itself (AuthServer) is exercised by the live E2E -- it has no test harness.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeImpersonationRoleTests : CaseEvaluationMultiOfficeTestBase
{
    // Matches RolePermissionValueProvider.ProviderName ("R").
    private const string RoleProviderName = "R";

    private readonly InternalUserRoleDataSeedContributor _roleSeeder;
    private readonly IIntakeShadowUserProvisioner _shadowProvisioner;
    private readonly IdentityRoleManager _roleManager;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeImpersonationRoleTests()
    {
        _roleSeeder = GetRequiredService<InternalUserRoleDataSeedContributor>();
        _shadowProvisioner = GetRequiredService<IIntakeShadowUserProvisioner>();
        _roleManager = GetRequiredService<IdentityRoleManager>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _permissionManager = GetRequiredService<IPermissionManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task TenantSeed_ReintroducesStaffSupervisorRole_WithOperationalGrantsButNotFrameworkPowers()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var role = await _roleManager.FindByNameAsync(
                    InternalUserRoleDataSeedContributor.StaffSupervisorRoleName);
                role.ShouldNotBeNull();
                role!.TenantId.ShouldBe(officeA.OfficeId);

                // Holds the top-tenant operational grants (soft-delete on operational entities +
                // the tenant dashboard).
                (await IsGrantedAsync("CaseEvaluation.Dashboard.Tenant")).ShouldBeTrue();
                (await IsGrantedAsync("CaseEvaluation.Appointments.Delete")).ShouldBeTrue();
                (await IsGrantedAsync("CaseEvaluation.InternalUsers.Create")).ShouldBeTrue();

                // Narrower than the tenant admin role: NO framework powers.
                (await IsGrantedAsync("AbpIdentity.Roles")).ShouldBeFalse();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task EnsureShadowUser_WithSupervisorRole_ProvisionsOwnShadowHoldingThatRole()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "supervisor.shadow.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        // Provision the operator's own shadow in the office, holding the Staff Supervisor role.
        await WithUnitOfWorkAsync(
            () => _shadowProvisioner.EnsureShadowUserAsync(
                officeA.OfficeId, operatorId, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
            requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByEmailAsync(operatorEmail);
                shadow.ShouldNotBeNull();
                shadow!.TenantId.ShouldBe(officeA.OfficeId);
                shadow.IsActive.ShouldBeTrue();
                (await _userManager.IsInRoleAsync(
                    shadow, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)).ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Regression, 2026-08-22. A Staff Supervisor pressing "Switch into practice" got a 403 from
    /// <c>/connect/token</c> carrying <c>Volo.Abp.Identity:DuplicateUserName</c>, and could not enter
    /// the office at all.
    ///
    /// <para>Cause: the office already held a shadow whose USERNAME was the operator's host address but
    /// whose EMAIL was not. The provisioner looked the shadow up by email only, missed it, and fell
    /// through to create a user whose username was already taken.</para>
    ///
    /// <para><b>Correction (2026-08-22).</b> This docstring originally blamed the pre-Phase-D per-tenant
    /// seed for that divergence. It did not cause it -- the seed writes the same value to both fields.
    /// The rows in question had been repointed by hand in a local database. The defect the test pins is
    /// real either way, and the revoke half of it (below) is the dangerous one.</para>
    ///
    /// <para>This test reproduces that divergence exactly, then asserts the provisioner ADOPTS the
    /// existing row rather than throwing. If the lookup regresses to email-only, the
    /// <c>EnsureShadowUserAsync</c> call throws and this test fails.</para>
    /// </summary>
    [Fact]
    public async Task EnsureShadowUser_WhenExistingShadowEmailDivergedFromUserName_AdoptsItInsteadOfThrowing()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "diverged.shadow.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        // The legacy row: username IS the operator's host address, email is something else entirely.
        var legacyShadowId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var legacy = new IdentityUser(
                    legacyShadowId,
                    userName: operatorEmail,
                    email: "diverged.shadow.test@example.test",
                    tenantId: officeA.OfficeId);
                (await _userManager.CreateAsync(legacy, "1q2w3E*r")).Succeeded.ShouldBeTrue();
            }
        }, requiresNew: true);

        var resolvedId = Guid.Empty;
        await WithUnitOfWorkAsync(
            async () => resolvedId = await _shadowProvisioner.EnsureShadowUserAsync(
                officeA.OfficeId, operatorId, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
            requiresNew: true);

        // Adopted, not duplicated: the same row comes back, now carrying the requested role.
        resolvedId.ShouldBe(legacyShadowId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByIdAsync(legacyShadowId.ToString());
                shadow.ShouldNotBeNull();
                shadow!.IsActive.ShouldBeTrue();
                (await _userManager.IsInRoleAsync(
                    shadow, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName)).ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// The same divergence, on the revoke path. This one failed SILENTLY rather than loudly:
    /// <c>DisableShadowUserAsync</c> looked up by email, found nothing, and returned without doing
    /// anything -- so unassigning an operator from an office left their shadow ACTIVE and their access
    /// intact. A revoke that reports success while granting continued access is the worse of the two
    /// bugs, which is why it gets its own test.
    /// </summary>
    [Fact]
    public async Task DisableShadowUser_WhenExistingShadowEmailDivergedFromUserName_ActuallyDeactivatesIt()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        var operatorEmail = "diverged.revoke.test@hcs.test";
        var operatorId = await EnsureHostOperatorAsync(operatorEmail);

        var legacyShadowId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var legacy = new IdentityUser(
                    legacyShadowId,
                    userName: operatorEmail,
                    email: "diverged.revoke.test@example.test",
                    tenantId: officeA.OfficeId);
                (await _userManager.CreateAsync(legacy, "1q2w3E*r")).Succeeded.ShouldBeTrue();
            }
        }, requiresNew: true);

        await WithUnitOfWorkAsync(
            () => _shadowProvisioner.DisableShadowUserAsync(officeA.OfficeId, operatorId),
            requiresNew: true);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                var shadow = await _userManager.FindByIdAsync(legacyShadowId.ToString());
                shadow.ShouldNotBeNull();
                shadow!.IsActive.ShouldBeFalse();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// The OTHER half of the #610 split, and the branch the change exists to create.
    ///
    /// <para>Before, "already inactive" and "no shadow at all" shared one silent
    /// <c>return</c>. They are not the same thing: the first is the idempotent path, the
    /// second means revoke was asked to disable a shadow, could not find one, and returned
    /// having done nothing -- so an unassigned operator can keep working access to that
    /// office.</para>
    ///
    /// <para><b>It warns, it does not throw</b>, and that is deliberate -- the production
    /// comment gives the reason: an operator assigned and unassigned before ever signing in
    /// legitimately has no shadow, and the caller cannot tell that apart from the email
    /// divergence that is a real fault. Throwing would fail the whole unassign over a case
    /// that is often benign. So the guarantee under test is that the silence became
    /// VISIBLE, not that it became an exception.</para>
    /// </summary>
    [Fact]
    public async Task DisableShadowUser_WhenNoShadowExistsAtAll_WarnsNamingTheOperatorAndOffice()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedTenantRolesAsync(officeA.OfficeId);

        // A host operator that has never been provisioned into office A, so FindShadowAsync
        // misses on BOTH username and email rather than on one of them.
        var operatorId = await EnsureHostOperatorAsync("never.provisioned.revoke@hcs.test");

        var captured = new CapturingLoggerProvider();
        GetRequiredService<ILoggerFactory>().AddProvider(captured);

        // Must not throw: the benign case (assigned and unassigned before first sign-in)
        // reaches exactly this branch, and failing the unassign over it would be wrong.
        await WithUnitOfWorkAsync(
            () => _shadowProvisioner.DisableShadowUserAsync(officeA.OfficeId, operatorId),
            requiresNew: true);

        var warning = captured.Entries.FirstOrDefault(
            e => e.Level == LogLevel.Warning && e.Message.Contains("no shadow user found to revoke"));

        warning.ShouldNotBeNull(
            "the not-found branch returned silently; that is the defect #610 is about.");

        // Naming both is the point. A warning that says only "not found" cannot be acted on:
        // the operator id is what identifies whose access may still be live, and the office
        // id is where to look for it.
        warning!.Message.ShouldContain(operatorId.ToString());
        warning.Message.ShouldContain(officeA.OfficeId.ToString());
    }

    /// <summary>
    /// Records what the domain service logged. Added via <c>ILoggerFactory.AddProvider</c> at
    /// test time rather than registered in the test module, because ABP's
    /// <c>DomainService.Logger</c> comes from the container and the module is shared by every
    /// multi-office test -- this keeps the capture scoped to the one test that needs it.
    /// </summary>
    private sealed record LogEntry(LogLevel Level, string Message);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<LogEntry> Entries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Capturing(this);

        public void Dispose()
        {
        }

        private sealed class Capturing : ILogger
        {
            private readonly CapturingLoggerProvider _owner;

            public Capturing(CapturingLoggerProvider owner) => _owner = owner;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _owner.Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
            }
        }
    }

    private Task SeedTenantRolesAsync(Guid officeId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                await _roleSeeder.SeedAsync(new DataSeedContext(officeId));
            }
        }, requiresNew: true);

    private async Task<Guid> EnsureHostOperatorAsync(string email)
    {
        var operatorId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var existing = await _userManager.FindByEmailAsync(email);
                if (existing != null)
                {
                    operatorId = existing.Id;
                    return;
                }
                var op = new IdentityUser(Guid.NewGuid(), email, email, tenantId: null)
                {
                    Name = "Supervisor",
                    Surname = "Shadow",
                };
                var createResult = await _userManager.CreateAsync(op, "1q2w3E*r");
                createResult.Succeeded.ShouldBeTrue();
                operatorId = op.Id;
            }
        }, requiresNew: true);
        return operatorId;
    }

    private async Task<bool> IsGrantedAsync(string permission)
    {
        var result = await _permissionManager.GetAsync(
            permission, RoleProviderName, InternalUserRoleDataSeedContributor.StaffSupervisorRoleName);
        return result.IsGranted;
    }
}
