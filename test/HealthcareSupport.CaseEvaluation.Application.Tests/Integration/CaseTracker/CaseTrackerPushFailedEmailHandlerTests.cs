using System;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The alert handler's template choice (#917). The two emails ask staff for different things, so the
/// mapping must be exact and must refuse an unknown kind rather than guess.
/// </summary>
public class CaseTrackerPushFailedEmailHandlerTests
{
    [Theory]
    [InlineData(CaseTrackerPushAlertKind.DeadLettered, NotificationTemplateConsts.Codes.CaseTrackerPushFailed)]
    [InlineData(CaseTrackerPushAlertKind.StillRetrying, NotificationTemplateConsts.Codes.CaseTrackerPushRetrying)]
    public void EachAlertKind_UsesItsOwnTemplate(CaseTrackerPushAlertKind kind, string expectedCode)
    {
        CaseTrackerPushFailedEmailHandler.TemplateCodeFor(kind).ShouldBe(expectedCode);
    }

    [Fact]
    public void AnUnknownKind_Throws_RatherThanSendingTheWrongEmail()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CaseTrackerPushFailedEmailHandler.TemplateCodeFor((CaseTrackerPushAlertKind)99));
    }

    [Fact]
    public void AnEventBuiltWithoutAKind_IsADeadLetterAlert()
    {
        // Every event raised before #917 meant a dead letter; the default keeps that meaning.
        new CaseTrackerPushFailedEto().Kind.ShouldBe(CaseTrackerPushAlertKind.DeadLettered);
    }
}
