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
using System.Threading;
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
    /// <summary>
    /// A fixed appointment date. Was <c>DateTime.UtcNow.Date.AddDays(7)</c>, which is the
    /// pattern src/BannedSymbols.txt calls the same bug as DateTime.Today and which makes the
    /// fixture a different value every day. Nothing here asserts on the date -- the resolver is
    /// exercised for WHO it yields -- so a constant is strictly better, and it matches how the
    /// rest of the suite builds appointments (new DateTime(..., DateTimeKind.Utc)).
    /// </summary>
    private static readonly DateTime FutureAppointmentDate =
        new(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
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
            appointmentDate: FutureAppointmentDate,
            requestConfirmationNumber: "TEST-RESOLVER",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = OfficeTenantId,
            BookedByUserId = bookerId,
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
            Substitute.For<IIdentityUserRepository>(),
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

        // 2026-11-04: the change-request side resolver tells the SUBMITTER's entry from the named
        // parties by its context suffix, so the suffix is pinned here as a literal.
        var booker = Assert.Single(recipients, r => r.To == "booker@falkinstein.test");
        Assert.EndsWith("/booker", booker.Context);
        Assert.True(AppointmentRecipientResolver.IsBookerEntry(booker));
        Assert.False(AppointmentRecipientResolver.IsBookerEntry(
            Assert.Single(recipients, r => r.To == "office@falkinstein.test")));
    }

    [Fact]
    public async Task ResolveAsync_WhenOfficeEmailUnset_AddsNoOfficeRecipient()
    {
        // task_12e094b2 (2026-07-21): an unset OfficeEmail adds no OfficeAdmin recipient (the
        // office's own copy is dropped, and the resolver logs a Warning). Party recipients are
        // unaffected -- here only the booker remains.
        var appointmentId = Guid.NewGuid();
        var bookerId = Guid.NewGuid();
        var patientId = Guid.NewGuid();

        var appointment = new Appointment(
            id: appointmentId,
            patientId: patientId,
            identityUserId: bookerId,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: FutureAppointmentDate,
            requestConfirmationNumber: "TEST-RESOLVER-NOOFFICE",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = OfficeTenantId,
            BookedByUserId = bookerId,
        };

        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(appointmentId).Returns(appointment);

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

        // Office inbox unset -> the office recipient must be skipped.
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail)
            .Returns((string?)null);

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(OfficeTenantId);
        currentTenant.Name.Returns(OfficeTenantName);

        var resolver = new AppointmentRecipientResolver(
            appointmentRepo,
            patientRepo,
            identityUserRepo,
            Substitute.For<IIdentityUserRepository>(),
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

        Assert.Single(recipients);
        Assert.DoesNotContain(recipients, r => r.Role == RecipientRole.OfficeAdmin);
    }

    // ---- #1196: attorney linked with no IdentityUser, appointment email column blank ----

    private static async Task<List<SendAppointmentEmailArgs>> ResolveAttorneyLinkAsync(
        Guid? linkUserId,
        string? masterEmail,
        string? userEmail,
        bool applicant)
    {
        var appointmentId = Guid.NewGuid();
        var attorneyId = Guid.NewGuid();
        var appointment = new Appointment(
            id: appointmentId,
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: FutureAppointmentDate,
            requestConfirmationNumber: "TEST-RESOLVER-ATTY",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = OfficeTenantId,
        };
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(appointmentId).Returns(appointment);

        var identityUserRepo = Substitute.For<IRepository<IdentityUser, Guid>>();
        if (linkUserId.HasValue)
        {
            identityUserRepo.FindAsync(linkUserId.Value)
                .Returns(new IdentityUser(linkUserId.Value, "atty", userEmail ?? "x@falkinstein.test", OfficeTenantId));
        }

        var applicantLinkRepo = Substitute.For<IAppointmentApplicantAttorneyRepository>();
        var defenseLinkRepo = Substitute.For<IAppointmentDefenseAttorneyRepository>();
        var applicantMasterRepo = Substitute.For<IRepository<ApplicantAttorney, Guid>>();
        var defenseMasterRepo = Substitute.For<IRepository<DefenseAttorney, Guid>>();
        applicantLinkRepo.GetQueryableAsync().Returns(
            (applicant
                ? new List<AppointmentApplicantAttorney> { new(Guid.NewGuid(), appointmentId, attorneyId, linkUserId) { TenantId = OfficeTenantId } }
                : new List<AppointmentApplicantAttorney>()).AsQueryable());
        defenseLinkRepo.GetQueryableAsync().Returns(
            (!applicant
                ? new List<AppointmentDefenseAttorney> { new(Guid.NewGuid(), appointmentId, attorneyId, linkUserId) { TenantId = OfficeTenantId } }
                : new List<AppointmentDefenseAttorney>()).AsQueryable());
        applicantMasterRepo.FindAsync(attorneyId)
            .Returns(new ApplicantAttorney(attorneyId, null, null, email: masterEmail));
        defenseMasterRepo.FindAsync(attorneyId)
            .Returns(new DefenseAttorney(attorneyId, null, null, email: masterEmail));

        var roleResolver = Substitute.For<IRecipientRoleResolver>();
        roleResolver.ClassifyAsync(Arg.Any<string>(), Arg.Any<RecipientRole>())
            .Returns(new RecipientRoleClassification(false, false, null));

        var settingProvider = Substitute.For<ISettingProvider>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(OfficeTenantId);
        currentTenant.Name.Returns(OfficeTenantName);

        var resolver = new AppointmentRecipientResolver(
            appointmentRepo,
            Substitute.For<IRepository<Patient, Guid>>(),
            identityUserRepo,
            Substitute.For<IIdentityUserRepository>(),
            applicantLinkRepo,
            applicantMasterRepo,
            defenseLinkRepo,
            defenseMasterRepo,
            Substitute.For<IRepository<AppointmentEmployerDetail, Guid>>(),
            settingProvider,
            currentTenant,
            roleResolver,
            NullLogger<AppointmentRecipientResolver>.Instance);

        return await resolver.ResolveAsync(appointmentId, NotificationKind.AppointmentDayReminder);
    }

    [Fact]
    public async Task ResolveAsync_ApplicantAttorneyLinkedWithoutUser_FallsBackToMasterEmail()
    {
        var recipients = await ResolveAttorneyLinkAsync(null, "aa-master@falkinstein.test", null, applicant: true);

        var r = Assert.Single(recipients);
        Assert.Equal("aa-master@falkinstein.test", r.To);
        Assert.Equal(RecipientRole.ApplicantAttorney, r.Role);
        Assert.False(r.IsRegistered);
    }

    [Fact]
    public async Task ResolveAsync_DefenseAttorneyLinkedWithoutUser_FallsBackToMasterEmail()
    {
        var recipients = await ResolveAttorneyLinkAsync(null, "da-master@falkinstein.test", null, applicant: false);

        var r = Assert.Single(recipients);
        Assert.Equal("da-master@falkinstein.test", r.To);
        Assert.Equal(RecipientRole.DefenseAttorney, r.Role);
    }

    [Fact]
    public async Task ResolveAsync_AttorneyWithUser_UsesTheUserEmailNotTheMaster()
    {
        var userId = Guid.NewGuid();
        var recipients = await ResolveAttorneyLinkAsync(userId, "master@falkinstein.test", "login@falkinstein.test", applicant: true);

        var r = Assert.Single(recipients);
        Assert.Equal("login@falkinstein.test", r.To);
        Assert.True(r.IsRegistered);
    }

    [Fact]
    public async Task ResolveAsync_AttorneyLinkedWithoutUserAndBlankMaster_YieldsNoRecipient()
    {
        var recipients = await ResolveAttorneyLinkAsync(null, null, null, applicant: true);

        Assert.Empty(recipients);
    }

    // ---- 2026-11-03: the booker is the account that SUBMITTED the request ----

    private static async Task<List<SendAppointmentEmailArgs>> ResolveSubmitterAsync(
        Guid? loginUserId, Guid? submitterId, string? submitterEmail, string[] submitterRoles, string? patientEmail = null)
    {
        var appointmentId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        var appointment = new Appointment(
            id: appointmentId,
            patientId: patientId,
            identityUserId: loginUserId,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: FutureAppointmentDate,
            requestConfirmationNumber: "TEST-RESOLVER-SUBMITTER",
            appointmentStatus: AppointmentStatusType.Approved)
        {
            TenantId = OfficeTenantId,
            BookedByUserId = submitterId,
        };
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(appointmentId).Returns(appointment);

        var patientRepo = Substitute.For<IRepository<Patient, Guid>>();
        if (patientEmail != null)
        {
            patientRepo.FindAsync(patientId).Returns(new Patient(
                patientId, null, null, null, OfficeTenantId, "TEST-First", "TEST-Last", patientEmail,
                default(Gender), new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc), default(PhoneNumberType)));
        }

        var identityUserRepo = Substitute.For<IRepository<IdentityUser, Guid>>();
        if (loginUserId.HasValue)
        {
            identityUserRepo.FindAsync(loginUserId.Value)
                .Returns(new IdentityUser(loginUserId.Value, "login", "patient-login@falkinstein.test", OfficeTenantId));
        }
        var queries = Substitute.For<IIdentityUserRepository>();
        if (submitterId.HasValue)
        {
            identityUserRepo.FindAsync(submitterId.Value)
                .Returns(new IdentityUser(submitterId.Value, "submitter", submitterEmail!, OfficeTenantId));
            queries.GetRoleNamesAsync(submitterId.Value, Arg.Any<CancellationToken>())
                .Returns(submitterRoles.ToList());
        }

        var applicantLinkRepo = Substitute.For<IAppointmentApplicantAttorneyRepository>();
        applicantLinkRepo.GetQueryableAsync().Returns(new List<AppointmentApplicantAttorney>().AsQueryable());
        var defenseLinkRepo = Substitute.For<IAppointmentDefenseAttorneyRepository>();
        defenseLinkRepo.GetQueryableAsync().Returns(new List<AppointmentDefenseAttorney>().AsQueryable());

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Id.Returns(OfficeTenantId);
        currentTenant.Name.Returns(OfficeTenantName);

        var resolver = new AppointmentRecipientResolver(
            appointmentRepo,
            patientRepo,
            identityUserRepo,
            queries,
            applicantLinkRepo,
            Substitute.For<IRepository<ApplicantAttorney, Guid>>(),
            defenseLinkRepo,
            Substitute.For<IRepository<DefenseAttorney, Guid>>(),
            Substitute.For<IRepository<AppointmentEmployerDetail, Guid>>(),
            Substitute.For<ISettingProvider>(),
            currentTenant,
            Substitute.For<IRecipientRoleResolver>(),
            NullLogger<AppointmentRecipientResolver>.Instance);

        return await resolver.ResolveAsync(appointmentId, NotificationKind.Submitted);
    }

    [Fact]
    public async Task ResolveAsync_TheBooker_IsTheSubmitter_NotThePatientLogin()
    {
        var submitterId = Guid.NewGuid();

        var recipients = await ResolveSubmitterAsync(
            Guid.NewGuid(), submitterId, "submitter@falkinstein.test", new[] { "Applicant Attorney" });

        var r = Assert.Single(recipients);
        Assert.Equal("submitter@falkinstein.test", r.To);
        Assert.DoesNotContain(recipients, x => x.To == "patient-login@falkinstein.test");
    }

    [Theory]
    [InlineData("Intake Staff")]
    [InlineData("Staff Supervisor")]
    [InlineData("admin")]
    [InlineData("IT Admin")]
    public async Task ResolveAsync_AStaffSubmitter_IsNotARecipient(string staffRole)
    {
        var recipients = await ResolveSubmitterAsync(
            null, Guid.NewGuid(), "staff@falkinstein.test", new[] { staffRole });

        Assert.Empty(recipients);
    }

    [Fact]
    public async Task ResolveAsync_ASubmitterWhoIsAlsoTheirOwnPatient_IsMailedOnce()
    {
        var recipients = await ResolveSubmitterAsync(
            null, Guid.NewGuid(), "same@falkinstein.test", new[] { "Patient" }, patientEmail: "SAME@falkinstein.test");

        Assert.Single(recipients);
    }

    [Fact]
    public async Task ResolveAsync_TheActualPatient_KeepsThePatientRole_WhenADifferentSubmitterBooked()
    {
        var recipients = await ResolveSubmitterAsync(
            null, Guid.NewGuid(), "paralegal@falkinstein.test", new[] { "Applicant Attorney" },
            patientEmail: "patient@falkinstein.test");

        Assert.Equal(2, recipients.Count);
        Assert.Equal(RecipientRole.Patient, recipients.Single(x => x.To == "patient@falkinstein.test").Role);
    }
}
