using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Seeds the 3 internal Gesco-side role tiers and grants their baseline permissions.
/// All 3 roles are HOST-SCOPED so they can be assigned across tenants from the host admin
/// UI (per Q21 lock 2026-04-24 + the brief's recommended scoping pattern). External role
/// permission grants are deferred -- the existing ExternalUserRoleDataSeedContributor seeds
/// the role names per tenant; admins assign permissions on first use.
///
/// NOTE: Permission strings are hardcoded as literals (mirroring the constants in
/// `Application.Contracts/Permissions/CaseEvaluationPermissions.cs`) because the Domain
/// layer cannot reference Application.Contracts under ABP's layered architecture. If a
/// permission name changes, both files must update.
///
/// Role matrix (Q21+Q22 lock):
///  - IT Admin        : every CaseEvaluation.* permission (admin-equivalent).
///  - Staff Supervisor: Dashboard.Tenant + per-entity Default/Create/Edit (no Delete) across operational entities.
///  - Clinic Staff    : Dashboard.Tenant + Appointments + Patients (.Default/.Create/.Edit), DoctorAvailabilities.Default (read-only).
/// </summary>
public class InternalUserRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string ItAdminRoleName        = "IT Admin";
    public const string StaffSupervisorRoleName = "Staff Supervisor";
    public const string ClinicStaffRoleName     = "Clinic Staff";

    // ABP role permission provider key.
    // Matches Volo.Abp.PermissionManagement.RolePermissionValueProvider.ProviderName ("R").
    private const string RoleProviderName = "R";

    private const string Group = "CaseEvaluation";

    private readonly IdentityRoleManager _roleManager;
    private readonly IPermissionManager _permissionManager;
    private readonly ICurrentTenant _currentTenant;

    public InternalUserRoleDataSeedContributor(
        IdentityRoleManager roleManager,
        IPermissionManager permissionManager,
        ICurrentTenant currentTenant)
    {
        _roleManager = roleManager;
        _permissionManager = permissionManager;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Host-only: skip the per-tenant pass.
        if (context?.TenantId != null)
        {
            return;
        }

        // Force host context (no tenant) so IdentityRole gets a null TenantId and grants
        // are written without tenant scope.
        using (_currentTenant.Change(null))
        {
            await EnsureRoleAsync(ItAdminRoleName);
            await EnsureRoleAsync(StaffSupervisorRoleName);
            await EnsureRoleAsync(ClinicStaffRoleName);

            await GrantAllAsync(ItAdminRoleName, ItAdminGrants());
            await GrantAllAsync(StaffSupervisorRoleName, StaffSupervisorGrants());
            await GrantAllAsync(ClinicStaffRoleName, ClinicStaffGrants());
        }
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        var existing = await _roleManager.FindByNameAsync(roleName);
        if (existing != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, tenantId: null);
        await _roleManager.CreateAsync(role);
    }

    private async Task GrantAllAsync(string roleName, IEnumerable<string> permissions)
    {
        foreach (var permission in permissions)
        {
            await _permissionManager.SetAsync(permission, RoleProviderName, roleName, isGranted: true);
        }
    }

    private static string Default(string entity) => $"{Group}.{entity}";
    private static string Create(string entity)  => $"{Group}.{entity}.Create";
    private static string Edit(string entity)    => $"{Group}.{entity}.Edit";
    private static string Delete(string entity)  => $"{Group}.{entity}.Delete";

    private static readonly string[] AllEntities =
    {
        "Books",
        "States",
        "AppointmentTypes",
        "AppointmentStatuses",
        "AppointmentLanguages",
        "Locations",
        "WcabOffices",
        "Doctors",
        "DoctorAvailabilities",
        "Patients",
        "Appointments",
        "AppointmentEmployerDetails",
        "AppointmentAccessors",
        "ApplicantAttorneys",
        "AppointmentApplicantAttorneys",
    };

    private static readonly string[] OperationalEntities =
    {
        "Doctors",
        "DoctorAvailabilities",
        "Patients",
        "Appointments",
        "AppointmentEmployerDetails",
        "AppointmentAccessors",
        "ApplicantAttorneys",
        "AppointmentApplicantAttorneys",
    };

    private static readonly string[] LookupReadEntities =
    {
        "States",
        "AppointmentTypes",
        "AppointmentLanguages",
        "Locations",
        "WcabOffices",
    };

    /// <summary>IT Admin: full CaseEvaluation.* tree.</summary>
    private static IEnumerable<string> ItAdminGrants()
    {
        yield return $"{Group}.Dashboard.Host";
        yield return $"{Group}.Dashboard.Tenant";

        foreach (var entity in AllEntities)
        {
            yield return Default(entity);
            yield return Create(entity);
            yield return Edit(entity);
            yield return Delete(entity);
        }
    }

    /// <summary>
    /// Staff Supervisor: Dashboard.Tenant + every operational entity .Default/.Create/.Edit
    /// (no .Delete). All lookup reads. Locations.Edit so the supervisor can manage their
    /// own clinic's location list. NO .Delete (hard-delete is IT-Admin-only per Q21 lock).
    /// </summary>
    private static IEnumerable<string> StaffSupervisorGrants()
    {
        yield return $"{Group}.Dashboard.Tenant";

        // Lookup reads (and Locations write, since locations are tenant-operational).
        foreach (var entity in LookupReadEntities)
        {
            yield return Default(entity);
        }
        yield return Create("Locations");
        yield return Edit("Locations");

        // Operational entities (no Delete).
        foreach (var entity in OperationalEntities)
        {
            yield return Default(entity);
            yield return Create(entity);
            yield return Edit(entity);
        }
    }

    /// <summary>
    /// Clinic Staff: front-desk receptionist tier. Tightly scoped to booking-flow inputs:
    /// Appointments + Patients (CRUD-minus-Delete), read-only DoctorAvailabilities + lookups.
    /// </summary>
    private static IEnumerable<string> ClinicStaffGrants()
    {
        yield return $"{Group}.Dashboard.Tenant";

        yield return Default("Appointments");
        yield return Create("Appointments");
        yield return Edit("Appointments");

        yield return Default("Patients");
        yield return Create("Patients");
        yield return Edit("Patients");

        yield return Default("DoctorAvailabilities");

        foreach (var entity in LookupReadEntities)
        {
            yield return Default(entity);
        }
    }
}
