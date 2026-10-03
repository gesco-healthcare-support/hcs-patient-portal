using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Pins the DEFAULT branch of the appointment visibility rule: a caller who matches no known
/// role is narrowed to the appointments it is a party to, not shown the whole office.
///
/// <para><b>WHY THE DEFAULT BRANCH NEEDS ITS OWN TESTS.</b> The rule used to classify a caller
/// as internal unless it held ONLY external roles, so a caller with no roles at all -- or with
/// a role the rule did not recognise -- fell through to "internal", and an internal caller sees
/// every appointment in the office. Every existing test used a caller with a real role, so that
/// branch was never exercised. The appointment list and the external-user lookup are bare
/// <c>[Authorize]</c> (signed in, nothing more), so the rule is the only thing between such a
/// caller and the office's appointments.</para>
///
/// <para><b>THE ZERO-ROLE CALLER IS NOT HYPOTHETICAL.</b> ABP's stock self-registration
/// assigns only roles flagged as default, and none is, so it created exactly this caller
/// (measured 2026-10-02). <see cref="StockSelfRegistrationRefusalTests"/> now closes that route,
/// but an account can still reach zero roles by having its roles removed, so the rule must hold
/// on its own rather than rely on the route staying shut.</para>
///
/// <para><b>THE DECOYS.</b> The seeded appointment belongs to another party, so a narrowed
/// caller must not see it; an Intake Staff caller (production grants) must, which proves the
/// appointment is visible at all. The seeded booker owns the patient record and holds no role,
/// which proves narrowing is not "deny everything" -- a zero-role caller who IS a party still
/// sees its own appointment.</para>
/// </summary>
[Collection(RealAuthorizationCollection.Name)]
public class UnrecognisedCallerVisibilityTests : CaseEvaluationRealAuthorizationTestBase
{
    private const string IntakeStaffRole = "Intake Staff";
    private const string PatientRole = "Patient";
    private const string LookupDecoyTerm = "TEST-lookupdecoy";

