using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
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

    public RequestConfirmationNumberGenerator(
        IRepository<Appointment, Guid> appointmentRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _appointmentRepository = appointmentRepository;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
    }

    /// <summary>
    /// The next number in sequence for the current tenant. Only rows whose number matches the
    /// exact prefix + width are considered, so a hand-entered or legacy value cannot skew the max.
    /// </summary>
    public virtual async Task<string> GenerateAsync()
    {
        var requiredLength = Prefix.Length + Digits;
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
