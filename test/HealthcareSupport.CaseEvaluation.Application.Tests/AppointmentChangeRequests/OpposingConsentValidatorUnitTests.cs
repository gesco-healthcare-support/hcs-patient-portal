using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// The consent gate in front of finalizing a change request: every side whose consent was
/// required must have said yes.
/// </summary>
/// <remarks>
/// The gate had no test, so a regression that let a refused or expired side through -- a
/// cancellation finalized over the other party's "no" -- would have gone unnoticed. The
/// refusing cases below each seed the side that must block.
/// </remarks>
public class OpposingConsentValidatorUnitTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    private static AppointmentChangeRequest Request() =>
        new(Guid.NewGuid(), tenantId: null, appointmentId: Guid.NewGuid(), ChangeRequestType.Cancel,
            cancellationReason: "Synthetic reason", reScheduleReason: null, newDoctorAvailabilityId: null);

    /// <summary>A request where the given side was asked and answered.</summary>
    private static AppointmentChangeRequest Answered(ChangeRequestSide side, bool approved)
    {
        var request = Request();
        request.IssueSideConsent(side, "synthetic-token-hash", Now.AddDays(3));
        request.RecordSideDecision(side, approved, "party@example.test", Now);
        return request;
    }

    [Fact]
    public void Passes_when_no_side_was_asked()
    {
        Should.NotThrow(() => OpposingConsentValidator.EnsureConsentGranted(Request(), consentGatingEnabled: true));
    }

    [Fact]
    public void Passes_when_every_asked_side_said_yes()
    {
        var request = Answered(ChangeRequestSide.SideA, approved: true);
        Should.NotThrow(() => OpposingConsentValidator.EnsureConsentGranted(request, consentGatingEnabled: true));
    }

    [Fact]
    public void Blocks_when_an_asked_side_said_no_and_names_both_sides()
    {
        var request = Answered(ChangeRequestSide.SideB, approved: false);

        var ex = Should.Throw<BusinessException>(() =>
            OpposingConsentValidator.EnsureConsentGranted(request, consentGatingEnabled: true));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted);
        ex.Data["sideAConsent"].ShouldBe(ChangeRequestConsentStatus.NotRequired);
        ex.Data["sideBConsent"].ShouldBe(ChangeRequestConsentStatus.Rejected);
    }

    [Fact]
    public void Blocks_while_an_asked_side_has_not_answered()
    {
        var request = Request();
        request.IssueSideConsent(ChangeRequestSide.SideA, "synthetic-token-hash", Now.AddDays(3));

        Should.Throw<BusinessException>(() =>
            OpposingConsentValidator.EnsureConsentGranted(request, consentGatingEnabled: true));
    }

    [Fact]
    public void Lets_everything_through_when_gating_is_switched_off()
    {
        var request = Answered(ChangeRequestSide.SideA, approved: false);
        Should.NotThrow(() => OpposingConsentValidator.EnsureConsentGranted(request, consentGatingEnabled: false));
    }

    [Fact]
    public void Refuses_a_missing_request()
    {
        Should.Throw<ArgumentNullException>(() =>
            OpposingConsentValidator.EnsureConsentGranted(null!, consentGatingEnabled: true));
    }
}
