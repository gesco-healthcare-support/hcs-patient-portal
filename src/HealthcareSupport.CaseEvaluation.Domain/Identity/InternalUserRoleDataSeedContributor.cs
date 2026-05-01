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
    // D.1 / W-I-3 (2026-04-30): Doctor role at tenant scope. One tenant = one
    // doctor's office / medical examiner's office; the office may host multiple
    // doctor accounts. Read-mostly + edit-own-availability scope; the row-level
    // "own appointments only" filter is a separate domain concern (W-DOC-1).
    public const string DoctorRoleName = "Doctor";

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
            // PER-TENANT pass: seed Staff Supervisor + Clinic Staff + Doctor per tenant + grant tenant-side permissions.
            using (_currentTenant.Change(context.TenantId))
            {
                await EnsureRoleAsync(StaffSupervisorRoleName, context.TenantId);
                await EnsureRoleAsync(ClinicStaffRoleName, context.TenantId);
                await EnsureRoleAsync(DoctorRoleName, context.TenantId);

                await GrantAllAsync(StaffSupervisorRoleName, StaffSupervisorGrants());
                await GrantAllAsync(ClinicStaffRoleName, ClinicStaffGrants());
                await GrantAllAsync(DoctorRoleName, DoctorGrants());
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
    // D.1 / W-I-1 + W-I-2 + W-I-3 (2026-04-30): two custom action permissions
    // exist outside the standard CRUD set --
    //   - AppointmentDocuments.Approve : approve / reject uploaded documents.
    //   - AppointmentPackets.Regenerate: regenerate the IME packet PDF.
    // Both internal roles need these for the office workflow per the W-I findings.
    private static string Approve(string entity) => $"{Group}.{entity}.Approve";
    private static string Regenerate(string entity) => $"{Group}.{entity}.Regenerate";

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
        "DefenseAttorneys",
        "AppointmentDefenseAttorneys",
        "AppointmentInjuryDetails",
        "AppointmentBodyParts",
        "AppointmentClaimExaminers",
        "AppointmentPrimaryInsurances",
        // D.1 (2026-04-30): AppointmentDocuments + CustomFields support full
        // CRUD so they participate in the standard Default/Create/Edit/Delete
        // loop. AppointmentPackets only defines .Default + .Regenerate (no
        // Create/Edit/Delete in CaseEvaluationPermissions.cs), so it is NOT
        // listed here -- its grants are yielded explicitly per role.
        // AppointmentChangeLogs only defines .Default (immutable audit rows);
        // also yielded explicitly per role.
        "AppointmentDocuments",
        "CustomFields",
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
        "DefenseAttorneys",
        "AppointmentDefenseAttorneys",
        "AppointmentInjuryDetails",
        "AppointmentBodyParts",
        "AppointmentClaimExaminers",
        "AppointmentPrimaryInsurances",
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
    ///
    /// D.1 (2026-04-30): two custom actions (AppointmentDocuments.Approve,
    /// AppointmentPackets.Regenerate) are not part of the standard CRUD loop
    /// and are yielded explicitly. AppointmentChangeLogs is read-only (only
    /// .Default exists); the CRUD loop's Create/Edit/Delete yields for that
    /// entity will be invalid permission strings if it were in AllEntities,
    /// so it is yielded as a one-off Default below.
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

        yield return Approve("AppointmentDocuments");
        yield return Regenerate("AppointmentPackets");
        yield return Default("AppointmentChangeLogs");
    }

    /// <summary>
    /// Staff Supervisor (TENANT scope): Dashboard.Tenant + every operational entity
    /// .Default/.Create/.Edit (no .Delete; hard-delete is IT-Admin-only). All lookup
    /// reads. Locations.Edit/.Create so the supervisor can manage their clinic's
    /// location list.
    ///
    /// D.1 / W-I-2 (2026-04-30): added the previously-missing supervisory powers
    /// flagged in the Wave 2 demo-lifecycle review. The supervisor must be able
    /// to approve uploaded documents, regenerate the IME packet, view the
    /// audit log, and read the per-AppointmentType field configuration.
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

        // D.1 / W-I-2: AppointmentDocuments full CRUD + Approve (no Delete --
        // hard-delete remains IT-Admin-only).
        yield return Default("AppointmentDocuments");
        yield return Create("AppointmentDocuments");
        yield return Edit("AppointmentDocuments");
        yield return Approve("AppointmentDocuments");

        // D.1 / W-I-2: AppointmentPackets read + regenerate.
        yield return Default("AppointmentPackets");
        yield return Regenerate("AppointmentPackets");

        // D.1 / W-I-2: read-only audit log access.
        yield return Default("AppointmentChangeLogs");

        // D.1 / W-I-2: read-only field-config access (the booker form fetches
        // these to render per-AppointmentType field state). Edit stays admin-only.
        yield return Default("CustomFields");
    }

    /// <summary>
    /// Clinic Staff (TENANT scope): front-desk receptionist tier. Tightly scoped to
    /// booking-flow inputs: Appointments + Patients (CRUD-minus-Delete), read-only
    /// DoctorAvailabilities + lookups.
    ///
    /// D.1 / W-I-1 (2026-04-30): the receptionist tier needs document upload +
    /// approve, packet regeneration, audit-log read, and field-config read to
    /// complete the demo workflow (upload Stage-8 doc, approve, regenerate
    /// packet). Create/Edit on documents is supervisor-only -- the receptionist
    /// may upload (the upload happens via the Appointments.Edit path) and
    /// approve/reject; structural edits to a document row stay supervisor-tier.
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

        // D.1 / W-I-1: document workflow + packet regeneration + audit visibility.
        yield return Default("AppointmentDocuments");
        yield return Approve("AppointmentDocuments");
        yield return Default("AppointmentPackets");
        yield return Regenerate("AppointmentPackets");
        yield return Default("AppointmentChangeLogs");
        yield return Default("CustomFields");
    }

    /// <summary>
    /// D.1 / W-I-3 (2026-04-30): Doctor (TENANT scope). Read-mostly persona;
    /// the doctor needs to inspect appointments + patients on their own
    /// schedule, manage their availability slots, and regenerate the packet
    /// for an appointment they own. Specifically NOT granted:
    ///   - .Create / .Edit on Appointments (booking is office workflow)
    ///   - .Create / .Edit on Patients (intake is receptionist workflow)
    ///   - .Approve on documents (sign-off lives with office staff)
    ///   - .Delete on anything
    /// Row-level "own appointments only" filtering is a separate domain
    /// concern (W-DOC-1) and is NOT enforced by this seeder; today a Doctor
    /// reads every appointment in their tenant. Tightening to per-doctor
    /// visibility is a follow-up.
    /// </summary>
    private static IEnumerable<string> DoctorGrants()
    {
        yield return $"{Group}.Dashboard.Tenant";

        foreach (var entity in LookupReadEntities)
        {
            yield return Default(entity);
        }

        yield return Default("Appointments");
        yield return Default("Patients");

        // Edit-own-availability: doctors self-manage their schedule.
        yield return Default("DoctorAvailabilities");
        yield return Create("DoctorAvailabilities");
        yield return Edit("DoctorAvailabilities");

        // Read-only across the per-injury / per-employer / attorney sub-entities.
        yield return Default("AppointmentInjuryDetails");
        yield return Default("AppointmentEmployerDetails");
        yield return Default("AppointmentApplicantAttorneys");
        yield return Default("AppointmentDefenseAttorneys");
        yield return Default("AppointmentClaimExaminers");
        yield return Default("AppointmentBodyParts");
        yield return Default("AppointmentPrimaryInsurances");

        // Read documents; regenerate the packet PDF.
        yield return Default("AppointmentDocuments");
        yield return Default("AppointmentPackets");
        yield return Regenerate("AppointmentPackets");

        // Audit visibility for own appointments + field-config read for the
        // booking-form lookups (booker form is closed to Doctor today, but
        // Default lookups must succeed for any appointment-detail render).
        yield return Default("AppointmentChangeLogs");
        yield return Default("CustomFields");
    }
}
