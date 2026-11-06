using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 -- caps how many times one account, and one network address, may try a confirmation
/// number with a patient's date of birth. A match hands a non-party the whole earlier case, and a
/// date of birth is not a secret, so the number of guesses is what keeps the check meaningful.
///
/// <para><b>Why here and not in the HTTP pipeline.</b> Two paths take the date-of-birth proof: the
/// lookup and the booking submit, which re-checks it. A limit on one route alone would leave the
/// other as an unthrottled way to test guesses. Both call this, so they share one budget.</para>
///
/// <para><b>Stamped BEFORE the match</b>, as the password-reset throttle is: a refused guess and a
/// match cost the same, so the limit itself never tells a caller which guesses were right.</para>
///
/// <para>Cache-backed (Redis in production, in-memory in tests). A cache failure fails OPEN with a
/// warning, like the account throttles: a Redis outage must not stop a booking.</para>
/// </summary>
public class ReEvaluationLookupThrottle : ITransientDependency
{
    /// <summary>Per account per hour. A party loading its own case never reaches this.</summary>
    public const int MaxPerUserPerHour = 10;

    /// <summary>Per network address per hour, so several accounts behind one address share a cap.</summary>
    public const int MaxPerAddressPerHour = 30;

    private const string KeyPrefix = "reeval-source-lookup";
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly IDistributedCache _cache;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ReEvaluationLookupThrottle> _logger;

    public ReEvaluationLookupThrottle(
        IDistributedCache cache,
        ICurrentUser currentUser,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ReEvaluationLookupThrottle> logger)
    {
        _cache = cache;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Refuses when either budget is spent, otherwise counts this attempt against both.
    /// </summary>
    public virtual async Task EnsureAllowedAndStampAsync()
    {
        var counters = Counters();

        foreach (var (key, max) in counters)
        {
            if (await ReadCountAsync(key) >= max)
            {
                _logger.LogInformation(
                    "ReEvaluationLookupThrottle: refused a re-evaluation lookup for user {UserId}; hourly limit reached.",
                    _currentUser.Id);
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ReEvaluationLookupThrottled);
            }
        }

        foreach (var (key, _) in counters)
        {
            await IncrementAsync(key);
        }
    }

    private List<(string Key, int Max)> Counters()
    {
        var counters = new List<(string Key, int Max)>();
        if (_currentUser.Id is { } userId)
        {
            counters.Add(($"{KeyPrefix}:user:{userId:N}", MaxPerUserPerHour));
        }

        // The address is hashed so the cache never holds a raw IP. Forwarded headers are applied
        // before this runs (CaseEvaluationHttpApiHostModule.ConfigureForwardedHeaders), so behind the
        // proxy this is the client, not the proxy's own address.
        var address = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(address))
        {
            counters.Add(($"{KeyPrefix}:address:{Digest(address)}", MaxPerAddressPerHour));
        }

        return counters;
    }

    private async Task<int> ReadCountAsync(string key)
    {
        try
        {
            var value = await _cache.GetStringAsync(key);
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
                ? count
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReEvaluationLookupThrottle: cache read failed; failing open.");
            return 0;
        }
    }

    private async Task IncrementAsync(string key)
    {
        try
        {
            // Not atomic, like the account throttles: two simultaneous attempts may count once. The
            // window restarts on each write, so a caller pinned at the cap stays there until they stop.
            var next = await ReadCountAsync(key) + 1;
            await _cache.SetStringAsync(
                key,
                next.ToString(CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Window });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReEvaluationLookupThrottle: cache write failed; this attempt was not counted.");
        }
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
