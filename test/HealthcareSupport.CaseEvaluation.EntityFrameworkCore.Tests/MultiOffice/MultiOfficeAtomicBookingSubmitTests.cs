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
using HealthcareSupport.CaseEvaluation.PhiAccess;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Validation;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Item B (2026-08-21) -- proves a booking submitted through
/// <c>AppointmentsAppService.SubmitAsync</c> is all-or-nothing.
///
/// <para><b>Why every group is asserted separately.</b> Bug F18 was a cascade that silently dropped
/// 2 of 8 child groups while reporting success. A single "it worked" assertion, or an assertion on
/// the result's <c>Total</c>, cannot catch that: two groups wrong in opposite directions still sum
/// correctly. So each group gets its own assertion.</para>
///
/// <para><b>WHAT THIS FILE DOES NOT COVER, corrected 2026-09-08.</b> The header above once ended
/// "and the rollback test proves the negative". It does not. The two tests that carried rollback
/// names were rejecting an oversized DTO at the validation boundary, so <c>SubmitAsync</c> never
/// executed and nothing was ever written -- measured as <c>AbpValidationException</c>. They are
/// renamed to say so. <b>The all-or-nothing guarantee itself has no regression guard, and one
/// cannot be written in this harness:</b> a control proved that a single row written in a unit of
/// work which throws before completing SURVIVES here, so rollback is unavailable regardless of the
/// code under test. Tracked as issue #732. Do not read any test in this file as atomicity
/// coverage.</para>
///
/// <para><b>Why the MultiOffice harness.</b> A submit needs a coherent office: catalog, location,
/// slot, patient, booker. This harness seeds exactly that, and it books inside a tenant context --
/// which since #462 is mandatory, because <c>PatientManager</c> now refuses to create a patient
/// with no practice and host-context booking therefore throws.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public partial class MultiOfficeAtomicBookingSubmitTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentsAppService _appointments;
    private readonly IPatientsAppService _patients;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetails;
    private readonly IAppointmentEmployerDetailsAppService _employerDetailsService;
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
    private readonly IReEvaluationSourcesAppService _reEvaluationSources;
    private readonly IRepository<PhiAccessLogEntry, Guid> _phiAccessLog;

    // Any Guid will do; it just has to be stable across the seeded rows.
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

    public MultiOfficeAtomicBookingSubmitTests()
    {
        _appointments = GetRequiredService<IAppointmentsAppService>();
        _patients = GetRequiredService<IPatientsAppService>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _employerDetails = GetRequiredService<IRepository<AppointmentEmployerDetail, Guid>>();
        _employerDetailsService = GetRequiredService<IAppointmentEmployerDetailsAppService>();
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
        _reEvaluationSources = GetRequiredService<IReEvaluationSourcesAppService>();
        _phiAccessLog = GetRequiredService<IRepository<PhiAccessLogEntry, Guid>>();
    }

    [Fact]
    public async Task SubmitAsync_WithEveryChildGroup_PersistsAllOfThem()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(21);
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

    /// <summary>
    /// DTO-boundary rejection, NOT atomicity. Renamed 2026-09-08 from
    /// <c>SubmitAsync_WhenAChildWriteFails_PersistsNothingAtAll</c>, which described a guarantee it
    /// has never exercised -- see the note on the trigger below and issue #732.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_WhenABodyPartExceedsItsMaxLength_IsRejectedBeforeAnyWrite()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(22);
        var patientEmail = $"rb-{Guid.NewGuid().ToString("N")[..8]}@example.test";
        var claimNumber = $"ROLLBACK-{Guid.NewGuid():N}";
        Guid slotId = Guid.Empty;

        await InOfficeAsync(office, async () =>
        {
            slotId = await InsertSlotAsync(office, date, new TimeOnly(11, 0), new TimeOnly(12, 0));
        });

        // WHAT ACTUALLY HAPPENS HERE. This comment previously said the patient, the appointment and
        // five child groups were all written before the throw, so the test "finds the wreckage" if
        // the transaction is not doing its job. That was false, and it is what kept the gap hidden.
        //
        // AppointmentBodyPartCreateDto.cs:11 carries [StringLength(500)]
        // (AppointmentBodyPartConsts.cs:12), so a 5000-character description is rejected by ABP's
        // validation interceptor BEFORE SubmitAsync executes. Nothing is written, and there is no
        // wreckage to find. Measured 2026-09-08 by capturing the exception: it is
        // AbpValidationException, which the old base-type ThrowAsync<Exception> accepted exactly as
        // it would have accepted a genuine rollback failure.
        //
        // So this is a boundary-rejection test and it is asserted as one. THE ATOMICITY GUARANTEE
        // ITSELF HAS NO TEST: see issue #732. It cannot be written in this harness -- a control
        // showed a single row written in a unit of work that threw before completing SURVIVES, so
        // rollback is not available here at all.
        var doomed = BuildSubmitDto(office, slotId, date.AddHours(11).AddMinutes(15), dayOffset: 220);
        doomed.Patient!.Email = patientEmail;
        doomed.InjuryDetails[0].Injury.ClaimNumber = claimNumber;
        doomed.InjuryDetails[0].BodyParts[0].BodyPartDescription = new string('x', 5000);

        // AbpValidationException specifically, MEASURED 2026-09-08. The base type used to be
        // asserted here, and it is what made the misnaming invisible: it accepted a DTO-validation
        // failure exactly as it would accept a rollback failure, so the test stayed green while
        // verifying something other than its own name. Five sibling assertions in this file already
        // assert a specific type; this converges on that convention rather than inventing one.
        await Should.ThrowAsync<AbpValidationException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(doomed)));

        // These three confirm the rejection happened BEFORE ANY WRITE, which is a real property and
        // is what this test verifies. They are NOT evidence of rollback: an empty database is
        // equally consistent with a database that was never touched, and here it was never touched.
        // Do not read them as atomicity coverage -- that is #732.
        await InOfficeAsync(office, async () =>
        {
            (await _injuryDetails.CountAsync(x => x.ClaimNumber == claimNumber))
                .ShouldBe(0, "a DTO rejected at the boundary must not have written an injury");

            (await _patientRepository.CountAsync(x => x.Email == patientEmail))
                .ShouldBe(0, "a DTO rejected at the boundary must not have created a patient");

            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId))
                .ShouldBe(0, "a DTO rejected at the boundary must not have booked the slot");
        });
    }

    [Fact]
    public async Task SubmitAsync_WithNeitherPatientIdNorPatient_ThrowsTheCodedError()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(23);
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
        var date = TestToday.AddDays(24);
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
        var date = TestToday.AddDays(25);
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
        var date = TestToday.AddDays(25);
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
        var date = TestToday.AddDays(26);
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

    /// <summary>
    /// #598 -- an EXTERNAL booker who is not the patient books against an existing, claimed record and
    /// sends edits. The booking must go through (the form always sends a patient update when a
    /// patient is loaded, so refusing would fail every such booking) and the stored record must stay
    /// exactly as it was. Before #598 the edits were applied.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_PatientUpdateFromAnExternalBookerForSomeoneElsesRecord_BooksButLeavesThePatientUnchanged()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(40);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var externalBooker = Guid.NewGuid();
        Patient? seeded = null;
        AppointmentSubmitResultDto? result = null;

        // Claimed by the office's booker user -- a real login that is NOT the external caller.
        await InOfficeAsync(office, async () =>
            seeded = await InsertPatientAsync(office, office.BookerUserId, suffix));

        // A party, so the supplied id is one they may book for; this test is about edits, not entitlement.
        await MakePartyToPatientAsync(office, seeded!.Id, externalBooker, date.AddDays(-20), 8);

        await InOfficeAsAsync(office, externalBooker, "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 400);
            input.Patient = null;
            input.PatientId = seeded!.Id;
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            input.PatientUpdate = BuildPatientUpdate(seeded, firstName: "Overwritten", city: "Overwritten City");

            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
        result!.PatientId.ShouldBe(seeded!.Id);
        await InOfficeAsync(office, async () =>
        {
            (await _appointmentRepository.FindAsync(result.AppointmentId)).ShouldNotBeNull(
                "the booking itself must still be created");

            var after = await _patientRepository.GetAsync(seeded.Id);
            after.FirstName.ShouldBe("Original", "an external booker's edit to someone else's record must be dropped");
            after.City.ShouldBe("Old City");
            after.GenderId.ShouldBe(Gender.Female);
        });
    }

    /// <summary>
    /// #598 -- the two-step path. Booking against an existing patient's id makes the booker that
    /// appointment's creator, which is a party relationship. It must NOT turn into the right to edit
    /// the patient afterwards through the booking edit endpoint.
    /// </summary>
    [Fact]
    public async Task Booking_for_an_existing_patient_does_not_grant_the_booker_the_right_to_edit_them()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(41);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var externalBooker = Guid.NewGuid();
        Patient? seeded = null;

        await InOfficeAsync(office, async () =>
            seeded = await InsertPatientAsync(office, office.BookerUserId, suffix));

        // A party, so the supplied id is one they may book for; this test is about edits, not entitlement.
        await MakePartyToPatientAsync(office, seeded!.Id, externalBooker, date.AddDays(-20), 8);

        // Step 1: book with the claimed patient's id and no edits. This succeeds.
        await InOfficeAsAsync(office, externalBooker, "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(10, 0), new TimeOnly(11, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(10).AddMinutes(15), dayOffset: 410);
            input.Patient = null;
            input.PatientId = seeded!.Id;
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            await _appointments.SubmitAsync(input);
        });

        // Step 2: the same booker tries to edit that patient directly.
        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            InOfficeAsAsync(office, externalBooker, "Applicant Attorney", () =>
                _patients.UpdatePatientForAppointmentBookingAsync(
                    seeded!.Id, BuildPatientUpdate(seeded, firstName: "Overwritten", city: "Overwritten City"))));

        await InOfficeAsync(office, async () =>
        {
            var after = await _patientRepository.GetAsync(seeded!.Id);
            after.FirstName.ShouldBe("Original");
            after.City.ShouldBe("Old City");
        });
    }

    [Fact]
    public async Task SubmitAsync_WhenTheProfileEditIsStale_ThrowsTheConflictCodeAndSavesNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(27);
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
    public async Task SubmitAsync_WhenABodyPartExceedsItsMaxLength_RejectsBeforeApplyingTheProfileEdit()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(28);
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
        // Renamed 2026-09-08. The comment here used to say body parts are written last, so the
        // profile edit "is long since applied when this blows". That was false for the same reason
        // as in SubmitAsync_WhenABodyPartExceedsItsMaxLength_IsRejectedBeforeAnyWrite: a
        // 5000-character description exceeds [StringLength(500)], so ABP rejects the DTO before
        // SubmitAsync runs and the profile edit is never applied in the first place. This test
        // verifies that the edit is not applied when the payload is rejected -- it has never shown
        // an applied edit being rolled back, and it cannot. See #732.
        doomed.InjuryDetails[0].BodyParts[0].BodyPartDescription = new string('x', 5000);
        // AbpValidationException specifically, MEASURED 2026-09-08. The base type used to be
        // asserted here, and it is what made the misnaming invisible: it accepted a DTO-validation
        // failure exactly as it would accept a rollback failure, so the test stayed green while
        // verifying something other than its own name. Five sibling assertions in this file already
        // assert a specific type; this converges on that convention rather than inventing one.
        await Should.ThrowAsync<AbpValidationException>(
            () => InOfficeAsync(office, () => _appointments.SubmitAsync(doomed)));

        await InOfficeAsync(office, async () =>
        {
            // The "fold the update into the transaction" decision is what this was written for --
            // before PR2 the wizard PUT the profile before the appointment POST, so a failed
            // booking left the edit applied. What this assertion actually demonstrates today is
            // narrower: a payload rejected at the DTO boundary never reaches the update at all.
            // The transaction half of that decision is unguarded; see #732.
            (await _patientRepository.GetAsync(seeded!.Id)).FirstName
                .ShouldBe("Original", "a payload rejected at the DTO boundary must not have applied "
                    + "the profile edit");
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
        var date = TestToday.AddDays(29);
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
            office, TestToday.AddDays(30), new TimeOnly(9, 0),
            AppointmentStatusType.Pending, dayOffset: 300);

        var date = TestToday.AddDays(31);
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
            office, TestToday.AddDays(32), new TimeOnly(9, 0),
            AppointmentStatusType.Approved, dayOffset: 320);

        var date = TestToday.AddDays(33);
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
    public async Task SubmitAsync_RevalWithoutSourceMode_NeedsNoSourceAndLinksNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);

        var date = TestToday.AddDays(60);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 600);
            input.Mode = BookingSubmitMode.RevalWithoutSource;
            input.SourceConfirmationNumber = null;
            result = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.EvaluationKind.ShouldBe(EvaluationKind.ReEvaluation);
            created.OriginalAppointmentId.ShouldBeNull();
            created.RescheduledFromAppointmentId.ShouldBeNull();
            created.RequestConfirmationNumber.ShouldNotBeNullOrWhiteSpace();
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
            office, TestToday.AddDays(34), new TimeOnly(9, 0),
            AppointmentStatusType.CancelledNoBill, dayOffset: 340);

        var date = TestToday.AddDays(35);
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
            office, TestToday.AddDays(36), new TimeOnly(9, 0),
            AppointmentStatusType.Approved, dayOffset: 360);

        var date = TestToday.AddDays(37);
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
            office, TestToday.AddDays(38), new TimeOnly(9, 0),
            AppointmentStatusType.Rejected, dayOffset: 380);

        var date = TestToday.AddDays(39);
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

    // ------------------------------------------------------------------ R2c (2026-11-06)
    //
    // A re-evaluation by someone who is NOT a party to the earlier appointment (a new paralegal at the
    // same firm, say): the confirmation number plus the patient's date of birth. The lookup hands such
    // a caller the whole earlier case, so each refusal is the same null, the attempts are capped, and
    // the submit checks the date again rather than trusting the lookup.

    // Every MultiOffice class writes slots into ONE shared database under a unique (office, location, day, from, to)
    // key, so these tests use evening hours nobody else books (18:00 sources, 19:00 re-evaluations), each on its own day.

    /// <summary>An earlier appointment booked by staff, with the patient's id and date of birth.</summary>
    private async Task<(Guid Id, string Number, Guid PatientId, DateTime DateOfBirth)> CreateReEvaluationSourceAsync(
        SeededOffice office, int day, int dayOffset, AppointmentStatusType status = AppointmentStatusType.Approved)
    {
        var source = await CreateSourceAppointmentAsync(
            office, TestToday.AddDays(day), new TimeOnly(18, 0), status, dayOffset);
        var patientId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            patientId = (await _appointmentRepository.GetAsync(source.Id)).PatientId);

        // BuildSubmitDto's patient date of birth for this offset.
        return (source.Id, source.Number, patientId, new DateTime(1985, 4, 12).AddDays(dayOffset));
    }

    private static ReEvaluationSourceLookupDto Lookup(string number, DateTime dateOfBirth) => new()
    {
        ConfirmationNumber = number,
        PatientDateOfBirth = dateOfBirth,
    };

    private async Task<List<PhiAccessLogEntry>> PhiRowsForAsync(SeededOffice office, Guid userId)
    {
        var rows = new List<PhiAccessLogEntry>();
        await InOfficeAsync(office, async () =>
            rows.AddRange(await _phiAccessLog.GetListAsync(e => e.UserId == userId)));
        return rows;
    }

    [Fact]
    public async Task ReEvaluationLookup_ByANonPartyWithTheDateOfBirth_ReturnsTheCaseWithoutWhatOnlyPartiesSee()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 62, dayOffset: 900);
        var stranger = Guid.NewGuid();
        ReEvaluationSourceDto? found = null;

        await InOfficeAsAsync(office, stranger, "Applicant Paralegal", async () =>
            found = await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth)));

        found.ShouldNotBeNull("the confirmation number and date of birth match an approved case");
        found!.CallerIsParty.ShouldBeFalse();
        found.Source.Appointment.Id.ShouldBe(source.Id);
        found.Source.Patient!.Id.ShouldBe(source.PatientId);
        found.Source.AppointmentInjuryDetails.Count.ShouldBe(2, "the form copies the injuries");
        found.Source.ClaimExaminer.ShouldNotBeNull();
        found.ApplicantAttorney!.FirmName.ShouldBe("Applicant Law Group");
        found.DefenseAttorney!.FirmName.ShouldBe("Defense Partners LLP");

        found.Source.AppointmentAccessors.ShouldBeEmpty("a non-party does not learn who else may see the case");
        found.Source.Patient.SocialSecurityNumber.ShouldBeNull("the form never prefills it");
        found.Source.IdentityUser.ShouldBeNull();
        found.Source.BookedByUser.ShouldBeNull();

        var row = (await PhiRowsForAsync(office, stranger)).ShouldHaveSingleItem();
        row.Action.ShouldBe(PhiAccessAction.ReEvaluationSourceLoadedByDateOfBirth);
        row.AppointmentId.ShouldBe(source.Id);
        row.PatientId.ShouldBe(source.PatientId);
    }

    [Fact]
    public async Task ReEvaluationLookup_ByStaff_IsAPartyAndKeepsTheAuthorizedUsers()
    {
        // The control for the test above: the same case through the same call keeps what the non-party
        // lost, so those empty values are the rule at work and not a graph that never loaded.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 63, dayOffset: 910);
        ReEvaluationSourceDto? found = null;

        await InOfficeAsync(office, async () =>
            found = await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth)));

        found.ShouldNotBeNull();
        found!.CallerIsParty.ShouldBeTrue();
        found.Source.AppointmentAccessors.Count.ShouldBe(2);
        found.Source.Patient!.SocialSecurityNumber.ShouldNotBeNullOrEmpty("a party sees the last four");
    }

    [Fact]
    public async Task ReEvaluationLookup_AWrongDateOfBirthAndAnUnknownNumber_GetTheSameNull_AndAreLogged()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 64, dayOffset: 920);
        var stranger = Guid.NewGuid();
        ReEvaluationSourceDto? wrongDate = new();
        ReEvaluationSourceDto? unknownNumber = new();

        await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
        {
            wrongDate = await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth.AddDays(1)));
            unknownNumber = await _reEvaluationSources.LookupAsync(Lookup("NOPE-UNKNOWN", source.DateOfBirth));
        });

        wrongDate.ShouldBeNull();
        unknownNumber.ShouldBeNull("the same answer, so the lookup never confirms a number exists");

        var rows = await PhiRowsForAsync(office, stranger);
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(r => r.Action == PhiAccessAction.ReEvaluationSourceLookupRefused);
        rows.ShouldAllBe(r => r.AppointmentId == null && r.PatientId == null, "a refusal names nobody");
    }

    [Fact]
    public async Task ReEvaluationLookup_ByANonPartyForACaseThatCannotBeReEvaluated_ReturnsNull()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(
            office, day: 65, dayOffset: 930, status: AppointmentStatusType.Pending);
        ReEvaluationSourceDto? found = new();

        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", async () =>
            found = await _reEvaluationSources.LookupAsync(Lookup(source.Number, source.DateOfBirth)));

        found.ShouldBeNull("a right date of birth does not open a case that cannot be re-evaluated");
    }

    [Fact]
    public async Task ReEvaluationLookup_IsRefusedAfterTheHourlyLimitForOneAccount()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var stranger = Guid.NewGuid();
        var guess = Lookup("NOPE-LIMIT", new DateTime(1980, 1, 1));

        for (var attempt = 0; attempt < LookupThrottleBudget.ReEvaluationSource.MaxPerUserPerHour; attempt++)
        {
            await InOfficeAsAsync(office, stranger, "Applicant Attorney", async () =>
                (await _reEvaluationSources.LookupAsync(guess)).ShouldBeNull());
        }

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, stranger, "Applicant Attorney", () => _reEvaluationSources.LookupAsync(guess)));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ReEvaluationLookupThrottled);

        // Another account is not held back by this one's guesses.
        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", async () =>
            (await _reEvaluationSources.LookupAsync(guess)).ShouldBeNull());
    }

    [Fact]
    public async Task SubmitAsync_RevalByANonPartyWithTheDateOfBirth_BooksForTheEarlierCasesPatient()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 66, dayOffset: 940);
        var date = TestToday.AddDays(44);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(19, 0), new TimeOnly(20, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(19).AddMinutes(15), dayOffset: 950);
            input.Mode = BookingSubmitMode.Reval;
            input.SourceConfirmationNumber = source.Number;
            input.SourcePatientDateOfBirth = source.DateOfBirth;
            input.Patient = null;
            input.PatientId = source.PatientId;
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            result = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.OriginalAppointmentId.ShouldBe(source.Id, "reval links on the re-eval chain");
            created.PatientId.ShouldBe(source.PatientId, "the proof entitles them to book for that patient");
        });
    }

    [Theory]
    [InlineData(false, 67, 960, 45)]
    [InlineData(true, 68, 970, 47)]
    public async Task SubmitAsync_RevalByANonPartyWithoutAMatchingDateOfBirth_IsRefusedAsNotFound(
        bool sendsADate, int sourceDay, int dayOffset, int bookingDay)
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: sourceDay, dayOffset: dayOffset);
        var date = TestToday.AddDays(bookingDay);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(19, 0), new TimeOnly(20, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(19).AddMinutes(15), dayOffset: dayOffset + 1);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = source.Number;
        input.SourcePatientDateOfBirth = sendsADate ? source.DateOfBirth.AddYears(1) : null;
        input.Patient = null;
        input.PatientId = source.PatientId;
        input.Accessors = new List<AppointmentAccessorCreateDto>();

        // SubmitAsync reports any uncoded failure as AppointmentSubmitFailed ("nothing was saved");
        // inside is the guard's not-found, the same one a number nobody issued gets.
        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitFailed);
        var notFound = refused.InnerException.ShouldBeOfType<EntityNotFoundException>();
        notFound.EntityType.ShouldBe(typeof(Appointment));
        notFound.Id.ShouldBe(source.Number, "the same not-found a number nobody issued gets");
        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId)).ShouldBe(0));
    }

    [Fact]
    public async Task SubmitAsync_RevalByANonPartyForACaseThatCannotBeReEvaluated_IsRefusedAsNotFound()
    {
        // The lookup re-applies the status rule itself, so only the submit proves the access rule's
        // own status gate: a right date of birth must not chain a re-evaluation off a Pending case.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(
            office, day: 70, dayOffset: 985, status: AppointmentStatusType.Pending);
        var date = TestToday.AddDays(49);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(19, 0), new TimeOnly(20, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(19).AddMinutes(15), dayOffset: 986);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = source.Number;
        input.SourcePatientDateOfBirth = source.DateOfBirth;
        input.Patient = null;
        input.PatientId = source.PatientId;
        input.Accessors = new List<AppointmentAccessorCreateDto>();

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitFailed);
        refused.InnerException.ShouldBeOfType<EntityNotFoundException>()
            .Id.ShouldBe(source.Number, "not the status-specific refusal a party gets");
    }

    [Fact]
    public async Task SubmitAsync_RevalProvenByDateOfBirth_DoesNotEntitleTheCallerToAnotherPatient()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var source = await CreateReEvaluationSourceAsync(office, day: 69, dayOffset: 980);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        Patient? someoneElse = null;
        await InOfficeAsync(office, async () =>
            someoneElse = await InsertPatientAsync(office, identityUserId: null, suffix));
        var date = TestToday.AddDays(48);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(19, 0), new TimeOnly(20, 0)));

        var input = BuildSubmitDto(office, slotId, date.AddHours(19).AddMinutes(15), dayOffset: 990);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = source.Number;
        input.SourcePatientDateOfBirth = source.DateOfBirth;
        input.Patient = null;
        input.PatientId = someoneElse!.Id;
        input.Accessors = new List<AppointmentAccessorCreateDto>();

        await Should.ThrowAsync<AbpAuthorizationException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", () => _appointments.SubmitAsync(input)));
    }

    // ------------------------------------------------------------------ mail choices (2026-11-04)
    //
    // Office decisions 1 and 2: the booker may stop following the case, and an attorney may send all
    // of the lawyer's mail to a same-side paralegal Authorized User instead. Stored on the appointment
    // itself; refused BEFORE any write when the redirect is not allowed.

    /// <summary>An external booker's opt-out is stored; the same request from staff is ignored.</summary>
    [Fact]
    public async Task SubmitAsync_TheOptOut_IsStoredForAnExternalBooker_AndIgnoredForStaff()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(43);
        AppointmentSubmitResultDto? external = null;
        AppointmentSubmitResultDto? staff = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 700);
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            input.BookerFollowsCase = false;
            external = await _appointments.SubmitAsync(input);
        });
        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date.AddDays(1), new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildSubmitDto(office, slotId, date.AddDays(1).AddHours(13).AddMinutes(15), dayOffset: 710);
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            input.BookerFollowsCase = false;
            staff = await _appointments.SubmitAsync(input);
        });

        await InOfficeAsync(office, async () =>
        {
            var optedOut = await _appointmentRepository.GetAsync(external!.AppointmentId);
            AppointmentMailPreferences.BookerFollowsCase(optedOut).ShouldBeFalse();
            AppointmentMailPreferences.GetAttorneyMailRedirect(optedOut).ShouldBeNull();

            var byStaff = await _appointmentRepository.GetAsync(staff!.AppointmentId);
            AppointmentMailPreferences.BookerFollowsCase(byStaff).ShouldBeTrue("a staff submitter's choice is ignored");
        });
    }

    /// <summary>
    /// 2026-11-05 (review): what an Authorized User added at booking by a Patient or Claim Examiner booker
    /// gets. A Patient manages no Authorized Users, and a claim examiner only Claim Examiner helpers; any
    /// other row is refused INSIDE the atomic submit, which fails the WHOLE booking (the rollback itself is
    /// proved on real SQL Server in <c>BookingAtomicitySqlServerTests</c>). The wizard therefore offers a
    /// Patient booker no Authorized User section and a Claim Examiner booker the one role they may add.
    /// </summary>
    [Theory]
    [InlineData("Patient", "Claim Examiner", 51)]
    [InlineData("Patient", "Applicant Paralegal", 42)]
    [InlineData("Claim Examiner", "Patient", 53)]
    [InlineData("Claim Examiner", "Applicant Paralegal", 54)]
    public async Task SubmitAsync_AnAuthorizedUserTheBookerMayNotManage_FailsTheWholeBooking(
        string bookerRole, string accessorRole, int day)
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(day);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0)));
        var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 760);
        input.Accessors = new List<AppointmentAccessorCreateDto>
        {
            new() { Email = $"acc-{Guid.NewGuid():N}@example.test", Role = accessorRole },
        };

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), bookerRole, () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBeOneOf(
            CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
            CaseEvaluationDomainErrorCodes.AppointmentAccessorOtherSideNotAllowed);
    }

    /// <summary>A Patient booking with no Authorized User (the wizard offers them no section) still submits.</summary>
    [Fact]
    public async Task SubmitAsync_ByAPatient_WithNoAuthorizedUsers_Books()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(58);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Patient", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 770);
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
    }

    /// <summary>A Claim Examiner booker may submit a Claim Examiner helper (their assistant) with the booking.</summary>
    [Fact]
    public async Task SubmitAsync_ByAClaimExaminer_WithAClaimExaminerHelper_Books()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(59);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Claim Examiner", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 780);
            input.Accessors = new List<AppointmentAccessorCreateDto>
            {
                new() { Email = $"helper-{Guid.NewGuid():N}@example.test", Role = "Claim Examiner" },
            };
            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
    }

    /// <summary>The redirect is stored with its side and the lawyer's address as booked, and the booker stops following.</summary>
    [Fact]
    public async Task SubmitAsync_AnAttorneyWithASameSideParalegal_StoresTheRedirectWithTheSideAndTheLawyerAddress()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(46);
        AppointmentSubmitResultDto? result = null;
        string? lawyer = null;

        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0)));
        var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 720);
        input.Accessors = new List<AppointmentAccessorCreateDto>
        {
            new() { Email = $"para-{Guid.NewGuid():N}@example.test", Role = "Applicant Paralegal" },
        };
        input.SendAttorneyMailToParalegal = true;
        lawyer = input.ApplicantAttorneyEmail;

        // 2026-11-04: the redirect is the NAMED lawyer's choice, so the caller's verified login is the typed
        // applicant attorney (in another case).
        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), lawyer!.ToUpperInvariant(), "Applicant Attorney", async () =>
            result = await _appointments.SubmitAsync(input));

        await InOfficeAsync(office, async () =>
        {
            var stored = await _appointmentRepository.GetAsync(result!.AppointmentId);
            var redirect = AppointmentMailPreferences.GetAttorneyMailRedirect(stored).ShouldNotBeNull();
            redirect.Side.ShouldBe(HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.ChangeRequestSide.SideA);
            redirect.LawyerEmail.ShouldBe(lawyer);
            AppointmentMailPreferences.BookerFollowsCase(stored).ShouldBeFalse();
        });
    }

    /// <summary>
    /// A paralegal, a patient or a staff caller cannot ask for the redirect (400, nothing written), and an
    /// attorney who sends no same-side paralegal -- or only an OTHER-side one -- is refused the same way.
    /// </summary>
    [Theory]
    [InlineData("Applicant Paralegal", "Applicant Paralegal", false, 47, true)]
    [InlineData("Patient", "Applicant Paralegal", false, 48, true)]
    [InlineData("admin", "Applicant Paralegal", false, 49, true)]
    [InlineData("Applicant Attorney", null, true, 55, true)]
    [InlineData("Applicant Attorney", "Defense Paralegal", true, 56, true)]
    // 2026-11-04: an attorney login booking for ANOTHER lawyer cannot silence that lawyer.
    [InlineData("Applicant Attorney", "Applicant Paralegal", false, 57, false)]
    public async Task SubmitAsync_ARefusedRedirect_Throws400AndWritesNothing(
        string callerRole, string? accessorRole, bool needsParalegal, int day, bool callerIsTheTypedLawyer)
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(day);
        var slotId = Guid.Empty;
        var before = 0;
        await InOfficeAsync(office, async () =>
        {
            slotId = await InsertSlotAsync(office, date, new TimeOnly(13, 0), new TimeOnly(14, 0));
            before = await _appointmentRepository.CountAsync();
        });

        var input = BuildSubmitDto(office, slotId, date.AddHours(13).AddMinutes(15), dayOffset: 740);
        input.Accessors = accessorRole == null
            ? new List<AppointmentAccessorCreateDto>()
            : new List<AppointmentAccessorCreateDto>
            {
                new() { Email = $"para-{Guid.NewGuid():N}@example.test", Role = accessorRole },
            };
        input.SendAttorneyMailToParalegal = true;

        var login = callerIsTheTypedLawyer ? input.ApplicantAttorneyEmail! : "TEST-another-lawyer@example.test";
        var thrown = await Should.ThrowAsync<BusinessException>(
            () => InOfficeAsWithLoginAsync(office, Guid.NewGuid(), login, callerRole, () => _appointments.SubmitAsync(input)));

        thrown.Code.ShouldBe(needsParalegal
            ? CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectNeedsParalegal
            : CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectAttorneyOnly);
        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync()).ShouldBe(before, "a refused submit writes nothing"));
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>
    /// A new booking by an EXTERNAL booker still writes every child group. The child create endpoints
    /// now refuse a caller who is not a party to the target appointment, and booking reaches five of
    /// them inside its own unit of work, before the booker is linked to the appointment any other way.
    /// It passes because <c>SubmitAsync</c> flushes the appointment, stamping its <c>CreatorId</c>,
    /// before any child is written.
    ///
    /// <para><see cref="SubmitAsync_WithEveryChildGroup_PersistsAllOfThem"/> cannot catch a break
    /// here: it books as the office admin, whom the read gate admits as staff whatever the
    /// appointment. Only an external booker exercises the path that would fail.</para>
    /// </summary>
    [Fact]
    public async Task SubmitAsync_ByAnExternalBooker_WithEveryChildGroup_PersistsAllOfThem()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(50);
        var externalBooker = Guid.NewGuid();
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, externalBooker, "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: 500);
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
        await InOfficeAsync(office, async () =>
        {
            (await _employerDetails.CountAsync(x => x.AppointmentId == result!.AppointmentId)).ShouldBe(1);
            (await _primaryInsurances.CountAsync(x => x.AppointmentId == result!.AppointmentId)).ShouldBe(1);
            (await _claimExaminers.CountAsync(x => x.AppointmentId == result!.AppointmentId)).ShouldBe(1);
            (await _applicantAttorneys.CountAsync(x => x.AppointmentId == result!.AppointmentId)).ShouldBe(1);
            (await _defenseAttorneys.CountAsync(x => x.AppointmentId == result!.AppointmentId)).ShouldBe(1);

            var injuries = await _injuryDetails.GetListAsync(x => x.AppointmentId == result!.AppointmentId);
            injuries.Count.ShouldBe(2);
            var injuryIds = injuries.Select(i => i.Id).ToList();
            (await _bodyParts.CountAsync(x => injuryIds.Contains(x.AppointmentInjuryDetailId))).ShouldBe(3);
        });
    }

    /// <summary>
    /// Office decision 3 (2026-11-04), the same-side rule at booking: an applicant-side booker may submit an
    /// Authorized User of their own side. The refusal of an other-side one, with the whole booking rolled
    /// back, is proved on real SQL Server in <c>BookingAtomicitySqlServerTests</c>.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_WithAnApplicantParalegalAccessor_ByAnApplicantAttorney_Books()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(52);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(14, 0), new TimeOnly(15, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(14).AddMinutes(15), dayOffset: 530);
            input.Accessors = new List<AppointmentAccessorCreateDto>
            {
                new() { Email = $"ap-{Guid.NewGuid():N}@example.test", Role = "Applicant Paralegal" },
            };
            result = await _appointments.SubmitAsync(input);
        });

        result.ShouldNotBeNull();
        result!.Accessors.ShouldBe(1);
    }

    /// <summary>
    /// The appointment view page adds an employer detail to an EXISTING appointment that has none
    /// (<c>appointment-view.component.ts</c>, a <c>POST</c> to the employer-details route). That is the
    /// one create caller outside booking, and its caller can already read the appointment, so the party
    /// check must let them through.
    /// </summary>
    [Fact]
    public async Task EmployerDetailCreate_OnTheirOwnAppointment_ByItsExternalBooker_Succeeds()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var booker = Guid.NewGuid();
        var appointmentId = await BookWithoutEmployerAsync(office, booker, dayOffset: 510);

        await InOfficeAsAsync(office, booker, "Applicant Attorney", () =>
            _employerDetailsService.CreateAsync(NewEmployerDetail(appointmentId)));

        await InOfficeAsync(office, async () =>
            (await _employerDetails.CountAsync(x => x.AppointmentId == appointmentId)).ShouldBe(1));
    }

    /// <summary>
    /// The create half of the child-record ownership rule, through the REAL read gate: another external
    /// user in the same office, holding the same Create permission, cannot attach a record to an
    /// appointment they are not a party to, and nothing is written.
    /// </summary>
    [Fact]
    public async Task EmployerDetailCreate_OnSomeoneElsesAppointment_IsRefusedAndWritesNothing()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var appointmentId = await BookWithoutEmployerAsync(office, Guid.NewGuid(), dayOffset: 520);
        var stranger = Guid.NewGuid();

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, stranger, "Applicant Attorney", () =>
                _employerDetailsService.CreateAsync(NewEmployerDetail(appointmentId))));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        await InOfficeAsync(office, async () =>
            (await _employerDetails.CountAsync(x => x.AppointmentId == appointmentId)).ShouldBe(0));
    }

    /// <summary>A booking by <paramref name="booker"/> with no employer detail, for the create tests.</summary>
    private async Task<Guid> BookWithoutEmployerAsync(SeededOffice office, Guid booker, int dayOffset)
    {
        var date = TestToday.AddDays(dayOffset / 10);
        Guid appointmentId = default;
        await InOfficeAsAsync(office, booker, "Applicant Attorney", async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(9, 0), new TimeOnly(10, 0));
            var input = BuildSubmitDto(office, slotId, date.AddHours(9).AddMinutes(15), dayOffset: dayOffset);
            input.Accessors = new List<AppointmentAccessorCreateDto>();
            input.EmployerDetail = null;
            appointmentId = (await _appointments.SubmitAsync(input)).AppointmentId;
        });
        return appointmentId;
    }

    private static AppointmentEmployerDetailCreateDto NewEmployerDetail(Guid appointmentId) => new()
    {
        AppointmentId = appointmentId,
        EmployerName = "TEST-Employer",
        Occupation = "TEST-Occupation",
    };

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

    /// <summary>
    /// <see cref="InOfficeAsync"/> as a chosen caller rather than the office's admin booker. #598's
    /// tests need an EXTERNAL booker who is not the patient; the admin identity would be admitted as
    /// staff and prove nothing about the external path.
    /// </summary>
    /// <summary>As <see cref="InOfficeAsAsync"/>, with a VERIFIED login email (the redirect rule reads it).</summary>
    private Task InOfficeAsWithLoginAsync(SeededOffice office, Guid userId, string login, string role, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, login, role))
            {
                await body();
            }
        }, requiresNew: true);

    private Task InOfficeAsAsync(SeededOffice office, Guid userId, string role, Func<Task> body) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, userId, role))
            {
                await body();
            }
        }, requiresNew: true);

    /// <summary>
    /// Makes <paramref name="booker"/> a PARTY to <paramref name="patientId"/> the way production does:
    /// a prior appointment they booked. Booking against a supplied patient id now requires this (or
    /// being staff, or the patient), so a test that books "for an existing patient" as an external
    /// attorney must first stand in the relationship that makes the booking legitimate. Inserted with
    /// no current user so the creator stamp stays empty and the booker field decides, which is the
    /// shape of the party rule.
    /// </summary>
    private Task MakePartyToPatientAsync(SeededOffice office, Guid patientId, Guid booker, DateTime date, int hour) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                var slotId = await InsertSlotAsync(office, date, new TimeOnly(hour, 0), new TimeOnly(hour + 1, 0));
                var prior = new Appointment(
                    id: Guid.NewGuid(),
                    patientId: patientId,
                    identityUserId: null,
                    appointmentTypeId: office.AppointmentTypeId,
                    locationId: office.LocationId,
                    doctorAvailabilityId: slotId,
                    appointmentDate: date.AddHours(hour),
                    requestConfirmationNumber: "PRIOR-" + Guid.NewGuid().ToString("N")[..8],
                    appointmentStatus: AppointmentStatusType.Pending);
                prior.RecordBookedBy(booker);
                await _appointmentRepository.InsertAsync(prior, autoSave: true);
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
            ApplicantAttorneyEmail = $"atty-a-{suffix}@example.test",
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
                Email = $"atty-a-{suffix}@example.test",
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
                new() { Email = $"acc1-{suffix}@example.test", Role = "Claim Examiner" },
                new() { Email = $"acc2-{suffix}@example.test", Role = "Patient" },
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
