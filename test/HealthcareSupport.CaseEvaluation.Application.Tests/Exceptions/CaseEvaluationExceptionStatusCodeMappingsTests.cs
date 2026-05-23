using System.Net;
using HealthcareSupport.CaseEvaluation.Exceptions;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Exceptions;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- unit tests for
/// <see cref="CaseEvaluationExceptionStatusCodeMappings.MapSharedRegistrationAndInternalUserCodes"/>.
/// The helper centralizes 10 BadRequest mappings that BOTH the
/// AuthServer host and the HttpApi.Host need (per the BUG-003 mirror
/// convention). Each mapping is asserted individually so a future
/// removal / status-code change surfaces as a deliberate test edit
/// rather than a silent drift.
///
/// <para>Mirrors the <see cref="HealthcareSupport.CaseEvaluation.HttpApiHost.Tests.CaseEvaluationHttpApiHostModuleTests"/>
/// pattern from BUG-025: no DI / no module bootstrap; the helper is
/// pure data-config, exercised directly against a fresh
/// <see cref="AbpExceptionHttpStatusCodeOptions"/> instance.</para>
/// </summary>
public class CaseEvaluationExceptionStatusCodeMappingsTests
{
    [Fact]
    public void MapSharedRegistrationAndInternalUserCodes_RegistersAllTenCodesAsBadRequest()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationExceptionStatusCodeMappings
            .MapSharedRegistrationAndInternalUserCodes(options);

        // Registration / external-signup family (4 codes).
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired
        ].ShouldBe(HttpStatusCode.BadRequest);

        // Appointment-flow attorney FirmName guard (1 code).
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentAttorneyFirmNameRequired
        ].ShouldBe(HttpStatusCode.BadRequest);

        // IT-Admin internal-user creation family (6 codes).
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserInvalidRole
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserRoleMissing
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserCreateFailed
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserTenantRequired
        ].ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void MapSharedRegistrationAndInternalUserCodes_IsIdempotent()
    {
        // The helper is invoked from BOTH host modules; invoking
        // twice on the same options dictionary must be a no-op
        // (options.Map overwrites duplicates with the same value).
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationExceptionStatusCodeMappings.MapSharedRegistrationAndInternalUserCodes(options);
        CaseEvaluationExceptionStatusCodeMappings.MapSharedRegistrationAndInternalUserCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired
        ].ShouldBe(HttpStatusCode.BadRequest);
        // Still exactly 10 mappings; no duplicate-key blow up.
        options.ErrorCodeToHttpStatusCodeMappings.Count.ShouldBe(10);
    }
}
