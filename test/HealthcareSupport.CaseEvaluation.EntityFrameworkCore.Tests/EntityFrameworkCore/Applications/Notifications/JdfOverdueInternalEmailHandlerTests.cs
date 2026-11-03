using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// <see cref="JdfOverdueInternalEmailHandler"/> on the real rig: an overdue joint declaration is an
/// internal problem, so since 2026-11-02 it goes to the office inbox and the office's switched-on Intake
/// Staff only, once each (Staff Supervisors get no per-event mail). A user in another role is seeded as a decoy, because "internal only" is the guarantee --
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
    public async Task EmailsTheOfficeInboxAndEachIntakeUserOnce_WithTheDocumentName_AndNoSupervisorOrOtherRole()
    {
        var staff = await SeedStaffAsync();
        await SetOfficeInboxAsync(OfficeInbox);

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentJointDeclarationOverdueInternal);
        sent.To.ShouldBeNull();
        sent.Recipients.ShouldBe(new[] { OfficeInbox, staff.Intake }, ignoreOrder: true);
        sent.Recipients.ShouldContain(OfficeInbox);
        sent.Recipients.ShouldContain(staff.Intake);
        sent.Recipients.Select(r => r.ToLowerInvariant()).Distinct().Count().ShouldBe(sent.Recipients.Count, "no address twice");
        sent.Recipients.ShouldNotContain(staff.Supervisor);
        sent.Recipients.ShouldNotContain(staff.Decoy);
        sent.Recipients.ShouldNotContain(staff.OtherOfficeSupervisor);
        sent.Recipients.ShouldNotContain(staff.OtherOfficeIntake);
        sent.Variables.Values.ShouldContain("Joint Declaration Form");
        sent.Variables.Values.ShouldContain(AppointmentsTestData.Appointment1RequestConfirmationNumber);
    }

    /// <summary>
    /// 2026-10-27: the email goes only to staff, who sign in on the staff site; a staff login does not work
    /// on the office site, so the link is the staff site's.
    /// </summary>
    [Fact]
    public async Task TheLink_IsTheStaffSite_NotTheOfficeSite()
    {
        await SeedStaffAsync();
        var urls = GetRequiredService<IAccountUrlBuilder>();
        var staffSite = await WithUnitOfWorkAsync(() => urls.BuildPortalRootUrlAsync(null));
        var officeSite = await WithUnitOfWorkAsync(() => urls.BuildPortalRootUrlAsync(TenantsTestData.TenantARef));
        staffSite.ShouldNotBe(officeSite, "the two sites must differ or this test proves nothing");

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Variables["PortalUrl"].ShouldBe(staffSite);
    }

    /// <summary>The inbox counts as a person: when it is an Intake user's address (any case) it gets one email.</summary>
    [Fact]
    public async Task AnInboxThatIsAlsoAnIntakeAddress_IsEmailedOnce_IgnoringCase()
    {
        var staff = await SeedStaffAsync();
        await SetOfficeInboxAsync(staff.Intake.ToUpperInvariant());

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.Count.ShouldBe(1);
        sent.Recipients[0].ShouldBe(staff.Intake, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task WithNoOfficeInboxSet_OnlyTheIntakeStaffAreEmailed()
    {
        var staff = await SeedStaffAsync();

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { staff.Intake });
    }

    [Fact]
    public async Task ASwitchedOffStaffUser_IsNotEmailed()
    {
        var staff = await SeedStaffAsync();
        var switchedOff = await InOfficeAAsync((users, roles) => RoleUserSeeder.CreateInactiveUserInRoleAsync(
            users, roles, TenantsTestData.TenantARef, "Intake Staff", "jdf-off"));

        await RaiseAsync(AppointmentsTestData.Appointment1Id);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { staff.Intake });
        sent.Recipients.ShouldNotContain(switchedOff);
    }

    private const string OfficeInbox = "TEST-jdf-office-inbox@test.local";

    private Task SetOfficeInboxAsync(string inbox) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
        }
    });

    private Task<string> InOfficeAAsync(Func<IdentityUserManager, IdentityRoleManager, Task<string>> seed) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                return await seed(GetRequiredService<IdentityUserManager>(), GetRequiredService<IdentityRoleManager>());
            }
        });

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
    /// A Staff Supervisor (who must get nothing), an Intake Staff
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
