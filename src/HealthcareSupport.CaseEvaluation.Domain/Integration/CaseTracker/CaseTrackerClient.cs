using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Typed HttpClient for the Case Tracker intake API. Registered in
/// <c>CaseEvaluationDomainModule</c> with its base address and timeout, mirroring the
/// packet-renderer sidecar client.
///
/// <para>Auth is the <c>X-Intake-Token</c> header, sent as the RAW token (their endpoint accepts
/// raw or <c>Bearer</c>-prefixed and strips the prefix). It is read per request from configuration
/// rather than captured at registration so rotating the secret does not require a restart.</para>
///
/// <para>HIPAA: neither the payload nor the token is ever logged. Log lines carry only the target
/// path and the status code, and the ledger's <c>LastError</c> holds the same non-PHI summary.</para>
/// </summary>
public class CaseTrackerClient : ICaseTrackerClient, ITransientDependency
{
    /// <summary>Header name agreed with the Case Tracker team.</summary>
    public const string IntakeTokenHeaderName = "X-Intake-Token";

    internal const string TokenConfigurationKey = "CaseTracker:IntakeToken";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CaseTrackerClient> _logger;

    public CaseTrackerClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<CaseTrackerClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public virtual async Task<CaseTrackerPushResult> PostAsync(
        string targetPath,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, targetPath)
            {
                // Content type is mandatory on their side; a missing or wrong one is a 415.
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
            };

            var token = _configuration[TokenConfigurationKey];
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.TryAddWithoutValidation(IntakeTokenHeaderName, token);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var result = CaseTrackerPushResult.FromStatusCode((int)response.StatusCode);

            _logger.LogInformation(
                "CaseTrackerClient: POST {TargetPath} -> {StatusCode} ({Outcome}).",
                targetPath, (int)response.StatusCode, result.Outcome);

            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // The request never produced a response (refused, DNS, TLS, timeout). Retryable: the far
            // side being unreachable says nothing about whether the message is valid. The exception
            // message is safe to keep -- it describes the transport, not the payload.
            _logger.LogWarning(
                "CaseTrackerClient: POST {TargetPath} failed in transport ({ExceptionType}).",
                targetPath, ex.GetType().Name);

            return CaseTrackerPushResult.FromTransportFailure($"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
