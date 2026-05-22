using System.Net;
using Volo.Abp.AspNetCore.ExceptionHandling;

namespace HealthcareSupport.CaseEvaluation.Exceptions;

/// <summary>
/// BUG-012 Sub-bug 2 (2026-05-22) -- the 10 error codes that must
/// produce HTTP 400 on BOTH the AuthServer host AND the HttpApi.Host
/// (per the BUG-003 convention -- the /api/public/external-signup/*
/// endpoints are loaded into both hosts; the status-code remap must
/// stay in sync). Previously inlined identically in both host modules,
/// flagged as a 65-line duplication block by Sonar.
///
/// <para>HttpApi.Host adds 4 appointment-state-machine codes + 1
/// InternalUserTenantMismatch code on top of this shared set; those
/// stay inline at the host-module level because they are host-specific
/// (controllers only loaded into HttpApi.Host).</para>
/// </summary>
public static class CaseEvaluationExceptionStatusCodeMappings
{
    /// <summary>
    /// Adds the 10 cross-host BadRequest mappings. Called from BOTH
    /// <c>CaseEvaluationAuthServerModule.ConfigureServices</c> and
    /// <c>CaseEvaluationHttpApiHostModule.ConfigureServices</c> inside
    /// the <c>Configure&lt;AbpExceptionHttpStatusCodeOptions&gt;</c>
    /// lambda. Idempotent -- <c>options.Map</c> overwrites duplicates,
    /// but the two hosts never invoke this twice each.
    /// </summary>
    public static void MapSharedRegistrationAndInternalUserCodes(
        AbpExceptionHttpStatusCodeOptions options)
    {
        // Registration flow (external-signup -- present in both hosts).
        options.Map(
            CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired,
            HttpStatusCode.BadRequest);

        // Appointment-flow attorney FirmName guard. Appointment routes
        // are HttpApi.Host-only TODAY, but the AuthServer mirror is the
        // documented convention for any controller that ever moves.
        options.Map(
            CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired,
            HttpStatusCode.BadRequest);

        // Internal-user (IT-Admin) creation flow.
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserInvalidRole,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserRoleMissing,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserCreateFailed,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed,
            HttpStatusCode.BadRequest);
        options.Map(
            CaseEvaluationDomainErrorCodes.InternalUserTenantRequired,
            HttpStatusCode.BadRequest);
    }
}
