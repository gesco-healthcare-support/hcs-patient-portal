using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Item B (2026-08-21) -- proves a booking submitted through
/// <c>AppointmentsAppService.SubmitAsync</c> is all-or-nothing.
///
/// <para><b>Why every group is asserted separately.</b> Bug F18 was a cascade that silently dropped
/// 2 of 8 child groups while reporting success. A single "it worked" assertion, or an assertion on
/// the result's <c>Total</c>, cannot catch that: two groups wrong in opposite directions still sum
/// correctly. So each group gets its own assertion, and the rollback test proves the negative.</para>
///
/// <para><b>Why the MultiOffice harness.</b> A submit needs a coherent office: catalog, location,
/// slot, patient, booker. This harness seeds exactly that, and it books inside a tenant context --
/// which since #462 is mandatory, because <c>PatientManager</c> now refuses to create a patient
/// with no practice and host-context booking therefore throws.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeAtomicBookingSubmitTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentsAppService _appointments;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetails;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsurances;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminers;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryDetails;
    private readonly IRepository<AppointmentBodyPart, Guid> _bodyParts;
    private readonly IRepository<AppointmentAccessor, Guid> _accessors;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantAttorneys;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseAttorneys;
    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly IRepository<NotificationTemplateType, Guid> _templateTypeRepository;
    private readonly IGuidGenerator _guidGenerator;

    // Any Guid will do; it just has to be stable across the seeded rows.
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

    public MultiOfficeAtomicBookingSubmitTests()
    {
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _employerDetails = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _primaryInsurances = GetRequiredService<IRepository<AppointmentPrimaryInsurance, Guid>>();
        _claimExaminers = GetRequiredService<IRepository<AppointmentClaimExaminer, Guid>>();
        _injuryDetails = GetRequiredService<IRepository<AppointmentInjuryDetail, Guid>>();
        _bodyParts = GetRequiredService<IRepository<AppointmentBodyPart, Guid>>();
        _accessors = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _applicantAttorneys = GetRequiredService<IRepository<AppointmentApplicantAttorney, Guid>>();
        _defenseAttorneys = GetRequiredService<IRepository<AppointmentDefenseAttorney, Guid>>();
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _templateTypeRepository = GetRequiredService<IRepository<NotificationTemplateType, Guid>>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task SubmitAsync_WithEveryChildGroup_PersistsAllOfThem()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(21);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            result = await _appointments.SubmitAsync(
                BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 210));
        });

        result.ShouldNotBeNull();

        // One assertion per group on the reported counts...
        result!.EmployerDetails.ShouldBe(1);
        result.PrimaryInsurances.ShouldBe(1);
        result.ClaimExaminers.ShouldBe(1);
        result.Accessors.ShouldBe(2);
        result.InjuryDetails.ShouldBe(2);
        result.BodyParts.ShouldBe(3);
        result.ApplicantAttorneys.ShouldBe(1);
        result.DefenseAttorneys.ShouldBe(1);

        // ...and again on what is actually in the database, because a count returned by the code
        // under test only proves the code agrees with itself.
        await InOfficeAsync(office, async () =>
        {
            (await _employerDetails.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(1);
            (await _primaryInsurances.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(1);
            (await _claimExaminers.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(1);
            (await _accessors.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(2);
            (await _applicantAttorneys.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(1);
            (await _defenseAttorneys.CountAsync(x => x.AppointmentId == result.AppointmentId)).ShouldBe(1);

            var injuries = await _injuryDetails.GetListAsync(x => x.AppointmentId == result.AppointmentId);
            injuries.Count.ShouldBe(2);

            var injuryIds = injuries.Select(i => i.Id).ToList();
            (await _bodyParts.CountAsync(x => injuryIds.Contains(x.AppointmentInjuryDetailId))).ShouldBe(3);
        });
    }

    [Fact]
    public async Task SubmitAsync_WhenAChildWriteFails_PersistsNothingAtAll()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(22);
        var patientEmail = $"rb-{Guid.NewGuid().ToString("N")[..8]}@example.test";
        var claimNumber = $"ROLLBACK-{Guid.NewGuid():N}";
        Guid slotId = Guid.Empty;

        await InOfficeAsync(office, async () =>
        {
            slotId = await InsertSlotAsync(office, date, new TimeOnly(11, 0), new TimeOnly(12, 0));
        });

        // The body part is invalid, and body parts are written LAST -- so by the time it throws the
        // patient, the appointment and five other groups have all been written. If the transaction
        // is not doing its job, this test finds the wreckage.
        var doomed = BuildSubmitDto(office, slotId, date.AddHours(11).AddMinutes(15), dayOffset: 220);
        doomed.Patient!.Email = patientEmail;
        doomed.InjuryDetails[0].Injury.ClaimNumber = claimNumber;
        doomed.InjuryDetails[0].BodyParts[0].BodyPartDescription = new string('x', 5000);

        await Should.ThrowAsync<Exception>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(doomed)));

        // Fresh unit of work: the failed one is gone, so this reads committed state only.
        await InOfficeAsync(office, async () =>
        {
            (await _injuryDetails.CountAsync(x => x.ClaimNumber == claimNumber))
                .ShouldBe(0, "the injury was written before the failing body part; it must be gone");

            (await _patientRepository.CountAsync(x => x.Email == patientEmail))
                .ShouldBe(0, "a failed booking must not leave an orphan patient behind");

            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId))
                .ShouldBe(0, "no appointment may survive a failed submit");
        });
    }

    [Fact]
    public async Task SubmitAsync_WithNeitherPatientIdNorPatient_ThrowsTheCodedError()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(23);
        BusinessException? thrown = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 230);
            input.Patient = null;
            input.PatientId = null;

            thrown = await Should.ThrowAsync<BusinessException>(() => _appointments.SubmitAsync(input));
        });

        // Code, never Message: ABP fills the localized message in at the HTTP boundary, so
        // in-process the message is null or the bare code.
        thrown!.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitPatientRequired);
    }

    [Fact]
    public async Task SubmitAsync_WhenThePatientAlreadyExists_ReusesThemInsteadOfDuplicating()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(24);
        AppointmentSubmitResultDto? first = null;
        AppointmentSubmitResultDto? second = null;

        // ONE patient identity, submitted twice. Copied field for field rather than reusing the
        // object, because that is what a repeat booker filling the form again actually sends.
        var template = BuildSubmitDto(office, Guid.Empty, date, dayOffset: 240).Patient!;
        var lastName = template.LastName;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(15, 0), new TimeOnly(16, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(15).AddMinutes(15), dayOffset: 240);
            input.Patient = ClonePatient(template);
            first = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(16, 0), new TimeOnly(17, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(16).AddMinutes(15), dayOffset: 240);
            input.Patient = ClonePatient(template);
            second = await _appointments.SubmitAsync(input);
        });

        second!.PatientId.ShouldBe(first!.PatientId, "the repeat booking must reuse the patient");
        second.PatientAlreadyExisted.ShouldBeTrue();

        await InOfficeAsync(office, async () =>
        {
            // By last name, not email: deduplication can resolve a patient whose stored email
            // differs from the one just submitted, so email is not a reliable identity here.
            (await _patientRepository.CountAsync(x => x.LastName == lastName)).ShouldBe(1);
        });
    }

    private static CreatePatientForAppointmentBookingInput ClonePatient(
        CreatePatientForAppointmentBookingInput source) => new()
        {
            FirstName = source.FirstName,
            LastName = source.LastName,
            Email = source.Email,
            DateOfBirth = source.DateOfBirth,
            PhoneNumber = source.PhoneNumber,
            SocialSecurityNumber = source.SocialSecurityNumber,
            ZipCode = source.ZipCode,
        };

    [Fact]
    public async Task SubmitAsync_AllocatesAConfirmationNumberAndReportsThePatient()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(25);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(8, 0), new TimeOnly(9, 0));
            result = await _appointments.SubmitAsync(
                BuildSubmitDto(office, slotId, date.AddHours(8).AddMinutes(15), dayOffset: 250));
        });

        result!.RequestConfirmationNumber.ShouldNotBeNullOrWhiteSpace();
        result.AppointmentId.ShouldNotBe(Guid.Empty);
        result.PatientId.ShouldNotBe(Guid.Empty);

        await InOfficeAsync(office, async () =>
        {
            var persisted = await _appointmentRepository.FindAsync(result.AppointmentId);
            persisted.ShouldNotBeNull();
            persisted!.PatientId.ShouldBe(result.PatientId);
            persisted.RequestConfirmationNumber.ShouldBe(result.RequestConfirmationNumber);
        });
    }


    /// <summary>
    /// Stubs every notification template for the office.
    ///
    /// <para>Needed because a successful submit publishes the submission event, whose handler
    /// renders the office and booker emails -- and the renderer THROWS on a missing template. The
    /// seeder does not seed templates, so without this the email cascade fails. Since the publish
    /// now runs after the commit, that failure no longer rolls the booking back, but it would still
    /// surface as a failed unit-of-work completion and mask what these tests are actually about.</para>
    /// </summary>
    // ------------------------------------------------------------------ PR2: the patient update
    //
    // The product has TWO patient-update paths and they are not interchangeable. The booking path
    // coalesces (input.X ?? current.X) and structurally cannot change gender, date of birth or
    // phone-number type; the self-service path overwrites and can. The next two tests pin each
    // behaviour separately, because a single shared test would pass while one door silently broke.

    [Fact]
    public async Task SubmitAsync_PatientUpdateFromANonPatientBooker_CoalescesAndCannotChangeGenderDobOrPhoneType()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(25);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Patient? seeded = null;

        // identityUserId null => this patient is NOT the caller, so staff-booking semantics apply.
        await InOfficeAsync(office, async () =>
            seeded = await InsertPatientAsync(office, identityUserId: null, suffix));

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 250);
            input.Patient = null;
            input.PatientId = seeded!.Id;
            input.PatientUpdate = BuildPatientUpdate(seeded, firstName: "Edited", city: "New City");

            await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var after = await _patientRepository.GetAsync(seeded!.Id);
            after.FirstName.ShouldBe("Edited", "a supplied field must be applied");
            after.City.ShouldBe("New City");
            after.GenderId.ShouldBe(Gender.Female, "the booking path must not change gender");
            after.DateOfBirth.Date.ShouldBe(new DateTime(1979, 3, 3), "...nor date of birth");
            after.PhoneNumberTypeId.ShouldBe(PhoneNumberType.Home, "...nor phone-number type");
        });
    }

    [Fact]
    public async Task SubmitAsync_PatientUpdateForTheCallersOwnRecord_OverwritesIncludingGenderDobAndPhoneType()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(26);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Patient? seeded = null;

        // The patient's login IS the caller, which is what selects self-service semantics.
        await InOfficeAsync(office, async () =>
            seeded = await InsertPatientAsync(office, office.BookerUserId, suffix));

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(10).AddMinutes(15), dayOffset: 260);
            input.Patient = null;
            input.PatientId = seeded!.Id;
            input.PatientUpdate = BuildPatientUpdate(seeded, firstName: "SelfEdited", city: "Own City");

            await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var after = await _patientRepository.GetAsync(seeded!.Id);
            after.FirstName.ShouldBe("SelfEdited");
            after.GenderId.ShouldBe(Gender.Other, "self-service overwrites gender");
            after.DateOfBirth.Date.ShouldBe(new DateTime(1999, 12, 31), "...and date of birth");
            after.PhoneNumberTypeId.ShouldBe(PhoneNumberType.Work, "...and phone-number type");
        });
    }

    [Fact]
    public async Task SubmitAsync_WhenTheProfileEditIsStale_ThrowsTheConflictCodeAndSavesNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(27);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Patient? seeded = null;
        var slotId = Guid.Empty;
        BusinessException? thrown = null;

        await InOfficeAsync(office, async () =>
        {
            seeded = await InsertPatientAsync(office, identityUserId: null, suffix);
            slotId = await InsertSlotAsync(office, date, new TimeOnly(12, 0), new TimeOnly(13, 0));
        });

        var stale = BuildSubmitDto(office, slotId, date.AddHours(12).AddMinutes(15), dayOffset: 270);
        stale.Patient = null;
        stale.PatientId = seeded!.Id;
        stale.PatientUpdate = BuildPatientUpdate(seeded, firstName: "Loser", city: "Nowhere");
        // Somebody else saved this patient after the wizard loaded it.
        stale.PatientUpdate.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        // The throw must escape InOfficeAsync, NOT be caught inside it. Catching it within the unit
        // of work leaves the failed work in the change tracker, and the UoW then tries to commit it
        // on the way out -- which is a different failure entirely (a duplicate confirmation number),
        // and it would mask whether the rollback actually happened.
        thrown = await Should.ThrowAsync<BusinessException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(stale)));

        // A distinct code, not AppointmentSubmitFailed: "safe to try again" is the one piece of
        // advice that cannot work here, because the same stale stamp fails identically.
        thrown!.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitPatientUpdateConflict);

        await InOfficeAsync(office, async () =>
        {
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId))
                .ShouldBe(0, "a rejected profile edit must take the whole booking with it");
            (await _patientRepository.GetAsync(seeded!.Id)).FirstName
                .ShouldBe("Original", "the losing edit must not have been applied");
        });
    }

    [Fact]
    public async Task SubmitAsync_WhenAChildWriteFails_AlsoRollsBackTheProfileEdit()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(28);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Patient? seeded = null;
        var slotId = Guid.Empty;

        await InOfficeAsync(office, async () =>
        {
            seeded = await InsertPatientAsync(office, identityUserId: null, suffix);
            slotId = await InsertSlotAsync(office, date, new TimeOnly(14, 0), new TimeOnly(15, 0));
        });

        var doomed = BuildSubmitDto(office, slotId, date.AddHours(14).AddMinutes(15), dayOffset: 280);
        doomed.Patient = null;
        doomed.PatientId = seeded!.Id;
        doomed.PatientUpdate = BuildPatientUpdate(seeded, firstName: "ShouldVanish", city: "Gone");
        // Body parts are written last, so the profile edit is long since applied when this blows.
        doomed.InjuryDetails[0].BodyParts[0].BodyPartDescription = new string('x', 5000);

        // Same reason as the stale-stamp test: the throw has to escape the unit of work so it is
        // disposed without completing, which is what makes this a rollback test at all.
        await Should.ThrowAsync<Exception>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(doomed)));

        await InOfficeAsync(office, async () =>
        {
            // This is the assertion the whole "fold the update into the transaction" decision exists
            // for. Before PR2 the wizard PUT the profile before the appointment POST, so a booking
            // that failed here left the edit applied.
            (await _patientRepository.GetAsync(seeded!.Id)).FirstName
                .ShouldBe("Original", "a failed booking must not leave the profile edit behind");
        });
    }

    // ------------------------------------------------------------------ PR2: the four booking modes
    //
    // /submit now covers all four flows, not just a first booking. Each non-Create mode must chain
    // off its source through the SAME eligibility gate the standalone endpoint uses, and must put
    // the source in the correct link column -- reval on the re-eval chain, re-book on the
    // replacement chain. Those two are not interchangeable; the Case Tracker reads them differently.

    [Fact]
    public async Task SubmitAsync_WithANonCreateModeButNoSource_ThrowsTheCodedError()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = DateTime.Today.AddDays(29);
        var slotId = Guid.Empty;

        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 290);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = null;

        var thrown = await Should.ThrowAsync<BusinessException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(input)));

        // Refused, not quietly downgraded to a first booking -- a reval that lost its chain would
        // mislabel the case folder on the Case Tracker side.
        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitSourceRequired);
    }

    [Fact]
    public async Task SubmitAsync_RevalModeWithAnIneligibleSource_IsRefusedByTheGate()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        // Left Pending. Reval requires Approved, so the gate must refuse -- this is the test that
        // proves /submit did not become a way around the eligibility checks.
        var source = await CreateSourceAppointmentAsync(
            office, DateTime.Today.AddDays(30), new TimeOnly(9, 0),
            AppointmentStatusType.Pending, dayOffset: 300);

        var date = DateTime.Today.AddDays(31);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 310);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = source.Number;

        await Should.ThrowAsync<BusinessException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(input)));

        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId))
                .ShouldBe(0, "a refused gate must leave no appointment behind"));
    }

    [Fact]
    public async Task SubmitAsync_RevalMode_LinksTheSourceOnTheReEvalChain()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        var source = await CreateSourceAppointmentAsync(
            office, DateTime.Today.AddDays(32), new TimeOnly(9, 0),
            AppointmentStatusType.Approved, dayOffset: 320);

        var date = DateTime.Today.AddDays(33);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 330);
            input.Mode = BookingSubmitMode.Reval;
            input.SourceConfirmationNumber = source.Number;
            result = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.OriginalAppointmentId.ShouldBe(source.Id, "reval links on the re-eval chain");
            created.RescheduledFromAppointmentId.ShouldBeNull("...and NOT the replacement chain");
            created.RequestConfirmationNumber.ShouldNotBe(
                source.Number, "a reval mints a fresh confirmation number");
        });
    }

    [Fact]
    public async Task SubmitAsync_ReBookMode_LinksTheSourceOnTheReplacementChain()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        // CanCreateReBook accepts CancelledNoBill / CancelledLate / an attendance outcome. There is
        // no plain "Cancelled" status in this product.
        var source = await CreateSourceAppointmentAsync(
            office, DateTime.Today.AddDays(34), new TimeOnly(9, 0),
            AppointmentStatusType.CancelledNoBill, dayOffset: 340);

        var date = DateTime.Today.AddDays(35);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 350);
            input.Mode = BookingSubmitMode.ReBook;
            input.SourceConfirmationNumber = source.Number;
            result = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.RescheduledFromAppointmentId.ShouldBe(
                source.Id, "a re-book links on the replacement chain");
            created.OriginalAppointmentId.ShouldBeNull("...and NOT the re-eval chain");
        });
    }

    [Fact]
    public async Task SubmitAsync_ReSubmitModeWithANonRejectedSource_IsRefusedByTheGate()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        // Approved, and ReSubmit requires Rejected -- so the gate must refuse.
        var source = await CreateSourceAppointmentAsync(
            office, DateTime.Today.AddDays(36), new TimeOnly(9, 0),
            AppointmentStatusType.Approved, dayOffset: 360);

        var date = DateTime.Today.AddDays(37);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 370);
        input.Mode = BookingSubmitMode.ReSubmit;
        input.SourceConfirmationNumber = source.Number;

        var thrown = await Should.ThrowAsync<BusinessException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(input)));

        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentReSubmitSourceNotRejected);
    }

    /// <summary>
    /// The first test to drive a re-submit end to end. It could not pass before 2026-08-22: ReSubmit
    /// carried the source's confirmation number forward, and the unique index on
    /// (TenantId, RequestConfirmationNumber) is still satisfied by the rejected source row, so every
    /// re-submit died on that constraint. Adrian's call was to mint a fresh number and carry the
    /// link on a column instead -- which is what this asserts.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_ReSubmitMode_MintsAFreshNumberAndLinksTheRejectedSource()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        var source = await CreateSourceAppointmentAsync(
            office, DateTime.Today.AddDays(38), new TimeOnly(9, 0),
            AppointmentStatusType.Rejected, dayOffset: 380);

        var date = DateTime.Today.AddDays(39);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 390);
            input.Mode = BookingSubmitMode.ReSubmit;
            input.SourceConfirmationNumber = source.Number;
            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
        result!.RequestConfirmationNumber.ShouldNotBe(
            source.Number, "two live appointments cannot share a confirmation number");

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result.AppointmentId);
            created.RescheduledFromAppointmentId.ShouldBe(
                source.Id, "a re-submit replaces the rejected request");

            // The rejected original must still be there, keeping its own number -- that audit trail
            // is the reason re-submit exists.
            var original = await _appointmentRepository.GetAsync(source.Id);
            original.RequestConfirmationNumber.ShouldBe(source.Number);
            original.AppointmentStatus.ShouldBe(AppointmentStatusType.Rejected);
        });
    }

    /// <summary>
    /// Books an appointment through the plain-create path, then forces it into the status a
    /// downstream flow's gate requires. The status is set directly rather than driven through the
    /// state machine because what is under test here is the SUBMIT path's mode handling, not the
    /// transition rules -- those have their own tests.
    /// </summary>
    private async Task<(Guid Id, string Number)> CreateSourceAppointmentAsync(
        SeededOffice office, DateTime date, TimeOnly from, AppointmentStatusType status, int dayOffset)
    {
        AppointmentSubmitResultDto? created = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, from, from.AddHours(1));
            created = await _appointments.SubmitAsync(
                BuildSubmitDto(office, slotId, date.AddHours(from.Hour).AddMinutes(15), dayOffset));
        });

        await InOfficeAsync(office, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(created!.AppointmentId);
            appointment.AppointmentStatus = status;
            await _appointmentRepository.UpdateAsync(appointment, autoSave: true);
        });

        return (created!.AppointmentId, created.RequestConfirmationNumber);
    }

    /// <summary>
    /// A patient with known, deliberately distinctive demographics, so a test can tell "the update
    /// was applied" apart from "the update was ignored" without ambiguity.
    /// </summary>
    private async Task<Patient> InsertPatientAsync(
        SeededOffice office, Guid? identityUserId, string suffix)
    {
        var patient = new Patient(
            id: Guid.NewGuid(),
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: identityUserId,
            tenantId: office.OfficeId,
            firstName: "Original",
            lastName: $"Holder{suffix}",
            email: $"holder-{suffix}@example.test",
            genderId: Gender.Female,
            dateOfBirth: new DateTime(1979, 3, 3),
            phoneNumberTypeId: PhoneNumberType.Home,
            phoneNumber: "5550000001",
            city: "Old City");

        await _patientRepository.InsertAsync(patient, autoSave: true);
        return patient;
    }

    /// <summary>
    /// The three fields the two update paths disagree about (gender, DOB, phone-number type) are
    /// ALWAYS set here, and always to something different from the seeded values. That is what makes
    /// the coalescing test meaningful: it asserts they did NOT take effect even though they were sent.
    /// </summary>
    private static PatientUpdateDto BuildPatientUpdate(Patient seeded, string firstName, string city) =>
        new()
        {
            FirstName = firstName,
            LastName = seeded.LastName,
            Email = seeded.Email,
            City = city,
            GenderId = Gender.Other,
            DateOfBirth = new DateTime(1999, 12, 31),
            PhoneNumberTypeId = PhoneNumberType.Work,
            ConcurrencyStamp = seeded.ConcurrencyStamp,
        };

    private async Task SeedNotificationTemplatesAsync(SeededOffice office)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                if (await _templateTypeRepository.FindAsync(EmailTypeId) == null)
                {
                    await _templateTypeRepository.InsertAsync(
                        new NotificationTemplateType(EmailTypeId, "Email"), autoSave: true);
                }

                var queryable = await _templateRepository.GetQueryableAsync();
                var existing = queryable.Select(x => x.TemplateCode).ToList();

                foreach (var code in NotificationTemplateConsts.Codes.All)
                {
                    if (existing.Contains(code))
                    {
                        continue;
                    }

                    await _templateRepository.InsertAsync(new NotificationTemplate(
                        id: _guidGenerator.Create(),
                        tenantId: office.OfficeId,
                        templateCode: code,
                        templateTypeId: EmailTypeId,
                        subject: $"[{code}] -- TEST",
                        bodyEmail: $"<p>Stub for {code}</p>",
                        bodySms: $"Stub for {code}",
                        description: null,
                        isActive: true), autoSave: true);
                }
            }
        }, requiresNew: true);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// Runs <paramref name="body"/> in the office's tenant context AND as the office's booker.
    ///
    /// <para>The identity is not optional. Writing accessors goes through
    /// <c>AppointmentReadAccessGuard.EnsureCanManageAccessorsAsync</c>, which asks whether the
    /// caller may manage that appointment -- with no principal there is no caller, so it denies.
    /// In production the booker is authenticated; the test has to say so too.</para>
    /// </summary>
    private Task InOfficeAsync(SeededOffice office, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, office.BookerUserId, "admin"))
            {
                await body();
            }
        }, requiresNew: true);

    private async Task<Guid> InsertSlotAsync(
        SeededOffice office, DateTime date, TimeOnly from, TimeOnly to)
    {
        var slot = new DoctorAvailability(
            id: Guid.NewGuid(),
            locationId: office.LocationId,
            availableDate: date,
            fromTime: from,
            toTime: to,
            bookingStatusId: BookingStatus.Available,
            capacity: 3);
        slot.TenantId = office.OfficeId;
        slot.AppointmentTypes.Add(
            new DoctorAvailabilityAppointmentType(slot.Id, office.AppointmentTypeId, office.OfficeId));
        await _slots.InsertAsync(slot, autoSave: true);
        return slot.Id;
    }

    /// <summary>
    /// A booking with EVERY child group populated: one employer detail, one primary insurance, one
    /// claim examiner, two accessors, two injuries carrying three body parts between them, and both
    /// attorneys. Distinct counts per group on purpose -- if the writer ever crossed two groups
    /// over, matching counts would hide it.
    /// </summary>
    private static AppointmentSubmitDto BuildSubmitDto(
        SeededOffice office, Guid slotId, DateTime date, int dayOffset = 0)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        return new AppointmentSubmitDto
        {
            Patient = new CreatePatientForAppointmentBookingInput
            {
                FirstName = "Submit",
                LastName = $"Tester{suffix}",
                Email = $"submit-{suffix}@example.test",
                // Distinct per booking on purpose. The deduplication scan counts a
                // null-vs-null field as a match, so patients that share a date of birth and a
                // phone number -- with SSN and ZIP both unset -- already match on four of six and
                // get merged. Varying these keeps unrelated test patients apart.
                DateOfBirth = new DateTime(1985, 4, 12).AddDays(dayOffset),
                PhoneNumber = $"555{dayOffset:D7}",
                SocialSecurityNumber = $"SSN-{suffix}",
                ZipCode = $"9{dayOffset:D4}",
            },
            IdentityUserId = office.BookerUserId,
            AppointmentTypeId = office.AppointmentTypeId,
            LocationId = office.LocationId,
            DoctorAvailabilityId = slotId,
            AppointmentDate = date,
            AppointmentStatus = AppointmentStatusType.Pending,
            PatientEmail = $"submit-{suffix}@example.test",
            ApplicantAttorneyEmail = $"aa-{suffix}@example.test",
            DefenseAttorneyEmail = $"da-{suffix}@example.test",
            ClaimExaminerEmail = $"ce-{suffix}@example.test",

            EmployerDetail = new AppointmentEmployerDetailCreateDto
            {
                EmployerName = "Acme Fabrication",
                Occupation = "Machinist",
            },
            PrimaryInsurance = new AppointmentPrimaryInsuranceCreateDto
            {
                Name = "Statewide Mutual",
            },
            ClaimExaminer = new AppointmentClaimExaminerCreateDto
            {
                Name = "Casey Examiner",
                Email = $"ce-{suffix}@example.test",
            },
            // FirmName is not optional for either attorney -- EnsureAttorneyFirmNamePresent
            // rejects a blank one.
            ApplicantAttorney = new ApplicantAttorneyDetailsDto
            {
                FirstName = "Avery",
                LastName = "Applicant",
                Email = $"aa-{suffix}@example.test",
                FirmName = "Applicant Law Group",
            },
            DefenseAttorney = new DefenseAttorneyDetailsDto
            {
                FirstName = "Devon",
                LastName = "Defense",
                Email = $"da-{suffix}@example.test",
                FirmName = "Defense Partners LLP",
            },
            Accessors = new List<AppointmentAccessorCreateDto>
            {
                new() { Email = $"acc1-{suffix}@example.test", Role = "Paralegal" },
                new() { Email = $"acc2-{suffix}@example.test", Role = "Assistant" },
            },
            InjuryDetails = new List<AppointmentInjurySubmitDto>
            {
                new()
                {
                    Injury = new AppointmentInjuryDetailCreateDto
                    {
                        DateOfInjury = new DateTime(2025, 2, 3),
                        ClaimNumber = $"CLM-{suffix}-1",
                        IsCumulativeInjury = false,
                        WcabAdj = "ADJ-1000001",
                        BodyPartsSummary = "Lower back; left knee",
                    },
                    BodyParts = new List<AppointmentBodyPartCreateDto>
                    {
                        new() { BodyPartDescription = "Lower back" },
                        new() { BodyPartDescription = "Left knee" },
                    },
                },
                new()
                {
                    Injury = new AppointmentInjuryDetailCreateDto
                    {
                        DateOfInjury = new DateTime(2025, 6, 9),
                        ClaimNumber = $"CLM-{suffix}-2",
                        IsCumulativeInjury = true,
                        WcabAdj = "ADJ-1000002",
                        BodyPartsSummary = "Right shoulder",
                    },
                    BodyParts = new List<AppointmentBodyPartCreateDto>
                    {
                        new() { BodyPartDescription = "Right shoulder" },
                    },
                },
            },
        };
    }
}
