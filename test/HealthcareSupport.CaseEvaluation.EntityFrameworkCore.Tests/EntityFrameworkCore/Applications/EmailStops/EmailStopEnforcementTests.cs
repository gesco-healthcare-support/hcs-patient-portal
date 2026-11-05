using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
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

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.EmailStops;

/// <summary>
/// 2026-11-05 email stops on the real rig: the REAL notification dispatcher, stop policy, outbox and
/// database, driven by the REAL routing handlers. Only the template renderer (a fixed subject and body, so no
/// per-office template rows are needed) and the party resolver (the parties each test chooses) are
/// replaced. What is asserted is the outbox row that would be sent: its To and every CC.
/// </summary>
public class EmailStopEnforcementTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string Lawyer = "TEST-es-lawyer@test.local";
    private const string DefenseAttorney = "TEST-es-da@test.local";
    private const string ClaimExaminer = "TEST-es-ce@test.local";
    private const string PatientEmail = "TEST-es-patient@test.local";
    private const string OfficeInbox = "TEST-es-inbox@test.local";

    private static readonly DateTime When = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationTemplateRenderer>(new FixedRenderer()));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    /// <summary>
    /// A "this case only" stop leaves the lawyer off EVERY email of that case -- the request (where they are
    /// a CC), the approval, the attorney packet, the Agree/Decline link and the change-request result -- while
    /// everyone else still gets each one. A control run without the stop shows the lawyer in those emails.
    /// </summary>
    [Fact]
    public async Task ACaseStop_LeavesTheLawyerOffEveryEmailOfThatCase_AndEveryoneElseStillGetsThem()
    {
        await ArrangeAsync();

        var control = await RowsWrittenByAsync(RaiseEveryCaseEmailAsync);
        control.ShouldContain(r => Addresses(r).Contains(Lawyer, StringComparer.OrdinalIgnoreCase),
            "positive control: without a stop the lawyer is in the case emails");

        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);
        var stopped = await RowsWrittenByAsync(RaiseEveryCaseEmailAsync);

        stopped.ShouldNotBeEmpty();
        stopped.ShouldAllBe(r => !Addresses(r).Contains(Lawyer, StringComparer.OrdinalIgnoreCase),
            "a stopped address is never a To and never a CC");
        stopped.ShouldContain(r => Addresses(r).Contains(DefenseAttorney, StringComparer.OrdinalIgnoreCase),
            "the other parties still get the case emails");
        stopped.Select(r => r.Context).ShouldContain(c => c.StartsWith("AppointmentRequested/", StringComparison.Ordinal),
            "the request still went out, to the people who were not stopped");
    }

    /// <summary>The case stop is per case: the same address on another case still gets mail.</summary>
    [Fact]
    public async Task ACaseStop_DoesNotReachAnotherCase()
    {
        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);

        var thisCase = await DispatchAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, Lawyer, DefenseAttorney);
        var otherCase = await DispatchAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, Lawyer, DefenseAttorney);

        thisCase.Select(r => r.To).ShouldBe(new[] { DefenseAttorney });
        otherCase.Select(r => r.To).OrderBy(a => a).ShouldBe(new[] { DefenseAttorney, Lawyer }.OrderBy(a => a));
    }

    /// <summary>An all-cases stop (the host list) silences the address in every practice.</summary>
    [Fact]
    public async Task AnAllCasesStop_SilencesTheAddressInBothOffices()
    {
        await AddGlobalStopAsync(Lawyer);

        var officeA = await DispatchAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, Lawyer, DefenseAttorney);
        var officeB = await DispatchAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, Lawyer, DefenseAttorney);
        var notACase = await DispatchAsync(TenantsTestData.TenantBRef, null, Lawyer, DefenseAttorney);

        officeA.Select(r => r.To).ShouldBe(new[] { DefenseAttorney });
        officeB.Select(r => r.To).ShouldBe(new[] { DefenseAttorney });
        notACase.Select(r => r.To).ShouldBe(new[] { DefenseAttorney });
    }

    /// <summary>A stopped To hands the email to the first remaining CC; a stopped CC is just left off.</summary>
    [Fact]
    public async Task AStoppedTo_PromotesTheFirstCc_AndAStoppedCcIsLeftOff()
    {
        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);

        var promoted = await DispatchToWithCcAsync(Lawyer, DefenseAttorney, ClaimExaminer);
        var ccDropped = await DispatchToWithCcAsync(DefenseAttorney, Lawyer, ClaimExaminer);
        var nobodyLeft = await DispatchToWithCcAsync(Lawyer);

        var row = promoted.ShouldHaveSingleItem();
        row.To.ShouldBe(DefenseAttorney);
        row.GetCcList().ShouldBe(new[] { ClaimExaminer });
        var second = ccDropped.ShouldHaveSingleItem();
        second.To.ShouldBe(DefenseAttorney);
        second.GetCcList().ShouldBe(new[] { ClaimExaminer });
        nobodyLeft.ShouldBeEmpty();
    }

    /// <summary>Turning emails back on (soft delete, both scopes) restores the mail.</summary>
    [Fact]
    public async Task TurningEmailsBackOn_RestoresTheMail()
    {
        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);
        var globalId = await AddGlobalStopAsync(Lawyer);
        (await DispatchAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, Lawyer)).ShouldBeEmpty();

        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var service = GetRequiredService<IEmailStopsAppService>();
                await service.ResumeForAppointmentAsync(AppointmentsTestData.Appointment1Id, Lawyer);
                await service.DeleteAsync(globalId);
            }
        });

        (await DispatchAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, Lawyer))
            .Select(r => r.To).ShouldBe(new[] { Lawyer });
    }

    /// <summary>Self-service account mail is never stopped: activation and password reset still go out.</summary>
    [Fact]
    public async Task AccountMail_StillReachesAStoppedAddress()
    {
        await AddGlobalStopAsync(Lawyer);

        foreach (var code in new[] { NotificationTemplateConsts.Codes.AccountActivation, NotificationTemplateConsts.Codes.ResetPassword })
        {
            var rows = await RowsWrittenByAsync(() => WithUnitOfWorkAsync(async () =>
            {
                using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
                {
                    await GetRequiredService<INotificationDispatcher>().DispatchAsync(
                        NotificationCase.None, code, new[] { new NotificationRecipient(Lawyer) },
                        new Dictionary<string, object?>(), $"TEST-account/{code}/{Guid.NewGuid():N}");
                }
            }));
            rows.Select(r => r.To).ShouldBe(new[] { Lawyer }, code);
        }
    }

    /// <summary>
    /// A row written BEFORE the stop is caught at the drain: a row whose only address is stopped is marked
    /// Stopped and never sent; a row with others left goes out without the stopped address.
    /// </summary>
    [Fact]
    public async Task TheDrain_StopsRowsWrittenBeforeTheStop()
    {
        var solo = (await DispatchAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, Lawyer)).ShouldHaveSingleItem();
        var shared = (await DispatchToWithCcAsync(Lawyer, DefenseAttorney)).ShouldHaveSingleItem();
        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);

        var sender = new RecordingSender();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var drain = new OutboxDrainService(
                    GetRequiredService<NotificationOutboxManager>(),
                    sender,
                    GetRequiredService<Volo.Abp.Timing.IClock>(),
                    GetRequiredService<Volo.Abp.Settings.ISettingProvider>(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboxDrainService>.Instance,
                    GetRequiredService<IEmailStopPolicy>());
                await drain.DrainDueAsync(500);
            }
        });

        var soloAfter = await GetRowAsync(solo.Id);
        soloAfter.Status.ShouldBe(NotificationOutboxStatus.Stopped);
        sender.Sent.ShouldNotContain(a => a.To == Lawyer || a.Cc.Contains(Lawyer));
        var sharedSend = sender.Sent.Single(a => a.IdempotencyKey == shared.IdempotencyKey);
        sharedSend.To.ShouldBe(DefenseAttorney);
        sharedSend.Cc.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------- the staff API

    [Fact]
    public async Task Stopping_RefusesTheOfficeInboxAndAnInvalidAddress()
    {
        await SetOfficeInboxAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var service = GetRequiredService<IEmailStopsAppService>();
                var inbox = await Should.ThrowAsync<BusinessException>(() => service.StopForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new StopAppointmentEmailsInput { Email = OfficeInbox, Scope = EmailStopScope.AllCases }));
                inbox.Code.ShouldBe(CaseEvaluationDomainErrorCodes.EmailStopInternalAddress);

                var invalid = await Should.ThrowAsync<BusinessException>(() => service.CreateAsync(
                    new CreateGlobalEmailStopInput { Email = "not-an-address" }));
                invalid.Code.ShouldBe(CaseEvaluationDomainErrorCodes.EmailStopInvalidEmail);
            }
        });
    }

    /// <summary>
    /// Only external people can be stopped: a host account (every host login is staff) and an office account
    /// holding a staff role are refused, while an office account holding an external role is accepted.
    /// </summary>
    [Fact]
    public async Task Stopping_RefusesAStaffLogin_OnTheHostOrInTheOffice_AndAcceptsAnExternalOne()
    {
        const string hostStaff = "TEST-es-host-staff@test.local";
        const string officeStaff = "TEST-es-office-intake@test.local";
        const string officeExternal = "TEST-es-office-attorney@test.local";
        await SeedUserAsync(null, "Staff Supervisor", "es-host", hostStaff);
        await SeedUserAsync(TenantsTestData.TenantARef, "Intake Staff", "es-intake", officeStaff);
        await SeedUserAsync(TenantsTestData.TenantARef, "Applicant Attorney", "es-attorney", officeExternal);

        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var service = GetRequiredService<IEmailStopsAppService>();
                foreach (var staff in new[] { hostStaff, officeStaff })
                {
                    var refused = await Should.ThrowAsync<BusinessException>(() => service.StopForAppointmentAsync(
                        AppointmentsTestData.Appointment1Id,
                        new StopAppointmentEmailsInput { Email = staff, Scope = EmailStopScope.Case }));
                    refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.EmailStopInternalAddress, staff);
                }

                var accepted = await service.StopForAppointmentAsync(
                    AppointmentsTestData.Appointment1Id,
                    new StopAppointmentEmailsInput { Email = officeExternal, Scope = EmailStopScope.Case });
                accepted.Scope.ShouldBe(EmailStopScope.Case);
            }
        });
    }

    [Fact]
    public async Task Stopping_IsIdempotent_AndAnAllCasesStopRecordsThePracticeItCameFrom()
    {
        EmailStopStatusDto first = null!, second = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var service = GetRequiredService<IEmailStopsAppService>();
                var input = new StopAppointmentEmailsInput { Email = Lawyer, Scope = EmailStopScope.AllCases, Note = "TEST- asked by phone" };
                first = await service.StopForAppointmentAsync(AppointmentsTestData.Appointment1Id, input);
                second = await service.StopForAppointmentAsync(AppointmentsTestData.Appointment1Id, input);
            }
        });

        second.Id.ShouldBe(first.Id);
        first.Scope.ShouldBe(EmailStopScope.AllCases);
        first.StoppedFromOfficeName.ShouldBe(TenantsTestData.TenantAName);
        first.Note.ShouldBe("TEST- asked by phone");

        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                var list = await GetRequiredService<IEmailStopsAppService>().GetListAsync(new GetGlobalEmailStopsInput { MaxResultCount = 50 });
                list.Items.Count(i => i.Email == Lawyer).ShouldBe(1);
            }
        });
    }

    [Fact]
    public async Task TheCard_ListsTheCaseExternalAddresses_WithTheirStops_AndNeverTheOfficeInbox()
    {
        await ArrangeAsync();
        await AddCaseStopAsync(AppointmentsTestData.Appointment1Id, Lawyer);
        await AddGlobalStopAsync(ClaimExaminer);

        AppointmentEmailRecipientsDto card = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                card = await GetRequiredService<IEmailStopsAppService>().GetAppointmentRecipientsAsync(AppointmentsTestData.Appointment1Id);
            }
        });

        card.Items.Select(i => i.Email).ShouldNotContain(OfficeInbox);
        var lawyer = card.Items.Single(i => i.Email == Lawyer);
        lawyer.Kinds.ShouldContain(EmailRecipientKind.ApplicantAttorney);
        lawyer.CaseStop.ShouldNotBeNull();
        lawyer.AllCasesStop.ShouldBeNull();
        var examiner = card.Items.Single(i => i.Email == ClaimExaminer);
        examiner.AllCasesStop.ShouldNotBeNull();
        examiner.CaseStop.ShouldBeNull();
        var da = card.Items.Single(i => i.Email == DefenseAttorney);
        da.CaseStop.ShouldBeNull();
        da.AllCasesStop.ShouldBeNull();
    }

    [Fact]
    public async Task TheConsentWarning_FiresOnlyWhenTheAddressIsTheLastOneLeftForASide()
    {
        await ArrangeAsync();

        var lawyerAlone = await ConsentImpactAsync(Lawyer);
        lawyerAlone.ThisCaseSides.ShouldBe(new[] { ChangeRequestSide.SideA });
        lawyerAlone.HasImpact.ShouldBeTrue();

        // The other side's lawyer is the only one for side B.
        (await ConsentImpactAsync(DefenseAttorney)).ThisCaseSides.ShouldBe(new[] { ChangeRequestSide.SideB });

        // Someone not able to answer for any side: no warning.
        (await ConsentImpactAsync(PatientEmail)).HasImpact.ShouldBeFalse();
    }

    // ---------------------------------------------------------------- arrange + act helpers

    private async Task ArrangeAsync()
    {
        await SetOfficeInboxAsync();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await AccessorTestSeeder.RemoveAllAsync(
                    GetRequiredService<IRepository<AppointmentAccessor, Guid>>(), AppointmentsTestData.Appointment1Id);
                var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
                var appointment = await appointments.GetAsync(AppointmentsTestData.Appointment1Id);
                appointment.PatientEmail = PatientEmail;
                appointment.ApplicantAttorneyEmail = Lawyer;
                appointment.DefenseAttorneyEmail = DefenseAttorney;
                appointment.AppointmentStatus = AppointmentStatusType.Approved;
                await appointments.UpdateAsync(appointment, autoSave: true);
                var patients = GetRequiredService<IRepository<Patient, Guid>>();
                var patient = await patients.GetAsync(appointment.PatientId);
                patient.Email = PatientEmail;
                await patients.UpdateAsync(patient, autoSave: true);
            }
        });

        _parties.Recipients.Clear();
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = Lawyer, Role = RecipientRole.ApplicantAttorney, IsRegistered = true, Context = "Resolver/Submitted/ApplicantAttorney/x/aa-email-col" });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = DefenseAttorney, Role = RecipientRole.DefenseAttorney, IsRegistered = true, Context = "Resolver/Submitted/DefenseAttorney/x/da-email-col" });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ClaimExaminer, Role = RecipientRole.ClaimExaminer, IsRegistered = true, Context = "Resolver/Submitted/ClaimExaminer/x/ce-email-col" });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = PatientEmail, Role = RecipientRole.Patient, IsRegistered = true, Context = "Resolver/Submitted/Patient/x/patient" });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = OfficeInbox, Role = RecipientRole.OfficeAdmin, IsRegistered = true, Context = "Resolver/Submitted/OfficeAdmin/x/office" });
    }

    private Task SeedUserAsync(Guid? tenantId, string role, string label, string email) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(tenantId))
        {
            var users = GetRequiredService<IdentityUserManager>();
            var address = await RoleUserSeeder.CreateUserInRoleAsync(
                users, GetRequiredService<IdentityRoleManager>(), tenantId, role, label);
            var user = (await users.FindByEmailAsync(address))!;
            (await users.SetEmailAsync(user, email)).Succeeded.ShouldBeTrue();
            (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
        }
    });

    private Task SetOfficeInboxAsync() => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, OfficeInbox);
        }
    });

    private async Task RaiseEveryCaseEmailAsync()
    {
        var a = AppointmentsTestData.Appointment1Id;
        var office = TenantsTestData.TenantARef;
        await WithUnitOfWorkAsync(() => GetRequiredService<BookingSubmissionEmailHandler>().HandleEventAsync(new AppointmentSubmittedEto
        {
            AppointmentId = a,
            TenantId = office,
            BookerUserId = Guid.NewGuid(),
            PatientId = PatientsTestData.Patient1Id,
            RequestConfirmationNumber = AppointmentsTestData.Appointment1RequestConfirmationNumber,
            AppointmentDate = new DateTime(2026, 12, 15, 9, 30, 0, DateTimeKind.Utc),
            SubmittedAt = When,
        }));
        await WithUnitOfWorkAsync(() => GetRequiredService<StatusChangeEmailHandler>().HandleEventAsync(new AppointmentStatusChangedEto
        {
            AppointmentId = a,
            TenantId = office,
            FromStatus = AppointmentStatusType.Pending,
            ToStatus = AppointmentStatusType.Approved,
            OccurredAt = When,
        }));
        await WithUnitOfWorkAsync(() => GetRequiredService<AttyCEPacketEmailHandler>().HandleEventAsync(new PacketGeneratedEto
        {
            AppointmentId = a,
            TenantId = office,
            PacketId = Guid.NewGuid(),
            Kind = PacketKind.AttorneyClaimExaminer,
            OccurredAt = When,
        }));
        var changeRequestId = await AddChangeRequestAsync();
        await WithUnitOfWorkAsync(() => GetRequiredService<ChangeRequestConsentRequestEmailHandler>().HandleEventAsync(new ChangeRequestConsentRequestedEto
        {
            AppointmentId = a,
            ChangeRequestId = changeRequestId,
            TenantId = office,
            ChangeRequestType = ChangeRequestType.Reschedule,
            OpposingRecipientEmail = Lawyer,
            OpposingRecipientRole = RecipientRole.ApplicantAttorney,
            Side = ChangeRequestSide.SideA,
            ConsentUrl = "https://test.local/consent/TEST-token",
            RoundNumber = 1,
            OccurredAt = When,
        }));
        await WithUnitOfWorkAsync(() => GetRequiredService<ChangeRequestApprovedEmailHandler>().HandleEventAsync(new AppointmentChangeRequestApprovedEto
        {
            AppointmentId = a,
            ChangeRequestId = changeRequestId,
            TenantId = office,
            ChangeRequestType = ChangeRequestType.Reschedule,
            OccurredAt = When,
        }));
    }

    private async Task<Guid> AddChangeRequestAsync()
    {
        var id = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>().InsertAsync(
                    new AppointmentChangeRequest(
                        id, TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, ChangeRequestType.Reschedule,
                        cancellationReason: null, reScheduleReason: "TEST- reschedule", newDoctorAvailabilityId: null),
                    autoSave: true);
            }
        });
        return id;
    }

    private Task AddCaseStopAsync(Guid appointmentId, string email) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<IRepository<AppointmentEmailStop, Guid>>().InsertAsync(
                new AppointmentEmailStop(Guid.NewGuid(), TenantsTestData.TenantARef, appointmentId, email, null, "TEST staff", When, null),
                autoSave: true);
        }
    });

    private async Task<Guid> AddGlobalStopAsync(string email)
    {
        var id = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                await GetRequiredService<IRepository<GlobalEmailStop, Guid>>().InsertAsync(
                    new GlobalEmailStop(id, email, null, "TEST staff", When, TenantsTestData.TenantARef, TenantsTestData.TenantAName, null),
                    autoSave: true);
            }
        });
        return id;
    }

    private Task<List<NotificationOutboxItem>> DispatchAsync(Guid officeId, Guid? appointmentId, params string[] to) =>
        RowsWrittenByAsync(() => WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                await GetRequiredService<INotificationDispatcher>().DispatchAsync(
                    appointmentId.HasValue ? NotificationCase.Of(appointmentId.Value) : NotificationCase.None,
                    NotificationTemplateConsts.Codes.AppointmentApprovedStakeholderEmails,
                    to.Select(a => new NotificationRecipient(a)).ToList(),
                    new Dictionary<string, object?>(),
                    $"TEST-es/{Guid.NewGuid():N}");
            }
        }), officeId);

    private Task<List<NotificationOutboxItem>> DispatchToWithCcAsync(string to, params string[] cc) =>
        RowsWrittenByAsync(() => WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await GetRequiredService<INotificationDispatcher>().DispatchToWithCcAsync(
                    NotificationCase.Of(AppointmentsTestData.Appointment1Id),
                    NotificationTemplateConsts.Codes.AppointmentApprovedStakeholderEmails,
                    new NotificationRecipient(to),
                    cc.Select(a => new NotificationRecipient(a)).ToList(),
                    new Dictionary<string, object?>(),
                    $"TEST-es-cc/{Guid.NewGuid():N}");
            }
        }));

    private async Task<List<NotificationOutboxItem>> RowsWrittenByAsync(Func<Task> act, Guid? officeId = null)
    {
        var office = officeId ?? TenantsTestData.TenantARef;
        var before = (await RowsAsync(office)).Select(r => r.Id).ToHashSet();
        await act();
        return (await RowsAsync(office)).Where(r => !before.Contains(r.Id)).ToList();
    }

    private async Task<List<NotificationOutboxItem>> RowsAsync(Guid officeId)
    {
        List<NotificationOutboxItem> rows = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                rows = await GetRequiredService<IRepository<NotificationOutboxItem, Guid>>().GetListAsync();
            }
        });
        return rows;
    }

    private async Task<NotificationOutboxItem> GetRowAsync(Guid id)
    {
        NotificationOutboxItem row = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                row = await GetRequiredService<IRepository<NotificationOutboxItem, Guid>>().GetAsync(id);
            }
        });
        return row;
    }

    private async Task<EmailStopConsentImpactDto> ConsentImpactAsync(string email)
    {
        EmailStopConsentImpactDto result = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                result = await GetRequiredService<IEmailStopsAppService>().GetConsentImpactAsync(
                    AppointmentsTestData.Appointment1Id, email, EmailStopScope.Case);
            }
        });
        return result;
    }

    private static List<string> Addresses(NotificationOutboxItem row) => row.GetCcList().Prepend(row.To).ToList();

    private sealed class FixedRenderer : INotificationTemplateRenderer
    {
        public Task<RenderedNotification> RenderAsync(
            string templateCode,
            IReadOnlyDictionary<string, object?> variables,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedNotification("TEST subject " + templateCode, "<p>TEST body</p>", null));
    }

    private sealed class RecordingSender : IOutboxEmailSender
    {
        public List<SendAppointmentEmailArgs> Sent { get; } = new();

        public Task SendAsync(SendAppointmentEmailArgs args)
        {
            Sent.Add(args);
            return Task.CompletedTask;
        }
    }
}
