using System.Net;
using HealthcareSupport.CaseEvaluation.Exceptions;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Exceptions;

/// <summary>
/// BUG-012 Sub-bug 1 (2026-05-22) -- unit tests for
/// <see cref="CaseEvaluationExceptionStatusCodeMappings.MapSharedRegistrationAndInternalUserCodes"/>.
/// The helper centralizes 11 BadRequest mappings that BOTH the
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
    public void MapSharedRegistrationAndInternalUserCodes_RegistersAllSharedCodesAsBadRequest()
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
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InviteEmailAlreadyRegistered
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

        // Staff-sent account activation (6 codes): refusals the person can act on are 400,
        // "too soon" is 429, a failed queue is 503.
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationAccountInactive
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationAlreadyActive
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationNoAddress
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationNoOfficeAdministrator
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationResendTooSoon
        ].ShouldBe(HttpStatusCode.TooManyRequests);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.ActivationNotQueued
        ].ShouldBe(HttpStatusCode.ServiceUnavailable);

        // One role per person (2 codes): both are refusals the person can act on.
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.OneRolePerPerson
        ].ShouldBe(HttpStatusCode.BadRequest);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.InternalUserRoleChangeNotAllowed
        ].ShouldBe(HttpStatusCode.BadRequest);

        // 2026-10-27: a staff role changed on the identity Users screen from the staff site points to Change role.
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.StaffRoleChangeUseStaffManagement
        ].ShouldBe(HttpStatusCode.BadRequest);

        // A Responsible User who is not a switched-on staff account: the Approve dialog shows the message.
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentResponsibleUserNotEligible
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
        // Still exactly 21 mappings; no duplicate-key blow up.
        options.ErrorCodeToHttpStatusCodeMappings.Count.ShouldBe(21);
    }

    /// <summary>
    /// B11 -- a refused upload is 422 and an unavailable scanner is 503. Unmapped, both would be
    /// ABP's default 403, which the SPA treats as a permission failure and shows nothing for.
    /// </summary>
    [Fact]
    public void MapUploadScanCodes_RefusalIs422_AndScannerUnavailableIs503()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationExceptionStatusCodeMappings.MapUploadScanCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.UploadRefused
        ].ShouldBe(HttpStatusCode.UnprocessableEntity);
        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.UploadScanUnavailable
        ].ShouldBe(HttpStatusCode.ServiceUnavailable);
        options.ErrorCodeToHttpStatusCodeMappings.Count.ShouldBe(2);
    }
}
