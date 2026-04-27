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
/// Seeds the 3 internal Gesco-side role tiers and grants their baseline permissions
/// per Q21+Q22 lock 2026-04-24:
///  - IT Admin         : HOST-scoped (one host-level role; cross-tenant authority).
///                       Grants every CaseEvaluation.* permission EXCEPT Dashboard.Tenant
///                       (which is MultiTenancySides.Tenant only and cannot be granted in
///                       host scope).
///  - Staff Supervisor : TENANT-scoped (per-tenant copy). Grants Dashboard.Tenant + every
///                       operational entity Default+Create+Edit (no Delete; hard-delete
///                       is IT Admin only).
///  - Clinic Staff     : TENANT-scoped. Grants Dashboard.Tenant + Appointments + Patients
///                       (.Default/.Create/.Edit), DoctorAvailabilities.Default (read-only).
///
/// External role permission grants are deferred -- the existing
/// ExternalUserRoleDataSeedContributor seeds the role names per tenant; admins assign
/// permissions on first use.
///
/// NOTE: Permission strings are hardcoded as literals (mirroring the constants in
/// `Application.Contracts/Permissions/CaseEvaluationPermissions.cs`) because the Domain
/// layer cannot reference Application.Contracts under ABP's layered architecture. If a
/// permission name changes, both files must update.
/// </summary>
public class InternalUserRoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string ItAdminRoleName = "IT Admin";
    public const string StaffSupervisorRoleName = "Staff Supervisor";
    public const string ClinicStaffRoleName = "Clinic Staff";

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
        if (context?.TenantId == null)
        {
            // HOST pass: seed IT Admin (host-scoped) + grant host-side permissions.
            using (_currentTenant.Change(null))
            {
                await EnsureRoleAsync(ItAdminRoleName, tenantId: null);
                await GrantAllAsync(ItAdminRoleName, ItAdminGrants());
            }
        }
        else
        {
            // PER-TENANT pass: seed Staff Supervisor + Clinic Staff per tenant + grant tenant-side permissions.
            using (_currentTenant.Change(context.TenantId))
            {
                await EnsureRoleAsync(StaffSupervisorRoleName, context.TenantId);
                await EnsureRoleAsync(ClinicStaffRoleName, context.TenantId);

                await GrantAllAsync(StaffSupervisorRoleName, StaffSupervisorGrants());
                await GrantAllAsync(ClinicStaffRoleName, ClinicStaffGrants());
            }
        }
    }

    private async Task EnsureRoleAsync(string roleName, Guid? tenantId)
    {
        var existing = await _roleManager.FindByNameAsync(roleName);
        if (existing != null)
        {
            return;
        }

        var role = new IdentityRole(Guid.NewGuid(), roleName, tenantId);
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
    private static string Create(string entity) => $"{Group}.{entity}.Create";
    private static string Edit(string entity) => $"{Group}.{entity}.Edit";
    private static string Delete(string entity) => $"{Group}.{entity}.Delete";

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

    /// <summary>
    /// IT Admin (HOST scope): full CaseEvaluation.* tree. Excludes Dashboard.Tenant
    /// because it is MultiTenancySides.Tenant only -- ABP rejects host-scoped grants
    /// for tenant-side permissions. Tenant admins (Staff Supervisor / Clinic Staff)
    /// hold Dashboard.Tenant inside their tenant scope.
    /// </summary>
    private static IEnumerable<string> ItAdminGrants()
    {
        yield return $"{Group}.Dashboard.Host";

        foreach (var entity in AllEntities)
        {
            yield return Default(entity);
            yield return Create(entity);
            yield return Edit(entity);
            yield return Delete(entity);
        }
    }

    /// <summary>
    /// Staff Supervisor (TENANT scope): Dashboard.Tenant + every operational entity
    /// .Default/.Create/.Edit (no .Delete; hard-delete is IT-Admin-only). All lookup
    /// reads. Locations.Edit/.Create so the supervisor can manage their clinic's
    /// location list.
    /// </summary>
    private static IEnumerable<string> StaffSupervisorGrants()
    {
        yield return $"{Group}.Dashboard.Tenant";

        foreach (var entity in LookupReadEntities)
        {
            yield return Default(entity);
        }
        yield return Create("Locations");
        yield return Edit("Locations");

        foreach (var entity in OperationalEntities)
        {
            yield return Default(entity);
            yield return Create(entity);
            yield return Edit(entity);
        }
    }

    /// <summary>
    /// Clinic Staff (TENANT scope): front-desk receptionist tier. Tightly scoped to
    /// booking-flow inputs: Appointments + Patients (CRUD-minus-Delete), read-only
    /// DoctorAvailabilities + lookups.
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
