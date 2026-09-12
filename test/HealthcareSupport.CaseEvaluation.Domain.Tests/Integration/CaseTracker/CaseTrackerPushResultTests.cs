using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the delivery status matrix agreed in
/// <c>docs/integration/case-tracker-api-contract.md</c> section I. This classification decides
/// whether a failed push is retried or dead-lettered, so getting it wrong either loses a case
/// silently (retrying a permanent failure until the cap, then alerting late) or gives up on a
/// recoverable one. Pure function -- no HTTP.
/// </summary>
public class CaseTrackerPushResultTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(202)]
    [InlineData(204)]
    public void SuccessStatusCodes_AreSuccess(int statusCode)
    {
        var result = CaseTrackerPushResult.FromStatusCode(statusCode);

        result.Outcome.ShouldBe(CaseTrackerPushOutcome.Success);
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(401)] // missing / invalid X-Intake-Token -- a retry can never fix it
    [InlineData(400)] // malformed payload -- our bug
    [InlineData(415)] // wrong content type -- our bug
    [InlineData(403)] // not in the contract, but a permission problem retrying will not solve
    [InlineData(422)] // semantic rejection of the body
    public void PermanentClientErrors_AreFatal(int statusCode)
    {
        var result = CaseTrackerPushResult.FromStatusCode(statusCode);

        result.Outcome.ShouldBe(CaseTrackerPushOutcome.Fatal);
        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData(404)] // doc-update before its intake was accepted -- retry once the intake lands
    [InlineData(408)] // server-side request timeout
    [InlineData(429)] // rate limited -- transient by definition
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    public void TransientErrors_AreRetryable(int statusCode)
    {
        var result = CaseTrackerPushResult.FromStatusCode(statusCode);

        result.Outcome.ShouldBe(CaseTrackerPushOutcome.Retryable);
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void TransportFailure_IsRetryable_AndCarriesTheReason()
    {
        var result = CaseTrackerPushResult.FromTransportFailure("connection refused");

        result.Outcome.ShouldBe(CaseTrackerPushOutcome.Retryable);
        result.StatusCode.ShouldBeNull();
        result.Error.ShouldBe("connection refused");
    }

    [Fact]
    public void StatusCodeResult_RecordsTheCode()
    {
        CaseTrackerPushResult.FromStatusCode(503).StatusCode.ShouldBe(503);
    }
}
