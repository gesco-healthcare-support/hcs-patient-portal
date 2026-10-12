using System;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The answer to a missing or wrong <c>X-Integration-Token</c> on reconcile and attendance: 403 with the
/// Gesco envelope, never 401. The Case Tracker's DEPLOYED client latches on a 401 for the process
/// lifetime, which stops both reconcile and attendance until a restart; a 403 is logged and retried.
/// Same decision and same body shape as the changes feed. The message is generic on purpose.
/// </summary>
internal static class CaseTrackerTokenRefusal
{
    public static ContentResult Forbidden() => new()
    {
        Content = CaseTrackerFeedResponseWriter.WriteError(
            "forbidden", "The request is not authorised.", null, Guid.NewGuid(), DateTime.UtcNow),
        ContentType = "application/json",
        StatusCode = StatusCodes.Status403Forbidden,
    };
}
