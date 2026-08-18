using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// 2026-08-17 -- translates the entity delete guards' <see cref="BusinessException"/> codes
/// into a <see cref="UserFriendlyException"/> carrying the localized message that already
/// exists for each of them.
///
/// <para><b>The defect this fixes.</b> Nine screens' delete buttons returned HTTP 500 with
/// "An internal error occurred during your request!" instead of saying why the record could
/// not be deleted. The guards were correct and the messages were already written; nothing
/// connected the two. ABP's <c>BusinessException</c> auto-localization via
/// <c>MapCodeNamespace</c> does not resolve in this codebase, so an untranslated throw
/// reaches the SPA with an empty <c>err.error.error.message</c> and the toast falls back to
/// ABP's generic text.</para>
///
/// <para><b>Why this is a second translator.</b>
/// <see cref="Appointments.AppointmentExceptionTranslator"/> solves the same problem for the
/// <c>CaseEvaluation:Appointment.*</c> namespace and is sealed and working. Widening it to
/// cover unrelated entities would be a refactor nobody asked for, so this deliberately
/// duplicates the shape instead. The duplication is a candidate for later consolidation into
/// one code-to-key registry -- worth doing when a third translator would otherwise appear,
/// not before.</para>
///
/// <para><b>Scope.</b> The nine <c>*.InUse</c> delete guards only. Every other raw
/// <c>BusinessException</c> outside the Appointment namespace still has the same defect;
/// this fixes the set the user actually hits from the People and lookup screens.</para>
///
/// <para>Localization stays in the Application layer. Do NOT push
/// <see cref="IStringLocalizer{T}"/> into the Domain managers -- they raise codes, and the
/// layer that talks to a client is the one that renders text for it.</para>
/// </summary>
public sealed class DomainErrorTranslator : ITransientDependency
{
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    public DomainErrorTranslator(IStringLocalizer<CaseEvaluationResource> l)
    {
        _l = l;
    }

    /// <summary>
    /// Returns either a fresh <see cref="UserFriendlyException"/> carrying the localized
    /// message (when the code is known) or the original exception unchanged. The caller
    /// should <c>throw</c> the result.
    ///
    /// <para>Unknown codes pass through deliberately, so wrapping a call in this cannot
    /// swallow an error it does not understand.</para>
    /// </summary>
    public BusinessException Translate(BusinessException ex)
    {
        if (ex is UserFriendlyException) return ex; // already carries a message
        var key = ResolveKey(ex.Code);
        if (key == null) return ex;

        var args = ResolveArgs(ex);
        var localized = args.Length > 0 ? _l[key, args] : _l[key];
        return new UserFriendlyException(code: ex.Code, message: localized);
    }

    /// <summary>
    /// Convenience for the guards that raise the code inline rather than receiving it from a
    /// Domain manager, so a call site reads as one throw instead of a construct-then-translate
    /// pair.
    /// </summary>
    public BusinessException Refuse(string code)
    {
        return Translate(new BusinessException(code));
    }

    private static string? ResolveKey(string? code) => code switch
    {
        CaseEvaluationDomainErrorCodes.ApplicantAttorneyInUse => "ApplicantAttorney:InUse",
        CaseEvaluationDomainErrorCodes.DefenseAttorneyInUse => "DefenseAttorney:InUse",
        CaseEvaluationDomainErrorCodes.PatientInUse => "Patient:InUse",
        CaseEvaluationDomainErrorCodes.DocumentInUse => "Document:InUse",
        CaseEvaluationDomainErrorCodes.AppointmentDocumentTypeInUse => "AppointmentDocumentType:InUse",
        CaseEvaluationDomainErrorCodes.AppointmentTypeInUse => "AppointmentType:InUse",
        CaseEvaluationDomainErrorCodes.AppointmentLanguageInUse => "AppointmentLanguage:InUse",
        CaseEvaluationDomainErrorCodes.StateInUse => "State:InUse",
        CaseEvaluationDomainErrorCodes.LocationInUse => "Location:InUse",
        _ => null,
    };

    /// <summary>
    /// Location is the only one of the nine whose message interpolates. Its guard attaches the
    /// blocking row count and the entity that holds them, and the message renders them in that
    /// order.
    /// </summary>
    private static object[] ResolveArgs(BusinessException ex)
    {
        if (ex.Data == null || ex.Data.Count == 0) return System.Array.Empty<object>();

        return ex.Code switch
        {
            CaseEvaluationDomainErrorCodes.LocationInUse
                when ex.Data["count"] is { } count && ex.Data["entity"] is { } entity
                    => new[] { count, entity },
            _ => System.Array.Empty<object>(),
        };
    }
}
