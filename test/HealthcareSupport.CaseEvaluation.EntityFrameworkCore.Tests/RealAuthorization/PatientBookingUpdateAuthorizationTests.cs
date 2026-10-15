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
/// #598 -- the PERMISSION half of the booking patient-edit rule, on the one harness whose
/// authorization pipeline is real. Internal staff must hold <c>CaseEvaluation.Patients.Edit</c> to
/// edit a patient through <c>UpdatePatientForAppointmentBookingAsync</c>, the same bar as the
/// regular edit. The ownership half is proven in <c>PatientsAppServiceBookingUpdateAccessTests</c>.
///
/// <para><b>Why an id that cannot be found.</b> The permission step runs BEFORE the patient lookup.
/// So against an unknown id the two outcomes separate cleanly: a refused internal caller gets
/// <see cref="AbpAuthorizationException"/> before any lookup, and an admitted one reaches the
/// lookup and gets a not-found. Delete the permission step and the refused caller also reaches the
/// lookup -- its assertion then fails by name. The same technique as
/// <c>PhiSurfaceAuthorizationTests</c>.</para>
///
/// <para><b>Why the production grant set.</b> The admitted caller is the per-office "Intake Staff"
/// role seeded by the production <c>InternalUserRoleDataSeedContributor</c>. If this check ever
/// refused the grants production ships, staff saves on the appointment view page would start
/// failing; this is the test that would say so.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class PatientBookingUpdateAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    /// <summary>An id no seeded patient carries; see the class remarks.</summary>
    private static readonly Guid UnknownPatientId = Guid.Parse("7e1a0c11-5980-4000-9000-0000abcdef01");

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PatientBookingUpdateAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task An_internal_role_without_patient_edit_is_refused_before_the_lookup()
    {
        var outcome = await CallAsync(InternalRoleWithoutPatientEditName);

        outcome.ShouldBeOfType<AbpAuthorizationException>(
            "an internal caller without CaseEvaluation.Patients.Edit must be refused before the "
            + "patient is looked up. Any other exception means the lookup ran, i.e. the permission "
            + "step is missing or no longer runs first.");
    }

    [Fact]
    public async Task Intake_staff_with_the_production_grants_are_not_refused()
    {
        var outcome = await CallAsync(InternalUserRoleDataSeedContributor.IntakeStaffRoleName);

        outcome.ShouldNotBeOfType<AbpAuthorizationException>(
            "Intake Staff hold Patients.Edit in production, so they must reach the lookup and get a "
            + "not-found for the unknown id. A refusal here would break staff saves on the "
            + "appointment view page.");
    }

    /// <summary>
    /// Runs the edit against <see cref="UnknownPatientId"/> as a fresh user holding
    /// <paramref name="role"/> inside the seeded office, and returns what it threw. A fresh id, so
    /// no ownership rule can admit the caller by accident.
    /// </summary>
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
                        .UpdatePatientForAppointmentBookingAsync(UnknownPatientId, new PatientUpdateDto
                        {
                            FirstName = "TEST-First",
                            LastName = "TEST-Last",
                            Email = "TEST-booking-edit@test.local",
                        });
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
