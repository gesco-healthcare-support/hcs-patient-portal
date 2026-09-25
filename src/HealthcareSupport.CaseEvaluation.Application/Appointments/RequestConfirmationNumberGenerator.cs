using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Microsoft.Extensions.Localization;
using HealthcareSupport.CaseEvaluation.Localization;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Allocates the next human-facing appointment number ("A00042").
///
/// <para>Extracted from <c>AppointmentsAppService</c> in phase 4d (2026-08-05). It had been a
/// private method there, which was fine while booking was the only path that created an
/// appointment. Finalizing a reschedule now creates a SECOND appointment and needs a number too,
/// and <c>(TenantId, RequestConfirmationNumber)</c> is a HARD UNIQUE INDEX -- so a duplicated
/// implementation would be two allocators writing to one unique column, free to drift apart. One
/// generator, two callers.</para>
///
/// <para>NOT collision-proof on its own, by design: it reads the current maximum and adds one, so
/// two concurrent callers can pick the same number. <see cref="ConfirmationNumberRetryPolicy"/> is
/// what resolves that race, and every caller must go through it.</para>
/// </summary>
public class RequestConfirmationNumberGenerator : ITransientDependency
{
    internal const string Prefix = "A";
    internal const int Digits = 5;

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;
    private readonly IDataFilter _dataFilter;

    public RequestConfirmationNumberGenerator(
        IRepository<Appointment, Guid> appointmentRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<CaseEvaluationResource> localizer,
        IDataFilter dataFilter)
    {
        _appointmentRepository = appointmentRepository;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
        _dataFilter = dataFilter;
    }

    /// <summary>
    /// The next number in sequence for the current tenant. Only rows whose number matches the
    /// exact prefix + width are considered, so a hand-entered or legacy value cannot skew the max.
    /// </summary>
    /// <remarks>
    /// <para>The soft-delete filter is disabled for the max read, so a deleted appointment's
    /// number is never handed out a second time. The number is not private to us -- it is in sent
    /// email, in the Case Tracker payload and in <c>create-rebook/{sourceConfirmationNumber}</c>
    /// URLs -- so two appointments answering to one number is unsupportable. Numbering is
    /// therefore strictly monotonic per office and shows gaps where appointments were deleted;
    /// a gap is correct, a reused number is not.</para>
    ///
    /// <para>This is also what makes <see cref="ConfirmationNumberRetryPolicy"/> able to
    /// converge. Reading through the filter, a soft-deleted row holding the number returned the
    /// SAME value on every retry, so all attempts burned and the booking 500'd (2026-08-19).
    /// The IMultiTenant filter is deliberately left ON, so each office numbers independently.</para>
    /// </remarks>
    public virtual async Task<string> GenerateAsync()
    {
        var requiredLength = Prefix.Length + Digits;

        // The queryable must be obtained AND executed inside the scope; the filter is
        // otherwise re-applied when the query runs.
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var query = await _appointmentRepository.GetQueryableAsync();

            var latestNumber = await _asyncExecuter.FirstOrDefaultAsync(
                query
                    .Where(x => x.RequestConfirmationNumber != null
                        && x.RequestConfirmationNumber.StartsWith(Prefix)
                        && x.RequestConfirmationNumber.Length == requiredLength)
                    .OrderByDescending(x => x.RequestConfirmationNumber)
                    .Select(x => x.RequestConfirmationNumber));

            var nextValue = 1;
            if (!string.IsNullOrWhiteSpace(latestNumber)
                && int.TryParse(latestNumber.Substring(Prefix.Length), out var currentValue))
            {
                nextValue = currentValue + 1;
            }

            var maxValue = (int)Math.Pow(10, Digits) - 1;
            if (nextValue > maxValue)
            {
                throw new UserFriendlyException(_localizer["Request confirmation number limit reached."]);
            }

            return $"{Prefix}{nextValue:D5}";
        }
    }
}
