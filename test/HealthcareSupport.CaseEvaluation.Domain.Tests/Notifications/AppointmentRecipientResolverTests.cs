using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Regression guard for the 2026-06-29 multi-tenant email fix: the resolver
/// must stamp the originating office (<c>ICurrentTenant.Id</c>) onto every
/// produced <see cref="SendAppointmentEmailArgs"/>. The recurring reminder
/// jobs run the resolver inside each office's <c>CurrentTenant.Change(officeId)</c>
/// scope; without the stamp they enqueued <c>TenantId=null</c> and the Hangfire
/// worker (SendAppointmentEmailJob) re-entered host scope, breaking per-office
/// isolation. Pure NSubstitute unit test -- no DB, no ABP fixture.
/// </summary>
public class AppointmentRecipientResolverTests
{
    private static readonly Guid OfficeTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string OfficeTenantName = "falkinstein";

    [Fact]
    public async Task ResolveAsync_stamps_sending_tenant_on_every_recipient()
    {
        var appointmentId = Guid.NewGuid();
        var bookerId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        // Appointment with a booker login and no party-email columns set, so the
        // resolver yields the booker (Patient) plus the office mailbox (OfficeAdmin)
        // -- two recipients reached by two different code paths.
        var appointment = new Appointment(
            id: appointmentId,
            patientId: patientId,
            identityUserId: bookerId,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: DateTime.UtcNow.Date.AddDays(7),
            requestConfirmationNumber: "TEST-RESOLVER",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = OfficeTenantId,
        };

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(appointmentId).Returns(appointment);

        // Patient row resolves to no email -> no extra recipient (avoids
        // constructing a Patient aggregate).
        var patientRepo = Substitute.For<IRepository<Patient, Guid>>();

        var identityUserRepo = Substitute.For<IRepository<IdentityUser, Guid>>();
        identityUserRepo.FindAsync(bookerId)
            .Returns(new IdentityUser(bookerId, "booker", "booker@falkinstein.test", OfficeTenantId));

        var applicantLinkRepo = Substitute.For<IAppointmentApplicantAttorneyRepository>();
        applicantLinkRepo.GetQueryableAsync()
            .Returns(new List<AppointmentApplicantAttorney>().AsQueryable());
        var defenseLinkRepo = Substitute.For<IAppointmentDefenseAttorneyRepository>();
        defenseLinkRepo.GetQueryableAsync()
            .Returns(new List<AppointmentDefenseAttorney>().AsQueryable());

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail)
            .Returns("office@falkinstein.test");

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(OfficeTenantId);
        currentTenant.Name.Returns(OfficeTenantName);

        var resolver = new AppointmentRecipientResolver(
            appointmentRepo,
            patientRepo,
            identityUserRepo,
            applicantLinkRepo,
            Substitute.For<IRepository<ApplicantAttorney, Guid>>(),
            defenseLinkRepo,
            Substitute.For<IRepository<DefenseAttorney, Guid>>(),
            Substitute.For<IRepository<AppointmentEmployerDetail, Guid>>(),
            settingProvider,
            currentTenant,
            Substitute.For<IRecipientRoleResolver>(),
            NullLogger<AppointmentRecipientResolver>.Instance);

        var recipients = await resolver.ResolveAsync(appointmentId, NotificationKind.AppointmentDayReminder);

        Assert.Equal(2, recipients.Count);
        Assert.All(recipients, r => Assert.Equal(OfficeTenantId, r.TenantId));
        Assert.All(recipients, r => Assert.Equal(OfficeTenantName, r.TenantName));
    }
}
