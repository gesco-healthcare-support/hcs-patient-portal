using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
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
/// One email per person per event, for a CANCEL REQUEST (2026-11-03). The submit event is published on
/// the real local event bus, so EVERY subscribed handler runs; only the dispatcher (records) and the
/// party resolver (the parties each test chooses) are replaced. Together with the consent email the
/// opposing side's representative is mailed, the assertion is across all dispatches: each address
/// appears in exactly ONE of them, whatever the template.
/// </summary>
public class CancelRequestEmailsTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string PatientEmail = "TEST-cancel-patient@test.local";
    private const string ApplicantAttorney = "TEST-cancel-aa@test.local";
    private const string DefenseAttorney = "TEST-cancel-da@test.local";
    private const string OfficeInbox = "TEST-cancel-inbox@test.local";

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    /// <summary>
    /// A staff-filed cancellation asks BOTH sides' representatives (the attorneys) to agree. The inbox gets
    /// ONE email, not two (the clinical-staff cancellation email is retired); the attorneys get only their
    /// consent email; the patient and the Intake Staff get the information notice.
    /// </summary>
    [Fact]
    public async Task AStaffFiledCancelRequest_GivesEveryAddressExactlyOneEmail()
    {
        WithParties();
        var intake = await SeedUserAsync("Intake Staff", "cancel-intake", confirmed: true);
        var changeRequestId = await AddCancelRequestAsync();

        await PublishSubmittedAsync(changeRequestId, submitter: intake.Id);
        await PublishConsentAsync(changeRequestId, ApplicantAttorney);
        await PublishConsentAsync(changeRequestId, DefenseAttorney);

        EmailsPerAddress().ShouldAllBe(a => a.Value == 1, string.Join(", ", EmailsPerAddress().Select(a => $"{a.Key}={a.Value}")));
        EmailsPerAddress().Keys.ShouldBe(
            new[] { PatientEmail, ApplicantAttorney, DefenseAttorney, OfficeInbox, intake.Email },
            ignoreOrder: true);
        TemplatesFor(OfficeInbox).ShouldBe(new[] { NotificationTemplateConsts.Codes.AppointmentCancelledRequest });
        TemplatesFor(ApplicantAttorney).ShouldBe(new[] { NotificationTemplateConsts.Codes.ChangeRequestConsentRequest });
        TemplatesFor(DefenseAttorney).ShouldBe(new[] { NotificationTemplateConsts.Codes.ChangeRequestConsentRequest });
        _dispatcher.Dispatches.ShouldNotContain(d => d.TemplateCode == NotificationTemplateConsts.Codes.ClinicalStaffCancellation);
    }

    /// <summary>
    /// A cancellation the patient files asks the OPPOSING side (the defense attorney) to agree: that person
    /// gets only the consent email. The patient's own side, the inbox and the Intake Staff get the notice once.
    /// </summary>
    [Fact]
    public async Task APartyFiledCancelRequest_TheOpposingRepresentativeGetsOnlyTheConsentEmail()
    {
        WithParties();
        var intake = await SeedUserAsync("Intake Staff", "cancel-party-intake", confirmed: true);
        var patientLogin = await SeedUserAsync("Patient", "cancel-patient-login", confirmed: true, email: PatientEmail);
        var changeRequestId = await AddCancelRequestAsync();

        await PublishSubmittedAsync(changeRequestId, submitter: patientLogin.Id);
        await PublishConsentAsync(changeRequestId, DefenseAttorney);

        EmailsPerAddress().ShouldAllBe(a => a.Value == 1, string.Join(", ", EmailsPerAddress().Select(a => $"{a.Key}={a.Value}")));
        TemplatesFor(DefenseAttorney).ShouldBe(new[] { NotificationTemplateConsts.Codes.ChangeRequestConsentRequest });
        TemplatesFor(ApplicantAttorney).ShouldBe(new[] { NotificationTemplateConsts.Codes.AppointmentCancelledRequest });
        TemplatesFor(OfficeInbox).ShouldBe(new[] { NotificationTemplateConsts.Codes.AppointmentCancelledRequest });
        TemplatesFor(intake.Email).ShouldBe(new[] { NotificationTemplateConsts.Codes.AppointmentCancelledRequest });
    }

    /// <summary>
    /// Control for the Facts above: with nobody holding a consent link (a submitter whose side cannot be
    /// resolved), the information notice still reaches every party, the inbox and the Intake Staff once.
    /// </summary>
    [Fact]
    public async Task WithNoConsentLinkHolder_EveryPartyGetsTheInformationNoticeOnce()
    {
        WithParties();
        await SeedUserAsync("Staff Supervisor", "cancel-unresolved-submitter", confirmed: true);
        var changeRequestId = await AddCancelRequestAsync();

        // A submitter id that is no account: external, no email, no role -> no side -> nobody holds a link.
        await PublishSubmittedAsync(changeRequestId, submitter: Guid.NewGuid());

        var notice = _dispatcher.Dispatches.ShouldHaveSingleItem();
        notice.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequest);
        notice.Recipients.ShouldBe(
            new[] { PatientEmail, ApplicantAttorney, DefenseAttorney, OfficeInbox },
            ignoreOrder: true);
    }

    // ------------------------------------------------------------------------

    private void WithParties()
    {
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = ApplicantAttorney, Role = RecipientRole.ApplicantAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = DefenseAttorney, Role = RecipientRole.DefenseAttorney, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = PatientEmail, Role = RecipientRole.Patient, IsRegistered = true });
        _parties.Recipients.Add(new SendAppointmentEmailArgs { To = OfficeInbox, Role = RecipientRole.OfficeAdmin, IsRegistered = true });
    }

    private Dictionary<string, int> EmailsPerAddress() =>
        _dispatcher.Dispatches
            .SelectMany(d => d.Recipients)
            .GroupBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

    private List<string> TemplatesFor(string address) =>
        _dispatcher.Dispatches
            .Where(d => d.Recipients.Contains(address, StringComparer.OrdinalIgnoreCase))
            .Select(d => d.TemplateCode)
            .ToList();

    private async Task<(Guid Id, string Email)> SeedUserAsync(string role, string label, bool confirmed, string? email = null)
    {
        (Guid Id, string Email) seeded = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var address = await RoleUserSeeder.CreateUserInRoleAsync(
                    users, GetRequiredService<IdentityRoleManager>(), TenantsTestData.TenantARef, role, label);
                var user = (await users.FindByEmailAsync(address))!;
                if (email != null)
                {
                    (await users.SetEmailAsync(user, email)).Succeeded.ShouldBeTrue();
                }
                user.SetEmailConfirmed(confirmed);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
                seeded = (user.Id, user.Email!);
            }
        });
        return seeded;
    }

    private async Task<Guid> AddCancelRequestAsync()
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
                        id, TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id,
                        ChangeRequestType.Cancel, cancellationReason: "TEST- cancel", reScheduleReason: null,
                        newDoctorAvailabilityId: null),
                    autoSave: true);
            }
        });
        _dispatcher.Clear();
        return id;
    }

    private Task PublishSubmittedAsync(Guid changeRequestId, Guid submitter) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new AppointmentChangeRequestSubmittedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ChangeRequestId = changeRequestId,
            TenantId = TenantsTestData.TenantARef,
            ChangeRequestType = ChangeRequestType.Cancel,
            SubmittedByUserId = submitter,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));

    private Task PublishConsentAsync(Guid changeRequestId, string holder) => WithUnitOfWorkAsync(() =>
        GetRequiredService<ILocalEventBus>().PublishAsync(new ChangeRequestConsentRequestedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ChangeRequestId = changeRequestId,
            TenantId = TenantsTestData.TenantARef,
            ChangeRequestType = ChangeRequestType.Cancel,
            OpposingRecipientEmail = holder,
            OpposingRecipientRole = RecipientRole.DefenseAttorney,
            ConsentUrl = "https://tenant.portal.test.local/consent/TEST-token",
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));
}
