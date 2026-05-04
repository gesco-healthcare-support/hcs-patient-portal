using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11b (2026-05-04) -- orchestrates the Phase 11a pure validators
/// against the per-tenant <see cref="SystemParameter"/> singleton +
/// <see cref="AppointmentType"/> name lookup. Centralizes the lead-time
/// and per-type max-time gates so <c>AppointmentsAppService.CreateAsync</c>
/// (and future re-request / reval flows) can call one method instead of
/// re-implementing the rule chain.
///
/// Mirrors OLD <c>AppointmentDomain.cs</c> Add-path validation:
///   - lead-time gate: slot.AvailableDate &gt;= today + AppointmentLeadTime
///   - max-time gate: slot.AvailableDate &lt;= today + max-for-type
///
/// Throws <see cref="BusinessException"/> with one of two error codes
/// (<see cref="CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime"/>
/// or <see cref="CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon"/>)
/// when a check fails. The number-of-days threshold is round-tripped on
/// the exception data so localization can render a useful message.
/// </summary>
public class BookingPolicyValidator
{
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;

    public BookingPolicyValidator(
        ISystemParameterRepository systemParameterRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository)
    {
        _systemParameterRepository = systemParameterRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
    }

    /// <summary>
    /// Throws <see cref="BusinessException"/> when the slot's
    /// <paramref name="slotDate"/> falls inside the lead-time window or
    /// past the per-AppointmentType max horizon. Resolves the max-time
    /// per <see cref="AppointmentType.Name"/> via Phase 11a
    /// <see cref="AppointmentBookingValidators.ResolveMaxTimeDaysForType"/>
    /// (PQME / PQME-REVAL / AME / AME-REVAL / OTHER routing).
    ///
    /// Uses <see cref="DateTime.Today"/> for the comparison anchor;
    /// callers in tests can swap by extracting the static helper
    /// <see cref="EvaluateBookingPolicy"/>.
    /// </summary>
    public virtual async Task ValidateAsync(DateTime slotDate, Guid appointmentTypeId)
    {
        var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
        if (systemParameter == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded);
        }

        var appointmentType = await _appointmentTypeRepository.GetAsync(appointmentTypeId);

        var result = EvaluateBookingPolicy(slotDate, DateTime.Today, appointmentType.Name, systemParameter);
        switch (result.Outcome)
        {
            case BookingPolicyOutcome.Allowed:
                return;
            case BookingPolicyOutcome.InsideLeadTime:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime)
                    .WithData("leadTimeDays", result.ThresholdDays);
            case BookingPolicyOutcome.PastMaxHorizon:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon)
                    .WithData("maxTimeDays", result.ThresholdDays);
            default:
                throw new InvalidOperationException(
                    $"Unhandled booking-policy outcome '{result.Outcome}'.");
        }
    }

    /// <summary>
    /// Pure helper that evaluates the lead-time + max-time gates without
    /// any IO. Extracted as <c>internal static</c> so unit tests can
    /// exercise every branch without standing up the full ABP harness
    /// (matches the Phase 3 / 5 / 6 / 7 / 11a pattern). Callers pass
    /// <c>DateTime.Today</c> from production; tests pass a fixed anchor.
    /// </summary>
    internal static BookingPolicyResult EvaluateBookingPolicy(
        DateTime slotDate,
        DateTime today,
        string? appointmentTypeName,
        SystemParameter systemParameter)
    {
        if (systemParameter == null) throw new ArgumentNullException(nameof(systemParameter));

        // Lead-time first: an in-the-past or sub-leadtime slot is rejected
        // even if it's also past max horizon (failing both is a logic
        // contradiction, but lead-time is the more user-actionable error).
        if (!AppointmentBookingValidators.IsSlotWithinLeadTime(slotDate, today, systemParameter.AppointmentLeadTime))
        {
            return new BookingPolicyResult(BookingPolicyOutcome.InsideLeadTime, systemParameter.AppointmentLeadTime);
        }

        var maxDays = AppointmentBookingValidators.ResolveMaxTimeDaysForType(appointmentTypeName, systemParameter);
        if (!AppointmentBookingValidators.IsSlotWithinMaxTime(slotDate, today, maxDays))
        {
            return new BookingPolicyResult(BookingPolicyOutcome.PastMaxHorizon, maxDays);
        }

        return new BookingPolicyResult(BookingPolicyOutcome.Allowed, ThresholdDays: 0);
    }
}

internal enum BookingPolicyOutcome
{
    Allowed,
    InsideLeadTime,
    PastMaxHorizon,
}

internal record BookingPolicyResult(BookingPolicyOutcome Outcome, int ThresholdDays);
