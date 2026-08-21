using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Pins which appointment statuses the status-change email handler acts on.
/// Send-back (InfoRequested) must be handled so the requester is emailed;
/// non-emailed lifecycle states must stay ignored. Pure static predicate,
/// reachable via the Application InternalsVisibleTo wiring.
/// </summary>
public class StatusChangeHandledStatusTests
{
    [Theory]
    [InlineData(AppointmentStatusType.InfoRequested)]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    public void Handles_send_back_and_existing_status_emails(AppointmentStatusType status)
    {
        StatusChangeEmailHandler.IsHandledStatus(status).ShouldBeTrue();
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    public void Ignores_non_emailed_statuses(AppointmentStatusType status)
    {
        StatusChangeEmailHandler.IsHandledStatus(status).ShouldBeFalse();
    }
}
