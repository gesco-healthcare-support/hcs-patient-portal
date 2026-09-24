using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// <see cref="JdfOverdueInternalEmailHandler"/> on the real rig: an overdue joint declaration is an
/// internal problem, so it goes to the office's Staff Supervisor and Intake Staff users only, once
/// each. A user in another role is seeded as a decoy, because "internal only" is the guarantee --
/// telling a patient's attorney that a form is late would leak an internal workflow problem. The same
/// two roles are also seeded in office B, because "the office's staff" is the other half of it.
/// </summary>
public class JdfOverdueInternalEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly RecordingNotificationDispatcher _dispatcher = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
    }

    [Fact]
    public async Task EmailsEachInternalStaffUserOnce_WithTheDocumentName_AndNoOtherRole()
    {
        var staff = await SeedStaffAsync();

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentJointDeclarationOverdueInternal);
        sent.To.ShouldBeNull();
        sent.Recipients.ShouldBe(new[] { staff.Supervisor, staff.Intake }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(staff.Decoy);
        sent.Recipients.ShouldNotContain(staff.OtherOfficeSupervisor);
        sent.Recipients.ShouldNotContain(staff.OtherOfficeIntake);
        sent.Variables.Values.ShouldContain("Joint Declaration Form");
        sent.Variables.Values.ShouldContain(AppointmentsTestData.Appointment1RequestConfirmationNumber);
    }

    [Fact]
    public async Task NoInternalStaff_SendsNothing()
    {
        // Positive control: the Fact above, identical except that it seeds the staff.
        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownAppointment_SendsNothing_EvenWithStaffPresent()
    {
        await SeedStaffAsync();

        await RaiseAsync(Guid.NewGuid());

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        await SeedStaffAsync();
        _dispatcher.Clear();

        await WithUnitOfWorkAsync(() => Handler().HandleEventAsync(null!));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private JdfOverdueInternalEmailHandler Handler() => GetRequiredService<JdfOverdueInternalEmailHandler>();

    private async Task RaiseAsync(Guid appointmentId)
    {
        _dispatcher.Clear();
        await WithUnitOfWorkAsync(() => Handler().HandleEventAsync(new AppointmentJointDeclarationOverdueEto
        {
            AppointmentId = appointmentId,
            TenantId = TenantsTestData.TenantARef,
            OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
        }));
    }

    /// <summary>
    /// A Staff Supervisor who ALSO holds Intake Staff (so the dedup is exercised), an Intake Staff
    /// user, and a decoy in another role, all in office A. Plus the OFFICE decoys: a Staff Supervisor
    /// and an Intake Staff user in office B, the right roles in the wrong office, which must never
    /// hear about office A's appointment.
    /// </summary>
    private async Task<(string Supervisor, string Intake, string Decoy, string OtherOfficeSupervisor, string OtherOfficeIntake)> SeedStaffAsync()
    {
        string supervisor = string.Empty, intake = string.Empty, decoy = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var office = TenantsTestData.TenantARef;
                supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Staff Supervisor", "jdf-supervisor");
                intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Intake Staff", "jdf-intake");
                decoy = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "TEST-other-role", "jdf-decoy");
                (await users.AddToRoleAsync((await users.FindByEmailAsync(supervisor))!, "Intake Staff")).Succeeded.ShouldBeTrue();
            }
        });

        string otherSupervisor = string.Empty, otherIntake = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantBRef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var office = TenantsTestData.TenantBRef;
                await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, office, "Staff Supervisor", 1);
                await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, office, "Intake Staff", 2);
                otherSupervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Staff Supervisor", "jdf-officeb-supervisor");
                otherIntake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, office, "Intake Staff", "jdf-officeb-intake");
            }
        });
        return (supervisor, intake, decoy, otherSupervisor, otherIntake);
    }
}
