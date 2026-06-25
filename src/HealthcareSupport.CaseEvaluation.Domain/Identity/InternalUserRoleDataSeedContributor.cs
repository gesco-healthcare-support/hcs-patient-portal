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
///  - Staff Supervisor : TENANT-scoped (per-tenant copy) and the TOP tenant role (IR1,
///                       2026-06-03). Grants Dashboard.Tenant + every operational entity
///                       Default+Create+Edit+Delete. Deletes are SOFT (all tenant entities
///                       are FullAudited/ISoftDelete, so removals are recoverable + audited;
///                       no hard purge exists). Also creates internal users
///                       (InternalUsers.Create) within its own tenant.
///  - Intake Staff     : TENANT-scoped. Grants Dashboard.Tenant + Appointments + Patients
///                       (.Default/.Create/.Edit), DoctorAvailabilities.Default (read-only).
///
/// External role permission grants are wired through the sister
/// <see cref="ExternalUserRoleDataSeedContributor"/> (Phase 1A,
/// 2026-05-06): the four external roles -- Patient, Claim Examiner,
/// Applicant Attorney, Defense Attorney -- get the same
/// <c>BookingBaselineGrants</c> set at seed time. Per-record ownership
/// filtering at the AppService layer is the only protection between
/// the four roles (audit D-18 tracks the follow-up review).
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
    public const string IntakeStaffRoleName = "Intake Staff";

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
            // HOST pass: seed the three host operator roles + grant host-side permissions.
            //  - IT Admin         : technical platform admin (full host tree).
            //  - Staff Supervisor : host operator that switches into ANY office as that
            //                       office's admin (stock tenant impersonation). Phase D
            //                       (2026-06-25, O-D1) moved it host-side; the per-tenant
            //                       Staff Supervisor role is no longer seeded.
            //  - Intake Staff     : thin host operator whose only office power is the gated
            //                       impersonation into its per-office shadow Intake user.
            using (_currentTenant.Change(null))
            {
                await EnsureRoleAsync(ItAdminRoleName, tenantId: null);
                await GrantAllAsync(ItAdminRoleName, ItAdminGrants());

                await EnsureRoleAsync(StaffSupervisorRoleName, tenantId: null);
                await GrantAllAsync(StaffSupervisorRoleName, StaffSupervisorHostGrants());

                await EnsureRoleAsync(IntakeStaffRoleName, tenantId: null);
                await GrantAllAsync(IntakeStaffRoleName, IntakeOperatorHostGrants());
            }
        }
        else
        {
            // PER-TENANT pass: seed the per-tenant Intake Staff role only. Phase D
            // (F-7a): this is the LIMITED front-desk identity that the auto-provisioned
            // per-office shadow Intake users hold (the impersonation targets). Staff
            // Supervisor is NOT seeded per tenant anymore -- it is a host operator (O-D1);
            // a host Supervisor switches in as the office `admin` instead.
            using (_currentTenant.Change(context.TenantId))
            {
                await EnsureRoleAsync(IntakeStaffRoleName, context.TenantId);
                await GrantAllAsync(IntakeStaffRoleName, IntakeStaffGrants());

                // 2026-05-19 -- tenant `admin` (Volo SaaS static admin) gets
                // CaseEvaluation.InternalUsers + .Create implicitly because
                // ABP auto-grants every tenant-side permission (including
                // MultiTenancySides.Both) to the static admin role. An
                // explicit GrantAllAsync(TenantAdminRoleName, ...) here
                // collides with the auto-grant on second-run (unique-index
                // violation on AbpPermissionGrants). If we ever need to
                // grant tenant-admin a permission that ABP does NOT
                // auto-grant, add an idempotent guard around SetAsync
                // rather than reintroducing the unconditional call.
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
        // G-03-01 (2026-06-03): per-appointment-type document-category master.
        // IT Admin gets full CRUD here; Staff Supervisor gets Default/Create/Edit
        // via an explicit block (kept out of OperationalEntities so Intake Staff
        // does not inherit read access in PR1). Delete stays IT-Admin-only.
        "AppointmentDocumentTypes",
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
    /// for tenant-side permissions. Tenant admins (Staff Supervisor / Intake Staff)
    /// hold Dashboard.Tenant inside their tenant scope.
    ///
    /// D.1 (2026-04-30): two custom actions (AppointmentDocuments.Approve,
    /// AppointmentPackets.Regenerate) are not part of the standard CRUD loop
    /// and are yielded explicitly. AppointmentChangeLogs is read-only (only
    /// .Default exists); the CRUD loop's Create/Edit/Delete yields for that
    /// entity will be invalid permission strings if it were in AllEntities,
    /// so it is yielded as a one-off Default below.
    /// </summary>
    internal static IEnumerable<string> ItAdminGrants()
    {
        yield return $"{Group}.Dashboard.Host";

        foreach (var entity in AllEntities)
        {
            yield return Default(entity);
            yield return Create(entity);
            yield return Edit(entity);
            yield return Delete(entity);
        }

        // F1 / Design B (2026-05-29) -- SSN reveal endpoint. Not part of the
        // standard CRUD loop, so yielded explicitly. Internal staff may reveal
        // any patient's SSN (verification during intake / review).
        yield return $"{Group}.Patients.RevealSsn";

        yield return Approve("AppointmentDocuments");
        yield return Regenerate("AppointmentPackets");
        yield return Default("AppointmentChangeLogs");

        // G-08-01 (2026-06-06) -- Appointment Request Report (read-only internal
        // worklist) + G-08-03 (2026-06-06) its PDF export.
        yield return Default("Reports");
        yield return $"{Group}.Reports.Export";

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
        // (Intake Staff / Staff Supervisor). 2026-05-19: the permission is
        // now MultiTenancySides.Both because the per-tenant `admin` role
        // also creates internal users in its own tenant (granted in the
        // per-tenant pass). 2026-06-03 (IR1): Staff Supervisor ALSO receives
        // this grant (top tenant role -- see StaffSupervisorGrants). Intake
        // Staff still does NOT -- creating users stays a supervisor/admin power.
        yield return Default("InternalUsers");
        yield return $"{Group}.InternalUsers.Create";
        yield return $"{Group}.InternalUsers.Edit";

        // 2026-05-19 -- IT Admin can create new tenants from the Volo
        // SaaS Tenants page (/saas/tenants). Host Admin (admin@abp.io)
        // already gets this implicitly as the ABP superuser. The Volo
        // permission name "Saas.Tenants[.Create]" is fixed by the Volo
        // SaaS module; we just opt IT Admin in. Tenant admin does NOT
        // receive this grant -- tenant creation stays a host-side power.
        yield return "Saas.Tenants";
        yield return "Saas.Tenants.Create";

        // Roles & access matrix (2026-06-16): IT Admin is the technical platform admin and
        // holds the ABP framework powers the custom seed previously omitted -- this is what
        // makes Roles / Audit / File / Language management + the clinic-switch reachable.
        // (Framework permission strings verified against ABP 10.0.2 source, spike
        // wf_3bdc4d2a-5e0.) All are Host- or Both-sided, so valid for this host-scoped role.
        yield return "Saas.Tenants.Impersonation";
        yield return "AbpIdentity.Roles";
        yield return "AbpIdentity.Roles.ManagePermissions";
        yield return "AuditLogging.AuditLogs";
        // Full File + Language management (parents + action children) -- the File
        // Management page lists DIRECTORIES first, so the DirectoryDescriptor grant is
        // required or the explorer 403s; the Languages page needs the Languages action
        // children for create/edit/delete/set-default.
        yield return "FileManagement.DirectoryDescriptor";
        yield return "FileManagement.DirectoryDescriptor.Create";
        yield return "FileManagement.DirectoryDescriptor.Update";
        yield return "FileManagement.DirectoryDescriptor.Delete";
        yield return "FileManagement.FileDescriptor";
        yield return "FileManagement.FileDescriptor.Create";
        yield return "FileManagement.FileDescriptor.Update";
        yield return "FileManagement.FileDescriptor.Delete";
        yield return "LanguageManagement.Languages";
        yield return "LanguageManagement.Languages.Create";
        yield return "LanguageManagement.Languages.Edit";
        yield return "LanguageManagement.Languages.Delete";
        yield return "LanguageManagement.Languages.ChangeDefault";
        yield return "LanguageManagement.LanguageTexts";
        yield return "LanguageManagement.LanguageTexts.Edit";
    }

    /// <summary>
    /// Staff Supervisor (HOST scope, Phase D 2026-06-25): the cross-office
    /// operator. A single host login that lists offices (<c>Saas.Tenants</c>),
    /// switches into ANY office as that office's <c>admin</c> via stock tenant
    /// impersonation (<c>Saas.Tenants.Impersonation</c>) -- so all supervisory
    /// work happens with the office admin's full powers once switched in (RD4:
    /// slightly broader than the old per-tenant Supervisor, accepted) -- sees a
    /// host overview (<c>Dashboard.Host</c> + the Phase C cross-office
    /// aggregation seam), assigns Intake operators to offices
    /// (<c>IntakeAssignments.Manage</c>), and creates host operators
    /// (<c>InternalUsers.Create/.Edit</c>). Deliberately does NOT hold the
    /// technical/framework powers (AbpIdentity.Roles, File/Language management,
    /// Saas.Tenants.Create) -- those stay IT-Admin-only. All grants are Host- or
    /// Both-sided, valid for this host-scoped role.
    /// </summary>
    internal static IEnumerable<string> StaffSupervisorHostGrants()
    {
        // Host overview dashboard (cross-office aggregation via ITenantWorkRunner).
        yield return $"{Group}.Dashboard.Host";

        // See the offices list + switch into any office as its admin.
        yield return "Saas.Tenants";
        yield return "Saas.Tenants.Impersonation";

        // Phase D -- assign / unassign Intake operators to offices (also
        // provisions / revokes the per-office shadow Intake user).
        yield return $"{Group}.IntakeAssignments";
        yield return $"{Group}.IntakeAssignments.Manage";

        // Create host operators (Intake Staff + Staff Supervisor). InternalUsers
        // is MultiTenancySides.Both; CreatableRoleNames bounds the creatable set.
        yield return Default("InternalUsers");
        yield return $"{Group}.InternalUsers.Create";
        yield return $"{Group}.InternalUsers.Edit";
    }

    /// <summary>
    /// Intake Staff -- HOST operator grants (Phase D 2026-06-25). The thin host
    /// login holds ONLY the office-switch capability
    /// (<c>IntakeImpersonation</c>): it lets the operator read its assigned
    /// offices and impersonate into each as the LIMITED per-office shadow Intake
    /// user (which holds the per-tenant Intake Staff role -- see
    /// <see cref="IntakeStaffGrants"/>). The per-office assignment gate
    /// (deny-by-default, enforced server-side in the impersonation grant) is the
    /// actual office boundary. NOT granted <c>Saas.Tenants.Impersonation</c>
    /// (cannot do a full admin tenant-switch) nor any tenant-side operational
    /// permission (those belong to the shadow user, not the host shell).
    /// </summary>
    internal static IEnumerable<string> IntakeOperatorHostGrants()
    {
        yield return $"{Group}.IntakeImpersonation";
    }

    /// <summary>
    /// Intake Staff (TENANT scope): front-desk receptionist tier. Tightly scoped to
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
    internal static IEnumerable<string> IntakeStaffGrants()
    {
        yield return $"{Group}.Dashboard.Tenant";

        // 2026-05-13: read access to every operational entity under the
        // tenant. Mirrors Staff Supervisor's loop but read-only -- the
        // appointment-view page fans out to per-entity endpoints
        // (AppointmentInjuryDetails, AppointmentEmployerDetails,
        // ApplicantAttorneys, etc.), each gated by [Authorize(...Default)].
        // Intake Staff is the front-line reviewer for every appointment in
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

        // F1 / Design B (2026-05-29) -- SSN reveal endpoint (intake staff
        // reveal any patient's SSN during phone-in intake / review).
        yield return $"{Group}.Patients.RevealSsn";

        // 2026-06-11 -- Intake Staff manages doctor availability slots
        // (create/edit/delete schedules). The OperationalEntities loop above
        // grants only DoctorAvailabilities.Default (read); the slot-management
        // page's Add/Edit/Delete actions hit CreateAsync / UpdateAsync /
        // DeleteAsync, each gated by its own child permission. Front-desk
        // intake staff is responsible for keeping the bookable slot grid
        // current, so it gets full slot CRUD (Adrian, 2026-06-11).
        yield return Create("DoctorAvailabilities");
        yield return Edit("DoctorAvailabilities");
        yield return Delete("DoctorAvailabilities");

        // W2-8 -- the booking-add SPA fires a separate POST per injury
        // draft (multi-injury support per OLD parity, see
        // angular/src/app/appointments/appointment-add.component.ts:2438).
        // Intake Staff is the canonical phone-in booker so it needs the
        // mutation grant alongside the existing Appointments / Patients
        // mutations. Without this, the booking-add flow succeeds on the
        // main appointment row but returns 403 on every auxiliary
        // injury POST -- silently breaking multi-injury bookings.
        yield return Create("AppointmentInjuryDetails");

        // 2026-06-08 (F-1) -- same per-child-POST 403 class as the injury
        // grant above. The booking-add submit also POSTs the Claim Examiner
        // (required, every booking) and, when filled, a Primary Insurance,
        // each to its own permission-gated standalone AppService
        // (appointment-add.component.ts: createClaimExaminer / primary
        // insurance). Without these Creates, Intake Staff (the canonical
        // phone-in booker) gets 403 on the CE attach, which aborts the rest
        // of the submit (injuries + auto-approve never run) -- a half-built
        // Pending appointment. CI1 makes a Claim Examiner mandatory, so a
        // booker who cannot create one cannot complete any booking. (AA/DA
        // links do NOT need a grant here -- the client attaches them via the
        // bare-[Authorize] appointment-scoped upsert routes, not the
        // standalone .Create AppServices.)
        yield return Create("AppointmentClaimExaminers");
        yield return Create("AppointmentPrimaryInsurances");
        // Same per-child-POST class: every injury posts >=1 structured body
        // part (POST /appointment-body-parts, OBS-41) and the optional
        // Authorized Users step posts accessors (POST /appointment-accessors).
        // Both go through permission-gated standalone AppServices, so the
        // canonical booker needs their Creates too -- otherwise the body-part
        // POST 403s mid-submit (after CE + injury succeed) and aborts the
        // auto-approve, leaving a half-built Pending appointment.
        yield return Create("AppointmentBodyParts");
        yield return Create("AppointmentAccessors");

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

        // G-08-01 (2026-06-06): Appointment Request Report (read-only) + G-08-03 PDF
        // export. Intake Staff is a primary report audience (the front-desk worklist).
        yield return Default("Reports");
        yield return $"{Group}.Reports.Export";

        // Phase 2.5 (2026-05-01) -- intake staff is the front-line approver
        // for new bookings. Change requests are read-only at this tier; only
        // the supervisor finalizes cancel / reschedule outcomes.
        yield return Approve("Appointments");
        yield return Reject("Appointments");
        yield return Default("AppointmentChangeRequests");
        yield return Default("SystemParameters");

        // Phase A (2026-05-05) -- intake staff uploads a signature so OLD
        // packets they are responsible for include a stamped image. Mirrors
        // OLD, where the receptionist, IT-admin, and supervisor tiers were
        // the three roles that could upload via the My-Profile page.
        yield return Default("UserSignatures");
        yield return $"{Group}.UserSignatures.ManageOwn";

        // 2026-05-15 -- front-desk intake staff is the most common
        // inviter (intake phone calls from prospective patients).
        // Same grant shape as the other two internal roles -- the
        // server gate is the same permission for all three.
        yield return Default("UserManagement");
        yield return $"{Group}.UserManagement.InviteExternalUser";
    }

}
