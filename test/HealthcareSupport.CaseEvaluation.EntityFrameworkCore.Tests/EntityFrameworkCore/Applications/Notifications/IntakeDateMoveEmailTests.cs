using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// The email for a staff DATE MOVE (office routing, 2026-11-03, oracle row 9 "a change finished"), on the real
/// rig: real appointment, users and roles, real local event bus, so the subscribed handler runs. Only the
/// dispatcher (records) and the party resolver (the parties each test chooses) are replaced. Pins who gets the
/// one date-moved email (parties, office inbox, active Intake Staff; never a Supervisor or a switched-off Intake
/// user), the patient rule before approval, that a panel-only or due-date-only save stays a change log to the
/// inbox, that date plus panel in one save is ONE email, and the context tag per change.
/// </summary>
public class IntakeDateMoveEmailTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string PatientEmail = "TEST-move-patient@test.local";
    private const string ApplicantAttorney = "TEST-move-aa@test.local";
    private const string DefenseAttorney = "TEST-move-da@test.local";
    private const string ClaimExaminer = "TEST-move-ce@test.local";
    private const string OfficeInbox = "TEST-move-inbox@test.local";

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    private static readonly Guid SlotA = Guid.Parse("a1a1a1a1-0000-0000-0000-00000000000a");
    private static readonly Guid SlotB = Guid.Parse("b2b2b2b2-0000-0000-0000-00000000000b");
    private static readonly DateTime SavedAt = new(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc);

    // ---- A date move ------------------------------------------------------------------------------

    /// <summary>
    /// An Approved appointment's date moves: the patient, the attorneys, the claim examiner, the inbox and the
    /// active Intake Staff get one email, each address once. NOT a Supervisor or a switched-off Intake user.
    /// </summary>
    [Fact]
    public async Task ADateMoveOnAnApprovedAppointment_GoesToThePartiesInboxAndIntake_NobodyElse()
    {
        WithParties();
        var (intake, supervisor, switchedOff) = await SeedOfficeStaffAsync("approved");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        var mail = _dispatcher.Dispatches.ShouldHaveSingleItem();
        mail.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin);
        mail.Recipients.ShouldBe(
            new[] { PatientEmail, ApplicantAttorney, DefenseAttorney, ClaimExaminer, OfficeInbox, intake },
            ignoreOrder: true);
        mail.Recipients.ShouldNotContain(supervisor);
        mail.Recipients.ShouldNotContain(switchedOff);
        ((string)mail.Variables["NewAppointmentDate"]!).ShouldBe("12/01/2026 9:30 AM");
    }

    /// <summary>A date move that reaches the parties never reads the change log (no AppointmentChangeLogs variable).</summary>
    [Fact]
    public async Task ADateMove_IsNotTheChangeLog()
    {
        WithParties();
        await SeedOfficeStaffAsync("notlog");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Variables.ShouldNotContainKey("AppointmentChangeLogs");
    }

    /// <summary>Pending, booked by an attorney: the patient hears nothing yet, everyone else is told.</summary>
    [Fact]
    public async Task ADateMoveOnAPendingAppointmentSomeoneElseBooked_LeavesThePatientOut()
    {
        WithParties();
        var (intake, _, _) = await SeedOfficeStaffAsync("pending");
        var attorneyLogin = await SeedUserAsync("Applicant Attorney", "pending-aa", ApplicantAttorney);
        await SetSubmitterAsync(attorneyLogin);
        await SetStatusAsync(AppointmentStatusType.Pending);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        var mail = _dispatcher.Dispatches.ShouldHaveSingleItem();
        mail.Recipients.ShouldNotContain(PatientEmail);
        mail.Recipients.ShouldBe(
            new[] { ApplicantAttorney, DefenseAttorney, ClaimExaminer, OfficeInbox, intake }, ignoreOrder: true);
    }

    /// <summary>Sent back for information is still before approval: the patient is left out.</summary>
    [Fact]
    public async Task ADateMoveOnAnInfoRequestedAppointment_LeavesThePatientOut()
    {
        WithParties();
        await SeedOfficeStaffAsync("info");
        await SetStatusAsync(AppointmentStatusType.InfoRequested);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldNotContain(PatientEmail);
    }

    /// <summary>Pending, but the patient submitted it: the patient is a normal recipient.</summary>
    [Fact]
    public async Task ADateMoveOnAPendingAppointmentThePatientSubmitted_IncludesThePatient()
    {
        WithParties();
        await SeedOfficeStaffAsync("pending-self");
        var patientLogin = await SeedUserAsync("Patient", "pending-patient", PatientEmail);
        await SetSubmitterAsync(patientLogin);
        await SetStatusAsync(AppointmentStatusType.Pending);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(PatientEmail);
    }

    /// <summary>A live post-approval status (a reschedule already requested) counts as approved: the patient is told.</summary>
    [Fact]
    public async Task ADateMoveOnAPostApprovalStatus_IncludesThePatient()
    {
        WithParties();
        await SeedOfficeStaffAsync("live");
        await SetStatusAsync(AppointmentStatusType.RescheduleRequested);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(PatientEmail);
    }

    // ---- Panel number / due date only ---------------------------------------------------------------

    /// <summary>A panel-number-only save is the office's own housekeeping: the change log to the inbox only.</summary>
    [Fact]
    public async Task APanelNumberOnlySave_IsTheChangeLogToTheInboxOnly()
    {
        WithParties();
        var (intake, _, _) = await SeedOfficeStaffAsync("panel");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: false, SlotA, SavedAt, Field("PanelNumber", "TEST-P1", "TEST-P2"));

        var mail = _dispatcher.Dispatches.ShouldHaveSingleItem();
        mail.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentChangeLogs);
        mail.Recipients.ShouldBe(new[] { OfficeInbox });
        mail.Recipients.ShouldNotContain(intake);
        ((string)mail.Variables["AppointmentChangeLogs"]!).ShouldContain("Panel Number");
    }

    /// <summary>A due-date-only save is the same: the change log to the inbox only.</summary>
    [Fact]
    public async Task ADueDateOnlySave_IsTheChangeLogToTheInboxOnly()
    {
        WithParties();
        await SeedOfficeStaffAsync("due");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: false, SlotA, SavedAt, Field("DueDate", "11/01/2026", "11/15/2026"));

        var mail = _dispatcher.Dispatches.ShouldHaveSingleItem();
        mail.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentChangeLogs);
        mail.Recipients.ShouldBe(new[] { OfficeInbox });
    }

    // ---- Date plus panel in ONE save ----------------------------------------------------------------

    /// <summary>
    /// Date and panel number in one save: ONE email event. Every address gets exactly one mail, the inbox does
    /// NOT also get the change log, and the panel number is not shown to the parties.
    /// </summary>
    [Fact]
    public async Task ADateAndPanelSave_IsOneEmailPerAddress_AndNoChangeLogToTheInbox()
    {
        WithParties();
        await SeedOfficeStaffAsync("both");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(
            dateMoved: true, SlotA, SavedAt,
            Field("AppointmentDate", "11/20/2026 9:00 AM", "12/01/2026 9:30 AM"),
            Field("PanelNumber", "TEST-P1", "TEST-P2"));

        _dispatcher.Dispatches.Count.ShouldBe(1, "one email event for the whole save");
        var mail = _dispatcher.Dispatches[0];
        mail.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin);
        mail.Recipients.Count.ShouldBe(mail.Recipients.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        mail.Variables.ShouldNotContainKey("AppointmentChangeLogs");
        mail.Variables.Values.OfType<string>().ShouldNotContain(v => v.Contains("TEST-P2", StringComparison.Ordinal));
    }

    // ---- Context tag --------------------------------------------------------------------------------

    /// <summary>A second move (a different slot, a later save) has its own tag, so it mails again.</summary>
    [Fact]
    public async Task ASecondDateMove_HasItsOwnContextTag()
    {
        WithParties();
        await SeedOfficeStaffAsync("second");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);
        await PublishAsync(dateMoved: true, SlotB, SavedAt.AddMinutes(5));

        _dispatcher.Dispatches.Count.ShouldBe(2);
        _dispatcher.Dispatches[0].ContextTag.ShouldNotBe(_dispatcher.Dispatches[1].ContextTag);
        _dispatcher.Dispatches[0].ContextTag.ShouldContain(SlotA.ToString());
        _dispatcher.Dispatches[1].ContextTag.ShouldContain(SlotB.ToString());
    }

    /// <summary>A move BACK onto the first slot is still a new occurrence (the save time differs).</summary>
    [Fact]
    public async Task AMoveBackOntoAnEarlierSlot_StillHasAFreshContextTag()
    {
        WithParties();
        await SeedOfficeStaffAsync("back");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);
        await PublishAsync(dateMoved: true, SlotB, SavedAt.AddMinutes(5));
        await PublishAsync(dateMoved: true, SlotA, SavedAt.AddMinutes(10));

        _dispatcher.Dispatches.Select(d => d.ContextTag).Distinct().Count().ShouldBe(3);
    }

    /// <summary>A replay of the SAME save carries the same tag, so the outbox collapses it onto the first send.</summary>
    [Fact]
    public async Task AReplayOfTheSameSave_HasTheSameContextTag()
    {
        WithParties();
        await SeedOfficeStaffAsync("replay");
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishAsync(dateMoved: true, SlotA, SavedAt);
        await PublishAsync(dateMoved: true, SlotA, SavedAt);

        _dispatcher.Dispatches.Count.ShouldBe(2);
        _dispatcher.Dispatches[0].ContextTag.ShouldBe(_dispatcher.Dispatches[1].ContextTag);
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

    private static IntakeChangedField Field(string name, string oldValue, string newValue) => new()
    {
        Section = "Appointment",
        FieldName = name,
        OldValue = oldValue,
        NewValue = newValue,
    };

    /// <summary>Publishes the event a staff save raises. A date move always lists the date field too.</summary>
    private async Task PublishAsync(bool dateMoved, Guid slotId, DateTime savedAt, params IntakeChangedField[] fields)
    {
        var changed = fields.ToList();
        if (changed.Count == 0)
        {
            changed.Add(Field("AppointmentDate", "11/20/2026 9:00 AM", "12/01/2026 9:30 AM"));
        }

        await WithUnitOfWorkAsync(() => GetRequiredService<ILocalEventBus>().PublishAsync(
            new AppointmentIntakeChangedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                DateOrTimeChanged = dateMoved,
                NewDoctorAvailabilityId = slotId,
                NewAppointmentDate = new DateTime(2026, 12, 1, 9, 30, 0),
                ChangedFields = changed,
                OccurredAt = savedAt,
            }));
    }

    private Task SetStatusAsync(AppointmentStatusType status) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, OfficeInbox);
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.AppointmentStatus = status;
            appointment.PatientEmail = PatientEmail;
            await appointments.UpdateAsync(appointment, autoSave: true);

            // The seeded patient row carries its own address; the patient rule matches on it.
            var patients = GetRequiredService<IRepository<Patient, Guid>>();
            var patient = await patients.GetAsync(appointment.PatientId);
            patient.Email = PatientEmail;
            await patients.UpdateAsync(patient, autoSave: true);
        }

        // Every test arranges its data first and this last, so count only what the event under test sends.
        _dispatcher.Clear();
    });

    private Task SetSubmitterAsync(Guid creatorId) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
            // CreatorId is an audit property with a protected setter; ABP's own helper sets it.
            ObjectHelper.TrySetProperty(appointment, a => a.CreatorId, () => (Guid?)creatorId);
            await appointments.UpdateAsync(appointment, autoSave: true);
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
}
