using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
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
/// #707 LAYER 3 -- proves that the <c>[Authorize]</c> attributes on the PHI surfaces
/// actually REFUSE a caller, against the real ABP authorization pipeline.
///
/// <para><b>WHY EVERY REFUSAL TEST HERE USES AN ID THAT CANNOT BE FOUND.</b> This is the
/// single most important thing to understand before editing this file, and getting it
/// wrong produces a test that cannot fail. (Three surfaces use an id no row carries; the
/// packet surface needs <see cref="EmptyIdProbe"/> instead, for the reason recorded
/// there.)</para>
///
/// <para>Take <c>PatientsAppService.GetFullSsnAsync</c>. It carries TWO gates, by design:
/// the declarative <c>[Authorize(Patients.RevealSsn)]</c> attribute, and an in-code
/// owner check (<c>SsnRevealAccess.CanReveal</c>) that throws
/// <see cref="AbpAuthorizationException"/> -- <b>the same exception type the attribute
/// produces</b>. Point a non-owner without the permission at a REAL patient id and the
/// call throws <c>AbpAuthorizationException</c> whether or not the attribute is present,
/// because the in-code check catches what the attribute would have. An assertion written
/// that way passes with the attribute deleted, which is precisely the class of defect
/// #707 exists to eliminate.</para>
///
/// <para>Against an id that does not exist, the two gates separate cleanly. The
/// interceptor runs BEFORE the method body, so an unpermitted caller is refused before any
/// repository lookup. A permitted caller reaches the body and the lookup fails instead.
/// The distinction is therefore:</para>
/// <list type="bullet">
///   <item><description>refused caller -&gt; <c>AbpAuthorizationException</c> (the
///   attribute fired)</description></item>
///   <item><description>permitted caller -&gt; some OTHER exception (the attribute passed
///   and the body ran)</description></item>
/// </list>
/// <para>Delete the attribute and the refused caller gets the other exception too, so the
/// test fails. Verified by mutation on 2026-09-16 -- see the PR for the measured
/// before/after on each surface.</para>
///
/// <para>The permitted-caller half of each pair is not decoration. #707 is explicit that a
/// negative guarantee cannot be proven against an empty fixture: without it, a harness that
/// refused EVERYTHING would look identical to a working one.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class PhiSurfaceAuthorizationTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string PatientRole = "Patient";
    private const string DefenseAttorneyRole = "Defense Attorney";

    /// <summary>An id no seeded row carries; see the class remarks for why this matters.</summary>
    private static readonly Guid UnknownId = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    /// <summary>
    /// The probe id for the packet surface, which needs a different one from every other
    /// surface here.
    ///
    /// <para><b>MEASURED 2026-09-16, AND THE FIRST VERSION OF THIS FILE GOT IT WRONG.</b>
    /// <c>AppointmentPacketsAppService.DownloadAsync</c> runs an in-code role check --
    /// <c>PacketVisibility.IsAllowed</c> -- that throws <see cref="AbpAuthorizationException"/>
    /// BEFORE it looks anything up. So against an unknown id the refusal test passed with
    /// the attribute deleted: the in-code check produced the same exception type the
    /// attribute would have. Mutation-testing the four surfaces together is what exposed
    /// it; three failed and this one did not.</para>
    ///
    /// <para><c>Guid.Empty</c> separates them, because the FIRST statement of that method
    /// body rejects an empty id with a <c>UserFriendlyException</c> -- before the
    /// visibility check. So an unpermitted caller still gets an authorization exception
    /// from the interceptor, while a body that runs at all throws something else.</para>
    /// </summary>
    private static readonly Guid EmptyIdProbe = Guid.Empty;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PhiSurfaceAuthorizationTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // ------------------------------------------------------------------------
    // SSN reveal -- CaseEvaluation.Patients.RevealSsn
    //
    // The contrast here is the one production actually ships: among the four external
    // roles, ExternalUserRoleDataSeedContributor grants RevealSsn to Patient and to nobody
    // else. Both roles below are real, authenticated, and hold the full booking baseline;
    // the ONLY difference between them is the permission under test.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task RevealSsn_IsRefused_ForAnExternalRoleWithoutThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertRefusedAsync(
            fixture, DefenseAttorneyRole,
            sp => sp.GetRequiredService<IPatientsAppService>().GetFullSsnAsync(UnknownId));
    }

    [Fact]
    public async Task RevealSsn_IsNotRefused_ForARoleHoldingThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertNotRefusedAsync(
            fixture, PatientRole,
            sp => sp.GetRequiredService<IPatientsAppService>().GetFullSsnAsync(UnknownId));
    }

    /// <summary>
    /// The full path, end to end: a permitted caller who is also the record owner gets the
    /// value back.
    ///
    /// <para>Nothing else in this file proves the pipeline can return data at all. Without
    /// this, a harness that threw on every call would satisfy every other assertion here --
    /// the refusals by throwing the right type, and the "not refused" cases by throwing
    /// something that merely is not an authorization exception.</para>
    /// </summary>
    [Fact]
    public async Task RevealSsn_ReturnsTheValue_ForThePermittedOwner()
    {
        var fixture = await GetFixtureAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(
                _principalAccessor, fixture.Office.BookerUserId, PatientRole))
            {
                var result = await GetRequiredService<IPatientsAppService>()
                    .GetFullSsnAsync(fixture.Office.PatientId);

                result.SocialSecurityNumber.ShouldBe(fixture.Office.PatientSsnSentinel);
            }
        }, requiresNew: true);
    }

    // ------------------------------------------------------------------------
    // Appointment documents / packets -- every production external role holds these, so
    // the refused caller is the purpose-built minimal role. It is not an empty role: see
    // MinimalRole_CanReachThePermissionItHolds below.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task AppointmentDocumentDownload_IsRefused_ForARoleWithoutThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertRefusedAsync(
            fixture, MinimalRoleName,
            sp => sp.GetRequiredService<IAppointmentDocumentsAppService>().DownloadAsync(UnknownId));
    }

    [Fact]
    public async Task AppointmentDocumentDownload_IsNotRefused_ForARoleHoldingThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertNotRefusedAsync(
            fixture, PatientRole,
            sp => sp.GetRequiredService<IAppointmentDocumentsAppService>().DownloadAsync(UnknownId));
    }

    [Fact]
    public async Task AppointmentPacketDownload_IsRefused_ForARoleWithoutThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertRefusedAsync(
            fixture, MinimalRoleName,
            sp => sp.GetRequiredService<IAppointmentPacketsAppService>().DownloadAsync(EmptyIdProbe));
    }

    [Fact]
    public async Task AppointmentPacketDownload_IsNotRefused_ForARoleHoldingThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertNotRefusedAsync(
            fixture, PatientRole,
            sp => sp.GetRequiredService<IAppointmentPacketsAppService>().DownloadAsync(EmptyIdProbe));
    }

    // ------------------------------------------------------------------------
    // Case Tracker push -- the surface that sends PHI off the box. No external role holds
    // it, so the Patient role (which holds everything else tested here) is the refused
    // caller and the purpose-built push role is the control.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CaseTrackerPush_IsRefused_ForAnExternalRoleWithoutThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertRefusedAsync(
            fixture, PatientRole,
            sp => sp.GetRequiredService<ICaseTrackerPushAppService>().PushAppointmentAsync(UnknownId));
    }

    [Fact]
    public async Task CaseTrackerPush_IsNotRefused_ForARoleHoldingThePermission()
    {
        var fixture = await GetFixtureAsync();

        await AssertNotRefusedAsync(
            fixture, PushRoleName,
            sp => sp.GetRequiredService<ICaseTrackerPushAppService>().PushAppointmentAsync(UnknownId));
    }

    // ------------------------------------------------------------------------

    /// <summary>
    /// The minimal role reaches the one permission it WAS granted.
    ///
    /// <para>This is what makes its refusals above evidence of anything. A role holding
    /// nothing is refused everywhere, including by a harness that is simply misconfigured,
    /// so its refusal would carry no information about the attribute under test.</para>
    /// </summary>
    [Fact]
    public async Task MinimalRole_CanReachThePermissionItHolds()
    {
        var fixture = await GetFixtureAsync();

        await AssertNotRefusedAsync(
            fixture, MinimalRoleName,
            sp => sp.GetRequiredService<IDoctorAvailabilitiesAppService>()
                .GetAsync(UnknownId));
    }

    // ------------------------------------------------------------------------

    private Task AssertRefusedAsync(
        AuthorizationFixture fixture,
        string role,
        Func<IServiceProvider, Task> call)
        => InvokeAsync(fixture, role, call,
            outcome => outcome.ShouldBeOfType<AbpAuthorizationException>(
                $"the {role} role does not hold the permission this method declares, so the " +
                "authorization interceptor should have refused the call before the method " +
                "body ran. Getting a different exception means the body WAS reached, which " +
                "means the [Authorize] attribute is missing or is not being enforced."));

    private Task AssertNotRefusedAsync(
        AuthorizationFixture fixture,
        string role,
        Func<IServiceProvider, Task> call)
        => InvokeAsync(fixture, role, call,
            outcome => outcome.ShouldNotBeOfType<AbpAuthorizationException>(
                $"the {role} role holds the permission this method declares, so the call " +
                "should have got past the interceptor and failed on the unknown id instead. " +
                "An authorization exception here means the harness is refusing callers it " +
                "should admit, and every refusal asserted in this file is worthless."));

    /// <summary>
    /// Runs <paramref name="call"/> as <paramref name="role"/> inside the seeded office and
    /// hands the resulting exception to <paramref name="assert"/>.
    ///
    /// <para>The caller id is a fresh Guid rather than the seeded booker so no test here
    /// accidentally satisfies an OWNER check and reports an authorization pass that came
    /// from record ownership rather than from the permission.
    /// <see cref="RevealSsn_ReturnsTheValue_ForThePermittedOwner"/> is the one place the
    /// owner path is exercised, and it says so.</para>
    /// </summary>
    private async Task InvokeAsync(
        AuthorizationFixture fixture,
        string role,
        Func<IServiceProvider, Task> call,
        Action<Exception> assert)
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

        caught.ShouldNotBeNull(
            "the call was made against an id that does not exist, so it must throw " +
            "something -- either an authorization refusal or a not-found. Returning " +
            "normally means this test is no longer exercising what it claims to.");

        assert(caught!);
    }
}
