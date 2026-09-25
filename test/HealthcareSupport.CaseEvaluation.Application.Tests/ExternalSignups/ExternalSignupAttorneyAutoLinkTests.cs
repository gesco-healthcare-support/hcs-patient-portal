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
/// NOT PINNED HERE, BECAUSE IT IS A DEFECT: an appointment link created for the attorney BEFORE
/// they registered (booking creates the record and the link with no user) is never claimed.
/// <c>RegisterAsync</c> claims the attorney record first (<c>ExternalSignupAppService.cs:922</c>
/// and <c>:963</c>), so the auto-link step (<c>:1009</c>) finds no unclaimed record and its
/// link-claiming block (<c>:1157-1167</c>, <c>:1220-1230</c>) never runs. The attorney still SEES
/// the appointment: the list and read guard admit them by the email stored on it. What the unclaimed
/// link costs is the attorney-scoped patient and booker lookups (<c>AppointmentsAppService.cs:542</c>,
/// <c>:561</c>, <c>:580</c>, <c>:599</c>), which read the link's user. Tracked as #1037. A test
/// asserting today's behaviour would pin the defect; the fix belongs to a src change.
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
}
