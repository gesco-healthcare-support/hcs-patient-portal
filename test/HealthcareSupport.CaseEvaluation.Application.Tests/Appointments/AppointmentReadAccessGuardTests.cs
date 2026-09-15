using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pins <see cref="AppointmentReadAccessGuard"/>'s edit and change-request gates.
///
/// <para>The point is the DIFFERENCE between the two. <c>CanEditAsync</c> is the SLIM gate
/// (internal / creator-booker / Edit-accessor). <c>CanRequestChangeAsync</c> is the BROAD gate,
/// which also admits the patient identity and any named party matched by the email+role rule.
/// The guard's own docstring records the regression that motivated the split: the change-request
/// flow used the slim gate and so wrongly 403'd the named attorney-of-record and the patient on a
/// paralegal-booked appointment. Several facts below are paired across the two gates specifically
/// so that collapsing one into the other fails here rather than in production.</para>
///
/// <para>Every fact builds its own appointment rather than using a seeded one: the integration
/// seed sets no <c>BookedByUserId</c>, no <c>CreatorId</c> and none of the four party-email
/// columns, so a seeded appointment cannot exercise the creator or email+role pathways at all.</para>
///
/// <para>Every database interaction is wrapped in <c>WithUnitOfWorkAsync</c> with the tenant scope
/// nested inside it, mirroring <c>AppointmentRepositoryTests</c>. This is load-bearing here in a way
/// it is not for the AppService tests next door: ABP opens a unit of work around an application
/// service automatically, but this guard is resolved and called directly, so without an explicit
/// unit of work each <c>autoSave</c> insert completes and disposes its own ambient context and the
/// next call fails with ObjectDisposedException.</para>
/// </summary>
public abstract class AppointmentReadAccessGuardTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentReadAccessGuard _guard;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected AppointmentReadAccessGuardTests()
    {
        _guard = GetRequiredService<AppointmentReadAccessGuard>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _accessorRepository = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    /// <summary>
    /// The parties on one fixture appointment.
    /// </summary>
    private sealed record GuardFixture(
        Guid AppointmentId,
        Guid BookerUserId,
        Guid EditAccessorUserId,
        Guid ViewAccessorUserId);

    /// <summary>
    /// Builds an appointment in TenantA carrying every pathway the two gates can match:
    /// a booker, the seeded Patient1 identity, all four party-email columns, and BOTH an
    /// Edit accessor and a View-only accessor.
    ///
    /// <para>Seeding both accessor types is load-bearing, not setup noise. A negative guarantee
    /// cannot be proven against a fixture that lacks the thing being rejected: with only a
    /// View-only row present, "View-only is denied" would also pass if the Edit-accessor check
    /// were deleted outright, because nothing would be admitted either way. The Edit row is the
    /// contrast case that makes the denial mean something.</para>
    ///
    /// <para>The insert runs while impersonating the booker so the guard's
    /// <c>CreatorId ?? BookedByUserId</c> coalesce is deterministic: ABP's audit interceptor
    /// stamps CreatorId from the current user, but skips on a tenant-claim mismatch. Either way
    /// the coalesce resolves to the booker, so these facts do not depend on which happens.</para>
    /// </summary>
    private async Task<GuardFixture> CreateFixtureAsync()
    {
        // The two accessor ids MUST be real seeded logins. AppointmentAccessor.IdentityUserId is a
        // required FK to AbpUsers (CaseEvaluationSharedModelConfiguration.cs:792) and the SQLite rig
        // enforces foreign keys (CaseEvaluationEntityFrameworkCoreTestModule.cs:123-129), so a fresh
        // Guid fails the insert. Impersonation is unaffected by this -- it writes nothing -- which is
        // why callers elsewhere in this class can still be invented ids.
        //
        // The booker is deliberately the opposite: BookedByUserId has NO foreign key of its own
        // (no HasOne is declared for it), so an id belonging to nobody inserts cleanly, and that is
        // what we want -- it guarantees the booker cannot satisfy any pathway except creator.
        var fixture = new GuardFixture(
            AppointmentId: Guid.NewGuid(),
            BookerUserId: Guid.NewGuid(),
            EditAccessorUserId: IdentityUsersTestData.DefenseAttorney1UserId,
            ViewAccessorUserId: IdentityUsersTestData.ClaimExaminer1UserId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(
                       _principalAccessor,
                       fixture.BookerUserId,
                       IdentityUsersTestData.ApplicantAttorneyRoleName))
            {
                var appointment = new Appointment(
                    id: fixture.AppointmentId,
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.Patient1UserId,
                    appointmentTypeId: LocationsTestData.AppointmentType1Id,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                    // Unique per fixture: the index on (TenantId, RequestConfirmationNumber) is a
                    // hard unique constraint and these accumulate across the shared collection.
                    requestConfirmationNumber: $"A9-GUARD-{Guid.NewGuid():N}",
                    appointmentStatus: AppointmentStatusType.Pending)
                {
                    TenantId = TenantsTestData.TenantARef,
                    PatientEmail = IdentityUsersTestData.Patient1Email,
                    ApplicantAttorneyEmail = IdentityUsersTestData.ApplicantAttorney1Email,
                    DefenseAttorneyEmail = IdentityUsersTestData.DefenseAttorney1Email,
                    ClaimExaminerEmail = IdentityUsersTestData.ClaimExaminer1Email,
                };
                appointment.RecordBookedBy(fixture.BookerUserId);
                await _appointmentRepository.InsertAsync(appointment, autoSave: true);

                await _accessorRepository.InsertAsync(
                    new AppointmentAccessor(
                        Guid.NewGuid(),
                        fixture.EditAccessorUserId,
                        fixture.AppointmentId,
                        AccessType.Edit)
                    { TenantId = TenantsTestData.TenantARef },
                    autoSave: true);

                await _accessorRepository.InsertAsync(
                    new AppointmentAccessor(
                        Guid.NewGuid(),
                        fixture.ViewAccessorUserId,
                        fixture.AppointmentId,
                        AccessType.View)
                    { TenantId = TenantsTestData.TenantARef },
                    autoSave: true);
            }
        });

        return fixture;
    }

    private Task<bool> CanEditAsAsync(GuardFixture fixture, Guid userId, string? email, params string[] roles) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, email, roles))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                return await _guard.CanEditAsync(appointment);
            }
        });

    private Task<bool> CanRequestChangeAsAsync(GuardFixture fixture, Guid userId, string? email, params string[] roles) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.RunWithEmail(_principalAccessor, userId, email, roles))
            {
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                return await _guard.CanRequestChangeAsync(appointment);
            }
        });

    // ------------------------------------------------------------------------
    // CanEditAsync -- the SLIM gate: internal / creator-booker / Edit-accessor.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CanEdit_InternalCaller_IsAllowed()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(
            fixture,
            IdentityUsersTestData.HostAdminId,
            IdentityUsersTestData.HostAdminEmail,
            IdentityUsersTestData.HostAdminRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanEdit_Booker_IsAllowed()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(
            fixture,
            fixture.BookerUserId,
            null,
            IdentityUsersTestData.ApplicantAttorneyRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanEdit_EditAccessor_IsAllowed()
    {
        // No email and no role claim: the accessor row is the only pathway that can fire, so a
        // pass here cannot be coming from anywhere else.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(fixture, fixture.EditAccessorUserId, null);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanEdit_ViewOnlyAccessor_IsDenied()
    {
        // The contrast case for CanEdit_EditAccessor_IsAllowed. Both rows exist on the same
        // appointment, so deleting the AccessType.Edit clause in AppointmentAccessRules flips
        // this pair rather than leaving both green.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(fixture, fixture.ViewAccessorUserId, null);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanEdit_PatientIdentity_IsDenied_BecauseTheEditGateIsSlim()
    {
        // Pairs with CanRequestChange_PatientIdentity_IsAllowed. The patient is deliberately
        // NOT admitted by the slim edit gate; if the two gates are ever collapsed into one,
        // exactly one of this pair breaks.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(
            fixture,
            IdentityUsersTestData.Patient1UserId,
            IdentityUsersTestData.Patient1Email,
            IdentityUsersTestData.PatientRoleName);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanEdit_NamedPartyByEmailAndRole_IsDenied_BecauseTheSlimGateHasNoEmailRule()
    {
        // Pairs with CanRequestChange_NamedPartyByEmailAndRole_IsAllowed.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanEditAsAsync(
            fixture,
            IdentityUsersTestData.ApplicantAttorney1UserId,
            IdentityUsersTestData.ApplicantAttorney1Email,
            IdentityUsersTestData.ApplicantAttorneyRoleName);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanEdit_ByAppointmentId_AgreesWithTheEntityOverload()
    {
        var fixture = await CreateFixtureAsync();

        var (byId, byEntity) = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principalAccessor, fixture.EditAccessorUserId))
            {
                var fromId = await _guard.CanEditAsync(fixture.AppointmentId);
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                var fromEntity = await _guard.CanEditAsync(appointment);
                return (fromId, fromEntity);
            }
        });

        byId.ShouldBeTrue();
        byId.ShouldBe(byEntity);
    }

    // ------------------------------------------------------------------------
    // CanRequestChangeAsync -- the BROAD gate: the slim pathways PLUS the
    // patient identity and the email+role named-party rule.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CanRequestChange_InternalCaller_IsAllowed()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            IdentityUsersTestData.HostAdminId,
            IdentityUsersTestData.HostAdminEmail,
            IdentityUsersTestData.HostAdminRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestChange_Booker_IsAllowed()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            fixture.BookerUserId,
            null,
            IdentityUsersTestData.ApplicantAttorneyRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestChange_PatientIdentity_IsAllowed()
    {
        // The regression this gate exists for: on a paralegal-booked appointment the patient
        // is not the creator and holds no accessor row, and the old slim gate 403'd them off
        // their own appointment. No email claim here, so this passes through the id-based
        // patient pathway rather than the email+role rule.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            IdentityUsersTestData.Patient1UserId,
            null,
            IdentityUsersTestData.PatientRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestChange_EditAccessor_IsAllowed()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(fixture, fixture.EditAccessorUserId, null);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestChange_ViewOnlyAccessor_IsDenied()
    {
        // A View-only accessor may read, but may not move someone else's appointment.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(fixture, fixture.ViewAccessorUserId, null);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanRequestChange_NamedPartyByEmailAndRole_IsAllowed()
    {
        // The attorney-of-record: named on the appointment by email column only -- not the
        // creator, no accessor row, no id-based link. This is the fact that requires
        // RunWithEmail; under WithCurrentUser.Run the caller email is null and the email+role
        // rule short-circuits to false before it can be exercised.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            IdentityUsersTestData.ApplicantAttorney1UserId,
            IdentityUsersTestData.ApplicantAttorney1Email,
            IdentityUsersTestData.ApplicantAttorneyRoleName);

        allowed.ShouldBeTrue();
    }

    [Fact]
    public async Task CanRequestChange_MatchingEmailButWrongRole_IsDenied()
    {
        // The role half of the email+role rule. Same address as the appointment's applicant-
        // attorney column, but the caller holds only Defense Attorney -- and the DA column on
        // this appointment is a different address. Without role-gating this would be allowed,
        // which is the leak the rule was written to close. An invented caller id is fine:
        // impersonation writes nothing, so no foreign key is involved.
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            Guid.NewGuid(),
            IdentityUsersTestData.ApplicantAttorney1Email,
            IdentityUsersTestData.DefenseAttorneyRoleName);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanRequestChange_UnrelatedCaller_IsDenied()
    {
        var fixture = await CreateFixtureAsync();

        var allowed = await CanRequestChangeAsAsync(
            fixture,
            Guid.NewGuid(),
            "TEST-outsider@test.local",
            IdentityUsersTestData.PatientRoleName);

        allowed.ShouldBeFalse();
    }

    [Fact]
    public async Task CanRequestChange_ByAppointmentId_AgreesWithTheEntityOverload()
    {
        var fixture = await CreateFixtureAsync();

        var (byId, byEntity) = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(
                       _principalAccessor,
                       IdentityUsersTestData.Patient1UserId,
                       IdentityUsersTestData.PatientRoleName))
            {
                var fromId = await _guard.CanRequestChangeAsync(fixture.AppointmentId);
                var appointment = await _appointmentRepository.GetAsync(fixture.AppointmentId);
                var fromEntity = await _guard.CanRequestChangeAsync(appointment);
                return (fromId, fromEntity);
            }
        });

        byId.ShouldBeTrue();
        byId.ShouldBe(byEntity);
    }

    // ------------------------------------------------------------------------
    // EnsureCanEditAsync -- throwing variant of the slim gate.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task EnsureCanEdit_AllowedCaller_DoesNotThrow()
    {
        var fixture = await CreateFixtureAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principalAccessor, fixture.BookerUserId))
            {
                await Should.NotThrowAsync(
                    async () => await _guard.EnsureCanEditAsync(fixture.AppointmentId));
            }
        });
    }

    [Fact]
    public async Task EnsureCanEdit_DeniedCaller_ThrowsAccessDenied()
    {
        // Deny-by-default, and it must be the shared access-denied business error rather than
        // an incidental failure -- the error code is asserted, not just the exception type.
        var fixture = await CreateFixtureAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principalAccessor, fixture.ViewAccessorUserId))
            {
                var thrown = await Should.ThrowAsync<BusinessException>(
                    async () => await _guard.EnsureCanEditAsync(fixture.AppointmentId));

                thrown.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
            }
        });
    }
}
