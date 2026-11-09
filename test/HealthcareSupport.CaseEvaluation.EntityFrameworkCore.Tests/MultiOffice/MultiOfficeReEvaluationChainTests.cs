using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.PhiAccess;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// W2 (2026-11-08) -- a re-evaluation follows on from the NEWEST case in the chain the typed number
/// belongs to, and "party" means a party to that newest case: the R2c lookup, the booking submit and
/// the forgotten-number email all walk the chain (<see cref="ReEvaluationChain"/>).
///
/// <para>A partial of <see cref="MultiOfficeAtomicBookingSubmitTests"/> for its seeding. Later cases
/// in a chain are inserted at 21:00-22:00 and re-evaluations booked at 22:00-23:00, hours no other
/// MultiOffice test uses, each on its own day.</para>
/// </summary>
public partial class MultiOfficeAtomicBookingSubmitTests
{
    /// <summary>A later case in a chain, for the same patient, inserted directly at 21:00 on <paramref name="day"/>.</summary>
    private async Task<(Guid Id, string Number)> InsertChainCaseAsync(
        SeededOffice office,
        Guid patientId,
        int day,
        Guid? reEvaluationOf = null,
        Guid? replaces = null,
        AppointmentStatusType status = AppointmentStatusType.Approved,
        string? claim = null)
    {
        var id = Guid.NewGuid();
        var number = "W2-" + Guid.NewGuid().ToString("N")[..8];
        await InOfficeAsync(office, async () =>
        {
            var date = TestToday.AddDays(day);
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(21, 0), new TimeOnly(22, 0));
            var appointment = new Appointment(
                id: id,
                patientId: patientId,
                identityUserId: null,
                appointmentTypeId: office.AppointmentTypeId,
                locationId: office.LocationId,
                doctorAvailabilityId: slotId,
                appointmentDate: date.AddHours(21),
                requestConfirmationNumber: number,
                appointmentStatus: status)
            {
                OriginalAppointmentId = reEvaluationOf,
                RescheduledFromAppointmentId = replaces,
                EvaluationKind = reEvaluationOf != null ? EvaluationKind.ReEvaluation : EvaluationKind.Evaluation,
            };
            await _appointmentRepository.InsertAsync(appointment, autoSave: true);
            if (claim != null)
            {
                await _injuryDetails.InsertAsync(new AppointmentInjuryDetail(
                    Guid.NewGuid(), id, new DateTime(2025, 2, 3), claim, false, "Lower back",
                    wcabAdj: "ADJ0000002"), autoSave: true);
            }
        });
        return (id, number);
    }

    /// <summary>
    /// An approved first exam for <paramref name="patientId"/> that <paramref name="booker"/> booked, inserted
    /// with no current user so the creator stamp stays empty and the booker decides who is a party (the
    /// shape <c>MakePartyToPatientAsync</c> uses). At 21:00 on <paramref name="day"/>.
    /// </summary>
    private async Task<(Guid Id, string Number)> InsertCaseBookedByAsync(SeededOffice office, Guid patientId, int day, Guid booker)
    {
        var id = Guid.NewGuid();
        var number = "W2B-" + Guid.NewGuid().ToString("N")[..8];
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                var date = TestToday.AddDays(day);
                var slotId = await InsertSlotAsync(office, date, new TimeOnly(21, 0), new TimeOnly(22, 0));
                var appointment = new Appointment(
                    id: id,
                    patientId: patientId,
                    identityUserId: null,
                    appointmentTypeId: office.AppointmentTypeId,
                    locationId: office.LocationId,
                    doctorAvailabilityId: slotId,
                    appointmentDate: date.AddHours(21),
                    requestConfirmationNumber: number,
                    appointmentStatus: AppointmentStatusType.Approved);
                appointment.RecordBookedBy(booker);
                await _appointmentRepository.InsertAsync(appointment, autoSave: true);
            }
        }, requiresNew: true);
        return (id, number);
    }

    private static ReEvaluationSourceLookupDto LookupWithoutDate(string number) => new() { ConfirmationNumber = number };

    private AppointmentSubmitDto RevalSubmit(
        SeededOffice office, Guid slotId, DateTime date, int dayOffset, string number, Guid patientId, DateTime? dateOfBirth)
    {
        var input = BuildSubmitDto(office, slotId, date.AddHours(22).AddMinutes(15), dayOffset);
        input.Mode = BookingSubmitMode.Reval;
        input.SourceConfirmationNumber = number;
        input.SourcePatientDateOfBirth = dateOfBirth;
        input.Patient = null;
        input.PatientId = patientId;
        input.Accessors = new List<AppointmentAccessorCreateDto>();
        return input;
    }

    [Fact]
    public async Task Chain_Lookup_ByStaffTypingTheFirstExam_FillsFromTheApprovedReEvaluation()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var first = await CreateReEvaluationSourceAsync(office, day: 82, dayOffset: 1100);
        var reEval = await InsertChainCaseAsync(office, first.PatientId, day: 101, reEvaluationOf: first.Id);
        ReEvaluationSourceDto? found = null;

        await InOfficeAsync(office, async () =>
            found = await _reEvaluationSources.LookupAsync(LookupWithoutDate(first.Number)));

        found.ShouldNotBeNull();
        found!.Source.Appointment.Id.ShouldBe(reEval.Id, "the newest case in the chain, not the typed one");
        found.FilledFromNewer.ShouldBeTrue();
        found.RequestedConfirmationNumber.ShouldBe(first.Number);
        found.CallerIsParty.ShouldBeTrue("staff can read every case in the office");
        found.CanBeReEvaluated.ShouldBeTrue();
    }

    [Fact]
    public async Task Chain_Lookup_AFormerPartyOfTheFirstExamOnly_NeedsTheDateOfBirth_AndGetsTheNonPartyView()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var patient = await CreateReEvaluationSourceAsync(office, day: 83, dayOffset: 1110);
        var formerParty = Guid.NewGuid();
        var booked = await InsertCaseBookedByAsync(office, patient.PatientId, day: 109, formerParty);
        var first = (booked.Id, booked.Number, patient.PatientId, patient.DateOfBirth);

        // Control: with no later case, the Authorized User row makes them a party, no date needed.
        ReEvaluationSourceDto? beforeTheReEvaluation = null;
        await InOfficeAsAsync(office, formerParty, "Applicant Attorney", async () =>
            beforeTheReEvaluation = await _reEvaluationSources.LookupAsync(LookupWithoutDate(first.Number)));
        beforeTheReEvaluation.ShouldNotBeNull();
        beforeTheReEvaluation!.CallerIsParty.ShouldBeTrue();
        beforeTheReEvaluation.FilledFromNewer.ShouldBeFalse();

        var reEval = await InsertChainCaseAsync(office, first.PatientId, day: 110, reEvaluationOf: first.Id);
        ReEvaluationSourceDto? withoutDate = new();
        ReEvaluationSourceDto? withDate = null;

        await InOfficeAsAsync(office, formerParty, "Applicant Attorney", async () =>
        {
            withoutDate = await _reEvaluationSources.LookupAsync(LookupWithoutDate(first.Number));
            withDate = await _reEvaluationSources.LookupAsync(Lookup(first.Number, first.DateOfBirth));
        });

        withoutDate.ShouldBeNull("not a party to the newest case: the office's one rule asks for the date of birth");
        withDate.ShouldNotBeNull();
        withDate!.Source.Appointment.Id.ShouldBe(reEval.Id);
        withDate.CallerIsParty.ShouldBeFalse();
        withDate.FilledFromNewer.ShouldBeTrue();
        withDate.Source.AppointmentAccessors.ShouldBeEmpty("the non-party view");
        withDate.Source.IdentityUser.ShouldBeNull();

        var rows = await PhiRowsForAsync(office, formerParty);
        rows.Count.ShouldBe(3);
        rows.Single(r => r.Action == PhiAccessAction.AppointmentViewed).AppointmentId.ShouldBe(first.Id);
        rows.Single(r => r.Action == PhiAccessAction.ReEvaluationSourceLookupRefused)
            .AppointmentId.ShouldBeNull("a refusal names nobody");
        rows.Single(r => r.Action == PhiAccessAction.ReEvaluationSourceLoadedByDateOfBirth)
            .AppointmentId.ShouldBe(reEval.Id, "the case actually opened is logged");
    }

    [Fact]
    public async Task Chain_Lookup_AStrangerWithTheDateOfBirth_IsTakenThroughARescheduleToTheLiveCase()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var first = await CreateReEvaluationSourceAsync(office, day: 84, dayOffset: 1120);
        var reEval = await InsertChainCaseAsync(
            office, first.PatientId, day: 103, reEvaluationOf: first.Id, status: AppointmentStatusType.RescheduledNoBill);
        var replacement = await InsertChainCaseAsync(
            office, first.PatientId, day: 104, reEvaluationOf: first.Id, replaces: reEval.Id);
        ReEvaluationSourceDto? found = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Paralegal", async () =>
            found = await _reEvaluationSources.LookupAsync(Lookup(first.Number, first.DateOfBirth)));

        found.ShouldNotBeNull();
        found!.Source.Appointment.Id.ShouldBe(replacement.Id);
        found.CallerIsParty.ShouldBeFalse();
    }

    [Fact]
    public async Task Chain_Lookup_APartyGetsItsOwnCaseBackWhenNothingInTheChainCanBeReEvaluated()
    {
        // Every re-evaluation load goes through the lookup now, so a party's not-yet-approved case comes
        // back with its status for the form's specific refusal, instead of a not-found.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var pending = await CreateReEvaluationSourceAsync(
            office, day: 85, dayOffset: 1130, status: AppointmentStatusType.Pending);
        ReEvaluationSourceDto? found = null;

        await InOfficeAsync(office, async () =>
            found = await _reEvaluationSources.LookupAsync(LookupWithoutDate(pending.Number)));

        found.ShouldNotBeNull();
        found!.Source.Appointment.Id.ShouldBe(pending.Id);
        found.Source.Appointment.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
        found.FilledFromNewer.ShouldBeFalse();
        found.CanBeReEvaluated.ShouldBeFalse("the form refuses it; the submit refuses it again");
    }

    [Fact]
    public async Task Chain_Submit_ByStaffTypingTheFirstExam_LinksTheNewestCase()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var first = await CreateReEvaluationSourceAsync(office, day: 86, dayOffset: 1140);
        var reEval = await InsertChainCaseAsync(office, first.PatientId, day: 105, reEvaluationOf: first.Id);
        var date = TestToday.AddDays(50);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(22, 0), new TimeOnly(23, 0));
            result = await _appointments.SubmitAsync(
                RevalSubmit(office, slotId, date, 1141, first.Number, first.PatientId, dateOfBirth: null));
        });

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.OriginalAppointmentId.ShouldBe(reEval.Id, "the Case Tracker's previous appointment is the newest case");
            created.EvaluationKind.ShouldBe(EvaluationKind.ReEvaluation);
        });
    }

    [Fact]
    public async Task Chain_Submit_AFormerPartyWithoutTheDateOfBirth_IsRefused_AndWithItLinksTheNewestCase()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var patient = await CreateReEvaluationSourceAsync(office, day: 87, dayOffset: 1150);
        var formerParty = Guid.NewGuid();
        var booked = await InsertCaseBookedByAsync(office, patient.PatientId, day: 111, formerParty);
        var first = (booked.Id, booked.Number, patient.PatientId, patient.DateOfBirth);
        var reEval = await InsertChainCaseAsync(office, first.PatientId, day: 112, reEvaluationOf: first.Id);
        var date = TestToday.AddDays(51);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(22, 0), new TimeOnly(23, 0)));

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, formerParty, "Applicant Attorney", () => _appointments.SubmitAsync(
                RevalSubmit(office, slotId, date, 1151, first.Number, first.PatientId, dateOfBirth: null))));
        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentSubmitFailed);
        refused.InnerException.ShouldBeOfType<EntityNotFoundException>().Id.ShouldBe(first.Number);

        AppointmentSubmitResultDto? result = null;
        await InOfficeAsAsync(office, formerParty, "Applicant Attorney", async () =>
            result = await _appointments.SubmitAsync(
                RevalSubmit(office, slotId, date, 1152, first.Number, first.PatientId, first.DateOfBirth)));

        await InOfficeAsync(office, async () =>
        {
            var created = await _appointmentRepository.GetAsync(result!.AppointmentId);
            created.OriginalAppointmentId.ShouldBe(reEval.Id);
            created.PatientId.ShouldBe(first.PatientId);
        });
    }

    [Fact]
    public async Task Chain_CreateRevalEndpoint_LinksTheNewestCaseToo()
    {
        // The booking form does not use this route, but it is live, so it must not link an older case.
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var first = await CreateReEvaluationSourceAsync(office, day: 88, dayOffset: 1160);
        var reEval = await InsertChainCaseAsync(office, first.PatientId, day: 108, reEvaluationOf: first.Id);
        var date = TestToday.AddDays(52);
        AppointmentDto? created = null;

        await InOfficeAsync(office, async () =>
        {
            var slotId = await InsertSlotAsync(office, date, new TimeOnly(22, 0), new TimeOnly(23, 0));
            created = await _appointments.CreateRevalAsync(first.Number, new AppointmentCreateDto
            {
                PatientId = first.PatientId,
                IdentityUserId = office.BookerUserId,
                AppointmentTypeId = office.AppointmentTypeId,
                LocationId = office.LocationId,
                DoctorAvailabilityId = slotId,
                AppointmentDate = date.AddHours(22).AddMinutes(15),
                RequestConfirmationNumber = "ignored-by-server",
                AppointmentStatus = AppointmentStatusType.Pending,
            });
        });

        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.GetAsync(created!.Id)).OriginalAppointmentId.ShouldBe(reEval.Id));
    }

    [Fact]
    public async Task Chain_NumberLookup_AClaimOnTheFirstExamOnly_EmailsTheNewestCasesNumber()
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        Patient? patient = null;
        await InOfficeAsync(office, async () => patient = await InsertPatientAsync(office, null, suffix));
        var claim = $"W2-CLM-{suffix}";
        var first = await SeedApprovedCaseAsync(office, patient!.Id, day: 91, claim);
        // The re-evaluation carries a different claim number: the chain is found through the first exam.
        var reEval = await InsertChainCaseAsync(
            office, patient.Id, day: 107, reEvaluationOf: first, claim: $"W2-NEW-{suffix}");
        const string login = "w2-asker@example.test";

        await InOfficeAsWithLoginAsync(office, Guid.NewGuid(), login, "Applicant Paralegal", () => Lookups.RequestAsync(
            new ConfirmationNumberLookupDto
            {
                PatientFirstName = patient.FirstName,
                PatientLastName = patient.LastName,
                PatientDateOfBirth = patient.DateOfBirth.Date,
                ClaimNumber = claim,
            }));

        (await LookupMailAsync(office, reEval.Id)).ShouldHaveSingleItem().To.ShouldBe(login);
        (await LookupMailAsync(office, first)).ShouldBeEmpty("the newest case's number is sent, not the first exam's");
    }
}
