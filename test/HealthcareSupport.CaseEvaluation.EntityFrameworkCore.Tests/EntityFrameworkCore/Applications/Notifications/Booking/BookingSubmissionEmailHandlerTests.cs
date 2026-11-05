using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Booking;

/// <summary>
/// <see cref="BookingSubmissionEmailHandler"/> on the real rig, for the paths no other test reaches:
/// the office mailbox's own "new request" notice, the booker-name fallbacks it carries, a booker who
/// cannot be found (treated as external, so the office's staff are told), and the guards. The staff
/// fan-out is asserted against OFFICE decoys: the same two roles in office B, whose roles sort first
/// (see <see cref="RoleUserSeeder.CreateRoleSortingFirstAsync"/>). Only the dispatcher and the
/// stakeholder resolver are replaced, so no email sender is resolved.
/// </summary>
public class BookingSubmissionEmailHandlerTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string OfficeMailbox = "TEST-booking-office-mailbox@test.local";
    private const string CcAddress = "TEST-booking-cc@test.local";

    // Field initializers run before the base constructor, so both exist when AfterAddApplication runs.
    private readonly RecordingNotificationDispatcher _dispatcher = new();
    private readonly StubAppointmentRecipientResolver _parties = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_dispatcher));
        services.Replace(ServiceDescriptor.Singleton<IAppointmentRecipientResolver>(_parties));
    }

    [Fact]
    public async Task TheOfficeMailbox_GetsItsOwnNotice_ApartFromThePartiesNotice()
    {
        WithPatientAndOfficeMailbox();
        await SetSubmitterAsync(IdentityUsersTestData.Patient1UserId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        var parties = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        parties.To.ShouldBe(IdentityUsersTestData.Patient1Email);
        parties.Recipients.ShouldNotContain(OfficeMailbox);

        var office = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice);
        office.Recipients.ShouldBe(new[] { OfficeMailbox });
        office.Variables["RoleDisplayName"].ShouldBe("office");
        office.Variables["AppointmentRequestConfirmationNumber"].ShouldBe(AppointmentsTestData.Appointment1RequestConfirmationNumber);
        office.Variables["RegisterUrl"].ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();
        office.Variables["LoginUrl"].ShouldBeOfType<string>().ShouldNotBeNullOrWhiteSpace();

        // No staff exist in this fixture, so the external-booker staff notice is skipped.
        _dispatcher.Dispatches.ShouldNotContain(d => d.TemplateCode == NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
    }

    [Fact]
    public async Task TheOfficeNotice_NamesABookerWithANameByThatName()
    {
        WithPatientAndOfficeMailbox();
        await NamePatient1UserAsync("TEST-Booker", "TEST-Named");
        await SetSubmitterAsync(IdentityUsersTestData.Patient1UserId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice).Variables["BookerFullName"]
            .ShouldBe("TEST-Booker TEST-Named");
    }

    [Fact]
    public async Task TheOfficeNotice_FallsBackToThePatientsName_WhenThereIsNoBookerAccount()
    {
        WithPatientAndOfficeMailbox();

        await RaiseAsync(bookerUserId: Guid.Empty);

        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice).Variables["BookerFullName"]
            .ShouldBe($"{PatientsTestData.Patient1FirstName} {PatientsTestData.Patient1LastName}");
    }

    [Fact]
    public async Task TheOfficeNotice_SaysUnknownBooker_WhenThereIsNeitherAccountNorPatient()
    {
        WithPatientAndOfficeMailbox();

        await RaiseAsync(bookerUserId: Guid.Empty, patientId: Guid.NewGuid());

        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice).Variables["BookerFullName"]
            .ShouldBe("(unknown booker)");
    }

    [Fact]
    public async Task ABookerWhoCannotBeFound_IsExternal_SoThisOfficesIntakeStaffAreTold_AndNoSupervisorOrOtherOffice()
    {
        WithPatientAndOfficeMailbox();
        var (supervisor, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking", lowIdRoles: false);
        var (otherSupervisor, otherIntake) = await SeedStaffAsync(TenantsTestData.TenantBRef, "booking-officeb", lowIdRoles: true);

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        var staff = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
        // 2026-11-02: only the Intake Staff are asked to approve; Staff Supervisors get no per-event mail.
        staff.Recipients.ShouldBe(new[] { intake });
        staff.Recipients.ShouldNotContain(supervisor);
        staff.Recipients.ShouldNotContain(otherSupervisor);
        staff.Recipients.ShouldNotContain(otherIntake);
    }

    [Fact]
    public async Task ASwitchedOffStaffUser_IsNotToldAboutTheRequest()
    {
        WithPatientAndOfficeMailbox();
        var (supervisor, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking-on", lowIdRoles: false);
        var switchedOff = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                switchedOff = await RoleUserSeeder.CreateInactiveUserInRoleAsync(
                    GetRequiredService<IdentityUserManager>(), GetRequiredService<IdentityRoleManager>(),
                    TenantsTestData.TenantARef, "Intake Staff", "booking-off");
            }
        });

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        var staff = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
        staff.Recipients.ShouldBe(new[] { intake });
        staff.Recipients.ShouldNotContain(supervisor);
        staff.Recipients.ShouldNotContain(switchedOff);
    }

    /// <summary>
    /// 2026-11-02: the office's CC list (<c>SystemParameter.CcEmailIds</c>) is no longer appended anywhere,
    /// so an office with no Intake Staff sends no approve/reject request at all, whatever its CC list holds.
    /// The same booking with an Intake user DOES send (positive control: the first Fact above).
    /// </summary>
    [Fact]
    public async Task AnOfficeWithNoIntakeStaff_SendsNoApproveRequest_EvenWithACcList()
    {
        WithPatientAndOfficeMailbox();
        await SetCcListAsync(CcAddress);

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        _dispatcher.Dispatches.Where(d => d.TemplateCode == NotificationTemplateConsts.Codes.PatientAppointmentApproveReject)
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task TheApproveRequest_GoesToIntakeOnly_EvenWhenTheOfficeInboxIsSet()
    {
        WithPatientAndOfficeMailbox();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, "TEST-booking-inbox@test.local");
            }
        });
        var (_, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking-inbox", lowIdRoles: false);

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        var staff = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
        staff.Recipients.ShouldBe(new[] { intake });
    }

    [Fact]
    public async Task TheApproveRequest_NeverIncludesTheCcList_AndNamesEachIntakeAddressOnce()
    {
        WithPatientAndOfficeMailbox();
        await SetCcListAsync(CcAddress);
        var (_, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking-cc", lowIdRoles: false);

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        var staff = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
        staff.Recipients.ShouldBe(new[] { intake });
        staff.Recipients.ShouldNotContain(CcAddress);
    }

    /// <summary>
    /// 2026-10-27: the request to approve goes to staff, who sign in on the staff site; a staff login does
    /// not work on the office site, so the link in that email is the staff site's.
    /// </summary>
    [Fact]
    public async Task TheStaffNotice_LinksToTheStaffSite_NotTheOfficeSite()
    {
        WithPatientAndOfficeMailbox();
        await SeedStaffAsync(TenantsTestData.TenantARef, "booking-link", lowIdRoles: false);
        var urls = GetRequiredService<IAccountUrlBuilder>();
        var staffSite = await WithUnitOfWorkAsync(() => urls.BuildPortalRootUrlAsync(null));
        var officeSite = await WithUnitOfWorkAsync(() => urls.BuildPortalRootUrlAsync(TenantsTestData.TenantARef));
        staffSite.ShouldNotBe(officeSite, "the two sites must differ or this test proves nothing");

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject).Variables["PortalUrl"].ShouldBe(staffSite);
    }

    [Fact]
    public async Task NoParties_SendsNoRequestNotice()
    {
        // Positive control: the first Fact, the same booking with the parties resolved.
        // 2026-11-04: the shared seed's accessor of Appointment1 is a case follower and would be mailed,
        // so this test (about "no parties") takes it off the appointment first.
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await AccessorTestSeeder.RemoveAllAsync(
                    GetRequiredService<IRepository<AppointmentAccessor, Guid>>(), AppointmentsTestData.Appointment1Id);
            }
        });

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownAppointment_SendsNothing()
    {
        WithPatientAndOfficeMailbox();

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId, appointmentId: Guid.NewGuid());

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullEvent_SendsNothing()
    {
        WithPatientAndOfficeMailbox();
        _dispatcher.Clear();

        await WithUnitOfWorkAsync(() => GetRequiredService<BookingSubmissionEmailHandler>().HandleEventAsync(null!));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task AMissingTemplate_IsSwallowed_SoTheBookingStands()
    {
        WithPatientAndOfficeMailbox();
        _dispatcher.ThrowTemplateNotFound = true;

        await Should.NotThrowAsync(() => RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId));

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    // ---- 2026-11-03: the booker is the SUBMITTER (the appointment's creator), not the patient login ----

    /// <summary>
    /// The request notice is addressed To the account that submitted the request, even when that login
    /// is not the patient's, and the patient -- who did not submit it -- hears nothing before approval.
    /// </summary>
    [Fact]
    public async Task ASubmittingAttorney_IsToldOfTheRequest_AndThePatientIsNot()
    {
        _parties.Recipients.Add(Party(IdentityUsersTestData.ApplicantAttorney1Email, RecipientRole.ApplicantAttorney));
        _parties.Recipients.Add(Party(PatientsTestData.Patient1Email, RecipientRole.Patient));
        _parties.Recipients.Add(Party(OfficeMailbox, RecipientRole.OfficeAdmin));
        await SetSubmitterAsync(IdentityUsersTestData.ApplicantAttorney1UserId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        var parties = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        parties.To.ShouldBe(IdentityUsersTestData.ApplicantAttorney1Email);
        parties.Recipients.ShouldNotContain(PatientsTestData.Patient1Email);
        Sent(NotificationTemplateConsts.Codes.AppointmentRequestedOffice).Recipients.ShouldBe(new[] { OfficeMailbox });
    }

    /// <summary>The control for the Fact above: the same booking submitted by the patient keeps the patient on it.</summary>
    [Fact]
    public async Task ThePatientWhoSubmittedTheirOwnRequest_IsToldOfIt()
    {
        _parties.Recipients.Add(Party(IdentityUsersTestData.ApplicantAttorney1Email, RecipientRole.ApplicantAttorney));
        _parties.Recipients.Add(Party(PatientsTestData.Patient1Email, RecipientRole.Patient));
        await SetSubmitterAsync(IdentityUsersTestData.Patient1UserId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        var parties = Sent(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        parties.Recipients.ShouldContain(PatientsTestData.Patient1Email);
    }

    /// <summary>
    /// A booking made by staff is auto-approved right after it is submitted, so it sends NO request
    /// emails (not the parties', not the office notice, not the approve/reject request): the Approved
    /// email is the one notice. The patient login in IdentityUserId is not what decides this.
    /// </summary>
    [Fact]
    public async Task AStaffBooking_SendsNoRequestEmailsAtAll()
    {
        WithPatientAndOfficeMailbox();
        var (_, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking-staffbooker", lowIdRoles: false);
        var staffId = await FindUserIdAsync(intake);
        await SetSubmitterAsync(staffId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        _dispatcher.Dispatches.ShouldBeEmpty();
    }

    /// <summary>The control for the Fact above: the same booking, submitted by an attorney, does send.</summary>
    [Fact]
    public async Task ANonStaffSubmitter_DoesSendTheRequestEmails_ControlForTheStaffBooking()
    {
        WithPatientAndOfficeMailbox();
        await SeedStaffAsync(TenantsTestData.TenantARef, "booking-staffcontrol", lowIdRoles: false);
        await SetSubmitterAsync(IdentityUsersTestData.ApplicantAttorney1UserId);

        await RaiseAsync(bookerUserId: IdentityUsersTestData.Patient1UserId);

        _dispatcher.Dispatches.Select(d => d.TemplateCode).ShouldContain(NotificationTemplateConsts.Codes.AppointmentRequestedRegistered);
        _dispatcher.Dispatches.Select(d => d.TemplateCode).ShouldContain(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
    }

    // ------------------------------------------------------------------------

    private static SendAppointmentEmailArgs Party(string email, RecipientRole role) => new()
    {
        To = email,
        Role = role,
        IsRegistered = true,
    };

    /// <summary>Records who submitted appointment 1 (<c>BookedByUserId</c>, the stamp the creator falls back to).</summary>
    private Task SetSubmitterAsync(Guid userId) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await repository.GetAsync(AppointmentsTestData.Appointment1Id);
            appointment.BookedByUserId = userId;
            await repository.UpdateAsync(appointment, autoSave: true);
        }
    });

    private async Task<Guid> FindUserIdAsync(string email)
    {
        var id = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                id = (await GetRequiredService<IdentityUserManager>().FindByEmailAsync(email))!.Id;
            }
        });
        return id;
    }

    private RecordedDispatch Sent(string templateCode) =>
        _dispatcher.Dispatches.Where(d => d.TemplateCode == templateCode).ShouldHaveSingleItem();

    private void WithPatientAndOfficeMailbox()
    {
        _parties.Recipients.Add(new SendAppointmentEmailArgs
        {
            To = IdentityUsersTestData.Patient1Email,
            Role = RecipientRole.Patient,
            IsRegistered = true,
        });
        _parties.Recipients.Add(new SendAppointmentEmailArgs
        {
            To = OfficeMailbox,
            Role = RecipientRole.OfficeAdmin,
            IsRegistered = true,
        });
    }

    private async Task RaiseAsync(Guid bookerUserId, Guid? patientId = null, Guid? appointmentId = null)
    {
        _dispatcher.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<BookingSubmissionEmailHandler>().HandleEventAsync(
            new AppointmentSubmittedEto
            {
                AppointmentId = appointmentId ?? AppointmentsTestData.Appointment1Id,
                TenantId = TenantsTestData.TenantARef,
                BookerUserId = bookerUserId,
                PatientId = patientId ?? PatientsTestData.Patient1Id,
                RequestConfirmationNumber = AppointmentsTestData.Appointment1RequestConfirmationNumber,
                AppointmentDate = new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
                SubmittedAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            }));
    }

    private Task SetCcListAsync(string ccEmailIds) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            await GetRequiredService<SystemParameterDataSeedContributor>()
                .SeedAsync(new DataSeedContext(TenantsTestData.TenantARef));
            var repository = GetRequiredService<ISystemParameterRepository>();
            var row = (await repository.GetCurrentTenantAsync()).ShouldNotBeNull();
            row.CcEmailIds = ccEmailIds;
            await repository.UpdateAsync(row, autoSave: true);
        }
    });

    private Task NamePatient1UserAsync(string name, string surname) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
        {
            var repository = GetRequiredService<IRepository<IdentityUser, Guid>>();
            var user = await repository.GetAsync(IdentityUsersTestData.Patient1UserId);
            user.Name = name;
            user.Surname = surname;
            await repository.UpdateAsync(user, autoSave: true);
        }
    });

    private async Task<(string Supervisor, string Intake)> SeedStaffAsync(Guid officeId, string label, bool lowIdRoles)
    {
        string supervisor = string.Empty, intake = string.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                if (lowIdRoles)
                {
                    await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, officeId, "Staff Supervisor", 1);
                    await RoleUserSeeder.CreateRoleSortingFirstAsync(roles, officeId, "Intake Staff", 2);
                }

                supervisor = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, officeId, "Staff Supervisor", $"{label}-supervisor");
                intake = await RoleUserSeeder.CreateUserInRoleAsync(users, roles, officeId, "Intake Staff", $"{label}-intake");
            }
        });
        return (supervisor, intake);
    }
}
