using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// Case followers (2026-11-04) on the real rig: the external booker and every accessor receive the case
/// emails the parties receive, once per person per event, except the notices written for specific people.
/// Real appointment, users, roles and accessor rows, real context resolver, real handlers; only the
/// dispatcher (records) and the party resolver (the parties each test chooses) are replaced.
///
/// <para>The shared seed puts one accessor on Appointment1 -- <see cref="Accessor"/> (role Applicant
/// Attorney, active, unconfirmed). Every "follower handler" Fact asserts that address is among the
/// recipients, every "excluded" Fact asserts it is NOT, and the dedupe / inactive / staff / patient-role
/// Facts add their own accessors beside it. <see cref="CaseFollowerCoverageGuardTests"/> pins that every
/// event handler is either one of the Facts here or on its excluded list.</para>
/// </summary>
public class CaseFollowerEmailTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string PatientEmail = "TEST-fol-patient@test.local";
    private const string ApplicantAttorney = "TEST-fol-aa@test.local";
    private const string DefenseAttorney = "TEST-fol-da@test.local";
    private const string ClaimExaminer = "TEST-fol-ce@test.local";
    private const string OfficeInbox = "TEST-fol-inbox@test.local";
    private const string PatientRoleAccessorEmail = "TEST-fol-patient-accessor@test.local";
    private const string BookerEmail = "TEST-fol-booker@test.local";

    /// <summary>The accessor the shared seed puts on Appointment1.</summary>
    private const string Accessor = IdentityUsersTestData.ApplicantAttorney1Email;

    private static readonly DateTime When = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    // ---- Follower handlers: the accessor is copied ------------------------------------------------

    /// <summary>Row 1a. The request notice copies the accessor; the office notice and the approve/reject mail do not.</summary>
    [Fact]
    public async Task Requested_CopiesTheAccessor_ButNotTheOfficeNoticeOrTheApproveRejectMail()
    {
        await ArrangeAsync();
        var (intake, _, _) = await SeedOfficeStaffAsync("req");
        await SetSubmitterAsync(await SeedUserAsync("Applicant Attorney", "req-booker", BookerEmail));

        await RaiseSubmittedAsync();

        var requested = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        requested.Recipients.ShouldContain(Accessor);
        requested.Recipients.ShouldContain(BookerEmail);
        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice).Recipients.ShouldBe(new[] { OfficeInbox });
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject).Recipients.ShouldBe(new[] { intake });
        _dispatcher.Dispatches.Where(d => d.Recipients.Contains(Accessor)).ShouldHaveSingleItem();
    }

    /// <summary>Rows 2 / 3. The approval mail copies the accessor (the patient-role rule no longer applies).</summary>
    [Fact]
    public async Task Approved_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt).Recipients.ShouldContain(Accessor);
    }

    [Fact]
    public async Task Rejected_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await RaiseStatusAsync(AppointmentStatusType.Rejected, AppointmentStatusType.Pending);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentRejected).Recipients.ShouldContain(Accessor);
    }

    [Fact]
    public async Task SentBack_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await RaiseStatusAsync(AppointmentStatusType.InfoRequested, AppointmentStatusType.Pending);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentInfoRequested).Recipients.ShouldContain(Accessor);
    }

    [Fact]
    public async Task Cancelled_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await RaiseStatusAsync(AppointmentStatusType.CancelledNoBill, AppointmentStatusType.Approved);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentCancelledNoBill).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 6.</summary>
    [Fact]
    public async Task ACancelRequest_CopiesTheAccessor()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Cancel);

        await PublishSubmittedAsync(changeRequestId, ChangeRequestType.Cancel, Guid.NewGuid());

        Sent(NotificationTemplateConsts.Codes.AppointmentCancelledRequest).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 7. Filed by an attorney, so a Patient-role accessor is left out (see the patient-rule Facts).</summary>
    [Fact]
    public async Task ARescheduleRequest_CopiesTheAccessor()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule, Guid.NewGuid());

        Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 8, the information notice (no link).</summary>
    [Fact]
    public async Task ConsentInfo_CopiesTheAccessor()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishOpenedAsync(changeRequestId, ApplicantAttorney, DefenseAttorney);

        Sent(NotificationTemplateConsts.Codes.ChangeRequestConsentInfo).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 9.</summary>
    [Fact]
    public async Task AFinishedChange_CopiesTheAccessor()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await Act(() => GetRequiredService<ChangeRequestApprovedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestApprovedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                OccurredAt = When,
            }));

        Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequestApproved).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 10.</summary>
    [Fact]
    public async Task ARejectedChange_CopiesTheAccessor()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await Act(() => GetRequiredService<ChangeRequestRejectedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestRejectedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                RejectionNotes = "TEST- not possible",
                OccurredAt = When,
            }));

        Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequestRejected).Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 11.</summary>
    [Fact]
    public async Task ADocumentUpload_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await Act(() => GetRequiredService<DocumentUploadedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentUploadedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                OccurredAt = When,
            }));

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 12, accept.</summary>
    [Fact]
    public async Task ADocumentAcceptance_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await Act(() => GetRequiredService<DocumentAcceptedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentAcceptedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                AcceptedByUserId = Guid.NewGuid(),
                OccurredAt = When,
            }));

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 12, reject.</summary>
    [Fact]
    public async Task ADocumentRejection_CopiesTheAccessor()
    {
        await ArrangeAsync();

        await Act(() => GetRequiredService<DocumentRejectedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentRejectedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                RejectionNotes = "TEST- unreadable",
                RejectedByUserId = Guid.NewGuid(),
                OccurredAt = When,
            }));

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(Accessor);
    }

    /// <summary>Row 13a: a staff date move. Approved, so the patient rule is not in play.</summary>
    [Fact]
    public async Task ADateMove_CopiesTheAccessor()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishIntakeChangedAsync(dateMoved: true);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldContain(Accessor);
    }

    /// <summary>The due-date reminder (live; not an oracle row).</summary>
    [Fact]
    public async Task TheDueDateReminder_CopiesTheAccessor()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Approved);

        await Act(() => GetRequiredService<AppointmentReminderEmailHandler>().HandleEventAsync(
            new AppointmentReminderEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                DaysUntilDue = 3,
                OccurredAt = When,
            }));

        Sent(NotificationTemplateConsts.Codes.AppointmentDueDateReminder).Recipients.ShouldContain(Accessor);
    }

    // ---- Excluded handlers: the accessor is NOT copied ---------------------------------------------

    /// <summary>
    /// The consent LINK goes to the side's representative; an accessor gets it only when it holds a law-office
    /// role of that side and was added by staff or that side (decision 3). This accessor holds the applicant
    /// attorney role but its adder is unknown, so it gets nothing: the link stays with the representative.
    /// </summary>
    [Fact]
    public async Task TheConsentLink_GoesOnlyToTheSideRepresentative()
    {
        await ArrangeAsync();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await Act(() => GetRequiredService<ChangeRequestConsentRequestEmailHandler>().HandleEventAsync(
            new ChangeRequestConsentRequestedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                OpposingRecipientEmail = ApplicantAttorney,
                OpposingRecipientRole = RecipientRole.ApplicantAttorney,
                Side = ChangeRequestSide.SideA,
                ConsentUrl = "https://test.local/consent/TEST-token",
                RoundNumber = 1,
                OccurredAt = When,
            }));

        Sent(NotificationTemplateConsts.Codes.ChangeRequestConsentRequest).Recipients.ShouldBe(new[] { ApplicantAttorney });
    }

    /// <summary>Row 9d: a decline goes to the inbox and the Intake Staff, by decision.</summary>
    [Fact]
    public async Task ADecline_DoesNotCopyTheAccessor()
    {
        await ArrangeAsync();
        var (intake, _, _) = await SeedOfficeStaffAsync("decline");
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await Act(() => GetRequiredService<ChangeRequestConsentDeclinedEmailHandler>().HandleEventAsync(
            new ChangeRequestConsentDeclinedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                RoundNumber = 1,
                Side = ChangeRequestSide.SideB,
                OccurredAt = When,
            }));

        Sent(NotificationTemplateConsts.Codes.ChangeRequestConsentDeclined).Recipients
            .ShouldBe(new[] { OfficeInbox, intake }, ignoreOrder: true);
    }

    /// <summary>Row 13b: a panel-number or due-date save is the office's housekeeping, inbox only.</summary>
    [Fact]
    public async Task APanelOnlyEdit_DoesNotCopyTheAccessor()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Approved);

        await PublishIntakeChangedAsync(dateMoved: false);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { OfficeInbox });
    }

    /// <summary>Row 16: a no-show or not-seen outcome is internal.</summary>
    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    public async Task AnAttendanceOutcome_DoesNotCopyTheAccessor(AppointmentStatusType outcome)
    {
        await ArrangeAsync();
        var (intake, _, _) = await SeedOfficeStaffAsync("attendance");

        await RaiseStatusAsync(outcome, AppointmentStatusType.Approved);

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients
            .ShouldBe(new[] { OfficeInbox, intake }, ignoreOrder: true);
    }

    /// <summary>Row 15: the 7 / 1 day appointment reminder is for the patient and the office.</summary>
    [Fact]
    public async Task TheDayReminder_DoesNotCopyTheAccessor()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Approved);

        await Act(() => GetRequiredService<AppointmentDayReminderEmailHandler>().HandleEventAsync(
            new AppointmentDayReminderEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                DaysUntil = 7,
                OccurredAt = When,
            }));

        _dispatcher.Dispatches.ShouldHaveSingleItem().Recipients.ShouldNotContain(Accessor);
    }

    /// <summary>Row 14 with the switch on (office decision 4, 2026-11-04): the attorney-role accessor gets the packet too.</summary>
    [Fact]
    public async Task TheAttorneyPacket_GoesToTheAttorneysAndTheAttorneyRoleAccessor()
    {
        await ArrangeAsync();

        await Act(() => GetRequiredService<AttyCEPacketEmailHandler>().HandleEventAsync(
            new PacketGeneratedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                PacketId = Guid.NewGuid(),
                Kind = PacketKind.AttorneyClaimExaminer,
                OccurredAt = When,
            }));

        _dispatcher.Dispatches.SelectMany(d => d.Recipients).ShouldBe(
            new[] { ApplicantAttorney, DefenseAttorney, ClaimExaminer, Accessor }, ignoreOrder: true);
    }

    // ---- Dedupe: one mail per person per event ------------------------------------------------------

    /// <summary>
    /// An accessor whose address equals a party's, and a booker who is also an accessor, each get ONE mail
    /// for the request (row 1a) and ONE for the approval (row 3).
    /// </summary>
    [Fact]
    public async Task AnAccessorWhoIsAlsoAPartyOrTheBooker_GetsOneMailPerEvent()
    {
        await ArrangeAsync();
        var bookerId = await SeedUserAsync("Applicant Attorney", "dedupe-booker", BookerEmail);
        await SetSubmitterAsync(bookerId);
        var partyAccessor = await SeedUserAsync("Defense Attorney", "dedupe-party", DefenseAttorney);
        await AddAccessorAsync(partyAccessor);
        await AddAccessorAsync(bookerId);

        await RaiseSubmittedAsync();
        var requested = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        EachAddressOnce(requested.Recipients);
        requested.Recipients.ShouldContain(DefenseAttorney);
        requested.Recipients.ShouldContain(BookerEmail);
        requested.Recipients.ShouldContain(Accessor);

        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);
        var approved = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        EachAddressOnce(approved.Recipients);
        approved.Recipients.ShouldContain(DefenseAttorney);
        approved.Recipients.ShouldContain(BookerEmail);
        approved.Recipients.ShouldContain(Accessor);
    }

    // ---- Who is a follower ---------------------------------------------------------------------------

    /// <summary>An accessor whose login is switched off, and an accessor who is office staff, are never mailed.</summary>
    [Fact]
    public async Task AnInactiveAccessor_AndAStaffAccessor_GetNothing()
    {
        await ArrangeAsync();
        var inactive = await SeedUserAsync("Applicant Attorney", "inactive-acc", "TEST-fol-inactive@test.local", active: false);
        var staff = await SeedUserAsync("Staff Supervisor", "staff-acc", "TEST-fol-staff@test.local");
        await AddAccessorAsync(inactive);
        await AddAccessorAsync(staff);

        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);

        var approved = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt);
        approved.Recipients.ShouldContain(Accessor); // positive control: a valid accessor is still copied
        approved.Recipients.ShouldNotContain("TEST-fol-inactive@test.local");
        approved.Recipients.ShouldNotContain("TEST-fol-staff@test.local");
    }

    // ---- The patient rule for a Patient-role accessor ------------------------------------------------

    /// <summary>
    /// Attorney-booked and still Pending: a Patient-role accessor hears nothing about the request, the
    /// rejection, the send-back, a reschedule request or a date move -- same as the patient. The other
    /// accessor (an attorney) is copied each time.
    /// </summary>
    [Fact]
    public async Task APatientRoleAccessor_IsLeftOutBeforeApproval_OfAnAttorneyBookedRequest()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Pending);
        await SetSubmitterAsync(await SeedUserAsync("Applicant Attorney", "pr-booker", BookerEmail));
        await AddAccessorAsync(await SeedUserAsync("Patient", "pr-accessor", PatientRoleAccessorEmail));

        await RaiseSubmittedAsync();
        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered).Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered).Recipients.ShouldContain(Accessor);

        await RaiseStatusAsync(AppointmentStatusType.Rejected, AppointmentStatusType.Pending);
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentRejected).Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentRejected).Recipients.ShouldContain(Accessor);

        await RaiseStatusAsync(AppointmentStatusType.InfoRequested, AppointmentStatusType.Pending);
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentInfoRequested).Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentInfoRequested).Recipients.ShouldContain(Accessor);

        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);
        await PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule, Guid.NewGuid());
        Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest).Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest).Recipients.ShouldContain(Accessor);

        await PublishIntakeChangedAsync(dateMoved: true);
        var moved = Sent(NotificationTemplateConsts.Codes.AppointmentRescheduleRequestByAdmin);
        moved.Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        moved.Recipients.ShouldContain(Accessor);
    }

    /// <summary>After approval the Patient-role accessor is a normal follower again (row 3).</summary>
    [Fact]
    public async Task APatientRoleAccessor_IsCopiedOnceTheAppointmentIsApproved()
    {
        await ArrangeAsync();
        await SetSubmitterAsync(await SeedUserAsync("Applicant Attorney", "ap-booker", BookerEmail));
        await AddAccessorAsync(await SeedUserAsync("Patient", "ap-accessor", PatientRoleAccessorEmail));

        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt).Recipients.ShouldContain(PatientRoleAccessorEmail);
    }

    /// <summary>The booker is never dropped by role: a booker holding the Patient role is still mailed about their request.</summary>
    [Fact]
    public async Task ABookerWhoHoldsThePatientRole_IsNeverDroppedByRole()
    {
        await ArrangeAsync();
        await SetStatusAsync(AppointmentStatusType.Pending);
        await SetSubmitterAsync(await SeedUserAsync("Patient", "pr-booker-patient", BookerEmail));

        await RaiseSubmittedAsync();

        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered).Recipients.ShouldContain(BookerEmail);
    }

    // ---- The consent information notice --------------------------------------------------------------

    /// <summary>
    /// Row 8 info: the booker and the accessor are told; a Patient-role accessor, an accessor carrying the
    /// patient's address, and an accessor who holds a consent link are not (the link holder's mail is their one).
    /// </summary>
    [Fact]
    public async Task ConsentInfo_TellsTheBookerAndAccessors_ButNotThePatientOrALinkHolder()
    {
        await ArrangeAsync();
        var (intake, _, _) = await SeedOfficeStaffAsync("info");
        await SetSubmitterAsync(await SeedUserAsync("Applicant Attorney", "info-booker", BookerEmail));
        await AddAccessorAsync(await SeedUserAsync("Patient", "info-patient-role", PatientRoleAccessorEmail));
        await AddAccessorAsync(await SeedUserAsync("Applicant Attorney", "info-patient-address", PatientEmail));
        await AddAccessorAsync(await SeedUserAsync("Defense Attorney", "info-link-holder", DefenseAttorney));
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        await PublishOpenedAsync(changeRequestId, ApplicantAttorney, DefenseAttorney);

        var info = Sent(NotificationTemplateConsts.Codes.ChangeRequestConsentInfo);
        info.Recipients.ShouldBe(
            new[] { ClaimExaminer, OfficeInbox, intake, Accessor, BookerEmail }, ignoreOrder: true);
        info.Recipients.ShouldNotContain(PatientRoleAccessorEmail);
        info.Recipients.ShouldNotContain(PatientEmail);
        info.Recipients.ShouldNotContain(DefenseAttorney);
    }

    // ---- The packet switch ---------------------------------------------------------------------------

    private static DocumentEmailContext ContextWith(params CaseFollower[] followers) => new()
    {
        Followers = followers,
    };

    private static CaseFollower Follower(string email, bool booker, params string[] roles) =>
        new(email, Guid.NewGuid(), booker, CaseFollowers.IsPatientRole(roles), CaseFollowers.RoleOf(roles), roles);

    /// <summary>
    /// The switch is the only gate: off gives nobody a packet; on (the default since 2026-11-04) gives it to a non-booker
    /// follower whose role may see the attorney packet -- not to a Patient-role accessor, and not to the booker
    /// (who has their own path).
    /// </summary>
    [Fact]
    public void TheSwitchIsTheOnlyGateOnAccessorPackets()
    {
        var ctx = ContextWith(
            Follower("TEST-fol-attorney-acc@test.local", booker: false, "Applicant Attorney"),
            Follower("TEST-fol-patient-acc@test.local", booker: false, "Patient"),
            Follower("TEST-fol-booker-acc@test.local", booker: true, "Applicant Attorney"));

        CaseFollowers.AccessorsReceivePackets.ShouldBeTrue();
        CaseFollowers.AccessorPacketRecipients(ctx).Select(r => r.Email)
            .ShouldBe(new[] { "TEST-fol-attorney-acc@test.local" });
        CaseFollowers.AccessorPacketRecipients(ctx, enabled: false).ShouldBeEmpty();
        CaseFollowers.AccessorPacketRecipients(ctx, enabled: true).Select(r => r.Email)
            .ShouldBe(new[] { "TEST-fol-attorney-acc@test.local" });
    }

    // ------------------------------------------------------------------------------------------------

    private RecordedDispatch Sent(string templateCode) =>
        _dispatcher.Dispatches.Single(d => d.TemplateCode == templateCode);

    private static void EachAddressOnce(IEnumerable<string> recipients) =>
        recipients.GroupBy(r => r, StringComparer.OrdinalIgnoreCase).ShouldAllBe(g => g.Count() == 1);

    private Task Act(Func<Task> action) => WithUnitOfWorkAsync(action);

    private void WithParties()
    {
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ApplicantAttorney, Role = RecipientRole.ApplicantAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = DefenseAttorney, Role = RecipientRole.DefenseAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ClaimExaminer, Role = RecipientRole.ClaimExaminer, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = PatientEmail, Role = RecipientRole.Patient, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = OfficeInbox, Role = RecipientRole.OfficeAdmin, IsRegistered = true });
    }

    /// <summary>The parties, the patient address and the office inbox every Fact is tested against.</summary>
    private async Task ArrangeAsync()
    {
        WithParties();
        await SetPatientAddressAsync();
    }

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

            var patients = GetRequiredService<IRepository<Patient, Guid>>();
            var patient = await patients.GetAsync(appointment.PatientId);
            patient.Email = PatientEmail;
            await patients.UpdateAsync(patient, autoSave: true);
        }
    });

    private Task SetStatusAsync(AppointmentStatusType status) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.AppointmentStatus = status;
            await appointments.UpdateAsync(appointment, autoSave: true);
        }
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

    private async Task<Guid> SeedUserAsync(string role, string label, string email, bool active = true)
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
                user.SetIsActive(active);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
                id = user.Id;
            }
        });
        return id;
    }

    private Task AddAccessorAsync(Guid userId) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await AccessorTestSeeder.AddAsync(
                GetRequiredService<IRepository<AppointmentAccessor, Guid>>(), userId, AppointmentsTestData.Appointment1Id);
        }
    });

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

    private async Task RaiseSubmittedAsync()
    {
        _dispatcher.Clear();
        await Act(() => GetRequiredService<BookingSubmissionEmailHandler>().HandleEventAsync(
            new AppointmentSubmittedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                BookerUserId = Guid.NewGuid(),
                PatientId = PatientsTestData.Patient1Id,
                RequestConfirmationNumber = AppointmentsTestData.Appointment1RequestConfirmationNumber,
                AppointmentDate = new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
                SubmittedAt = When,
            }));
    }

    private async Task RaiseStatusAsync(AppointmentStatusType to, AppointmentStatusType from)
    {
        _dispatcher.Clear();
        await Act(() => GetRequiredService<StatusChangeEmailHandler>().HandleEventAsync(
            new AppointmentStatusChangedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                FromStatus = from,
                ToStatus = to,
                OccurredAt = When,
            }));
    }

    private async Task PublishSubmittedAsync(Guid changeRequestId, ChangeRequestType type, Guid submitter)
    {
        _dispatcher.Clear();
        await Act(() => GetRequiredService<ChangeRequestSubmittedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestSubmittedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = type,
                SubmittedByUserId = submitter,
                OccurredAt = When,
            }));
    }

    private async Task PublishOpenedAsync(Guid changeRequestId, params string[] linkHolders)
    {
        _dispatcher.Clear();
        await Act(() => GetRequiredService<ChangeRequestConsentInfoEmailHandler>().HandleEventAsync(
            new ChangeRequestConsentOpenedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                RoundNumber = 1,
                ConsentLinkEmails = linkHolders.ToList(),
                OccurredAt = When,
            }));
    }

    private async Task PublishIntakeChangedAsync(bool dateMoved)
    {
        _dispatcher.Clear();
        await Act(() => GetRequiredService<IntakeChangedEmailHandler>().HandleEventAsync(
            new AppointmentIntakeChangedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                DateOrTimeChanged = dateMoved,
                NewAppointmentDate = new DateTime(2026, 12, 1, 9, 30, 0),
                ChangedFields = new List<IntakeChangedField>
                {
                    new()
                    {
                        Section = "Appointment",
                        FieldName = dateMoved ? "AppointmentDate" : "PanelNumber",
                        OldValue = "TEST-old",
                        NewValue = "TEST-new",
                    },
                },
                OccurredAt = When,
            }));
    }
}
