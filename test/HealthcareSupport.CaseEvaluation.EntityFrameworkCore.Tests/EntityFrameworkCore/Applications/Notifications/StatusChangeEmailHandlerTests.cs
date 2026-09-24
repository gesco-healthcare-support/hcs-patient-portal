using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
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

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _recipients = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_recipients));
    }

    // ---- Approved: the stakeholder email To the booker, and the responsible user's own email ----

    [Fact]
    public async Task Approved_EmailsTheBookerWithTheStakeholderCcd_AndTheResponsibleUserByName()
    {
        WithStakeholder();
        await SetResponsibleUserNameAsync("TEST-First", "TEST-Last");
        await ChangeAppointmentAsync(a =>
        {
            a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId;
            a.InternalUserComments = StaffNote;
        });

        await RaiseAsync(AppointmentStatusType.Approved);

        _dispatcher.Dispatches.Count.ShouldBe(2);
        var ext = _dispatcher.Dispatches.Single(d => d.TemplateCode == NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        ext.To.ShouldNotBeNullOrWhiteSpace();
        ext.Recipients.ShouldContain(StakeholderEmail);
        ext.Recipients.ShouldAllBe(r => !string.IsNullOrWhiteSpace(r)); // the blank-address decoy was dropped
        ext.Variables["InternalUserComments"].ShouldBe("<b> Please note: </b>" + StaffNote);

        var internalLeg = _dispatcher.Dispatches.Single(d => d.TemplateCode == NotificationTemplateConsts.Codes.PatientAppointmentApprovedInternal);
        internalLeg.To.ShouldBeNull();
        internalLeg.Recipients.ShouldBe(new[] { IdentityUsersTestData.TenantAdmin1Email });
        internalLeg.Variables["ResponsibleUserName"].ShouldBe("TEST-First TEST-Last");
        internalLeg.Variables["InternalUserComments"].ShouldBe("<b> Staff comments for an appointment: </b>" + StaffNote);
    }

    [Fact]
    public async Task Approved_GreetsTheResponsibleUserByFirstName_WhenThereIsNoSurname()
    {
        WithStakeholder();
        await SetResponsibleUserNameAsync("TEST-First", null);
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId);

        await RaiseAsync(AppointmentStatusType.Approved);

        InternalLeg().Variables["ResponsibleUserName"].ShouldBe("TEST-First");
    }

    [Fact]
    public async Task Approved_GreetsTheResponsibleUserBySurname_WhenThereIsNoFirstName()
    {
        WithStakeholder();
        await SetResponsibleUserNameAsync(null, "TEST-Last");
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId);

        await RaiseAsync(AppointmentStatusType.Approved);

        InternalLeg().Variables["ResponsibleUserName"].ShouldBe("TEST-Last");
    }

    [Fact]
    public async Task Approved_GreetsTheResponsibleUserByEmail_WhenTheyHaveNoName()
    {
        WithStakeholder();
        await SetResponsibleUserNameAsync(null, null);
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = IdentityUsersTestData.TenantAdmin1UserId);

        await RaiseAsync(AppointmentStatusType.Approved);

        InternalLeg().Variables["ResponsibleUserName"].ShouldBe(IdentityUsersTestData.TenantAdmin1Email);
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
    public async Task Approved_SkipsTheInternalEmail_WhenTheResponsibleUserCannotBeFound()
    {
        WithStakeholder();
        await ChangeAppointmentAsync(a => a.PrimaryResponsibleUserId = Guid.NewGuid());

        await RaiseAsync(AppointmentStatusType.Approved);

        _dispatcher.Dispatches.Select(d => d.TemplateCode)
            .ShouldBe(new[] { NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt });
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

    // ---- The four stakeholder statuses: they send to the booker, and skip with no stakeholders ----

    [Theory]
    [InlineData(AppointmentStatusType.Rejected, NotificationTemplateConsts.Codes.PatientAppointmentRejected)]
    [InlineData(AppointmentStatusType.CheckedIn, NotificationTemplateConsts.Codes.PatientAppointmentCheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut, NotificationTemplateConsts.Codes.PatientAppointmentCheckedOut)]
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
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
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

    private RecordedDispatch InternalLeg() => _dispatcher.Dispatches
        .Single(d => d.TemplateCode == NotificationTemplateConsts.Codes.PatientAppointmentApprovedInternal);

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

    private Task SetResponsibleUserNameAsync(string? name, string? surname) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
            var user = await repository.GetAsync(IdentityUsersTestData.TenantAdmin1UserId);
            user.Name = name;
            user.Surname = surname;
            await repository.UpdateAsync(user, autoSave: true);
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
