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
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;

/// <summary>
/// The booker's mail choices and the paralegal consent link (office decisions of 2026-11-04) on the real
/// rig: real appointment, users, roles, accessor rows, real context resolver and real handlers; only the
/// dispatcher (records) and the party resolver (the parties each test chooses) are replaced.
///
/// <para><b>Decision 1.</b> A booker who unticked "follow the case" gets the request receipt, once, and
/// nothing after it; a booker who is also a named party still gets the party mail.
/// <b>Decision 2.</b> While the lawyer's mail is redirected the lawyer's address appears in NO recorded mail
/// from any handler, each mail still has an addressee, and the side's paralegal gets it instead; the
/// redirect lapses when the paralegal is switched off or staff change the attorney column.
/// <b>Decision 3.</b> The consent link goes to the representative and to the same-side law-office
/// followers, never to the other side. <b>Decision 4.</b> Authorized Users get the attorney packet.</para>
/// </summary>
public class BookerMailChoicesEmailTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string Lawyer = "TEST-mc-lawyer@test.local";
    private const string Paralegal = "TEST-mc-paralegal@test.local";
    private const string Booker = "TEST-mc-booker@test.local";
    private const string DefenseAttorney = "TEST-mc-da@test.local";
    private const string DefenseParalegal = "TEST-mc-da-paralegal@test.local";
    private const string ClaimExaminer = "TEST-mc-ce@test.local";
    private const string PatientEmail = "TEST-mc-patient@test.local";
    private const string OfficeInbox = "TEST-mc-inbox@test.local";
    private const string NewAttorney = "TEST-mc-new-aa@test.local";

    private static readonly DateTime When = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    private Guid _lawyerId;
    private Guid _paralegalId;

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    // ---- Decision 1: the booker opted out ---------------------------------------------------------

    /// <summary>
    /// The receipt (the Requested mail) still reaches the booker, exactly ONCE; no later event mails the
    /// booker: not the approval, a document, a reschedule request, the date move, the reminder or the packet.
    /// A control run with the booker following shows every one of those mails does reach the booker.
    /// </summary>
    [Fact]
    public async Task AnOptedOutBooker_GetsTheReceiptOnce_AndNothingAfter()
    {
        await ArrangeAsync(bookerIsLawyer: false);

        // Control: the booker follows, so the receipt and every later mail reach the booker.
        await SetBookerFollowsAsync(true);
        await RunAsync(RaiseSubmittedAsync);
        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered).Recipients.ShouldContain(Booker);
        var control = await RunLaterMailAsync();
        control.ShouldAllBe(m => m.Value.Contains(Booker, StringComparer.OrdinalIgnoreCase),
            "positive control: every later mail reaches a booker who follows");

        // Opted out: the receipt once, then nothing.
        await SetBookerFollowsAsync(false);
        await RunAsync(RaiseSubmittedAsync);
        var receipt = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        receipt.To.ShouldBe(Booker);
        receipt.Recipients.Count(r => string.Equals(r, Booker, StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        _dispatcher.Dispatches.Count(d => d.Recipients.Contains(Booker)).ShouldBe(1, "one mail names the booker: the receipt");

        var later = await RunLaterMailAsync();
        later.ShouldAllBe(m => !m.Value.Contains(Booker, StringComparer.OrdinalIgnoreCase));
        later.Count.ShouldBe(control.Count, "the opt-out dropped the booker, not whole mails");
    }

    /// <summary>A booker who opted out but is ALSO the typed attorney of record still gets the party mail as that party.</summary>
    [Fact]
    public async Task AnOptedOutBooker_WhoIsAlsoTheTypedAttorney_StillGetsThePartyMail()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        await SetBookerFollowsAsync(false);

        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt).Recipients.ShouldContain(Lawyer);
    }

    // ---- Decision 2: the lawyer's mail goes to the paralegal -----------------------------------------

    /// <summary>
    /// THE GUARD (Host_6 requirement): for every handler on the follower list, plus the consent-link handler,
    /// raise its event with the redirect in force and assert the lawyer's address is in NO recorded mail (To
    /// or CC), every mail still has an addressee, and the paralegal gets each mail the lawyer would have.
    /// The same event is run first without the redirect: a handler whose control mail does not name the
    /// lawyer would prove nothing, so that is asserted too.
    /// </summary>
    [Fact]
    public async Task WithTheRedirectInForce_TheLawyerIsInNoMail_FromAnyHandler_AndEachMailHasAnAddressee()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        var scenarios = new (string Name, Func<Task> Raise)[]
        {
            ("Requested", RaiseSubmittedAsync),
            ("Approved", () => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending)),
            ("Rejected", () => RaiseStatusAsync(AppointmentStatusType.Rejected, AppointmentStatusType.Pending)),
            ("SentBack", () => RaiseStatusAsync(AppointmentStatusType.InfoRequested, AppointmentStatusType.Pending)),
            ("Cancelled", () => RaiseStatusAsync(AppointmentStatusType.CancelledNoBill, AppointmentStatusType.Approved)),
            // After "Cancelled": an open cancel request would suppress a later no-bill cancellation mail.
            ("CancelRequest", async () =>
                await PublishSubmittedAsync(await AddChangeRequestAsync(ChangeRequestType.Cancel), ChangeRequestType.Cancel)),
            ("RescheduleRequest", () => PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule)),
            ("ConsentInfo", () => PublishOpenedAsync(changeRequestId, ChangeRequestSide.SideB, DefenseAttorney)),
            ("FinishedChange", () => PublishApprovedAsync(changeRequestId)),
            ("RejectedChange", () => PublishRejectedAsync(changeRequestId)),
            ("DocumentUploaded", () => PublishDocumentUploadedAsync(null)),
            ("DocumentAccepted", PublishDocumentAcceptedAsync),
            ("DocumentRejected", PublishDocumentRejectedAsync),
            ("DateMove", () => PublishIntakeChangedAsync(dateMoved: true)),
            ("DueDateReminder", PublishDueDateReminderAsync),
            ("AttorneyPacket", PublishAttorneyPacketAsync),
            ("ConsentLink", () => PublishConsentRequestAsync(changeRequestId, ChangeRequestSide.SideA, Lawyer)),
        };

        foreach (var (name, raise) in scenarios)
        {
            await SetRedirectAsync(false);
            var control = await RunAsync(raise);
            control.SelectMany(d => d.Recipients).ShouldContain(Lawyer, $"{name}: positive control, the lawyer is mailed");
            var controlTemplates = control.Where(d => d.Recipients.Contains(Lawyer)).Select(d => d.TemplateCode).ToList();

            await SetRedirectAsync(true);
            var redirected = await RunAsync(raise);

            redirected.ShouldNotBeEmpty($"{name}: the mail must still go to somebody");
            foreach (var mail in redirected)
            {
                mail.Recipients.ShouldNotContain(Lawyer, $"{name}/{mail.TemplateCode}: the lawyer must get nothing");
                if (mail.To != null)
                {
                    mail.To.ShouldNotBeNullOrWhiteSpace($"{name}: a mail needs a To");
                }
            }
            foreach (var template in controlTemplates)
            {
                redirected.Where(d => d.TemplateCode == template).SelectMany(d => d.Recipients)
                    .ShouldContain(Paralegal, $"{name}/{template}: the paralegal gets what the lawyer would have");
            }
        }
    }

    /// <summary>
    /// Coordinator case (a): the lawyer is the PROMOTED To (the creator holds the attorney role and the
    /// appointment names him) and is the packet booker. With the redirect in force the To of the request /
    /// approval mails is someone else, never empty, and the packet booker rule sends him nothing.
    /// </summary>
    [Fact]
    public async Task WithTheRedirectInForce_ThePromotedTo_AndThePacketBooker_AreNotTheLawyer_AndEachMailHasATo()
    {
        await ArrangeAsync(bookerIsLawyer: true);

        // Control: the lawyer is the promoted To and gets the packet as the booker.
        await RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending);
        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt).To.ShouldBe(Lawyer);
        await SetRedirectAsync(true);

        foreach (var raise in new Func<Task>[]
                 {
                     RaiseSubmittedAsync,
                     () => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending),
                     () => RaiseStatusAsync(AppointmentStatusType.InfoRequested, AppointmentStatusType.Pending),
                     PublishDueDateReminderAsync,
                 })
        {
            var mails = await RunAsync(raise);
            var toMails = mails.Where(m => m.To != null).ToList();
            toMails.ShouldNotBeEmpty();
            toMails.ShouldAllBe(m => !string.IsNullOrWhiteSpace(m.To) && m.To != Lawyer);
            mails.SelectMany(m => m.Recipients).ShouldNotContain(Lawyer);
        }

        var packet = await RunAsync(PublishAttorneyPacketAsync);
        packet.SelectMany(m => m.Recipients).ShouldNotContain(Lawyer);
        packet.SelectMany(m => m.Recipients).ShouldContain(Paralegal);
    }

    /// <summary>An uploader who must get no mail (the redirected lawyer) is not the To of a document notice; the next recipient is.</summary>
    [Fact]
    public async Task ADocumentUploadedByTheRedirectedLawyer_IsAddressedToSomeoneElse()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        await SetRedirectAsync(true);

        var mails = await RunAsync(() => PublishDocumentUploadedAsync(_lawyerId));

        var notice = mails.ShouldHaveSingleItem();
        notice.To.ShouldNotBeNullOrWhiteSpace();
        notice.To.ShouldNotBe(Lawyer);
        notice.Recipients.ShouldNotContain(Lawyer);
        notice.Recipients.ShouldContain(Paralegal);
    }

    /// <summary>The redirect stops applying when the side's last active paralegal is switched off: the lawyer is mailed again.</summary>
    [Fact]
    public async Task TheRedirectLapses_WhenTheParalegalIsSwitchedOff_AndTheLawyerIsMailedAgain()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        await SetRedirectAsync(true);
        (await RunAsync(() => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending)))
            .SelectMany(m => m.Recipients).ShouldNotContain(Lawyer);

        await SwitchOffAsync(_paralegalId);

        var after = await RunAsync(() => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending));
        after.SelectMany(m => m.Recipients).ShouldContain(Lawyer);
        after.SelectMany(m => m.Recipients).ShouldNotContain(Paralegal);
    }

    /// <summary>The redirect applies only to the stored address: when staff put a new attorney on the case, the new attorney is mailed.</summary>
    [Fact]
    public async Task TheRedirectLapses_WhenStaffChangeTheAttorneyColumn_AndTheNewAttorneyIsMailed()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        await SetRedirectAsync(true);
        await UpdateAppointmentAsync(a => a.ApplicantAttorneyEmail = NewAttorney);
        _parties.Recipients.RemoveAll(r => r.Role == RecipientRole.ApplicantAttorney);
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = NewAttorney, Role = RecipientRole.ApplicantAttorney, IsRegistered = true });

        var mails = await RunAsync(() => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending));

        var recipients = mails.SelectMany(m => m.Recipients).ToList();
        recipients.ShouldContain(NewAttorney);
        recipients.ShouldContain(Paralegal);
    }

    // ---- Decision 3: the consent link ------------------------------------------------------------------

    /// <summary>
    /// Side A's link goes to the representative AND the follower holding a side-A law-office role who was
    /// added by that side; not to a side-A paralegal an OTHER-side account added, not to a side-B follower.
    /// Under the redirect only the paralegal gets it. Side B's link reaches the defense paralegal, not side A's.
    /// </summary>
    [Fact]
    public async Task TheConsentLink_GoesToTheSideRepresentativeAndTheSameSideFollowers()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        // A side-A paralegal planted by a DEFENSE attorney's account, and a side-B paralegal added by side B.
        var defenseId = await SeedUserAsync("Defense Attorney", "mc-defense", DefenseAttorney);
        var plantedId = await SeedUserAsync("Applicant Paralegal", "mc-planted", "TEST-mc-planted@test.local");
        await AddAccessorAsync(plantedId, defenseId);
        var defenseParalegalId = await SeedUserAsync("Defense Paralegal", "mc-da-para", DefenseParalegal);
        await AddAccessorAsync(defenseParalegalId, defenseId);

        var sideA = await RunAsync(() => PublishConsentRequestAsync(changeRequestId, ChangeRequestSide.SideA, Lawyer));
        sideA.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { Lawyer, Paralegal }, ignoreOrder: true);

        var sideB = await RunAsync(() => PublishConsentRequestAsync(changeRequestId, ChangeRequestSide.SideB, DefenseAttorney));
        sideB.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { DefenseAttorney, DefenseParalegal }, ignoreOrder: true);

        await SetRedirectAsync(true);
        var redirected = await RunAsync(() => PublishConsentRequestAsync(changeRequestId, ChangeRequestSide.SideA, Lawyer));
        redirected.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { Paralegal });
    }

    /// <summary>
    /// Nobody is left unable to answer: with the redirect in force but no Authorized User who may answer for
    /// the side (the only applicant-side paralegal was added by the DEFENSE side), the link goes back to the
    /// side representative -- the lawyer -- rather than to nobody.
    /// </summary>
    [Fact]
    public async Task UnderTheRedirect_WithNoParalegalWhoMayAnswer_TheLinkFallsBackToTheRepresentative()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);
        // The lawyer's own paralegal is switched off; the only active applicant paralegal was planted by
        // side B, so the redirect still applies (an active side-A paralegal exists) but may not answer.
        await SwitchOffAsync(_paralegalId);
        var defenseId = await SeedUserAsync("Defense Attorney", "mc-fb-defense", DefenseAttorney);
        var plantedId = await SeedUserAsync("Applicant Paralegal", "mc-fb-planted", "TEST-mc-fb-planted@test.local");
        await AddAccessorAsync(plantedId, defenseId);
        await SetRedirectAsync(true);

        var sent = await RunAsync(() => PublishConsentRequestAsync(changeRequestId, ChangeRequestSide.SideA, Lawyer));

        sent.ShouldHaveSingleItem().Recipients.ShouldBe(new[] { Lawyer });
    }

    /// <summary>Everyone who got a link is a link holder: they stay off the information notice and off the cancel-request party mail.</summary>
    [Fact]
    public async Task LinkRecipients_AreLinkHolders_TheyStayOffConsentInfo()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);

        var info = await RunAsync(() => PublishOpenedAsync(changeRequestId, ChangeRequestSide.SideA, Lawyer));

        var recipients = info.ShouldHaveSingleItem().Recipients;
        recipients.ShouldNotContain(Lawyer);
        recipients.ShouldNotContain(Paralegal, "the paralegal holds side A's link, so it is not also told here");
        recipients.ShouldContain(ClaimExaminer);
    }

    // ---- Decision 4: packets to Authorized Users -------------------------------------------------------

    /// <summary>An attorney- or paralegal-role Authorized User gets the attorney packet; a Patient-role one does not.</summary>
    [Fact]
    public async Task TheAttorneyPacket_GoesToAttorneyAndParalegalAccessors_NotToAPatientRoleAccessor()
    {
        await ArrangeAsync(bookerIsLawyer: true);
        await AddAccessorAsync(await SeedUserAsync("Patient", "mc-patient-acc", "TEST-mc-patient-acc@test.local"), _lawyerId);

        var packet = await RunAsync(PublishAttorneyPacketAsync);

        var recipients = packet.SelectMany(m => m.Recipients).ToList();
        recipients.ShouldContain(Paralegal);
        recipients.ShouldNotContain("TEST-mc-patient-acc@test.local");
    }

    // ------------------------------------------------------------------------------------------------

    private RecordedDispatch Sent(string templateCode) =>
        _dispatcher.Dispatches.Single(d => d.TemplateCode == templateCode);

    /// <summary>Runs the later (non-receipt) mails once each and returns template -> recipients.</summary>
    private async Task<Dictionary<string, IReadOnlyList<string>>> RunLaterMailAsync()
    {
        var result = new Dictionary<string, IReadOnlyList<string>>();
        var changeRequestId = await AddChangeRequestAsync(ChangeRequestType.Reschedule);
        var steps = new (string Name, Func<Task> Raise)[]
        {
            ("approved", () => RaiseStatusAsync(AppointmentStatusType.Approved, AppointmentStatusType.Pending)),
            ("document", () => PublishDocumentUploadedAsync(null)),
            ("reschedule", () => PublishSubmittedAsync(changeRequestId, ChangeRequestType.Reschedule)),
            ("datemove", () => PublishIntakeChangedAsync(dateMoved: true)),
            ("reminder", PublishDueDateReminderAsync),
            ("packet", PublishAttorneyPacketAsync),
        };
        foreach (var (name, raise) in steps)
        {
            var mails = await RunAsync(raise);
            result[name] = mails.SelectMany(m => m.Recipients).ToList();
        }
        return result;
    }

    private async Task<List<RecordedDispatch>> RunAsync(Func<Task> raise)
    {
        _dispatcher.Clear();
        await raise();
        return _dispatcher.Dispatches.ToList();
    }

    private Task Act(Func<Task> action) => WithUnitOfWorkAsync(action);

    /// <summary>
    /// The case: a lawyer who is the creator (the booker, holding the Applicant Attorney role) and the typed
    /// attorney of record, or -- when <paramref name="bookerIsLawyer"/> is false -- a separate external booker;
    /// one Applicant Paralegal Authorized User added by the lawyer; the parties, the patient address and the
    /// office inbox. The shared seed's own accessor is removed so only these followers exist.
    /// </summary>
    private async Task ArrangeAsync(bool bookerIsLawyer)
    {
        await SetOfficeAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await AccessorTestSeeder.RemoveAllAsync(
                    GetRequiredService<IRepository<AppointmentAccessor, Guid>>(), AppointmentsTestData.Appointment1Id);
            }
        });

        _lawyerId = await SeedUserAsync("Applicant Attorney", "mc-lawyer", Lawyer);
        _paralegalId = await SeedUserAsync("Applicant Paralegal", "mc-para", Paralegal);
        await AddAccessorAsync(_paralegalId, _lawyerId);

        // A separate booker holds the Claim Examiner role: an attorney booker would be PROMOTED to the To
        // (the typed attorney) and the receipt anchor would be the lawyer, not the booker.
        var submitter = bookerIsLawyer ? _lawyerId : await SeedUserAsync("Claim Examiner", "mc-booker", Booker);
        await UpdateAppointmentAsync(a =>
        {
            a.ApplicantAttorneyEmail = Lawyer;
            a.AppointmentStatus = AppointmentStatusType.Approved;
            ObjectHelper.TrySetProperty(a, x => x.CreatorId, () => (Guid?)submitter);
        });

        _parties.Recipients.Clear();
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = Lawyer, Role = RecipientRole.ApplicantAttorney, IsRegistered = true, Context = "Resolver/Submitted/ApplicantAttorney/x/aa-email-col" });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = DefenseAttorney, Role = RecipientRole.DefenseAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ClaimExaminer, Role = RecipientRole.ClaimExaminer, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = PatientEmail, Role = RecipientRole.Patient, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = OfficeInbox, Role = RecipientRole.OfficeAdmin, IsRegistered = true });
        if (!bookerIsLawyer)
        {
            // The resolver tags a submitter who is not a named party Patient, with the "/booker" context.
            _parties.Recipients.Add(new SendAppointmentEmailArgs
            {
                To = Booker,
                Role = RecipientRole.Patient,
                IsRegistered = true,
                Context = "Resolver/Submitted/Patient/x/" + AppointmentRecipientResolver.BookerContextSuffix,
            });
        }
    }

    private Task SetOfficeAsync() => WithUnitOfWorkAsync(async () =>
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

    private Task UpdateAppointmentAsync(Action<Appointment> change) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
            change(appointment);
            await appointments.UpdateAsync(appointment, autoSave: true);
        }
    });

    private Task SetBookerFollowsAsync(bool follows) =>
        UpdateAppointmentAsync(a => AppointmentMailPreferences.SetBookerFollowsCase(a, follows));

    /// <summary>Stores or clears the redirect (side A, the lawyer). It implies the booker does not follow, as at submit.</summary>
    private Task SetRedirectAsync(bool on) => UpdateAppointmentAsync(a =>
    {
        if (on)
        {
            AppointmentMailPreferences.SetAttorneyMailRedirect(a, ChangeRequestSide.SideA, Lawyer);
            AppointmentMailPreferences.SetBookerFollowsCase(a, false);
        }
        else
        {
            a.RemoveProperty(AppointmentMailPreferences.AttorneyMailRedirectSideKey);
            a.RemoveProperty(AppointmentMailPreferences.AttorneyMailRedirectEmailKey);
            AppointmentMailPreferences.SetBookerFollowsCase(a, true);
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

    private Task SwitchOffAsync(Guid userId) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var users = GetRequiredService<IdentityUserManager>();
            var user = (await users.FindByIdAsync(userId.ToString()))!;
            user.SetIsActive(false);
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }
    });

    /// <summary>An Authorized User row ADDED BY <paramref name="adderId"/> (the creator stamp the consent rule reads).</summary>
    private Task AddAccessorAsync(Guid userId, Guid adderId) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var row = new AppointmentAccessor(Guid.NewGuid(), userId, AppointmentsTestData.Appointment1Id, AccessType.View);
            ObjectHelper.TrySetProperty(row, a => a.CreatorId, () => (Guid?)adderId);
            await GetRequiredService<IRepository<AppointmentAccessor, Guid>>().InsertAsync(row, autoSave: true);
        }
    });

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
        return id;
    }

    private Task RaiseSubmittedAsync() => Act(() => GetRequiredService<BookingSubmissionEmailHandler>().HandleEventAsync(
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

    private Task RaiseStatusAsync(AppointmentStatusType to, AppointmentStatusType from) =>
        Act(() => GetRequiredService<StatusChangeEmailHandler>().HandleEventAsync(
            new AppointmentStatusChangedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                FromStatus = from,
                ToStatus = to,
                OccurredAt = When,
            }));

    private Task PublishSubmittedAsync(Guid changeRequestId, ChangeRequestType type) =>
        Act(() => GetRequiredService<ChangeRequestSubmittedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestSubmittedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = type,
                SubmittedByUserId = Guid.NewGuid(),
                OccurredAt = When,
            }));

    private Task PublishOpenedAsync(Guid changeRequestId, ChangeRequestSide askedSide, string linkHolder) =>
        Act(() => GetRequiredService<ChangeRequestConsentInfoEmailHandler>().HandleEventAsync(
            new ChangeRequestConsentOpenedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                RoundNumber = 1,
                ConsentLinkEmails = new List<string> { linkHolder },
                ConsentLinkSides = new List<ChangeRequestSide> { askedSide },
                OccurredAt = When,
            }));

    private Task PublishConsentRequestAsync(Guid changeRequestId, ChangeRequestSide side, string representative) =>
        Act(() => GetRequiredService<ChangeRequestConsentRequestEmailHandler>().HandleEventAsync(
            new ChangeRequestConsentRequestedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                OpposingRecipientEmail = representative,
                OpposingRecipientRole = side == ChangeRequestSide.SideA
                    ? RecipientRole.ApplicantAttorney
                    : RecipientRole.DefenseAttorney,
                Side = side,
                ConsentUrl = "https://test.local/consent/TEST-token",
                RoundNumber = 1,
                OccurredAt = When,
            }));

    private Task PublishApprovedAsync(Guid changeRequestId) =>
        Act(() => GetRequiredService<ChangeRequestApprovedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestApprovedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                OccurredAt = When,
            }));

    private Task PublishRejectedAsync(Guid changeRequestId) =>
        Act(() => GetRequiredService<ChangeRequestRejectedEmailHandler>().HandleEventAsync(
            new AppointmentChangeRequestRejectedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                ChangeRequestId = changeRequestId,
                TenantId = TenantsTestData.TenantARef,
                ChangeRequestType = ChangeRequestType.Reschedule,
                RejectionNotes = "TEST- not possible",
                OccurredAt = When,
            }));

    private Task PublishDocumentUploadedAsync(Guid? uploader) =>
        Act(() => GetRequiredService<DocumentUploadedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentUploadedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                UploadedByUserId = uploader,
                OccurredAt = When,
            }));

    private Task PublishDocumentAcceptedAsync() =>
        Act(() => GetRequiredService<DocumentAcceptedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentAcceptedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                AcceptedByUserId = Guid.NewGuid(),
                OccurredAt = When,
            }));

    private Task PublishDocumentRejectedAsync() =>
        Act(() => GetRequiredService<DocumentRejectedEmailHandler>().HandleEventAsync(
            new AppointmentDocumentRejectedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                AppointmentDocumentId = Guid.NewGuid(),
                TenantId = TenantsTestData.TenantARef,
                RejectionNotes = "TEST- unreadable",
                RejectedByUserId = Guid.NewGuid(),
                OccurredAt = When,
            }));

    private Task PublishIntakeChangedAsync(bool dateMoved) =>
        Act(() => GetRequiredService<IntakeChangedEmailHandler>().HandleEventAsync(
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

    private Task PublishDueDateReminderAsync() =>
        Act(() => GetRequiredService<AppointmentReminderEmailHandler>().HandleEventAsync(
            new AppointmentReminderEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                DaysUntilDue = 3,
                OccurredAt = When,
            }));

    private Task PublishAttorneyPacketAsync() =>
        Act(() => GetRequiredService<AttyCEPacketEmailHandler>().HandleEventAsync(
            new PacketGeneratedEto
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                PacketId = Guid.NewGuid(),
                Kind = PacketKind.AttorneyClaimExaminer,
                OccurredAt = When,
            }));
}