    private static readonly SemaphoreSlim DecoyLock = new(1, 1);
    private static bool _decoySeeded;

    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public UnrecognisedCallerVisibilityTests()
    {
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task ZeroRoleCaller_IsNarrowed_AndDoesNotSeeAnotherPartysAppointment()
    {
        var fixture = await GetFixtureAsync();

        var visible = await AsCallerAsync(fixture, Guid.NewGuid(), Array.Empty<string>(),
            sp => sp.GetRequiredService<AppointmentVisibilityService>().GetVisibleAppointmentIdsAsync());

        visible.ShouldNotBeNull(
            "null means 'no narrowing, show the whole office'. A caller with no role is not internal.");
        visible!.ShouldNotContain(fixture.Office.AppointmentId);
    }

    [Fact]
    public async Task ZeroRoleCaller_AppointmentList_ExcludesAnotherPartysAppointment()
    {
        var fixture = await GetFixtureAsync();

        var ids = await AsCallerAsync(fixture, Guid.NewGuid(), Array.Empty<string>(), async sp =>
            (await sp.GetRequiredService<IAppointmentsAppService>().GetListAsync(new GetAppointmentsInput()))
                .Items.Select(i => i.Appointment.Id).ToList());

        ids.ShouldNotContain(fixture.Office.AppointmentId,
            "the appointment list is a bare [Authorize]; a signed-in caller with no role must see " +
            "only appointments it is a party to, and it is not a party to this one.");
    }

    [Fact]
    public async Task UnrecognisedRoleCaller_IsNarrowed()
    {
        var fixture = await GetFixtureAsync();

        // The minimal role is real and holds a real permission, but is neither internal nor one of
        // the four external roles -- exactly the "role the rule does not recognise" case.
        var visible = await AsCallerAsync(fixture, Guid.NewGuid(), new[] { MinimalRoleName },
            sp => sp.GetRequiredService<AppointmentVisibilityService>().GetVisibleAppointmentIdsAsync());

        visible.ShouldNotBeNull();
        visible!.ShouldNotContain(fixture.Office.AppointmentId);
    }

    [Fact]
    public async Task InternalCaller_IsNotNarrowed_AndSeesTheAppointment()
    {
        var fixture = await GetFixtureAsync();

        var visible = await AsCallerAsync(fixture, Guid.NewGuid(), new[] { IntakeStaffRole },
            sp => sp.GetRequiredService<AppointmentVisibilityService>().GetVisibleAppointmentIdsAsync());
        visible.ShouldBeNull("an internal role is the one case that skips narrowing");

        var ids = await AsCallerAsync(fixture, Guid.NewGuid(), new[] { IntakeStaffRole }, async sp =>
            (await sp.GetRequiredService<IAppointmentsAppService>().GetListAsync(new GetAppointmentsInput()))
                .Items.Select(i => i.Appointment.Id).ToList());
        ids.ShouldContain(fixture.Office.AppointmentId,
            "the control: without it, a list that returned nothing to anyone would pass every test here.");
    }

    [Fact]
    public async Task ZeroRoleCaller_WhoIsAParty_StillSeesTheirOwnAppointment()
    {
        var fixture = await GetFixtureAsync();

        // The seeded booker owns the patient record and holds no role.
        var visible = await AsCallerAsync(fixture, fixture.Office.BookerUserId, Array.Empty<string>(),
            sp => sp.GetRequiredService<AppointmentVisibilityService>().GetVisibleAppointmentIdsAsync());

        visible.ShouldNotBeNull();
        visible!.ShouldContain(fixture.Office.AppointmentId,
            "narrowing must not become 'deny everything': the party pathways still apply.");
    }

    [Fact]
    public async Task ZeroRoleCaller_ExternalUserLookup_FindsNobody()
    {
        var fixture = await GetFixtureAsync();
        await EnsureLookupDecoyAsync(fixture);

        var count = await AsCallerAsync(fixture, Guid.NewGuid(), Array.Empty<string>(), async sp =>
            (await sp.GetRequiredService<IExternalSignupAppService>().GetExternalUserLookupAsync(LookupDecoyTerm))
                .Items.Count);

        count.ShouldBe(0,
            "a caller with no role and no appointments has no co-parties; the tenant-wide search is " +
            "for internal staff only.");
    }

    [Fact]
    public async Task InternalCaller_ExternalUserLookup_FindsTheDecoy()
    {
        var fixture = await GetFixtureAsync();
        await EnsureLookupDecoyAsync(fixture);

        var count = await AsCallerAsync(fixture, Guid.NewGuid(), new[] { IntakeStaffRole }, async sp =>
            (await sp.GetRequiredService<IExternalSignupAppService>().GetExternalUserLookupAsync(LookupDecoyTerm))
                .Items.Count);

        count.ShouldBeGreaterThan(0,
            "the control for the test above: the term does match an external user in this office.");
    }

    private async Task<T> AsCallerAsync<T>(
        AuthorizationFixture fixture, Guid userId, string[] roles, Func<IServiceProvider, Task<T>> call)
    {
        var result = default(T)!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(fixture.Office.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, userId, roles))
            {
                result = await call(ServiceProvider);
            }
        }, requiresNew: true);
        return result;
    }

    /// <summary>An external (Patient-role) user in the office whose email carries the search term.</summary>
    private async Task EnsureLookupDecoyAsync(AuthorizationFixture fixture)
    {
        await DecoyLock.WaitAsync();
        try
        {
            if (_decoySeeded)
            {
                return;
            }

            await WithUnitOfWorkAsync(async () =>
            {
                using (_currentTenant.Change(fixture.Office.OfficeId))
                {
                    var userManager = GetRequiredService<IdentityUserManager>();
                    var email = $"{LookupDecoyTerm}@example.test";
                    var user = new IdentityUser(Guid.NewGuid(), email, email, fixture.Office.OfficeId);
                    (await userManager.CreateAsync(user, "TEST-Pw1!aaaa")).Succeeded.ShouldBeTrue();
                    (await userManager.AddToRoleAsync(user, PatientRole)).Succeeded.ShouldBeTrue();
                }
            }, requiresNew: true);

            _decoySeeded = true;
        }
        finally
        {
            DecoyLock.Release();
        }
    }
}
