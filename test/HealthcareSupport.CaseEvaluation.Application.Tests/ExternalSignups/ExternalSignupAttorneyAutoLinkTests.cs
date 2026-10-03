using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// What an attorney's registration claims: the attorney record that staff created under their
/// email before they had an account, and appointments that named their email but were never
/// linked. <c>ExternalSignupRegistrationTests</c> covers the
/// patient and claim-examiner claims; the two attorney paths had no test.
/// </summary>
/// <remarks>
/// <para>
/// A link created for the attorney BEFORE they registered (booking creates the record and the
/// link with no user) is claimed at registration (#1037). <c>RegisterAsync</c> adopts the
/// attorney record first, so the auto-link step finds no UNCLAIMED record; it patches the links of
/// every record the new account OWNS. Pinned by the <c>_pre_booked_link</c> tests below, each with a
/// decoy link under another attorney that must stay unclaimed.
/// </para>
/// Each test seeds a DECOY: an unlinked attorney record under a different email, which the
/// registration must leave alone. Without it, a claim that ignored the email would pass. All names
/// and emails are synthetic.
/// </remarks>
public abstract class ExternalSignupAttorneyAutoLinkTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string Password = "Test1234!";

    private readonly IExternalSignupAppService _signups;
    private readonly IdentityUserManager _users;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IRepository<ApplicantAttorney, Guid> _applicants;
    private readonly IRepository<DefenseAttorney, Guid> _defenses;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantLinks;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseLinks;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupAttorneyAutoLinkTests()
    {
        _signups = GetRequiredService<IExternalSignupAppService>();
        _users = GetRequiredService<IdentityUserManager>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _applicants = GetRequiredService<IRepository<ApplicantAttorney, Guid>>();
        _defenses = GetRequiredService<IRepository<DefenseAttorney, Guid>>();
        _applicantLinks = GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>();
        _defenseLinks = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    private async Task<T> InOfficeA<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    /// <summary>A pending appointment for the seeded patient, optionally naming attorney emails.</summary>
    private Task<Appointment> InsertAppointmentAsync(string token, string suffix, Action<Appointment>? shape = null) =>
        InOfficeA(async () =>
        {
            var appointment = new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: null,
                appointmentTypeId: LocationsTestData.AppointmentType1Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
                appointmentDate: new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: $"A9{suffix}{token}",
                appointmentStatus: AppointmentStatusType.Pending);
            shape?.Invoke(appointment);
            return await _appointments.InsertAsync(appointment, autoSave: true);
        });

    private Task<Guid> RegisterAsync(ExternalUserType type, string email) =>
        InOfficeA(async () =>
        {
            await _signups.RegisterAsync(new ExternalUserSignUpDto
            {
                UserType = type,
                Email = email,
                Password = Password,
                ConfirmPassword = Password,
                FirstName = "Synthetic",
                LastName = "Attorney",
                FirmName = "Synthetic Firm",
                TenantId = TenantsTestData.TenantARef,
            });
            return (await _users.FindByEmailAsync(email))!.Id;
        });

    [Fact]
    public async Task A_defense_attorney_registering_claims_their_record_and_is_linked_to_named_appointments()
    {
        var token = NewToken();
        var email = $"def-{token}@example.test";
        var namedAppointment = await InsertAppointmentAsync(token, "D2", a => a.DefenseAttorneyEmail = email.ToUpperInvariant());
        var (masterId, decoyId) = await InOfficeA(async () =>
        {
            var master = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: email), autoSave: true);
            // LOAD-BEARING DECOY: unlinked, but under another email.
            var decoy = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: $"other-{token}@example.test"), autoSave: true);
            return (master.Id, decoy.Id);
        });

        var accountId = await RegisterAsync(ExternalUserType.DefenseAttorney, email);

        await InOfficeA(async () =>
        {
            (await _defenses.GetAsync(masterId)).IdentityUserId.ShouldBe(accountId);
            (await _defenses.GetAsync(decoyId)).IdentityUserId.ShouldBeNull();
            var created = (await _defenseLinks.GetListAsync(l => l.AppointmentId == namedAppointment.Id)).ShouldHaveSingleItem();
            created.DefenseAttorneyId.ShouldBe(masterId);
            created.IdentityUserId.ShouldBe(accountId);
            return true;
        });
    }

    [Fact]
    public async Task A_defense_attorney_registering_claims_a_pre_booked_link_but_not_another_attorneys()
    {
        var token = NewToken();
        var email = $"prelink-def-{token}@example.test";
        var bookedAppointment = await InsertAppointmentAsync(token, "D4", a => a.DefenseAttorneyEmail = email);
        var decoyAppointment = await InsertAppointmentAsync(token, "D5");
        var (masterId, decoyId) = await InOfficeA(async () =>
        {
            var master = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: email), autoSave: true);
            var decoy = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: $"other-{token}@example.test"), autoSave: true);
            // What booking leaves behind: a link with no user.
            await _defenseLinks.InsertAsync(new AppointmentDefenseAttorney(Guid.NewGuid(), bookedAppointment.Id, master.Id, null), autoSave: true);
            // LOAD-BEARING DECOY: another attorney's unclaimed link, same office.
            await _defenseLinks.InsertAsync(new AppointmentDefenseAttorney(Guid.NewGuid(), decoyAppointment.Id, decoy.Id, null), autoSave: true);
            return (master.Id, decoy.Id);
        });

        var accountId = await RegisterAsync(ExternalUserType.DefenseAttorney, email);

        await InOfficeA(async () =>
        {
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == bookedAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            var decoyLink = (await _defenseLinks.GetListAsync(l => l.AppointmentId == decoyAppointment.Id)).ShouldHaveSingleItem();
            decoyLink.DefenseAttorneyId.ShouldBe(decoyId);
            decoyLink.IdentityUserId.ShouldBeNull();
            masterId.ShouldNotBe(decoyId);
            return true;
        });
    }

    [Fact]
    public async Task An_applicant_attorney_registering_claims_a_pre_booked_link_but_not_another_attorneys()
    {
        var token = NewToken();
        var email = $"prelink-app-{token}@example.test";
        var bookedAppointment = await InsertAppointmentAsync(token, "A4", a => a.ApplicantAttorneyEmail = email);
        var decoyAppointment = await InsertAppointmentAsync(token, "A5");
        var decoyId = await InOfficeA(async () =>
        {
            var master = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Firm", null, null) { Email = email };
            await _applicants.InsertAsync(master, autoSave: true);
            var decoy = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Other Firm", null, null) { Email = $"other-{token}@example.test" };
            await _applicants.InsertAsync(decoy, autoSave: true);
            await _applicantLinks.InsertAsync(new AppointmentApplicantAttorney(Guid.NewGuid(), bookedAppointment.Id, master.Id, null), autoSave: true);
            // LOAD-BEARING DECOY: another attorney's unclaimed link, same office.
            await _applicantLinks.InsertAsync(new AppointmentApplicantAttorney(Guid.NewGuid(), decoyAppointment.Id, decoy.Id, null), autoSave: true);
            return decoy.Id;
        });

        var accountId = await RegisterAsync(ExternalUserType.ApplicantAttorney, email);

        await InOfficeA(async () =>
        {
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == bookedAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            var decoyLink = (await _applicantLinks.GetListAsync(l => l.AppointmentId == decoyAppointment.Id)).ShouldHaveSingleItem();
            decoyLink.ApplicantAttorneyId.ShouldBe(decoyId);
            decoyLink.IdentityUserId.ShouldBeNull();
            return true;
        });
    }

    [Fact]
    public async Task A_defense_attorney_with_no_prior_record_gets_one_and_is_linked_to_the_named_appointment()
    {
        var token = NewToken();
        var email = $"new-def-{token}@example.test";
        var namedAppointment = await InsertAppointmentAsync(token, "N1", a => a.DefenseAttorneyEmail = email);

        var accountId = await RegisterAsync(ExternalUserType.DefenseAttorney, email);

        await InOfficeA(async () =>
        {
            var master = (await _defenses.GetListAsync(d => d.IdentityUserId == accountId)).ShouldHaveSingleItem();
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == namedAppointment.Id)).ShouldHaveSingleItem()
                .DefenseAttorneyId.ShouldBe(master.Id);
            return true;
        });
    }

    [Fact]
    public async Task An_applicant_attorney_registering_claims_their_record_and_named_appointments_but_not_an_already_linked_one()
    {
        var token = NewToken();
        var email = $"app-{token}@example.test";
        var namedAppointment = await InsertAppointmentAsync(token, "A2", a => a.ApplicantAttorneyEmail = email);
        var alreadyLinked = await InsertAppointmentAsync(token, "A3", a => a.ApplicantAttorneyEmail = email);
        var (masterId, decoyId) = await InOfficeA(async () =>
        {
            var master = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Firm", null, null) { Email = email };
            await _applicants.InsertAsync(master, autoSave: true);
            // LOAD-BEARING DECOY: unlinked, but under another email.
            var decoy = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Other Firm", null, null) { Email = $"other-{token}@example.test" };
            await _applicants.InsertAsync(decoy, autoSave: true);
            // An appointment that already has an applicant attorney must not get a second one.
            await _applicantLinks.InsertAsync(new AppointmentApplicantAttorney(Guid.NewGuid(), alreadyLinked.Id, decoy.Id, null), autoSave: true);
            return (master.Id, decoy.Id);
        });

        var accountId = await RegisterAsync(ExternalUserType.ApplicantAttorney, email);

        await InOfficeA(async () =>
        {
            (await _applicants.GetAsync(masterId)).IdentityUserId.ShouldBe(accountId);
            (await _applicants.GetAsync(decoyId)).IdentityUserId.ShouldBeNull();
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == namedAppointment.Id)).ShouldHaveSingleItem()
                .ApplicantAttorneyId.ShouldBe(masterId);
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == alreadyLinked.Id)).ShouldHaveSingleItem()
                .ApplicantAttorneyId.ShouldBe(decoyId);
            return true;
        });
    }

    // Two unclaimed records under one email: RegisterAsync adopts one, and the auto-link step's own
    // claim loop picks up the other. Neither claim is saved yet when the link patch runs, so a link
    // under the loop-claimed record is found only through the claimed list, not the owned query.
    // The unique email index would normally forbid two such rows; the test model does not carry it,
    // and the step is defensive about exactly that.
    [Fact]
    public async Task An_applicant_attorney_registering_claims_links_under_every_unclaimed_record_with_their_email()
    {
        var token = NewToken();
        var email = $"twin-app-{token}@example.test";
        var firstAppointment = await InsertAppointmentAsync(token, "A6", a => a.ApplicantAttorneyEmail = email);
        var secondAppointment = await InsertAppointmentAsync(token, "A7", a => a.ApplicantAttorneyEmail = email);
        await InOfficeA(async () =>
        {
            var first = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Firm", null, null) { Email = email };
            await _applicants.InsertAsync(first, autoSave: true);
            var second = new ApplicantAttorney(Guid.NewGuid(), null, null, "Synthetic Firm", null, null) { Email = email.ToUpperInvariant() };
            await _applicants.InsertAsync(second, autoSave: true);
            await _applicantLinks.InsertAsync(new AppointmentApplicantAttorney(Guid.NewGuid(), firstAppointment.Id, first.Id, null), autoSave: true);
            await _applicantLinks.InsertAsync(new AppointmentApplicantAttorney(Guid.NewGuid(), secondAppointment.Id, second.Id, null), autoSave: true);
            return true;
        });

        var accountId = await RegisterAsync(ExternalUserType.ApplicantAttorney, email);

        await InOfficeA(async () =>
        {
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == firstAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            (await _applicantLinks.GetListAsync(l => l.AppointmentId == secondAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            return true;
        });
    }

    [Fact]
    public async Task A_defense_attorney_registering_claims_links_under_every_unclaimed_record_with_their_email()
    {
        var token = NewToken();
        var email = $"twin-def-{token}@example.test";
        var firstAppointment = await InsertAppointmentAsync(token, "D6", a => a.DefenseAttorneyEmail = email);
        var secondAppointment = await InsertAppointmentAsync(token, "D7", a => a.DefenseAttorneyEmail = email);
        await InOfficeA(async () =>
        {
            var first = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: email), autoSave: true);
            var second = await _defenses.InsertAsync(new DefenseAttorney(Guid.NewGuid(), null, null, email: email.ToUpperInvariant()), autoSave: true);
            await _defenseLinks.InsertAsync(new AppointmentDefenseAttorney(Guid.NewGuid(), firstAppointment.Id, first.Id, null), autoSave: true);
            await _defenseLinks.InsertAsync(new AppointmentDefenseAttorney(Guid.NewGuid(), secondAppointment.Id, second.Id, null), autoSave: true);
            return true;
        });

        var accountId = await RegisterAsync(ExternalUserType.DefenseAttorney, email);

        await InOfficeA(async () =>
        {
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == firstAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            (await _defenseLinks.GetListAsync(l => l.AppointmentId == secondAppointment.Id)).ShouldHaveSingleItem()
                .IdentityUserId.ShouldBe(accountId);
            return true;
        });
    }
}
