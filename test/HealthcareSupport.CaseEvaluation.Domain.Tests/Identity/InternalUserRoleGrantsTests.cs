using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Pins the internal roles' permission grant sets to the decided access matrix.
/// Pure -- reads the static grant generators, no DB. Security-sensitive:
/// over-granting a technical power to a business role, or losing a booking-
/// required read for the in-office Intake identity, must fail here.
///
/// Phase D (2026-06-25): Staff Supervisor + the Intake operator are now HOST
/// operators (single host login each). The host Supervisor switches into an
/// office as its admin; the host Intake operator holds only the gated
/// office-switch capability and lands as the per-tenant Intake Staff shadow
/// user. The per-tenant "Intake Staff" grant set
/// (<see cref="InternalUserRoleDataSeedContributor.IntakeStaffGrants"/>) is what
/// the shadow user holds and is unchanged from the front-desk tier.
/// </summary>
public class InternalUserRoleGrantsTests
{
    private static readonly HashSet<string> ItAdmin =
        InternalUserRoleDataSeedContributor.ItAdminGrants().ToHashSet();
    private static readonly HashSet<string> SupervisorHost =
        InternalUserRoleDataSeedContributor.StaffSupervisorHostGrants().ToHashSet();
    private static readonly HashSet<string> IntakeOperatorHost =
        InternalUserRoleDataSeedContributor.IntakeOperatorHostGrants().ToHashSet();
    private static readonly HashSet<string> IntakeShadow =
        InternalUserRoleDataSeedContributor.IntakeStaffGrants().ToHashSet();

    // ---- IT Admin: the technical platform admin holds the framework powers ----
    [Theory]
    [InlineData("AbpIdentity.Roles")]
    [InlineData("AbpIdentity.Roles.ManagePermissions")]
    [InlineData("AuditLogging.AuditLogs")]
    [InlineData("FileManagement.FileDescriptor")]
    [InlineData("FileManagement.FileDescriptor.Delete")]
    [InlineData("FileManagement.DirectoryDescriptor")]
    [InlineData("FileManagement.DirectoryDescriptor.Create")]
    [InlineData("LanguageManagement.Languages")]
    [InlineData("LanguageManagement.Languages.Create")]
    [InlineData("LanguageManagement.Languages.ChangeDefault")]
    [InlineData("Saas.Tenants")]
    [InlineData("Saas.Tenants.Create")]
    [InlineData("Saas.Tenants.Impersonation")]
    public void ItAdmin_has_framework_powers(string permission) => ItAdmin.ShouldContain(permission);

    // ---- Host Staff Supervisor: cross-office operator (switch-in-as-admin) ----
    [Theory]
    [InlineData("CaseEvaluation.Dashboard.Host")]
    [InlineData("Saas.Tenants")]
    [InlineData("Saas.Tenants.Impersonation")]
    [InlineData("CaseEvaluation.IntakeAssignments")]
    [InlineData("CaseEvaluation.IntakeAssignments.Manage")]
    [InlineData("CaseEvaluation.InternalUsers.Create")]
    [InlineData("CaseEvaluation.InternalUsers.Edit")]
    public void SupervisorHost_has_operator_powers(string permission) =>
        SupervisorHost.ShouldContain(permission);

    // ...but NOT the IT-Admin-only framework powers, NOT tenant-create, and NOT
    // the deep tenant operational CRUD (it gets those by switching in AS admin,
    // not by holding them at host scope), NOT the intake-only switch capability.
    [Theory]
    [InlineData("AbpIdentity.Roles")]
    [InlineData("AbpIdentity.Roles.ManagePermissions")]
    [InlineData("FileManagement.FileDescriptor")]
    [InlineData("LanguageManagement.Languages")]
    [InlineData("Saas.Tenants.Create")]
    [InlineData("CaseEvaluation.Dashboard.Tenant")]
    [InlineData("CaseEvaluation.Appointments.Create")]
    [InlineData("CaseEvaluation.Patients.Create")]
    [InlineData("CaseEvaluation.IntakeImpersonation")]
    public void SupervisorHost_excludes_technical_and_tenant_crud(string permission) =>
        SupervisorHost.ShouldNotContain(permission);

    // ---- Host Intake operator: ONLY the gated office-switch capability ----
    [Fact]
    public void IntakeOperatorHost_holds_only_the_switch_capability()
    {
        IntakeOperatorHost.ShouldContain("CaseEvaluation.IntakeImpersonation");
        IntakeOperatorHost.Count.ShouldBe(1);
    }

    // ---- Phase E: per-office branding (name + logo). Both host operators manage it
    // from the host-side central manager; the per-office admin auto-holds the Both-
    // sided permission and edits in-office (not pinned here -- ABP grants it). ----
    [Fact]
    public void Branding_grantedToHostOperators()
    {
        ItAdmin.ShouldContain("CaseEvaluation.Branding");
        ItAdmin.ShouldContain("CaseEvaluation.Branding.Edit");
        SupervisorHost.ShouldContain("CaseEvaluation.Branding");
        SupervisorHost.ShouldContain("CaseEvaluation.Branding.Edit");
    }

    [Theory]
    [InlineData("Saas.Tenants")]
    [InlineData("Saas.Tenants.Impersonation")]
    [InlineData("CaseEvaluation.Dashboard.Host")]
    [InlineData("CaseEvaluation.IntakeAssignments")]
    [InlineData("CaseEvaluation.IntakeAssignments.Manage")]
    [InlineData("CaseEvaluation.Appointments.Create")]
    [InlineData("CaseEvaluation.InternalUsers.Create")]
    public void IntakeOperatorHost_excludes_admin_and_management_powers(string permission) =>
        IntakeOperatorHost.ShouldNotContain(permission);

    // ---- Per-tenant Intake Staff (the shadow user's identity): clinic-local ----
    // operations + functional reads only; never config / technical / host powers.
    [Theory]
    [InlineData("CaseEvaluation.NotificationTemplates")]
    [InlineData("CaseEvaluation.NotificationTemplates.Edit")]
    [InlineData("CaseEvaluation.SystemParameters.Edit")]
    [InlineData("CaseEvaluation.InternalUsers")]
    [InlineData("CaseEvaluation.InternalUsers.Create")]
    [InlineData("AbpIdentity.Roles")]
    [InlineData("AuditLogging.AuditLogs")]
    [InlineData("FileManagement.FileDescriptor")]
    [InlineData("Saas.Tenants")]
    [InlineData("Saas.Tenants.Impersonation")]
    [InlineData("CaseEvaluation.IntakeImpersonation")]
    public void IntakeShadow_excludes_config_and_technical(string permission) =>
        IntakeShadow.ShouldNotContain(permission);

    [Fact]
    public void IntakeShadow_keeps_operations_and_booking_reads()
    {
        IntakeShadow.ShouldContain("CaseEvaluation.Appointments.Create");
        IntakeShadow.ShouldContain("CaseEvaluation.Patients.Create");
        IntakeShadow.ShouldContain("CaseEvaluation.DoctorAvailabilities.Create");
        // Behind-the-scenes reads the booking flow needs (no management screen).
        IntakeShadow.ShouldContain("CaseEvaluation.SystemParameters");
        IntakeShadow.ShouldContain("CaseEvaluation.UserManagement.InviteExternalUser");
    }
}
