using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.WcabOffices;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// <see cref="PacketTokenResolver"/>, which fills every token of a generated packet from the
/// appointment's records. It had no test at all; its constructor was never run.
/// </summary>
/// <remarks>
/// <para>
/// Unit tests over substituted repositories, the tranche 4 shape. <c>FirstOrDefaultAsync</c> is an
/// ABP EXTENSION that runs against the repository's queryable and <c>AsyncExecuter</c>, so each
/// substitute is handed its rows as an in-memory queryable plus ABP's real executer, which falls
/// back to LINQ-to-objects when no EF provider is registered.
/// </para>
/// <para>
/// The responsible-user signature (<c>GetSignatureBytesAsync</c>, <c>:453-471</c>) is bound to
/// <c>IdentityUserManager</c> and excluded by the approved area plan, so no appointment here names
/// a primary responsible user. All values are synthetic: <c>TEST-</c> names, <c>@test.local</c>,
/// 555 phones.
/// </para>
/// </remarks>
public class PacketTokenResolverTests
{
    private static readonly DateTime FixedNow = new(2031, 5, 6, 18, 0, 0, DateTimeKind.Utc);

    private static IRepository<T, Guid> Repo<T>(params T[] rows)
        where T : class, IEntity<Guid>
    {
        var list = rows.ToList();
        var repo = Substitute.For<IRepository<T, Guid>>();
        repo.GetQueryableAsync().Returns(_ => list.AsQueryable());
        repo.AsyncExecuter.Returns(new AsyncQueryableExecuter(Array.Empty<IAsyncQueryableProvider>()));
        repo.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => list.FirstOrDefault(r => r.Id == ci.ArgAt<Guid>(0)));
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => list.First(r => r.Id == ci.ArgAt<Guid>(0)));
        repo.GetListAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => list.AsQueryable().Where(ci.ArgAt<Expression<Func<T, bool>>>(0)).ToList());
        return repo;
    }

    private sealed class World
    {
        public List<Appointment> Appointments { get; } = new();
        public List<Patient> Patients { get; } = new();
        public List<DoctorAvailability> Slots { get; } = new();
        public List<Location> Locations { get; } = new();
        public List<AppointmentType> Types { get; } = new();
        public List<AppointmentEmployerDetail> Employers { get; } = new();
        public List<AppointmentApplicantAttorney> ApplicantLinks { get; } = new();
        public List<ApplicantAttorney> Applicants { get; } = new();
        public List<AppointmentDefenseAttorney> DefenseLinks { get; } = new();
        public List<DefenseAttorney> Defenses { get; } = new();
        public List<AppointmentInjuryDetail> Injuries { get; } = new();
        public List<AppointmentClaimExaminer> Examiners { get; } = new();
        public List<AppointmentPrimaryInsurance> Insurances { get; } = new();
        public List<WcabOffice> WcabOffices { get; } = new();
        public List<State> States { get; } = new();
        public List<AppointmentLanguage> Languages { get; } = new();
        public List<IdentityUser> Users { get; } = new();

        public PacketTokenResolver Resolver()
        {
            var clock = Substitute.For<IClock>();
            clock.Now.Returns(FixedNow);
            var patients = Repo(Patients.ToArray());
            return new PacketTokenResolver(
                Repo(Appointments.ToArray()), patients, new AppointmentPatientSnapshotResolver(patients),
                Repo(Slots.ToArray()), Repo(Locations.ToArray()), Repo(Types.ToArray()), Repo(Employers.ToArray()),
                Repo(ApplicantLinks.ToArray()), Repo(Applicants.ToArray()), Repo(DefenseLinks.ToArray()), Repo(Defenses.ToArray()),
                Repo(Injuries.ToArray()), Repo(Examiners.ToArray()), Repo(Insurances.ToArray()), Repo(WcabOffices.ToArray()),
                Repo(States.ToArray()), Repo(Languages.ToArray()), Repo(Users.ToArray()),
                userManager: null!, Substitute.For<IBlobContainer<UserSignaturesContainer>>(), clock);
        }
    }

    private static Appointment NewAppointment(Guid patientId, Guid typeId, Guid locationId, Guid slotId) =>
        new(Guid.NewGuid(), patientId, identityUserId: null, typeId, locationId, slotId,
            appointmentDate: new DateTime(2031, 6, 2, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "a90777", appointmentStatus: AppointmentStatusType.Approved);

    [Fact]
    public async Task Every_section_is_filled_upper_cased_from_the_appointments_records()
    {
        var w = new World();
        var ca = new State(Guid.NewGuid(), "TEST-California");
        var nv = new State(Guid.NewGuid(), "TEST-Nevada");
        w.States.AddRange(new[] { ca, nv });
        var language = new AppointmentLanguage(Guid.NewGuid(), "TEST-Spanish");
        w.Languages.Add(language);
        var patient = new Patient(Guid.NewGuid(), ca.Id, language.Id, null, null, "TEST-Pat", "TEST-Doe", "pat@test.local",
            Gender.Unspecified, new DateTime(1980, 2, 3, 0, 0, 0, DateTimeKind.Utc), PhoneNumberType.Home,
            phoneNumber: "555-0100", street: "1 TEST Way", city: "TEST City", zipCode: "90001", interpreterVendorName: "TEST Vendor");
        w.Patients.Add(patient);
        var slot = new DoctorAvailability(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2031, 6, 2, 0, 0, 0, DateTimeKind.Utc),
            new TimeOnly(9, 30), new TimeOnly(10, 30), BookingStatus.Booked);
        w.Slots.Add(slot);
        var location = new Location(slot.LocationId, nv.Id, "TEST Clinic", 12.5m, true, "2 TEST Road", "TEST Town", "89001");
        w.Locations.Add(location);
        var type = new AppointmentType(Guid.NewGuid(), "TEST-Qme");
        w.Types.Add(type);
        var appointment = NewAppointment(patient.Id, type.Id, location.Id, slot.Id);
        appointment.PanelNumber = "p-1";
        w.Appointments.Add(appointment);
        w.Employers.Add(new AppointmentEmployerDetail(Guid.NewGuid(), appointment.Id, ca.Id, "TEST Employer", "TEST Role")
        { Street = "3 TEST St", City = "TEST Works", ZipCode = "90002" });
        var lawyerUser = new IdentityUser(Guid.NewGuid(), "test-lawyer", "lawyer@test.local") { Name = "TEST-Ada", Surname = "TEST-Law" };
        w.Users.Add(lawyerUser);
        var applicant = new ApplicantAttorney(Guid.NewGuid(), ca.Id, lawyerUser.Id, "TEST Applicant Firm") { Street = "4 TEST Ave", City = "TEST Law City", ZipCode = "90003" };
        w.Applicants.Add(applicant);
        w.ApplicantLinks.Add(new AppointmentApplicantAttorney(Guid.NewGuid(), appointment.Id, applicant.Id, lawyerUser.Id));
        var defense = new DefenseAttorney(Guid.NewGuid(), nv.Id, null, "TEST Defense Firm") { Street = "5 TEST Blvd", City = "TEST Def City", ZipCode = "89002" };
        w.Defenses.Add(defense);
        w.DefenseLinks.Add(new AppointmentDefenseAttorney(Guid.NewGuid(), appointment.Id, defense.Id, null));
        var wcab = new WcabOffice(Guid.NewGuid(), ca.Id, "TEST Wcab", "TW", true, "6 TEST Ct", "TEST Board", "90004");
        w.WcabOffices.Add(wcab);
        w.Injuries.Add(new AppointmentInjuryDetail(Guid.NewGuid(), appointment.Id, new DateTime(2029, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            "clm-1", false, "TEST back", wcabAdj: "adj-1", wcabOfficeId: wcab.Id));
        w.Injuries.Add(new AppointmentInjuryDetail(Guid.NewGuid(), appointment.Id, new DateTime(2030, 3, 4, 0, 0, 0, DateTimeKind.Utc),
            "clm-2", false, "TEST knee", wcabAdj: "adj-2"));
        w.Insurances.Add(new AppointmentPrimaryInsurance(Guid.NewGuid(), appointment.Id, true)
        { Name = "TEST Insurer", Street = "7 TEST Pl", City = "TEST Cover", StateId = ca.Id, Zip = "90005", PhoneNumber = "555-0101" });
        w.Examiners.Add(new AppointmentClaimExaminer(Guid.NewGuid(), appointment.Id, true)
        { Name = "TEST Examiner", Street = "8 TEST Ln", City = "TEST Claims", StateId = nv.Id, Zip = "89003", PhoneNumber = "555-0102" });

        var ctx = await w.Resolver().ResolveAsync(appointment.Id);

        ctx.PatientFirstName.ShouldBe("TEST-PAT");
        ctx.PatientState.ShouldBe("TEST-CALIFORNIA");
        ctx.PatientDateOfBirth.ShouldBe(PacketDateStamp.Format(patient.DateOfBirth));
        ctx.PatientInterpreterRequired.ShouldBe("Yes");
        ctx.PatientInterpreterLanguage.ShouldBe("TEST-SPANISH");
        ctx.RequestConfirmationNumber.ShouldBe("A90777");
        ctx.PanelNumber.ShouldBe("P-1");
        ctx.AppointmentTime.ShouldBe("9:30 AM");
        ctx.AvailableDate.ShouldBe(PacketDateStamp.Format(slot.AvailableDate));
        ctx.LocationName.ShouldBe("TEST CLINIC");
        ctx.LocationParkingFee.ShouldBe("12.5");
        ctx.LocationState.ShouldBe("TEST-NEVADA");
        ctx.AppointmentType.ShouldBe("TEST-QME");
        ctx.EmployerName.ShouldBe("TEST EMPLOYER");
        ctx.EmployerState.ShouldBe("TEST-CALIFORNIA");
        ctx.PatientAttorneyName.ShouldBe("TEST-ADA TEST-LAW");
        ctx.PatientAttorneyCity.ShouldBe("TEST LAW CITY");
        ctx.DefenseAttorneyName.ShouldBe("TEST DEFENSE FIRM", "no account, so the firm name stands in");
        ctx.DefenseAttorneyState.ShouldBe("TEST-NEVADA");
        ctx.InjuryClaimNumber.ShouldBe("CLM-1 CLM-2 ");
        ctx.InjuryWcabAdj.ShouldBe("ADJ-1 ADJ-2 ");
        ctx.InjuryWcabOfficeName.ShouldBe("TEST WCAB  ", "the second injury has no WCAB office");
        ctx.InjuryWcabOfficeState.ShouldBe("TEST-CALIFORNIA  ");
        ctx.InjuryPrimaryInsuranceName.ShouldBe("TEST INSURER ");
        ctx.InjuryPrimaryInsuranceState.ShouldBe("TEST-CALIFORNIA ");
        ctx.InjuryClaimExaminerName.ShouldBe("TEST EXAMINER ");
        ctx.InjuryClaimExaminerPhoneNumber.ShouldBe("555-0102 ");
        ctx.DateNow.ShouldBe(PacketDateStamp.GeneratedOn(FixedNow));
    }

    [Fact]
    public async Task An_appointment_with_nothing_attached_leaves_every_section_empty()
    {
        var w = new World();
        var appointment = NewAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        w.Appointments.Add(appointment);

        var ctx = await w.Resolver().ResolveAsync(appointment.Id);

        ctx.RequestConfirmationNumber.ShouldBe("A90777");
        ctx.PatientFirstName.ShouldBeEmpty();
        ctx.PatientInterpreterRequired.ShouldBeEmpty();
        ctx.AvailableDate.ShouldBeEmpty();
        ctx.LocationName.ShouldBeEmpty();
        ctx.AppointmentType.ShouldBeEmpty();
        ctx.EmployerName.ShouldBeEmpty();
        ctx.PatientAttorneyName.ShouldBeEmpty();
        ctx.DefenseAttorneyName.ShouldBeEmpty();
        ctx.InjuryClaimNumber.ShouldBeEmpty();
        ctx.DateNow.ShouldBe(PacketDateStamp.GeneratedOn(FixedNow));
    }

    [Fact]
    public async Task A_patient_without_an_interpreter_vendor_needs_none_and_a_typed_language_wins()
    {
        var w = new World();
        var language = new AppointmentLanguage(Guid.NewGuid(), "TEST-Unused");
        w.Languages.Add(language);
        var patient = new Patient(Guid.NewGuid(), null, language.Id, null, null, "TEST-Pat", "TEST-Doe", "pat2@test.local",
            Gender.Unspecified, new DateTime(1981, 1, 1, 0, 0, 0, DateTimeKind.Utc), PhoneNumberType.Home, othersLanguageName: "TEST-Typed");
        w.Patients.Add(patient);
        var appointment = NewAppointment(patient.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        w.Appointments.Add(appointment);

        var ctx = await w.Resolver().ResolveAsync(appointment.Id);

        ctx.PatientInterpreterRequired.ShouldBe("No");
        ctx.PatientInterpreterLanguage.ShouldBeEmpty();
        PacketTokenResolver.DeriveInterpreter("TEST-Typed", "TEST Vendor").ShouldBe(("Yes", "TEST-TYPED"));
        PacketTokenResolver.DeriveInterpreter(null, "   ").ShouldBe(("No", string.Empty));
    }

    [Fact]
    public async Task An_attorney_account_with_no_name_falls_back_to_the_firm_and_a_missing_record_leaves_the_section_empty()
    {
        var w = new World();
        var appointment = NewAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        w.Appointments.Add(appointment);
        var namelessUser = new IdentityUser(Guid.NewGuid(), "test-nameless", "nameless@test.local");
        w.Users.Add(namelessUser);
        var applicant = new ApplicantAttorney(Guid.NewGuid(), null, namelessUser.Id, "TEST Fallback Firm");
        w.Applicants.Add(applicant);
        w.ApplicantLinks.Add(new AppointmentApplicantAttorney(Guid.NewGuid(), appointment.Id, applicant.Id, namelessUser.Id));
        // A defense link whose attorney record no longer exists.
        w.DefenseLinks.Add(new AppointmentDefenseAttorney(Guid.NewGuid(), appointment.Id, Guid.NewGuid(), null));

        var ctx = await w.Resolver().ResolveAsync(appointment.Id);

        ctx.PatientAttorneyName.ShouldBe("TEST FALLBACK FIRM");
        ctx.PatientAttorneyState.ShouldBeEmpty();
        ctx.DefenseAttorneyName.ShouldBeEmpty();
        ctx.DefenseAttorneyStreet.ShouldBeEmpty();
    }
}
