using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
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
/// <see cref="StatusChangeEmailHandler"/> on the real rig: real appointment, real context resolver,
/// real users and roles. Two seams are replaced, and they are the only two the assertions need:
/// <list type="bullet">
///   <item><description><see cref="INotificationDispatcher"/> records instead of sending, so no email
///   sender is resolved at all;</description></item>
///   <item><description><see cref="IAppointmentRecipientResolver"/> returns the stakeholders each test
///   chooses, including a blank-address decoy the handler must drop.</description></item>
/// </list>
/// Every "sends nothing" Fact sits beside a Fact that sends, on the same appointment, so a silent
/// test cannot pass because the handler never ran.
/// </summary>
public class StatusChangeEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string StakeholderEmail = "TEST-status-stakeholder@test.local";
    private const string StaffNote = "TEST- staff note";
    private const string OfficeInbox = "TEST-office-inbox@test.local";

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _recipients = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_recipients));
    }

    // ---- Approved: ONE email, To the booker with the stakeholders CC'd ----
    // 2026-11-02: the responsible user is retired. OLD's second email to them (PatientAppointmentApprovedInternal)
    // is no longer sent, whatever PrimaryResponsibleUserId an older appointment still stores.

    [Fact]
    public async Task Approved_EmailsTheBookerWithTheStakeholderCcd_AndNotAStoredResponsibleUser()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a =>
        {
            a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId; // a switched-on staff account
            a.InternalUserComments = StaffNote;
        });

        await RaiseAsync(AppointmentStatusType.Approved);

        var ext = _dispatcher.Dispatches.ShouldHaveSingleItem();
        ext.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        ext.To.ShouldNotBeNullOrWhiteSpace();
        ext.Recipients.ShouldContain(StakeholderEmail);
        ext.Recipients.ShouldAllBe(r => !string.IsNullOrWhiteSpace(r)); // the blank-address decoy was dropped
        ext.Recipients.ShouldNotContain(IdentityUsersTestData.TenantAdmin1Email);
        ext.Variables["InternalUserComments"].ShouldBe("<b> Please note: </b>" + StaffNote);
    }

    [Fact]
    public async Task Approved_SendsOnlyTheStakeholderEmail_WhenNoResponsibleUserIsSet()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = null);

        await RaiseAsync(AppointmentStatusType.Approved);

        _dispatcher.Dispatches.Select(d => d.TemplateCode)
            .ShouldBe(new[] { NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt });
    }

    [Fact]
    public async Task Approved_AStoredSwitchedOffResponsibleUser_NoLongerSendsTheOfficeInboxASecondEmail()
    {
        // Until 2026-11-02 the office inbox stood in for a switched-off responsible user with its own email.
        WithStakeholder();
        var (switchedOffId, switchedOffEmail) = await SeedSwitchedOffStaffAsync("approved-left");
        await SetOfficeInboxAsync(OfficeInbox);
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = switchedOffId);

        await RaiseAsync(AppointmentStatusType.Approved);

        var ext = _dispatcher.Dispatches.ShouldHaveSingleItem();
        ext.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        ext.Recipients.ShouldNotContain(switchedOffEmail);
    }

    // ---- Rejected ----

    [Fact]
    public async Task Rejected_CarriesTheAppointmentsRejectionNotes()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a => a.RejectionNotes = "TEST- rejected on the appointment");

        await RaiseAsync(AppointmentStatusType.Rejected, reason: "TEST- reason on the event");

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientAppointmentRejected);
        sent.Recipients.ShouldContain(StakeholderEmail);
        sent.Variables.Values.ShouldContain("TEST- rejected on the appointment");
    }

    [Fact]
    public async Task Rejected_FallsBackToTheEventsReason_WhenTheAppointmentHasNoNotes()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a => a.RejectionNotes = null);

        await RaiseAsync(AppointmentStatusType.Rejected, reason: "TEST- reason on the event");

        _dispatcher.Dispatches.ShouldHaveSingleItem().Variables.Values.ShouldContain("TEST- reason on the event");
    }

    // ---- The stakeholder statuses: they send to the booker, and skip with no stakeholders ----

    [Theory]
    [InlineData(AppointmentStatusType.Rejected, NotificationTemplateConsts.Codes.PatientAppointmentRejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill, NotificationTemplateConsts.Codes.PatientAppointmentCancelledNoBill)]
    public async Task StakeholderStatus_EmailsTheBookerWithTheStakeholderCcd(AppointmentStatusType status, string template)
    {
        WithStakeholder();

        await RaiseAsync(status);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(template);
        sent.To.ShouldNotBeNullOrWhiteSpace();
        sent.Recipients.ShouldContain(StakeholderEmail);
    }

    [Theory]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    public async Task StakeholderStatus_SendsNothing_WhenEveryStakeholderHasABlankAddress(AppointmentStatusType status)
    {
        // Present but unusable: the handler drops blank addresses, which leaves no stakeholders.
        _recipients.Recipients.Add(new SendAppointmentEmailArgs { To = " ", Role = RecipientRole.Patient });

        await RaiseAsync(status);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelledNoBill_CarriesTheCancellationReason()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a => a.CancellationReason = "TEST- cancelled for the test");

        await RaiseAsync(AppointmentStatusType.CancelledNoBill);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Variables["CancellationReason"]
            .ShouldBe("TEST- cancelled for the test");
    }

    // ---- NoShow: internal staff only, once each, nobody else ----

    [Fact]
    public async Task NoShow_DoesNotEmailASwitchedOffStaffUser()
    {
        string intake = string.Empty, switchedOff = string.Empty;
        await InTenantAAsync(async (users, roles) =>
        {
            intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Intake Staff", "noshow-on");
            switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Intake Staff", "noshow-off");
        });

        await RaiseAsync(AppointmentStatusType.NoShow);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.Recipients.ShouldBe(new[] { intake });
        sent.Recipients.ShouldNotContain(switchedOff);
    }

    [Fact]
    public async Task NoShow_EmailsEachStaffSupervisorAndIntakeStaffUserOnce_AndNoOtherRole()
    {
        WithStakeholder(); // present on purpose: NoShow must NOT use the stakeholder set
        string supervisor = string.Empty, intake = string.Empty, decoy = string.Empty;
        await InTenantAAsync(async (users, roles) =>
        {
            supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Staff Supervisor", "noshow-supervisor");
            intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Intake Staff", "noshow-intake");
            decoy = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "TEST-other-role", "noshow-decoy");
            // The supervisor also holds Intake Staff, so the dedup is what keeps them to one email.
            (await users.AddToRoleAsync((await users.FindByEmailAsync(supervisor))!, "Intake Staff")).Succeeded.ShouldBeTrue();
        });
        // The OFFICE decoys: the right two roles in the wrong office.
        string otherOfficeSupervisor = string.Empty, otherOfficeIntake = string.Empty;
        await InOfficeAsync(TenantsTestData.TenantBRef, async (users, roles) =>
        {
            await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, TenantsTestData.TenantBRef, "Staff Supervisor", 1);
            await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, TenantsTestData.TenantBRef, "Intake Staff", 2);
            otherOfficeSupervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantBRef, "Staff Supervisor", "noshow-officeb-supervisor");
            otherOfficeIntake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantBRef, "Intake Staff", "noshow-officeb-intake");
        });

        await RaiseAsync(AppointmentStatusType.NoShow);

        var sent = _dispatcher.Dispatches.ShouldHaveSingleItem();
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.PatientAppointmentNoShow);
        sent.To.ShouldBeNull();
        sent.Recipients.ShouldBe(new[] { supervisor, intake }, ignoreOrder: true);
        sent.Recipients.ShouldNotContain(decoy);
        sent.Recipients.ShouldNotContain(StakeholderEmail);
        sent.Recipients.ShouldNotContain(otherOfficeSupervisor);
        sent.Recipients.ShouldNotContain(otherOfficeIntake);
    }

    // ---- Guards ----

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        WithStakeholder();

        await WithUnitOfWorkAsync(() => Handler().HandleEventAsync(null!));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownAppointment_SendsNothing()
    {
        WithStakeholder();

        await RaiseAsync(AppointmentStatusType.Rejected, appointmentId: Guid.NewGuid());

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingTemplate_IsLoggedAndSwallowed_NotThrown()
    {
        WithStakeholder();
        _dispatcher.ThrowTemplateNotFound = true;

        // A status email is a side effect of a committed change; a missing template must not undo it.
        await Should.NotThrowAsync(() => RaiseAsync(AppointmentStatusType.Rejected));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------------

    private StatusChangeEmailHandler Handler() => GetRequiredService<StatusChangeEmailHandler>();

    /// <summary>One real stakeholder plus a blank-address decoy the handler must drop.</summary>
    private void WithStakeholder()
    {
        _recipients.Recipients.Add(new SendAppointmentEmailArgs { To = StakeholderEmail, Role = RecipientRole.ApplicantAttorney });
        _recipients.Recipients.Add(new SendAppointmentEmailArgs { To = "", Role = RecipientRole.DefenseAttorney });
    }

    private async Task RaiseAsync(AppointmentStatusType status, Guid? appointmentId = null, string? reason = null)
    {
        _dispatcher.Clear();
        await WithUnitOfWorkAsync(() => Handler().HandleEventAsync(new AppointmentStatusChangedEto
        {
            AppointmentId = appointmentId ?? AppointmentsTestData.Appointment1Id,
            TenantId = TenantsTestData.TenantARef,
            ToStatus = status,
            Reason = reason,
            OccurredAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
        }));
    }

    private Task ChangeAppointmentAsync(Action<Appointment> change) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await repository.GetAsync(AppointmentsTestData.Appointment1Id);
            change(appointment);
            await repository.UpdateAsync(appointment, autoSave: true);
        }
    });

    /// <summary>A switched-off Intake Staff account in office A: its role stays, its login is off.</summary>
    private async Task<(Guid Id, string Email)> SeedSwitchedOffStaffAsync(string label)
    {
        (Guid Id, string Email) seeded = default;
        await InTenantAAsync(async (users, roles) =>
        {
            var email = await RoleUserSeeder.CreateInactiveUserInRoleAsync(
                users, roles, TenantsTestData.TenantARef, "Intake Staff", label);
            seeded = ((await users.FindByEmailAsync(email))!.Id, email);
        });
        return seeded;
    }

    private Task SetOfficeInboxAsync(string inbox) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
        }
    });

    private Task InTenantAAsync(Func<IdentityUserManager, IdentityRoleManager, Task> action) =>
        InOfficeAsync(TenantsTestData.TenantARef, action);

    private Task InOfficeAsync(Guid officeId, Func<IdentityUserManager, IdentityRoleManager, Task> action) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(officeId))
        {
            await action(GetRequiredService<IdentityUserManager>(), GetRequiredService<IdentityRoleManager>());
        }
    });
}
