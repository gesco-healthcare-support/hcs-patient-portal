using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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
    public async Task ABookerWhoCannotBeFound_IsExternal_SoThisOfficesStaffAreTold_AndNoOtherOffices()
    {
        WithPatientAndOfficeMailbox();
        var (supervisor, intake) = await SeedStaffAsync(TenantsTestData.TenantARef, "booking", lowIdRoles: false);
        var (otherSupervisor, otherIntake) = await SeedStaffAsync(TenantsTestData.TenantBRef, "booking-officeb", lowIdRoles: true);

        await RaiseAsync(bookerUserId: Guid.NewGuid());

        var staff = Sent(NotificationTemplateConsts.Codes.PatientAppointmentApproveReject);
        staff.Recipients.ShouldBe(new[] { supervisor, intake }, ignoreOrder: true);
        staff.Recipients.ShouldNotContain(otherSupervisor);
        staff.Recipients.ShouldNotContain(otherIntake);
    }

    [Fact]
    public async Task NoParties_SendsNoRequestNotice()
    {
        // Positive control: the first Fact, the same booking with the parties resolved.
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

    // ------------------------------------------------------------------------

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
