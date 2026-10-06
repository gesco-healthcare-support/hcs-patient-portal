using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// #707 LAYER 3, THE STAFF-ONLY WRITE SURFACE, AGAINST THE ROLES AND GRANTS PRODUCTION SEEDS.
///
/// <para><see cref="PhiSurfaceMethodAuthorizationTests"/> proves each attribute with a synthetic
/// single-grant role. That shows the ATTRIBUTE works. It cannot show that the grants production
/// WRITES keep the four external roles away from these methods, because no real external role
/// appears in it. This file closes that: the callers are the real Patient, Applicant Attorney,
/// Claim Examiner and Defense Attorney roles with the grants the external-role seeder wrote in
/// GetFixtureAsync, and the control is the real Staff Supervisor with the grants the internal-role
/// seeder wrote. A seeder change that gives an external role one of these permissions fails here,
/// even though no attribute changed.</para>
///
/// <para><b>HOW A REFUSAL IS ATTRIBUTED TO THE ATTRIBUTE.</b> Several of these methods also carry
/// in-code guards that throw <see cref="AbpAuthorizationException"/> too, so the exception TYPE
/// cannot tell the attribute from the guard. The interceptor's exception carries an ABP error code
/// (Volo.Authorization:...); the in-code ones carry none. <see cref="IsInterceptorRefusal"/> tests
/// the code, so deleting an attribute fails its refusal case even when a guard would have refused
/// the same caller, with no need for a probe id chosen to dodge the guard.</para>
///
/// <para>Data-light by design: every call targets an id that does not exist (or an invalid role
/// name, for the one create), so nothing is written even for the entitled caller.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class StaffOnlyEndpointsRealRoleAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    // Independent literals, deliberately not ExternalRoleConsts: a test sharing its source with the
    // code under test moves with it and can never fail.
    private static readonly string[] ExternalRoles =
        ["Patient", "Applicant Attorney", "Claim Examiner", "Defense Attorney"];

    private const string StaffSupervisor = "Staff Supervisor";

    private const string InterceptorCodePrefix = "Volo.Authorization:";

    private static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-0000000000fd");

    private static readonly IReadOnlyDictionary<string, StaffCase> Cases = BuildCases()
        .ToDictionary(c => c.Name, StringComparer.Ordinal);

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public StaffOnlyEndpointsRealRoleAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    public static TheoryData<string, string> ExternalRoleByCase()
    {
        var data = new TheoryData<string, string>();
        foreach (var name in Cases.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            foreach (var role in ExternalRoles)
            {
                data.Add(name, role);
            }
        }
        return data;
    }

    public static TheoryData<string> CaseNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            data.Add(name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(ExternalRoleByCase))]
    public async Task IsRefusedByTheAttribute_ForEveryRealExternalRole(string caseName, string role)
    {
        var staffCase = Cases[caseName];
        var fixture = await GetFixtureAsync();

        var outcome = await InvokeAsync(fixture, role, staffCase.Call);

        IsInterceptorRefusal(outcome).ShouldBeTrue(
            $"{role} must be refused by the authorization interceptor on {caseName} " +
            $"({staffCase.Permission}). Got: {Describe(outcome)}. A plain AbpAuthorizationException " +
            "without an ABP error code means an in-code guard refused the caller and the " +
            "[Authorize] attribute is missing. Any other outcome means either the attribute is " +
            "missing or the seeder now grants an external role a staff permission.");
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task IsNotRefusedByTheAttribute_ForTheRealStaffSupervisor(string caseName)
    {
        var staffCase = Cases[caseName];
        var fixture = await GetFixtureAsync();

        var outcome = await InvokeAsync(fixture, StaffSupervisor, staffCase.Call);

        IsInterceptorRefusal(outcome).ShouldBeFalse(
            $"{StaffSupervisor} is the entitled role for {caseName} ({staffCase.Permission}) and " +
            $"must get past the interceptor. Got: {Describe(outcome)}. A refusal here means the " +
            "seeded grants no longer give it this permission, and every refusal above is then " +
            "unproven.");
    }

    [Fact]
    public void TheCaseTable_CoversTheLifecycleChangeRequestsInternalUsersAndChildDeletes()
    {
        var permissions = Cases.Values.Select(c => c.Permission).ToHashSet(StringComparer.Ordinal);

        permissions.ShouldContain(CaseEvaluationPermissions.Appointments.Approve);
        permissions.ShouldContain(CaseEvaluationPermissions.Appointments.Reject);
        permissions.ShouldContain(CaseEvaluationPermissions.Appointments.Edit);
        permissions.ShouldContain(CaseEvaluationPermissions.Appointments.Delete);
        permissions.ShouldContain(CaseEvaluationPermissions.AppointmentChangeRequests.Approve);
        permissions.ShouldContain(CaseEvaluationPermissions.AppointmentChangeRequests.Reject);
        permissions.ShouldContain(CaseEvaluationPermissions.InternalUsers.Create);
        permissions.ShouldContain(CaseEvaluationPermissions.InternalUsers.Edit);
        permissions.ShouldContain(CaseEvaluationPermissions.AppointmentInjuryDetails.Delete);
        permissions.ShouldContain(CaseEvaluationPermissions.AppointmentDefenseAttorneys.Delete);
    }

    private static bool IsInterceptorRefusal(Exception? outcome) =>
        outcome is AbpAuthorizationException refusal
        && refusal.Code != null
        && refusal.Code.StartsWith(InterceptorCodePrefix, StringComparison.Ordinal);

    private static string Describe(Exception? outcome) =>
        outcome == null
            ? "no exception"
            : outcome.GetType().Name + " (code " + (outcome as AbpAuthorizationException)?.Code + ")";

    private static IEnumerable<StaffCase> BuildCases()
    {
        // Appointment lifecycle decisions.
        yield return new("Appointments.Update", CaseEvaluationPermissions.Appointments.Edit,
            sp => sp.GetRequiredService<IAppointmentsAppService>().UpdateAsync(UnknownId, new AppointmentUpdateDto()));
        yield return new("Appointments.Delete", CaseEvaluationPermissions.Appointments.Delete,
            sp => sp.GetRequiredService<IAppointmentsAppService>().DeleteAsync(UnknownId));
        yield return new("Appointments.GetPendingCount", CaseEvaluationPermissions.Appointments.Edit,
            sp => sp.GetRequiredService<IAppointmentsAppService>().GetPendingCountAsync());
        yield return new("Appointments.Approve", CaseEvaluationPermissions.Appointments.Approve,
            sp => sp.GetRequiredService<IAppointmentsAppService>().ApproveAsync(UnknownId));
        yield return new("Appointments.Reject", CaseEvaluationPermissions.Appointments.Reject,
            sp => sp.GetRequiredService<IAppointmentsAppService>()
                .RejectAsync(UnknownId, new RejectAppointmentInput { Reason = "TEST-probe reason" }));
        yield return new("AppointmentApproval.Approve", CaseEvaluationPermissions.Appointments.Approve,
            sp => sp.GetRequiredService<IAppointmentApprovalAppService>().ApproveAppointmentAsync(
                UnknownId, new ApproveAppointmentInput { PrimaryResponsibleUserId = Guid.NewGuid() }));
        yield return new("AppointmentApproval.Reject", CaseEvaluationPermissions.Appointments.Reject,
            sp => sp.GetRequiredService<IAppointmentApprovalAppService>()
                .RejectAppointmentAsync(UnknownId, new RejectAppointmentInput { Reason = "TEST-probe reason" }));

        // Change-request decisions.
        yield return new("ChangeRequests.ApproveCancellation", CaseEvaluationPermissions.AppointmentChangeRequests.Approve,
            sp => Approval(sp).ApproveCancellationAsync(UnknownId, new ApproveCancellationInput()));
        yield return new("ChangeRequests.RejectCancellation", CaseEvaluationPermissions.AppointmentChangeRequests.Reject,
            sp => Approval(sp).RejectCancellationAsync(UnknownId, new RejectChangeRequestInput { Reason = "TEST-probe reason" }));
        yield return new("ChangeRequests.ConfirmRescheduleDate", CaseEvaluationPermissions.AppointmentChangeRequests.Approve,
            sp => Approval(sp).ConfirmRescheduleDateAsync(UnknownId, new ConfirmRescheduleDateInput { DoctorAvailabilityId = Guid.NewGuid() }));
        yield return new("ChangeRequests.ResendConsentRequest", CaseEvaluationPermissions.AppointmentChangeRequests.Approve,
            sp => Approval(sp).ResendConsentRequestAsync(UnknownId));
        yield return new("ChangeRequests.ApproveReschedule", CaseEvaluationPermissions.AppointmentChangeRequests.Approve,
            sp => Approval(sp).ApproveRescheduleAsync(UnknownId, new ApproveRescheduleInput()));
        yield return new("ChangeRequests.RejectReschedule", CaseEvaluationPermissions.AppointmentChangeRequests.Reject,
            sp => Approval(sp).RejectRescheduleAsync(UnknownId, new RejectChangeRequestInput { Reason = "TEST-probe reason" }));

        // Internal user administration. An invalid role name makes the create stop before any write.
        yield return new("InternalUsers.Create", CaseEvaluationPermissions.InternalUsers.Create,
            sp => sp.GetRequiredService<IInternalUsersAppService>().CreateAsync(new CreateInternalUserDto
            {
                Email = "TEST-probe@example.test",
                FirstName = "TEST",
                LastName = "Probe",
                RoleName = "TEST-not-a-creatable-role",
            }));
        yield return new("InternalUsers.SendPasswordResetEmail", CaseEvaluationPermissions.InternalUsers.Edit,
            sp => sp.GetRequiredService<IInternalUsersAppService>().SendPasswordResetEmailAsync(UnknownId));

        // The seven appointment child-resource deletes (Staff Supervisor and admin only).
        yield return new("AppointmentInjuryDetails.Delete", CaseEvaluationPermissions.AppointmentInjuryDetails.Delete,
            sp => sp.GetRequiredService<IAppointmentInjuryDetailsAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentBodyParts.Delete", CaseEvaluationPermissions.AppointmentBodyParts.Delete,
            sp => sp.GetRequiredService<IAppointmentBodyPartsAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentClaimExaminers.Delete", CaseEvaluationPermissions.AppointmentClaimExaminers.Delete,
            sp => sp.GetRequiredService<IAppointmentClaimExaminersAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentPrimaryInsurances.Delete", CaseEvaluationPermissions.AppointmentPrimaryInsurances.Delete,
            sp => sp.GetRequiredService<IAppointmentPrimaryInsurancesAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentEmployerDetails.Delete", CaseEvaluationPermissions.AppointmentEmployerDetails.Delete,
            sp => sp.GetRequiredService<IAppointmentEmployerDetailsAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentApplicantAttorneys.Delete", CaseEvaluationPermissions.AppointmentApplicantAttorneys.Delete,
            sp => sp.GetRequiredService<IAppointmentApplicantAttorneysAppService>().DeleteAsync(UnknownId));
        yield return new("AppointmentDefenseAttorneys.Delete", CaseEvaluationPermissions.AppointmentDefenseAttorneys.Delete,
            sp => sp.GetRequiredService<IAppointmentDefenseAttorneysAppService>().DeleteAsync(UnknownId));
    }

    private static IAppointmentChangeRequestsApprovalAppService Approval(IServiceProvider sp) =>
        sp.GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();

    /// <summary>
    /// Runs the call as <paramref name="role"/> in the seeded office, with a fresh caller id so no
    /// outcome can come from record ownership, and returns what it threw (null when it returned).
    /// </summary>
    private async Task<Exception?> InvokeAsync(
        AuthorizationFixture fixture, string role, Func<IServiceProvider, Task> call)
    {
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), role))
            {
                try
                {
                    await call(ServiceProvider);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        return caught;
    }

    private sealed record StaffCase(string Name, string Permission, Func<IServiceProvider, Task> Call);
}
