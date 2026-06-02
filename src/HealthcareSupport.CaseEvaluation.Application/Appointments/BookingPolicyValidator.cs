using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
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
public class BookingPolicyValidator : ITransientDependency
{
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    public BookingPolicyValidator(
        ISystemParameterRepository systemParameterRepository,
        IRepository<AppointmentType, Guid> appointmentTypeRepository,
        IStringLocalizer<CaseEvaluationResource> l)
    {
        _systemParameterRepository = systemParameterRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _l = l;
    }

    /// <summary>
    /// Throws <see cref="BusinessException"/> when the slot's
    /// <paramref name="slotDate"/> falls inside the lead-time window or
    /// past the per-AppointmentType max horizon. Resolves the max-time from
    /// the type's <see cref="AppointmentType.MaxTimeCategory"/> via
    /// <see cref="AppointmentBookingValidators.ResolveMaxTimeDaysForType"/>
    /// (data-driven PQME / AME / OTHER horizons).
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
            // 2026-05-28 -- UserFriendlyException so the localized message reaches
            // the client; ABP's BusinessException auto-localization does not
            // resolve in this codebase (see AppointmentReadAccessGuard.cs:162-167).
            throw new UserFriendlyException(
                code: CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded,
                message: _l["SystemParameter:NotSeeded"]);
        }

        var appointmentType = await _appointmentTypeRepository.GetAsync(appointmentTypeId);

        var result = EvaluateBookingPolicy(slotDate, DateTime.Today, appointmentType.MaxTimeCategory, systemParameter);
        switch (result.Outcome)
        {
            case BookingPolicyOutcome.Allowed:
                return;
            case BookingPolicyOutcome.InsideLeadTime:
                throw new UserFriendlyException(
                    code: CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime,
                    message: _l["Appointment:BookingDateInsideLeadTime", result.ThresholdDays]);
            case BookingPolicyOutcome.PastMaxHorizon:
                throw new UserFriendlyException(
                    code: CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon,
                    message: _l["Appointment:BookingDatePastMaxHorizon", result.ThresholdDays]);
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
        AppointmentMaxTimeCategory? maxTimeCategory,
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

        var maxDays = AppointmentBookingValidators.ResolveMaxTimeDaysForType(maxTimeCategory, systemParameter);
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
