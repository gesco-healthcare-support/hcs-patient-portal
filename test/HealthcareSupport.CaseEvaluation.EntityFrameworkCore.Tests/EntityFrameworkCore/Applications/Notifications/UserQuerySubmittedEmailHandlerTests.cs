using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// <see cref="UserQuerySubmittedEmailHandler"/> on the real rig. Since 2026-11-02 a query naming one of the
/// office's appointments (any status) goes to the office inbox + the active Intake Staff; any other query
/// goes to the inbox + every active host IT Admin. (The retired responsible user is never mailed.) The
/// IT-Admin Facts seed a host user in ANOTHER role as a decoy, so "only IT Admins" is shown against
/// someone who could have been wrongly included; the office Facts seed a Staff Supervisor, a switched-off
/// Intake user and another office's Intake user, none of whom may be mailed.
/// </summary>
public class UserQuerySubmittedEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string Message = "TEST- a question from the portal";
    private const string OfficeInbox = "TEST-office-b-inbox@test.local";
    private const string OfficeAInbox = "TEST-office-a-inbox@test.local";

    private readonly RecordingNotificationDispatcher _dispatcher = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
    }

    // 2026-11-02: a question about one of THIS office's appointments (the number matches, any status) goes
    // to the office inbox + the active Intake Staff. Any other question goes to the inbox + every active
    // host IT Admin. The stored responsible user is retired and never mailed.

    [Fact]
    public async Task ApprovedAppointment_GoesToTheOfficeInboxAndIntake_WithTheCaseInTheSubject()
    {
        // Appointment2 is seeded Approved in office B; Patient2 is a switched-on office-B user with an email,
        // the exact user OLD's routing would have mailed.
        await ChangeAppointment2Async(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.Patient2UserId);
        await SetOfficeBInboxAsync(OfficeInbox);
        var staff = await SeedOfficeStaffAsync(TenantsTestData.TenantBRef, "query-b");
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.UserQuery);
        sent.Recipients.ShouldBe(new[] { OfficeInbox, staff.Intake }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(staff.Supervisor);
        sent.Recipients.ShouldNotContain(staff.SwitchedOff);
        sent.Recipients.ShouldNotContain(IdentityUsersTestData.Patient2Email);
        sent.Recipients.ShouldNotContain(admins.Admin);
        sent.Variables["UserQueryMessage"].ShouldBe(Message);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBeOfType<string>().ShouldStartWith(" - ");
    }

    [Fact]
    public async Task NonApprovedAppointmentOfThisOffice_GoesToTheInboxAndIntake_WithNoIdentityInTheSubject()
    {
        // Appointment1 is seeded Pending in office A: it still routes to the office, but the subject
        // names the case only for an Approved match.
        await ChangeAppointment1Async(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId);
        await SetOfficeInboxAsync(TenantsTestData.TenantARef, OfficeAInbox);
        var staff = await SeedOfficeStaffAsync(TenantsTestData.TenantARef, "query-a");
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment1RequestConfirmationNumber, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { OfficeAInbox, staff.Intake }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(IdentityUsersTestData.TenantAdmin1Email);
        sent.Recipients.ShouldNotContain(admins.Admin);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBe(string.Empty);
    }

    [Fact]
    public async Task MatchedAppointment_WithNoOfficeInbox_GoesToTheIntakeOnly_NotTheItAdmins()
    {
        var staff = await SeedOfficeStaffAsync(TenantsTestData.TenantBRef, "query-b-noinbox");
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { staff.Intake });
        sent.Recipients.ShouldNotContain(admins.Admin);
    }

    [Fact]
    public async Task MatchedAppointment_AnInboxThatIsAnIntakeAddress_IsEmailedOnce_IgnoringCase()
    {
        var staff = await SeedOfficeStaffAsync(TenantsTestData.TenantBRef, "query-b-dup");
        await SetOfficeBInboxAsync(staff.Intake.ToUpperInvariant());

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.Count.ShouldBe(1);
        sent.Recipients[0].ShouldBe(staff.Intake, StringCompareShould.IgnoreCase);
    }

    [Fact]
    public async Task MatchedAppointment_IntakeOfAnotherOffice_IsNeverMailed()
    {
        await SetOfficeBInboxAsync(OfficeInbox);
        var officeB = await SeedOfficeStaffAsync(TenantsTestData.TenantBRef, "query-b-own");
        var officeA = await SeedOfficeStaffAsync(TenantsTestData.TenantARef, "query-a-decoy");

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { OfficeInbox, officeB.Intake }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(officeA.Intake);
    }

    // A general question (no number, or one that matches nothing here): the inbox + every active IT Admin.

    [Fact]
    public async Task NoConfirmationNumber_GoesToTheInboxAndEveryItAdmin_NotTheIntakeOrTheOtherRole()
    {
        await SetOfficeBInboxAsync(OfficeInbox);
        var staff = await SeedOfficeStaffAsync(TenantsTestData.TenantBRef, "query-b-general");
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantBRef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { OfficeInbox, admins.Admin }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(admins.Decoy);
        sent.Recipients.ShouldNotContain(staff.Intake);
        sent.Recipients.ShouldNotContain(staff.Supervisor);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBe(string.Empty);
    }

    [Fact]
    public async Task NoConfirmationNumber_WithNoOfficeInbox_GoesToTheItAdminsOnly()
    {
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { admins.Admin });
        sent.Recipients.ShouldNotContain(admins.Decoy);
    }

    [Fact]
    public async Task ConfirmationNumberOfAnotherOfficesAppointment_IsAGeneralQuestion()
    {
        // The OFFICE decoy. Appointment2 is Approved in office B. Raised from office A, the number must not
        // resolve across offices, or a query would reach another office's staff with that appointment's
        // identity suffix in the subject.
        await ChangeAppointment2Async(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.Patient2UserId);
        await SetOfficeInboxAsync(TenantsTestData.TenantARef, OfficeAInbox);
        var officeA = await SeedOfficeStaffAsync(TenantsTestData.TenantARef, "query-a-general");
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(AppointmentsTestData.Appointment2RequestConfirmationNumber, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { OfficeAInbox, admins.Admin }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(officeA.Intake);
        sent.Recipients.ShouldNotContain(IdentityUsersTestData.Patient2Email);
        sent.Variables["UserQuerySubjectIdentity"].ShouldBe(string.Empty);
    }

    [Fact]
    public async Task AnotherOfficesInbox_IsNeverUsed()
    {
        // The OFFICE decoy for the inbox: office B has one, the question is asked in office A, which
        // has none. It must reach the IT Admins, never office B's inbox.
        await SetOfficeBInboxAsync(OfficeInbox);
        var admins = await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldNotContain(OfficeInbox);
        sent.Recipients.ShouldContain(admins.Admin);
    }

    [Fact]
    public async Task ASwitchedOffItAdmin_IsNotEmailed()
    {
        var admins = await SeedItAdminsAsync();
        var switchedOff = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(
                    GetRequiredService<IdentityUserManager>(), GetRequiredService<IdentityRoleManager>(),
                    null, InternalUserRoleDataSeedContributor.ItAdminRoleName, "query-itadmin-off");
            }
        });

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldContain(admins.Admin);
        sent.Recipients.ShouldNotContain(switchedOff);
    }

    [Fact]
    public async Task NobodyToTell_SendsNothing()
    {
        // Positive control for this path: NoConfirmationNumber_GoesToTheInboxAndEveryItAdmin_... above,
        // identical except that it seeds an inbox and an IT Admin.
        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMessage_SendsNothing(string message)
    {
        await SeedItAdminsAsync();

        await RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef, message);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTemplate_IsLoggedAndSwallowed_NotThrown()
    {
        await SeedItAdminsAsync();
        _dispatcher.ThrowTemplateNotFound = true;

        await Should.NotThrowAsync(() => RaiseAsync(confirmationNumber: null, TenantsTestData.TenantARef));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private async Task RaiseAsync(string? confirmationNumber, Guid tenantId, string message = Message)
    {
        _dispatcher.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<UserQuerySubmittedEmailHandler>().HandleEventAsync(
            new UserQuerySubmittedEto
            {
                UserQueryId = Guid.NewGuid(),
                Message = message,
                RequestConfirmationNumber = confirmationNumber,
                TenantId = tenantId,
                OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            }));
    }

    /// <summary>A host IT Admin, plus a host user in another role as the decoy.</summary>
    private async Task<(string Admin, string Decoy)> SeedItAdminsAsync()
    {
        string admin = string.Empty, decoy = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                admin = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, roles, null, InternalUserRoleDataSeedContributor.ItAdminRoleName, "query-itadmin");
                decoy = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, roles, null, "TEST-host-other-role", "query-decoy");
            }
        });
        return (admin, decoy);
    }

    /// <summary>An active Intake user, a Staff Supervisor and a switched-off Intake user in the office.</summary>
    private async Task<(string Intake, string Supervisor, string SwitchedOff)> SeedOfficeStaffAsync(Guid officeId, string label)
    {
        string intake = string.Empty, supervisor = string.Empty, switchedOff = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, officeId, "Intake Staff", label + "-intake");
                supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, officeId, "Staff Supervisor", label + "-supervisor");
                switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(users, roles, officeId, "Intake Staff", label + "-off");
            }
        });
        return (intake, supervisor, switchedOff);
    }

    private Task SetOfficeBInboxAsync(string inbox) => SetOfficeInboxAsync(TenantsTestData.TenantBRef, inbox);

    private Task SetOfficeInboxAsync(Guid tenantId, string inbox) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
        }
    });

    private Task ChangeAppointment1Async(Action<Appointment> change) =>
        ChangeAppointmentAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, change);

    private Task ChangeAppointment2Async(Action<Appointment> change) =>
        ChangeAppointmentAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, change);

    private Task ChangeAppointmentAsync(Guid tenantId, Guid appointmentId, Action<Appointment> change) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(tenantId))
            {
                var repository = GetRequiredService<IRepository<Appointment, Guid>>();
                var appointment = await repository.GetAsync(appointmentId);
                change(appointment);
                await repository.UpdateAsync(appointment, autoSave: true);
            }
        });
}
