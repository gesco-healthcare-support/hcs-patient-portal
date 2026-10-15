using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// The PERMISSION half of the booking patient-read rule, on the one harness whose authorization
/// pipeline is real. Internal staff must hold <c>CaseEvaluation.Patients</c> to read a patient through
/// <c>GetPatientForAppointmentBookingAsync</c>, the same bar as the regular read. The ownership half is
/// proven in <c>PatientsAppServiceBookingReadAccessTests</c>.
///
/// <para><b>Why an id that cannot be found.</b> The permission step runs BEFORE the patient lookup. A
/// refused internal caller gets <see cref="AbpAuthorizationException"/> before any lookup; an admitted
/// one reaches the lookup and gets a not-found. Delete the permission step and the refused caller also
/// reaches the lookup and fails this test by name. The same technique as
/// <c>PatientBookingUpdateAuthorizationTests</c>.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class PatientBookingReadPermissionTests : CaseEvaluationRealAuthorizationTestBase
{
    private static readonly Guid UnknownPatientId = Guid.Parse("7e1a0c11-5980-4000-9000-0000abcd99f1");

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PatientBookingReadPermissionTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task An_internal_role_without_the_patient_permission_is_refused_before_the_lookup()
    {
        var outcome = await CallAsync(InternalRoleWithoutPatientEditName);

        outcome.ShouldBeOfType<AbpAuthorizationException>(
            "an internal caller without CaseEvaluation.Patients must be refused before the patient is "
            + "looked up. Any other exception means the lookup ran, i.e. the permission step is missing "
            + "or no longer runs first.");
    }

    [Fact]
    public async Task Intake_staff_with_the_production_grants_reach_the_lookup()
    {
        var outcome = await CallAsync(InternalUserRoleDataSeedContributor.IntakeStaffRoleName);

        outcome.ShouldBeOfType<Volo.Abp.Domain.Entities.EntityNotFoundException>(
            "Intake Staff hold CaseEvaluation.Patients in production, so they must reach the lookup and "
            + "get the staff not-found for the unknown id.");
    }

    private async Task<Exception> CallAsync(string role)
    {
        var fixture = await GetFixtureAsync();
        Exception? caught = null;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), role))
            {
                try
                {
                    await ServiceProvider.GetRequiredService<IPatientsAppService>()
                        .GetPatientForAppointmentBookingAsync(UnknownPatientId);
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }
        }, requiresNew: true);

        caught.ShouldNotBeNull(
            "the call targets an id that does not exist, so it must throw either a refusal or a "
            + "not-found. Returning normally means this test no longer exercises what it claims.");
        return caught!;
    }
}
