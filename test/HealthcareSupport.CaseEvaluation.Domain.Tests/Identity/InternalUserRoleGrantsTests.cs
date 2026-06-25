using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Pins the three internal roles' permission grant sets to the decided access
/// matrix (docs/plans/2026-06-16-internal-roles-access-model.md). Pure -- reads
/// the static grant generators, no DB. Security-sensitive: over-granting a
/// technical power to a business role, or losing a booking-required read for
/// Intake, must fail here.
/// </summary>
public class InternalUserRoleGrantsTests
{
    private static readonly HashSet<string> ItAdmin =
        InternalUserRoleDataSeedContributor.ItAdminGrants().ToHashSet();
    private static readonly HashSet<string> Supervisor =
        InternalUserRoleDataSeedContributor.StaffSupervisorGrants().ToHashSet();
    private static readonly HashSet<string> Intake =
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

    // ---- Staff Supervisor: business + the two 2026-06-16 additions ----
    [Theory]
    [InlineData("CaseEvaluation.SystemParameters.Edit")]
    [InlineData("AuditLogging.AuditLogs")]
    public void Supervisor_gains_sysparams_edit_and_audit_read(string permission) =>
        Supervisor.ShouldContain(permission);

    // ...but NONE of the technical / host powers.
    [Theory]
    [InlineData("AbpIdentity.Roles")]
    [InlineData("AbpIdentity.Roles.ManagePermissions")]
    [InlineData("FileManagement.FileDescriptor")]
    [InlineData("LanguageManagement.Languages")]
    [InlineData("Saas.Tenants")]
    [InlineData("Saas.Tenants.Create")]
    [InlineData("Saas.Tenants.Impersonation")]
    public void Supervisor_does_not_get_technical_powers(string permission) =>
        Supervisor.ShouldNotContain(permission);

    [Fact]
    public void Supervisor_keeps_business_essentials()
    {
        Supervisor.ShouldContain("CaseEvaluation.Appointments");
        Supervisor.ShouldContain("CaseEvaluation.NotificationTemplates.Edit");
        // Business admin manages their clinic staff (confirmed 2026-06-16).
        Supervisor.ShouldContain("CaseEvaluation.InternalUsers.Create");
        Supervisor.ShouldContain("CaseEvaluation.InternalUsers.Edit");
    }

    // ---- Intake: clinic-local operations + functional reads only ----
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
    public void Intake_excludes_config_and_technical(string permission) =>
        Intake.ShouldNotContain(permission);

    [Fact]
    public void Intake_keeps_operations_and_booking_reads()
    {
        Intake.ShouldContain("CaseEvaluation.Appointments.Create");
        Intake.ShouldContain("CaseEvaluation.Patients.Create");
        Intake.ShouldContain("CaseEvaluation.DoctorAvailabilities.Create");
        // Behind-the-scenes reads the booking flow needs (no management screen).
        Intake.ShouldContain("CaseEvaluation.SystemParameters");
        Intake.ShouldContain("CaseEvaluation.UserManagement.InviteExternalUser");
    }
}
