using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Identity;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The booker's mail choices at submit (office decisions 1 and 2, 2026-11-04): what is accepted, what is
/// refused with which code, and what is stored. Pure: no database. The end-to-end proof that a refusal
/// writes nothing is in the multi-office submit tests.
/// </summary>
public class AppointmentMailChoicesTests
{
    private const string Lawyer = "TEST-choice-lawyer@test.local";

    private static AppointmentSubmitDto Submit(
        bool follows = true,
        bool redirect = false,
        params string[] accessorRoles)
    {
        return new AppointmentSubmitDto
        {
            BookerFollowsCase = follows,
            SendAttorneyMailToParalegal = redirect,
            ApplicantAttorneyEmail = Lawyer,
            Accessors = accessorRoles
                .Select(r => new AppointmentAccessorCreateDto
                {
                    Email = $"TEST-{Guid.NewGuid():N}@test.local",
                    Role = r,
                })
                .ToList(),
        };
    }

    [Fact]
    public void ByDefault_TheBookerFollows_AndNothingIsRedirected()
    {
        var choices = AppointmentMailChoices.Validate(Submit(), new[] { ExternalRoleConsts.ApplicantAttorney });

        choices.BookerFollowsCase.ShouldBeTrue();
        choices.RedirectSide.ShouldBeNull();
    }

    [Fact]
    public void AnExternalBooker_MayOptOut()
    {
        var choices = AppointmentMailChoices.Validate(Submit(follows: false), new[] { ExternalRoleConsts.Patient });

        choices.BookerFollowsCase.ShouldBeFalse();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Staff Supervisor")]
    [InlineData("Intake Staff")]
    public void ForStaff_TheOptOutIsIgnored(string staffRole)
    {
        AppointmentMailChoices.Validate(Submit(follows: false), new[] { staffRole })
            .BookerFollowsCase.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ExternalRoleConsts.ApplicantAttorney, ChangeRequestSide.SideA, ExternalRoleConsts.ApplicantParalegal)]
    [InlineData(ExternalRoleConsts.DefenseAttorney, ChangeRequestSide.SideB, ExternalRoleConsts.DefenseParalegal)]
    public void AnAttorneyWithASameSideParalegal_MayRedirect_AndTheBookerStopsFollowing(
        string callerRole, ChangeRequestSide side, string paralegalRole)
    {
        var choices = AppointmentMailChoices.Validate(
            Submit(redirect: true, accessorRoles: new[] { ExternalRoleConsts.ClaimExaminer, paralegalRole }),
            new[] { callerRole });

        choices.RedirectSide.ShouldBe(side);
        choices.BookerFollowsCase.ShouldBeFalse("the redirect implies the booker does not follow");
    }

    [Theory]
    [InlineData(ExternalRoleConsts.ApplicantParalegal)]
    [InlineData(ExternalRoleConsts.DefenseParalegal)]
    [InlineData(ExternalRoleConsts.Patient)]
    [InlineData(ExternalRoleConsts.ClaimExaminer)]
    [InlineData("admin")]
    [InlineData("Staff Supervisor")]
    public void ARedirectFromAnythingButAnAttorney_IsRefused(string callerRole)
    {
        var thrown = Should.Throw<BusinessException>(() => AppointmentMailChoices.Validate(
            Submit(redirect: true, accessorRoles: ExternalRoleConsts.ApplicantParalegal),
            new[] { callerRole }));

        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectAttorneyOnly);
    }

    [Fact]
    public void ARedirectWithoutAParalegalOnTheSameSubmit_IsRefused()
    {
        var thrown = Should.Throw<BusinessException>(() => AppointmentMailChoices.Validate(
            Submit(redirect: true, accessorRoles: ExternalRoleConsts.ClaimExaminer),
            new[] { ExternalRoleConsts.ApplicantAttorney }));

        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectNeedsParalegal);
    }

    [Fact]
    public void AParalegalOfTheOtherSide_DoesNotCount()
    {
        var thrown = Should.Throw<BusinessException>(() => AppointmentMailChoices.Validate(
            Submit(redirect: true, accessorRoles: ExternalRoleConsts.DefenseParalegal),
            new[] { ExternalRoleConsts.ApplicantAttorney }));

        thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAttorneyMailRedirectNeedsParalegal);
    }

    [Fact]
    public void NoRedirectIsRequestedByDefault_SoNoRefusalCanFire()
    {
        // A paralegal booking without asking for a redirect is untouched (positive control for the refusals).
        Should.NotThrow(() => AppointmentMailChoices.Validate(
            Submit(), new[] { ExternalRoleConsts.ApplicantParalegal }));
    }

    [Fact]
    public void Apply_StoresTheOptOut_AndTheRedirectWithTheSideAndTheBookedLawyerAddress()
    {
        var appointment = NewAppointment(applicantAttorneyEmail: Lawyer);

        AppointmentMailChoices.Apply(
            appointment,
            new AppointmentMailChoices.SubmitChoices(false, ChangeRequestSide.SideA),
            callerVerifiedEmail: "TEST-login@test.local");

        AppointmentMailPreferences.BookerFollowsCase(appointment).ShouldBeFalse();
        var redirect = AppointmentMailPreferences.GetAttorneyMailRedirect(appointment).ShouldNotBeNull();
        redirect.Side.ShouldBe(ChangeRequestSide.SideA);
        redirect.LawyerEmail.ShouldBe(Lawyer);
    }

    [Fact]
    public void Apply_FallsBackToTheCallersVerifiedLoginEmail_WhenTheAttorneyColumnIsBlank()
    {
        var appointment = NewAppointment(applicantAttorneyEmail: null);

        AppointmentMailChoices.Apply(
            appointment,
            new AppointmentMailChoices.SubmitChoices(false, ChangeRequestSide.SideA),
            callerVerifiedEmail: "TEST-login@test.local");

        AppointmentMailPreferences.GetAttorneyMailRedirect(appointment)!.LawyerEmail.ShouldBe("TEST-login@test.local");
    }

    [Fact]
    public void Apply_StoresNothing_ForTheDefaults()
    {
        var appointment = NewAppointment(applicantAttorneyEmail: Lawyer);

        AppointmentMailChoices.Apply(appointment, new AppointmentMailChoices.SubmitChoices(true, null), null);

        AppointmentMailPreferences.BookerFollowsCase(appointment).ShouldBeTrue();
        AppointmentMailPreferences.GetAttorneyMailRedirect(appointment).ShouldBeNull();
        appointment.ExtraProperties.ShouldBeEmpty();
    }

    [Fact]
    public void ApplyTo_ShowsTheStoredChoicesOnTheDetailDto()
    {
        var appointment = NewAppointment(applicantAttorneyEmail: Lawyer);
        var dto = new AppointmentDto();
        AppointmentMailChoices.ApplyTo(appointment, dto);
        dto.BookerFollowsCase.ShouldBeTrue();
        dto.AttorneyMailToParalegal.ShouldBeFalse();

        AppointmentMailChoices.Apply(
            appointment, new AppointmentMailChoices.SubmitChoices(false, ChangeRequestSide.SideA), null);
        AppointmentMailChoices.ApplyTo(appointment, dto);

        dto.BookerFollowsCase.ShouldBeFalse();
        dto.AttorneyMailToParalegal.ShouldBeTrue();
    }

    private static Appointment NewAppointment(string? applicantAttorneyEmail) =>
        new(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 12, 1, 9, 0, 0),
            requestConfirmationNumber: "TEST-A0001",
            appointmentStatus: AppointmentStatusType.Pending)
        {
            ApplicantAttorneyEmail = applicantAttorneyEmail,
        };
}
