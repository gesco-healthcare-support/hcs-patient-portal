using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- pure unit tests for the opposing-side consent
/// transitions on <see cref="AppointmentChangeRequest"/>. No ABP harness (same
/// style as <c>ChangeRequestApprovalValidatorUnitTests</c>): the consent logic
/// lives as plain methods on the aggregate so it is testable without a DB.
/// </summary>
public class ChangeRequestConsentUnitTests
{
    private static readonly DateTime Now = new(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IssueConsent_FromFresh_SetsPending()
    {
        var r = NewRequest();
        var submitter = Guid.NewGuid();

        r.IssueConsent("hash123", ChangeRequestSide.SideA, submitter, Now.AddDays(7));

        r.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Pending);
        r.ConsentTokenHash.ShouldBe("hash123");
        r.RequestingSide.ShouldBe(ChangeRequestSide.SideA);
        r.SubmittedByUserId.ShouldBe(submitter);
        r.ConsentExpiresAt.ShouldBe(Now.AddDays(7));
    }

    [Fact]
    public void IssueConsent_Twice_Throws()
    {
        var r = NewRequest();
        r.IssueConsent("h1", ChangeRequestSide.SideA, Guid.NewGuid(), Now.AddDays(7));

        Should.Throw<BusinessException>(() =>
                r.IssueConsent("h2", ChangeRequestSide.SideB, Guid.NewGuid(), Now.AddDays(7)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
    }

    [Fact]
    public void RecordConsentDecision_Yes_SetsApprovedAndGranted()
    {
        var r = PendingRequest();

        r.RecordConsentDecision(approved: true, respondedByEmail: "rep@example.com", nowUtc: Now);

        r.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Approved);
        r.IsConsentGranted().ShouldBeTrue();
        r.ConsentRespondedByEmail.ShouldBe("rep@example.com");
        r.ConsentRespondedAt.ShouldBe(Now);
    }

    [Fact]
    public void RecordConsentDecision_No_SetsRejectedNotGranted()
    {
        var r = PendingRequest();

        r.RecordConsentDecision(approved: false, respondedByEmail: "rep@example.com", nowUtc: Now);

        r.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Rejected);
        r.IsConsentGranted().ShouldBeFalse();
    }

    [Fact]
    public void RecordConsentDecision_Twice_Throws()
    {
        var r = PendingRequest();
        r.RecordConsentDecision(true, "rep@example.com", Now);

        Should.Throw<BusinessException>(() =>
                r.RecordConsentDecision(false, "rep@example.com", Now))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentAlreadyResponded);
    }

    [Fact]
    public void IsConsentExpired_PastExpiry_True()
    {
        var r = NewRequest();
        r.IssueConsent("h", ChangeRequestSide.SideA, Guid.NewGuid(), Now.AddDays(-1));

        r.IsConsentExpired(Now).ShouldBeTrue();
    }

    [Fact]
    public void MarkConsentExpired_SetsExpiredAndNotGranted()
    {
        var r = NewRequest();
        r.IssueConsent("h", ChangeRequestSide.SideA, Guid.NewGuid(), Now.AddDays(-1));

        r.MarkConsentExpired(Now);

        r.ConsentStatus.ShouldBe(ChangeRequestConsentStatus.Expired);
        r.IsConsentGranted().ShouldBeFalse();
    }

    private static AppointmentChangeRequest NewRequest() => new(
        id: Guid.NewGuid(),
        tenantId: null,
        appointmentId: Guid.NewGuid(),
        changeRequestType: ChangeRequestType.Reschedule,
        cancellationReason: null,
        reScheduleReason: "Schedule conflict",
        newDoctorAvailabilityId: Guid.NewGuid());

    private static AppointmentChangeRequest PendingRequest()
    {
        var r = NewRequest();
        r.IssueConsent("hash", ChangeRequestSide.SideA, Guid.NewGuid(), Now.AddDays(7));
        return r;
    }
}
