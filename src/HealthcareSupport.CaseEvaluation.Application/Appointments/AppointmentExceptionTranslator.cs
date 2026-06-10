using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-05-28 -- centralized translator from <see cref="BusinessException"/>
/// (carrying a <c>CaseEvaluation:Appointment.*</c> code) into a
/// <see cref="UserFriendlyException"/> with a localized message.
///
/// <para>Necessary because ABP's <c>BusinessException</c> auto-localization
/// via <c>MapCodeNamespace</c> does not resolve in this codebase (see
/// <c>AppointmentReadAccessGuard.cs:162-167</c>). Without translation, the
/// SPA's plan-6 catch block in <c>appointment-add.component.ts</c> reads
/// an empty <c>err.error.error.message</c> and the toast falls back to
/// ABP's generic "An internal error occurred during your request!".</para>
///
/// <para>Wrap any call into <see cref="AppointmentManager"/> or
/// <see cref="AppointmentApprovalValidator"/> in
/// <c>try { ... } catch (BusinessException ex) { throw _translator.Translate(ex); }</c>.
/// Codes not present in the map pass through unchanged so the existing
/// HTTP-status filter behavior is preserved.</para>
/// </summary>
public sealed class AppointmentExceptionTranslator : ITransientDependency
{
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    public AppointmentExceptionTranslator(IStringLocalizer<CaseEvaluationResource> l)
    {
        _l = l;
    }

    /// <summary>
    /// Returns either a fresh <see cref="UserFriendlyException"/> carrying the
    /// localized message (when the code is known) or the original exception
    /// (unchanged). Caller should <c>throw</c> the result.
    /// </summary>
    public BusinessException Translate(BusinessException ex)
    {
        if (ex is UserFriendlyException) return ex; // already has a message
        var key = ResolveKey(ex.Code);
        if (key == null) return ex;
        // Some codes carry data on the exception that the localized message
        // interpolates. We forward the named keys we know about; unknown
        // data slots fall through (the message uses positional placeholders
        // so the ordering matters).
        var args = ResolveArgs(ex);
        var localized = args.Length > 0 ? _l[key, args] : _l[key];
        return new UserFriendlyException(code: ex.Code, message: localized);
    }

    private static string? ResolveKey(string? code) => code switch
    {
        CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime
            => "Appointment:BookingDateInsideLeadTime",
        CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon
            => "Appointment:BookingDatePastMaxHorizon",
        CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull
            => "Appointment:BookingSlotFull",
        CaseEvaluationDomainErrorCodes.AppointmentBookingSlotClosed
            => "Appointment:BookingSlotClosed",
        CaseEvaluationDomainErrorCodes.AppointmentBookingSlotTypeMismatch
            => "Appointment:BookingSlotTypeMismatch",
        CaseEvaluationDomainErrorCodes.AppointmentPanelNumberRequiredForPqme
            => "Appointment:PanelNumberRequiredForPqme",
        CaseEvaluationDomainErrorCodes.AppointmentPanelNumberNotAllowedForType
            => "Appointment:PanelNumberNotAllowedForType",
        CaseEvaluationDomainErrorCodes.AppointmentInvalidTransition
            => "Appointment:InvalidTransition",
        CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresInjuryDetail
            => "Appointment:ApprovalRequiresInjuryDetail",
        CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresClaimExaminer
            => "Appointment:ApprovalRequiresClaimExaminer",
        CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresPanelStrikeList
            => "Appointment:ApprovalRequiresPanelStrikeList",
        CaseEvaluationDomainErrorCodes.AppointmentApprovalRequiresResponsibleUser
            => "Appointment:ApprovalRequiresResponsibleUser",
        CaseEvaluationDomainErrorCodes.AppointmentNotPendingForApproval
            => "Appointment:NotPendingForApproval",
        CaseEvaluationDomainErrorCodes.AppointmentNotPendingForRejection
            => "Appointment:NotPendingForRejection",
        CaseEvaluationDomainErrorCodes.AppointmentRejectionRequiresNotes
            => "Appointment:RejectionRequiresNotes",
        CaseEvaluationDomainErrorCodes.AppointmentReSubmitSourceNotRejected
            => "Appointment:ReSubmitSourceNotRejected",
        CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApproved
            => "Appointment:RevalSourceNotApproved",
        CaseEvaluationDomainErrorCodes.AppointmentRevalSourceNotApprovedAdminHint
            => "Appointment:RevalSourceNotApprovedAdminHint",
        CaseEvaluationDomainErrorCodes.AppointmentAccessDenied
            => "Appointment:AccessDenied",
        CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded
            => "SystemParameter:NotSeeded",
        _ => null,
    };

    private static object[] ResolveArgs(BusinessException ex)
    {
        if (ex.Data == null || ex.Data.Count == 0) return System.Array.Empty<object>();
        // Codes that carry interpolated values in their localized message.
        return ex.Code switch
        {
            CaseEvaluationDomainErrorCodes.AppointmentBookingDateInsideLeadTime
                when ex.Data["leadTimeDays"] is { } v => new[] { v },
            CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon
                when ex.Data["maxTimeDays"] is { } v => new[] { v },
            CaseEvaluationDomainErrorCodes.AppointmentBookingSlotFull
                when ex.Data["activeCount"] is { } a && ex.Data["capacity"] is { } c
                    => new[] { a, c },
            _ => System.Array.Empty<object>(),
        };
    }
}
