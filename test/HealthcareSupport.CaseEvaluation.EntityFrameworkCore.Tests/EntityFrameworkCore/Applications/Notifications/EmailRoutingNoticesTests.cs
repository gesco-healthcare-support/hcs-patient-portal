using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// The new and changed email notices of the office's routing change (2026-11-03, PR B2a), on the real rig:
/// real appointment, real users and roles, real local event bus, so every subscribed handler runs. Only the
/// dispatcher (records) and the party resolver (the parties each test chooses) are replaced. Each Fact pins a
/// row's must-receive AND must-not-receive set from the routing oracle: rows 7 (reschedule request), 8
/// (consent info), 9d (decline) and 15 (appointment reminder).
/// </summary>
public class EmailRoutingNoticesTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string PatientEmail = "TEST-notice-patient@test.local";
    private const string ApplicantAttorney = "TEST-notice-aa@test.local";
    private const string DefenseAttorney = "TEST-notice-da@test.local";
    private const string ClaimExaminer = "TEST-notice-ce@test.local";
    private const string OfficeInbox = "TEST-notice-inbox@test.local";

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    // ---- Row 7: a reschedule REQUEST -------------------------------------------------------------

    /// <summary>
    /// An attorney asks to reschedule: the parties, the office inbox and the Intake Staff are told, each
    /// address once. NOT the patient (somebody else asked), a Supervisor, or a switched-off Intake user.
    /// </summary>
    [Fact]
    public async Task ARescheduleRequestAnAttorneyFiled_GoesToThePartiesInboxAndIntake_NotThePatient()
    {
        WithParties();
        await SetPatientAddressAsync();
        var (intake, supervisor, switchedOff) = await SeedOfficeStaffAsync("resched");
        var attorneyLogin = await SeedUserAsync("Applicant Attorney", "resched-aa", email: ApplicantAttorney);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule, attorneyLogin);

        var notice = _dispatcher.Dispatches.ShouldHaveSingleItem();
        notice.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest);
        notice.Recipients.ShouldBe(
            new[] { ApplicantAttorney, DefenseAttorney, ClaimExaminer, OfficeInbox, intake }, ignoreOrder: true);
        notice.Recipients.ShouldNotContain(PatientEmail);
        notice.Recipients.ShouldNotContain(supervisor);
        notice.Recipients.ShouldNotContain(switchedOff);
        ((string)notice.Variables["ConsentDetailsBlock"]!).ShouldNotContain("Requested new date");
    }

    /// <summary>The same request, filed by the patient (confirmed email): the patient is told too.</summary>
    [Fact]
    public async Task ARescheduleRequestThePatientFiled_AlsoGoesToThePatient()
    {
        WithParties();
        await SetPatientAddressAsync();
        var patientLogin = await SeedUserAsync("Patient", "resched-patient", email: PatientEmail);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule, patientLogin);

        var notice = _dispatcher.Dispatches.ShouldHaveSingleItem();
        notice.Recipients.ShouldContain(PatientEmail);
        notice.Recipients.Count.ShouldBe(notice.Recipients.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // ---- Row 8: the consent INFO notice -----------------------------------------------------------

    /// <summary>
    /// Staff set a date: both attorneys hold consent links, so the information notice goes to the claim
    /// examiner, the inbox and the Intake Staff. NOT the patient, the link holders, a Supervisor or a
    /// switched-off Intake user.
    /// </summary>
    [Fact]
    public async Task ConsentInfo_GoesToTheClaimExaminerInboxAndIntake_NotThePatientOrTheLinkHolders()
    {
        WithParties();
        var (intake, supervisor, switchedOff) = await SeedOfficeStaffAsync("info");
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishOpenedAsync(changeRequestId, ApplicantAttorney, DefenseAttorney);

        var notice = _dispatcher.Dispatches.ShouldHaveSingleItem();
        notice.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.ChangeRequestConsentInfo);
        notice.Recipients.ShouldBe(new[] { ClaimExaminer, OfficeInbox, intake }, ignoreOrder: true);
        notice.Recipients.ShouldNotContain(PatientEmail);
        notice.Recipients.ShouldNotContain(supervisor);
        notice.Recipients.ShouldNotContain(switchedOff);
    }

    /// <summary>
    /// No defense attorney: the claim examiner IS the defense side's consent representative and holds a link,
    /// so that mail is their one email and the information notice leaves them out.
    /// </summary>
    [Fact]
    public async Task ConsentInfo_LeavesOutAClaimExaminerWhoHoldsTheDefenseSideLink()
    {
        WithParties();
        var (intake, _, _) = await SeedOfficeStaffAsync("info-ce");
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishOpenedAsync(changeRequestId, ApplicantAttorney, ClaimExaminer);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { OfficeInbox, intake }, ignoreOrder: true);
    }

    /// <summary>
    /// No applicant attorney: the PATIENT is the applicant side's consent representative and gets the link
    /// mail. They never get the information notice (it is not for the patient).
    /// </summary>
    [Fact]
    public async Task ConsentInfo_NeverGoesToThePatient_EvenWhenThePatientIsTheApplicantSideRep()
    {
        WithParties();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishOpenedAsync(changeRequestId, PatientEmail, DefenseAttorney);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldNotContain(PatientEmail);
    }

    // ---- Row 9d: a side DECLINES ------------------------------------------------------------------

    /// <summary>A decline tells the inbox and the Intake Staff at once, and nobody else.</summary>
    [Fact]
    public async Task ADecline_GoesToTheInboxAndIntakeOnly_NotThePartiesOrASupervisor()
    {
        WithParties();
        var (intake, supervisor, switchedOff) = await SeedOfficeStaffAsync("decline");
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishDeclinedAsync(changeRequestId, ChangeRequestSide.SideB);

        var notice = _dispatcher.Dispatches.ShouldHaveSingleItem();
        notice.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.ChangeRequestConsentDeclined);
        notice.Recipients.ShouldBe(new[] { OfficeInbox, intake }, ignoreOrder: true);
        notice.Recipients.ShouldNotContain(PatientEmail);
        notice.Recipients.ShouldNotContain(ApplicantAttorney);
        notice.Recipients.ShouldNotContain(DefenseAttorney);
        notice.Recipients.ShouldNotContain(ClaimExaminer);
        notice.Recipients.ShouldNotContain(supervisor);
        notice.Recipients.ShouldNotContain(switchedOff);
        notice.Variables["ChangeActionLabel"].ShouldBe("reschedule");
    }

    // ---- Row 15: the appointment REMINDER ---------------------------------------------------------

    /// <summary>
    /// An Approved appointment 7 days out: the patient, the inbox and the Intake Staff get the reminder with
    /// "in 7 days". NOT the attorneys, the claim examiner, a Supervisor or a switched-off Intake user.
    /// </summary>
    [Fact]
    public async Task TheReminder_GoesToThePatientInboxAndIntake_NobodyElse()
    {
        WithParties();
        await SetPatientAddressAsync();
        var (intake, supervisor, switchedOff) = await SeedOfficeStaffAsync("reminder");
        await ChangeAppointmentAsync(a => a.AppointmentStatus = AppointmentStatusType.Approved);

        _dispatcher.Clear();
        await PublishReminderAsync(daysUntil: 7);

        var reminder = _dispatcher.Dispatches.ShouldHaveSingleItem();
        reminder.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDayReminder);
        reminder.Recipients.ShouldBe(new[] { PatientEmail, OfficeInbox, intake }, ignoreOrder: true);
        reminder.Recipients.ShouldNotContain(ApplicantAttorney);
        reminder.Recipients.ShouldNotContain(DefenseAttorney);
        reminder.Recipients.ShouldNotContain(ClaimExaminer);
        reminder.Recipients.ShouldNotContain(supervisor);
        reminder.Recipients.ShouldNotContain(switchedOff);
        reminder.Variables["ReminderWhen"].ShouldBe("in 7 days");
    }

    /// <summary>The 7-day and the 1-day reminder each send once: their context tags differ by offset.</summary>
    [Fact]
    public async Task TheReminder_At7AndAt1Days_UsesOneContextTagPerOffset()
    {
        await SetPatientAddressAsync();
        await ChangeAppointmentAsync(a => a.AppointmentStatus = AppointmentStatusType.Approved);

        _dispatcher.Clear();
        await PublishReminderAsync(daysUntil: 7);
        await PublishReminderAsync(daysUntil: 1);

        _dispatcher.Dispatches.Count.ShouldBe(2);
        _dispatcher.Dispatches[0].Variables["ReminderWhen"].ShouldBe("in 7 days");
        _dispatcher.Dispatches[1].Variables["ReminderWhen"].ShouldBe("tomorrow");
        _dispatcher.Dispatches[0].ContextTag.ShouldBe($"AppointmentDayReminder/T-7/{AppointmentsTestData.Appointment1Id}");
        _dispatcher.Dispatches[1].ContextTag.ShouldBe($"AppointmentDayReminder/T-1/{AppointmentsTestData.Appointment1Id}");
    }

    /// <summary>An appointment that is no longer Approved (cancelled since the job ran) gets no reminder.</summary>
    [Fact]
    public async Task TheReminder_IsNotSentForAnAppointmentThatIsNoLongerApproved()
    {
        await SetPatientAddressAsync();
        await ChangeAppointmentAsync(a => a.AppointmentStatus = AppointmentStatusType.CancelledNoBill);

        _dispatcher.Clear();
        await PublishReminderAsync(daysUntil: 1);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    /// <summary>A patient whose own login has been switched off is not mailed; the office copies still go.</summary>
    [Fact]
    public async Task TheReminder_SkipsAPatientWhoseLoginIsSwitchedOff()
    {
        await SetPatientAddressAsync();
        var (intake, _, _) = await SeedOfficeStaffAsync("reminder-off");
        var switchedOffLogin = await SeedUserAsync("Patient", "reminder-off-patient", email: "TEST-notice-patient-login@test.local");
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var user = (await users.FindByIdAsync(switchedOffLogin.ToString()))!;
                user.SetIsActive(false);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();

                var patients = GetRequiredService<IRepository<Patient, Guid>>();
                var appointment = await GetRequiredService<IRepository<Appointment, Guid>>().GetAsync(AppointmentsTestData.Appointment1Id);
                var patient = await patients.GetAsync(appointment.PatientId);
                patient.IdentityUserId = switchedOffLogin;
                await patients.UpdateAsync(patient, autoSave: true);
            }
        });
        await ChangeAppointmentAsync(a => a.AppointmentStatus = AppointmentStatusType.Approved);

        _dispatcher.Clear();
        await PublishReminderAsync(daysUntil: 1);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { OfficeInbox, intake }, ignoreOrder: true);
    }

    // ------------------------------------------------------------------------

    private void WithParties()
    {
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ApplicantAttorney, Role = RecipientRole.ApplicantAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = DefenseAttorney, Role = RecipientRole.DefenseAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ClaimExaminer, Role = RecipientRole.ClaimExaminer, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = PatientEmail, Role = RecipientRole.Patient, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = OfficeInbox, Role = RecipientRole.OfficeAdmin, IsRegistered = true });
    }

    /// <summary>Gives the appointment the patient address the notices are tested against, and sets the office inbox.</summary>
    private Task SetPatientAddressAsync() => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, OfficeInbox);
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.PatientEmail = PatientEmail;
            await appointments.UpdateAsync(appointment, autoSave: true);

            // The seeded patient row carries its own address; the reminder mails the patient row first.
            var patients = GetRequiredService<IRepository<Patient, Guid>>();
            var patient = await patients.GetAsync(appointment.PatientId);
            patient.Email = PatientEmail;
            await patients.UpdateAsync(patient, autoSave: true);
        }
    });

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

    private async Task<Guid> SeedUserAsync(string role, string label, string email)
    {
        var id = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var address = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, GetRequiredService<IdentityRoleManager>(), TenantsTestData.TenantARef, role, label);
                var user = (await users.FindByEmailAsync(address))!;
                (await users.SetEmailAsync(user, email)).Succeeded.ShouldBeTrue();
                user.SetEmailConfirmed(true);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
                id = user.Id;
            }
        });
        return id;
    }

    /// <summary>Office A: an active Intake user, a Staff Supervisor, and a switched-off Intake user.</summary>
    private async Task<(string Intake, string Supervisor, string SwitchedOff)> SeedOfficeStaffAsync(string label)
    {
        string intake = string.Empty, supervisor = string.Empty, switchedOff = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Intake Staff", label + "-intake");
                supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Staff Supervisor", label + "-supervisor");
                switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(users, roles, TenantsTestData.TenantARef, "Intake Staff", label + "-off");
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, OfficeInbox);
            }
        });
        return (intake, supervisor, switchedOff);
    }

    private async Task<Guid> AddChangeRequestAsync(ChangeRequestType type)
    {
        var id = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, OfficeInbox);
                await GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>().InsertAsync(
                    new AppointmentChangeRequest(
                        id, TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, type,
                        cancellationReason: type == ChangeRequestType.Cancel ? "TEST- cancel" : null,
                        reScheduleReason: type == ChangeRequestType.Reschedule ? "TEST- reschedule" : null,
                        newDoctorAvailabilityId: null),
                    autoSave: true);
            }
        });
        _dispatcher.Clear();
        return id;
    }

    private Task PublishSubmittedAsync(Guid changeRequestId, ChangeRequestType type, Guid submitter) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new AppointmentChangeRequestSubmittedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ChangeRequestId = changeRequestId,
            TenantId = TenantsTestData.TenantARef,
            ChangeRequestType = type,
            SubmittedByUserId = submitter,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));

    private Task PublishOpenedAsync(Guid changeRequestId, params string[] linkHolders) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new ChangeRequestConsentOpenedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ChangeRequestId = changeRequestId,
            TenantId = TenantsTestData.TenantARef,
            ChangeRequestType = ChangeRequestType.Reschedule,
            RoundNumber = 1,
            ConsentLinkEmails = linkHolders.ToList(),
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));

    private Task PublishDeclinedAsync(Guid changeRequestId, ChangeRequestSide side) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new ChangeRequestConsentDeclinedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ChangeRequestId = changeRequestId,
            TenantId = TenantsTestData.TenantARef,
            ChangeRequestType = ChangeRequestType.Reschedule,
            RoundNumber = 1,
            Side = side,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));

    private Task PublishReminderAsync(int daysUntil) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new AppointmentDayReminderEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            TenantId = TenantsTestData.TenantARef,
            DaysUntil = daysUntil,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));
}
