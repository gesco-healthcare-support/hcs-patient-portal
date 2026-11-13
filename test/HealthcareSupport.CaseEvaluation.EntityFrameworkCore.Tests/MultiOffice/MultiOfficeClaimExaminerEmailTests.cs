using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// 2026-11-13 -- the claim examiner EMAIL is optional only for a law-office booker (attorney or paralegal)
/// who also gave the OTHER side's attorney a real email, and placeholder emails (unknown@..., na@...) are
/// refused up front. A partial of <see cref="MultiOfficeAtomicBookingSubmitTests"/> for its seeding.
/// Slots are inserted at 23:10-23:40, an hour no other MultiOffice test uses, each on its own day.
/// </summary>
public partial class MultiOfficeAtomicBookingSubmitTests
{
    private async Task<(SeededOffice Office, AppointmentSubmitDto Input, Guid SlotId)> BuildCeEmailBookingAsync(
        int day, int dayOffset)
    {
        var (office, _) = await GetSeededOfficesAsync();
        await SeedNotificationTemplatesAsync(office);
        var date = TestToday.AddDays(day);
        var slotId = Guid.Empty;
        await InOfficeAsync(office, async () =>
            slotId = await InsertSlotAsync(office, date, new TimeOnly(23, 10), new TimeOnly(23, 40)));
        var input = BuildSubmitDto(office, slotId, date.AddHours(23).AddMinutes(15), dayOffset: dayOffset);
        input.Accessors = new List<AppointmentAccessorCreateDto>();
        return (office, input, slotId);
    }

    private static void LeaveClaimExaminerEmailBlank(AppointmentSubmitDto input)
    {
        input.ClaimExaminerEmail = null;
        input.ClaimExaminer!.Email = null;
    }

    [Theory]
    [InlineData("Applicant Attorney", 20, 1201)]
    [InlineData("Applicant Paralegal", 21, 1202)]
    [InlineData("Defense Attorney", 22, 1203)]
    [InlineData("Defense Paralegal", 23, 1204)]
    public async Task ALawOfficeBooker_WhoGaveTheOtherSidesAttorneyEmail_MayLeaveTheClaimExaminerEmailBlank(
        string role, int day, int dayOffset)
    {
        var (office, input, _) = await BuildCeEmailBookingAsync(day, dayOffset);
        LeaveClaimExaminerEmailBlank(input);
        AppointmentSubmitResultDto? result = null;

        await InOfficeAsAsync(office, Guid.NewGuid(), role, async () => result = await _appointments.SubmitAsync(input));

        result!.ClaimExaminers.ShouldBe(1, "the examiner row still exists: approval needs one active row");
        await InOfficeAsync(office, async () =>
        {
            (await _appointmentRepository.GetAsync(result.AppointmentId)).ClaimExaminerEmail.ShouldBeNull();
            (await _claimExaminers.GetListAsync(x => x.AppointmentId == result.AppointmentId))
                .ShouldHaveSingleItem().Email.ShouldBeNull();
        });
    }

    /// <summary>The same blank is refused (400, nothing written) when the other side's attorney email is missing.</summary>
    [Theory]
    [InlineData("Applicant Attorney", 24, 1205, true)]
    [InlineData("Defense Paralegal", 25, 1206, false)]
    public async Task ALawOfficeBooker_WithNoOtherSideAttorneyEmail_MustGiveTheClaimExaminerEmail(
        string role, int day, int dayOffset, bool applicantSide)
    {
        var (office, input, slotId) = await BuildCeEmailBookingAsync(day, dayOffset);
        LeaveClaimExaminerEmailBlank(input);
        if (applicantSide)
        {
            input.DefenseAttorneyEmail = null;
            input.DefenseAttorney = null;
        }
        else
        {
            input.ApplicantAttorneyEmail = null;
            input.ApplicantAttorney = null;
        }

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), role, () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentClaimExaminerEmailRequired);
        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId)).ShouldBe(0));
    }

    /// <summary>A placeholder as the other side's email is refused outright, so it can never count as "given".</summary>
    [Fact]
    public async Task APlaceholderOtherSideEmail_IsRefused()
    {
        var (office, input, _) = await BuildCeEmailBookingAsync(26, 1207);
        LeaveClaimExaminerEmailBlank(input);
        input.DefenseAttorneyEmail = "unknown@firm.test";
        input.DefenseAttorney!.Email = "unknown@firm.test";

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPlaceholderEmail);
    }

    /// <summary>Staff, patient and claim examiner bookers keep the claim examiner email required.</summary>
    [Theory]
    [InlineData("Patient", 27, 1208)]
    [InlineData("Claim Examiner", 28, 1209)]
    [InlineData("admin", 29, 1210)]
    public async Task OtherBookers_MustStillGiveTheClaimExaminerEmail(string role, int day, int dayOffset)
    {
        var (office, input, slotId) = await BuildCeEmailBookingAsync(day, dayOffset);
        LeaveClaimExaminerEmailBlank(input);

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), role, () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentClaimExaminerEmailRequired);
        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId)).ShouldBe(0));
    }

    /// <summary>A placeholder on any party email of a booking is refused before anything is written.</summary>
    [Theory]
    [InlineData("claim", 30, 1211)]
    [InlineData("claimRow", 31, 1212)]
    [InlineData("patient", 32, 1213)]
    [InlineData("applicant", 33, 1214)]
    [InlineData("defenseRow", 34, 1215)]
    [InlineData("accessor", 35, 1216)]
    public async Task APlaceholderEmail_OnAnyParty_RefusesTheBookingBeforeAnythingIsWritten(
        string where, int day, int dayOffset)
    {
        var (office, input, slotId) = await BuildCeEmailBookingAsync(day, dayOffset);
        switch (where)
        {
            case "claim": input.ClaimExaminerEmail = "unknown@gmail.com"; break;
            case "claimRow": input.ClaimExaminer!.Email = "na@gmail.com"; break;
            case "patient": input.PatientEmail = "test@example.com"; break;
            case "applicant": input.ApplicantAttorneyEmail = "xx@firm.test"; break;
            case "defenseRow": input.DefenseAttorney!.Email = "none@firm.test"; break;
            default:
                input.Accessors = new List<AppointmentAccessorCreateDto>
                {
                    new() { Email = "unknown+helper@gmail.com", Role = "Applicant Paralegal" },
                };
                break;
        }

        var refused = await Should.ThrowAsync<BusinessException>(() =>
            InOfficeAsAsync(office, Guid.NewGuid(), "Applicant Attorney", () => _appointments.SubmitAsync(input)));

        refused.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPlaceholderEmail);
        await InOfficeAsync(office, async () =>
            (await _appointmentRepository.CountAsync(x => x.DoctorAvailabilityId == slotId)).ShouldBe(0));
    }
}
