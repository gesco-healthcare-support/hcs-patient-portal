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
            // Doctor is a non-user reference entity per OLD spec (Phase 0.1, 2026-05-01):
            // Staff Supervisor manages the Doctor on its behalf; no Doctor user role exists.
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
    // D.1 / W-I-1 + W-I-2 + W-I-3 (2026-04-30): two custom action permissions
    // exist outside the standard CRUD set --
    //   - AppointmentDocuments.Approve : approve / reject uploaded documents.
    //   - AppointmentPackets.Regenerate: regenerate the IME packet PDF.
    // Both internal roles need these for the office workflow per the W-I findings.
    private static string Approve(string entity) => $"{Group}.{entity}.Approve";
    private static string Regenerate(string entity) => $"{Group}.{entity}.Regenerate";
    // Phase 2.5 (2026-05-01): per-action permission helpers for the
    // approval / change-request lifecycle. RequestCancellation +
    // RequestReschedule are external-user actions; permission strings exist
    // so AppService [Authorize(...)] attributes can reference them, but
    // INTERNAL roles do NOT receive these grants -- external roles are
    // seeded separately in <c>ExternalUserRoleDataSeedContributor</c>.
    private static string Reject(string entity) => $"{Group}.{entity}.Reject";

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
        // Phase 5 (2026-05-03): IT Admin master Document catalog + per-
        // AppointmentType package templates. PackageDetails has an extra
        // ManageDocuments action yielded explicitly below.
        "Documents",
        "PackageDetails",
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

        // Phase 2.5 (2026-05-01) -- approval + change-request lifecycle.
        yield return Approve("Appointments");
        yield return Reject("Appointments");
        yield return Default("AppointmentChangeRequests");
        yield return Approve("AppointmentChangeRequests");
        yield return Reject("AppointmentChangeRequests");
        yield return Default("NotificationTemplates");
        yield return Edit("NotificationTemplates");
        yield return Default("SystemParameters");
        yield return Edit("SystemParameters");

        // Phase 5 (2026-05-03) -- PackageDetails has a custom ManageDocuments
        // action that gates Link / Unlink endpoints. AllEntities yields the
        // standard CRUD; this yields the extra action explicitly.
        yield return $"{Group}.PackageDetails.ManageDocuments";

        // Phase A (2026-05-05) -- per-user signature upload, internal-only.
        // Default lets IT Admin see the feature in the admin UI; ManageOwn
        // gates the upload of their own signature for stamping on the
        // Patient Packet PDF.
        yield return Default("UserSignatures");
        yield return $"{Group}.UserSignatures.ManageOwn";

        // 2026-05-15 -- admin-issued invitation for new external users.
        // Default parent gates menu visibility; InviteExternalUser gates
        // the create-invite endpoint itself.
        yield return Default("UserManagement");
        yield return $"{Group}.UserManagement.InviteExternalUser";

        // 2026-05-15 -- IT Admin (host-scoped) creates new internal users
        // (Clinic Staff / Staff Supervisor). The permission is
        // MultiTenancySides.Host so it lives in the host pass; Staff
        // Supervisor + Clinic Staff intentionally do NOT receive it
        // (OLD parity: only IT Admin creates internal accounts).
        yield return Default("InternalUsers");
        yield return $"{Group}.InternalUsers.Create";
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

        // Phase 2.5 (2026-05-01) -- supervisor approval surface for booking
        // approval + cancel / reschedule requests; tenant-side notification
        // template editing.
        yield return Approve("Appointments");
        yield return Reject("Appointments");
        yield return Default("AppointmentChangeRequests");
        yield return Approve("AppointmentChangeRequests");
        yield return Reject("AppointmentChangeRequests");
        yield return Default("NotificationTemplates");
        yield return Edit("NotificationTemplates");
        yield return Default("SystemParameters");

        // Phase A (2026-05-05) -- supervisor uploads a signature so OLD packets
        // they are responsible for include a stamped image (per OLD parity).
        yield return Default("UserSignatures");
        yield return $"{Group}.UserSignatures.ManageOwn";

        // 2026-05-15 -- supervisor invites external users (Patient,
        // Applicant Attorney, Defense Attorney, Claim Examiner) so
        // recipients can self-register on the tenant portal.
        yield return Default("UserManagement");
        yield return $"{Group}.UserManagement.InviteExternalUser";
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

        // 2026-05-13: read access to every operational entity under the
        // tenant. Mirrors Staff Supervisor's loop but read-only -- the
        // appointment-view page fans out to per-entity endpoints
        // (AppointmentInjuryDetails, AppointmentEmployerDetails,
        // ApplicantAttorneys, etc.), each gated by [Authorize(...Default)].
        // Clinic Staff is the front-line reviewer for every appointment in
        // their tenant, so they need read on the full operational set.
        // Mutation rights stay scoped explicitly below to Appointments +
        // Patients (and AppointmentDocuments.Approve / AppointmentPackets.
        // Regenerate per the existing receptionist-tier scope).
        foreach (var entity in OperationalEntities)
        {
            yield return Default(entity);
        }

        yield return Create("Appointments");
        yield return Edit("Appointments");

        yield return Create("Patients");
        yield return Edit("Patients");

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

        // Phase 2.5 (2026-05-01) -- clinic staff is the front-line approver
        // for new bookings. Change requests are read-only at this tier; only
        // the supervisor finalizes cancel / reschedule outcomes.
        yield return Approve("Appointments");
        yield return Reject("Appointments");
        yield return Default("AppointmentChangeRequests");
        yield return Default("SystemParameters");

        // Phase A (2026-05-05) -- clinic staff uploads a signature so OLD
        // packets they are responsible for include a stamped image. Mirrors
        // OLD where ClinicStaff/ITAdmin/StaffSupervisor were the three roles
        // that could upload via the My-Profile page.
        yield return Default("UserSignatures");
        yield return $"{Group}.UserSignatures.ManageOwn";

        // 2026-05-15 -- front-desk clinic staff is the most common
        // inviter (intake phone calls from prospective patients).
        // Same grant shape as the other two internal roles -- the
        // server gate is the same permission for all three.
        yield return Default("UserManagement");
        yield return $"{Group}.UserManagement.InviteExternalUser";
    }

}
