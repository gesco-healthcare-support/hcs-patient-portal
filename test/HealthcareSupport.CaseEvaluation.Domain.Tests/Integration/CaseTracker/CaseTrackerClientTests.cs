using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerClient"/>, the one place the portal POSTs patient data to
/// the Case Tracker.
///
/// <para><b>WHAT IS PINNED.</b> Every push is a JSON POST to the given path, carrying the intake token
/// header when one is configured and no header when it is not. The response status decides the
/// outcome (success / retry / give up). A request that never got a response -- refused, timed out,
/// cancelled -- is reported as a transport failure rather than thrown, while any OTHER exception still
/// reaches the caller.</para>
///
/// <para><b>NO NETWORK.</b> The <see cref="HttpClient"/> is built over a fake
/// <see cref="HttpMessageHandler"/> that records the request and returns a canned response, so nothing
/// leaves the machine. The results asserted are the recorded request and the returned result.
/// Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerClientTests
{
    private const string Payload = "{\"data\":{\"TEST\":\"payload\"}}";

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _respond(request);
        }
    }

    private static (CaseTrackerClient Client, FakeHandler Handler) Build(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string? token = "TEST-intake-token")
    {
        var handler = new FakeHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://casetracker.test.local/") };
        var configuration = Substitute.For<IConfiguration>();
        configuration["CaseTracker:IntakeToken"].Returns(token);
        return (new CaseTrackerClient(http, configuration, NullLogger<CaseTrackerClient>.Instance), handler);
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    [Fact]
    public async Task PostAsync_SendsAJsonPostWithTheTokenAndReportsSuccess()
    {
        var (client, handler) = Build(_ => Status(HttpStatusCode.OK));

        var result = await client.PostAsync("api/TEST/intake", Payload);

        result.IsSuccess.ShouldBeTrue();
        result.StatusCode.ShouldBe(200);
        handler.Request.ShouldNotBeNull();
        handler.Request.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri!.ToString().ShouldBe("https://casetracker.test.local/api/TEST/intake");
        handler.Request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
        handler.Body.ShouldBe(Payload);
        handler.Request.Headers.GetValues(CaseTrackerClient.IntakeTokenHeaderName).ShouldBe(new[] { "TEST-intake-token" });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostAsync_NoTokenConfigured_SendsNoTokenHeader(string? token)
    {
        var (client, handler) = Build(_ => Status(HttpStatusCode.OK), token);

        await client.PostAsync("api/TEST/intake", Payload);

        handler.Request!.Headers.Contains(CaseTrackerClient.IntakeTokenHeaderName).ShouldBeFalse();
    }

    /// <summary>
    /// The far side's status decides what happens to the row: retried on a transient answer, given up
    /// on a definitive rejection.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Created, CaseTrackerPushOutcome.Success)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CaseTrackerPushOutcome.Retryable)]
    [InlineData(HttpStatusCode.TooManyRequests, CaseTrackerPushOutcome.Retryable)]
    [InlineData(HttpStatusCode.BadRequest, CaseTrackerPushOutcome.Fatal)]
    [InlineData(HttpStatusCode.UnsupportedMediaType, CaseTrackerPushOutcome.Fatal)]
    public async Task PostAsync_ReportsTheOutcomeTheResponseStatusCalls(HttpStatusCode code, CaseTrackerPushOutcome expected)
    {
        var (client, _) = Build(_ => Status(code));

        var result = await client.PostAsync("api/TEST/intake", Payload);

        result.Outcome.ShouldBe(expected);
        result.StatusCode.ShouldBe((int)code);
    }

    /// <summary>
    /// A request that never got an answer (refused, DNS, TLS, timeout, cancelled) is a transport
    /// failure -- reported, not thrown -- and names the exception type so the dead-letter screen shows
    /// what happened.
    /// </summary>
    [Theory]
    [InlineData("http")]
    [InlineData("timeout")]
    [InlineData("cancelled")]
    public async Task PostAsync_NoResponseAtAll_IsReportedAsATransportFailure(string kind)
    {
        Exception failure = kind switch
        {
            "http" => new HttpRequestException("TEST-connection refused"),
            "timeout" => new TaskCanceledException("TEST-timed out"),
            _ => new OperationCanceledException("TEST-cancelled"),
        };
        var (client, _) = Build(_ => throw failure);

        var result = await client.PostAsync("api/TEST/intake", Payload);

        result.IsSuccess.ShouldBeFalse();
        result.StatusCode.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldStartWith(failure.GetType().Name + ":");
    }

    [Fact]
    public async Task PostAsync_AnyOtherException_ReachesTheCaller()
    {
        var (client, _) = Build(_ => throw new InvalidOperationException("TEST-programming error"));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => client.PostAsync("api/TEST/intake", Payload));

        thrown.Message.ShouldBe("TEST-programming error");
    }
}
