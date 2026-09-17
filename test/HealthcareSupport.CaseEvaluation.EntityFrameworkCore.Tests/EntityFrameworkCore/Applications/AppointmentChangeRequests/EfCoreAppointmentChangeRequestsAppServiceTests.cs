using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Integration coverage for <see cref="AppointmentChangeRequestsAppService"/> -- the SUBMIT side
/// of the cancel / reschedule lifecycle plus the active-request read. Nothing in the repository
/// reached this class before: grepping the test tree for its name returns exactly one hit, and
/// that hit is a <c>Skip</c> string naming the separate approval partial
/// (<c>AppointmentChangeRequestsApprovalAppService</c>, a DIFFERENT class, out of scope here).
///
/// <para>WHAT THIS FILE PINS -- the seams that live in the AppService itself, not in its
/// collaborators. Three of them:</para>
/// <list type="number">
/// <item><description>The internal/external SPLIT. One role check
/// (<c>BookingFlowRoles.IsInternalUserCaller</c>) is threaded into three different decisions:
/// whether a Pending appointment may be cancelled at all (B1), which booking horizon a proposed
/// reschedule slot is measured against (60 external / 90 internal), and which consent shape is
/// issued. Each direction is pinned separately, because hardcoding the flag either way is a
/// one-character change that a same-direction-only test would not catch.</description></item>
/// <item><description>WHICH access gate the flow composes -- the BROAD
/// <c>CanRequestChangeAsync</c> (F-013) rather than the slim <c>CanEditAsync</c> -- and that the
/// flow keeps its OWN error code when the gate refuses.</description></item>
/// <item><description>That a reschedule submit solicits NO consent (phase 4b), asserted against a
/// fixture where both sides DO have a representative, so the guarantee is a real withholding
/// rather than an empty-fixture accident.</description></item>
/// </list>
///
/// <para>WHAT THIS FILE DOES NOT PIN, deliberately:</para>
/// <list type="bullet">
/// <item><description>The booking-policy arithmetic itself. <c>BookingPolicyValidatorUnitTests</c>
/// already covers <c>EvaluateBookingPolicy</c> across every branch; the facts here assert only
/// that the AppService hands it the correct role flag.</description></item>
/// <item><description>The nine access pathways. <c>AppointmentReadAccessGuardTests</c> already
/// pins each one for both gates; the facts here assert only which gate is called.</description></item>
/// <item><description>Authorization. <c>AddAlwaysAllowAuthorization()</c> runs in the test module,
/// so every <c>[Authorize]</c> is a no-op and a test asserting an authorization failure could not
/// fail. The refusals below are per-row BUSINESS rules, not the authorization pipeline.</description></item>
/// <item><description>The cancel path's consent-ISSUING branches (the service's lines that
/// auto-grant a side and token the opposing one). See the block comment above the cancellation
/// facts: they are unobservable in this rig and faking them would assert invented scaffolding.
/// The two consent-SKIP branches, which are observable and distinguishable, ARE pinned.</description></item>
/// </list>
///
/// <para>MEMBERS THAT CANNOT BE REACHED THROUGH THE PUBLIC SURFACE, so no Fact pretends to cover
/// them:</para>
/// <list type="bullet">
/// <item><description><c>input == null</c> (both submit methods). ABP's
/// <c>MethodInvocationValidator</c> rejects a null non-optional, non-primitive parameter with
/// <c>AbpValidationException</c> before the method body runs.</description></item>
/// <item><description><c>string.IsNullOrWhiteSpace(input.Reason)</c> /
/// <c>(input.ReScheduleReason)</c>. Both DTO properties carry <c>[Required]</c>, and
/// <c>RequiredAttribute</c> trims, so a blank string never reaches the service's own guard. A test
/// here would pin the attribute while appearing to pin the service. Same inversion already
/// recorded at <c>AppointmentsAppServiceTests.cs:85</c> for the FluentValidation half.</description></item>
/// <item><description><c>appointment == null</c> on the reschedule path.
/// <c>EnsureCanEditAsync</c> runs first and its
/// <c>AppointmentReadAccessGuard.CanRequestChangeAsync(Guid)</c> calls <c>GetAsync</c>, which
/// throws the SAME exception type for the SAME entity first.</description></item>
/// <item><description>The <c>!ConsentGatingEnabled</c> branch -- a compile-time
/// <c>const bool = true</c>, so the branch is dead -- and <c>!changeRequest.TenantId.HasValue</c>,
/// which the manager fills from the appointment.</description></item>
/// <item><description>A non-existent proposed slot. Reachable, but the manager throws the same
/// exception type for the same entity a few lines later, so no assertion can tell the two
/// apart.</description></item>
/// </list>
///
/// <para>ALL DATA SYNTHETIC: <c>TEST-</c> identifiers and <c>@test.local</c> addresses (RFC-reserved,
/// unroutable). The rig is shared and accumulates across the whole run -- one SQLite connection, no
/// rollback between tests -- so every Fact seeds its own appointment, slot and appointment type
/// under a unique token and asserts only on ids it created. Nothing counts rows.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreAppointmentChangeRequestsAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentChangeRequestsAppService _changeRequestsAppService;
    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentUser _currentUser;

    public EfCoreAppointmentChangeRequestsAppServiceTests()
    {
        _changeRequestsAppService = GetRequiredService<IAppointmentChangeRequestsAppService>();
        _changeRequestRepository = GetRequiredService<IAppointmentChangeRequestRepository>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _appointmentTypeRepository = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _accessorRepository = GetRequiredService<IRepository<AppointmentAccessor, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentUser = GetRequiredService<ICurrentUser>();
    }

    // ========================================================================
    // Fixture
    // ========================================================================

    /// <summary>One seeded case: the appointment plus the rows a Fact may need to name.</summary>
    private sealed record ChangeRequestFixture(
        string Token,
        Guid AppointmentId,
        Guid PatientId,
        Guid AppointmentTypeId,
        Guid CurrentSlotId,
        string PartyEmail);

    /// <summary>
    /// The DoctorAvailability uniqueness index is
    /// <c>(TenantId, LocationId, AvailableDate, FromTime, ToTime)</c> filtered on soft-delete
    /// (CaseEvaluationSharedModelConfiguration.cs:340). Slots here accumulate across the whole
    /// run, so FromTime is drawn from a monotonic counter as well as the date varying -- either
    /// alone would eventually collide.
    /// </summary>
    private static int _slotSequence;

    private static TimeOnly NextFromTime()
    {
        var n = Interlocked.Increment(ref _slotSequence);
        return new TimeOnly(0, 0).AddMinutes(n % 1200);
    }

    private static string NewToken() => Guid.NewGuid().ToString("N").Substring(0, 8);

    /// <summary>
    /// Stamps the office id onto an <see cref="AppointmentType"/> before insert.
    ///
    /// <para>LOAD-BEARING, and the single most likely way this file breaks if removed.
    /// <c>AppointmentType</c> IS <c>IMultiTenant</c> (AppointmentType.cs:15) and the integration
    /// seeder inserts its two rows under <c>_currentTenant.Change(null)</c>, i.e. at HOST scope --
    /// the comment there calling the type "host-only (NOT IMultiTenant)" is stale. Inside
    /// <c>Change(TenantARef)</c> the automatic multi-tenant filter therefore hides every seeded
    /// type, and <c>BookingPolicyValidator.ValidateAsync</c> does a bare <c>GetAsync</c> on the
    /// appointment's type -- which would throw <c>EntityNotFoundException</c> instead of the
    /// horizon error the reschedule Facts are actually about.</para>
    ///
    /// <para>ABP stamps TenantId from <c>ICurrentTenant</c> on insert anyway (which is how the
    /// production <c>AppointmentTypeDataSeedContributor</c> gets per-office rows without ever
    /// assigning the property). Setting it explicitly costs one reflection call and removes the
    /// dependency on that behaviour, rather than leaving the fixture resting on it. The property
    /// is <c>protected set</c>, so reflection is the only way in from a test.</para>
    /// </summary>
    private static void StampTenantId(AppointmentType appointmentType, Guid tenantId)
    {
        var setter = typeof(AppointmentType)
            .GetProperty(nameof(AppointmentType.TenantId), BindingFlags.Public | BindingFlags.Instance)
            ?.GetSetMethod(nonPublic: true);
        setter.ShouldNotBeNull(
            "AppointmentType.TenantId no longer has a non-public setter; the fixture can no "
            + "longer make the type visible inside the office and every reschedule Fact would "
            + "fail on a not-found type rather than on the rule it names.");
        setter!.Invoke(appointmentType, new object?[] { tenantId });
    }

    /// <summary>
    /// Seeds one appointment in TenantA with its own office-scoped appointment type, its own
    /// current slot, and its own patient.
    ///
    /// <para>NOTHING SEEDED IS SHARED and nothing seeded is reused. Appointment1 is Pending and in
    /// TenantA, Appointment2 is Approved but in TenantB, and every seeded slot is dated 2026-06,
    /// which is in the past relative to the clock -- so a cancel against one trips the no-cancel
    /// window and a reschedule onto one trips the lead-time gate. None of them can carry these
    /// Facts.</para>
    ///
    /// <para>THE PATIENT IS CREATED WITH AN EMPTY EMAIL AND NO IDENTITY USER, and that is
    /// load-bearing rather than laziness. <c>AppointmentRecipientResolver</c> skips a blank address
    /// (<c>AddIfPresent</c> -> <c>IsNullOrWhiteSpace</c>), and with no join rows, no party-email
    /// columns, no booker identity and an unset OfficeEmail setting the appointment resolves ZERO
    /// recipients. That is the only shape under which a cancellation submit survives this rig at
    /// all -- see the block comment above the cancellation Facts. Pass
    /// <paramref name="partyEmails"/> to opt back into a represented case.</para>
    /// </summary>
    /// <param name="status">Status of the appointment itself; drives the B1 Pending-source rule.</param>
    /// <param name="currentSlotDayOffset">
    /// Days from today for the appointment's OWN slot. 30 keeps it clear of the 2-day no-cancel
    /// window with a wide margin, so the Pacific-vs-UTC anchor cannot flip it.
    /// </param>
    /// <param name="partyEmails">
    /// When true the appointment names a Side A representative (the patient email column) and a
    /// Side B one (the defense attorney email column), both at the fixture's own token address.
    /// </param>
    private async Task<ChangeRequestFixture> SeedFixtureAsync(
        AppointmentStatusType status,
        int currentSlotDayOffset,
        bool partyEmails = false)
    {
        var token = NewToken();
        var fixture = new ChangeRequestFixture(
            Token: token,
            AppointmentId: Guid.NewGuid(),
            PatientId: Guid.NewGuid(),
            AppointmentTypeId: Guid.NewGuid(),
            CurrentSlotId: Guid.NewGuid(),
            PartyEmail: $"TEST-cr-{token}@test.local");

        await WithUnitOfWorkAsync(async () =>
        {
            // Seeded OUTSIDE any impersonation, under the ambient internal admin. ABP therefore
            // stamps CreatorId with the admin's id, which means the creator pathway of the access
            // gate is NOT what admits the external callers below -- the email+role rule and the
            // accessor row are, which is exactly what those Facts claim to exercise.
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var appointmentType = new AppointmentType(
                    id: fixture.AppointmentTypeId,
                    name: $"TEST-CR-Type-{token}",
                    description: null,
                    evaluationType: null,
                    // Null MaxTimeCategory resolves to the OTHER horizon (60 days), which is the
                    // external cap the reschedule Facts measure against.
                    maxTimeCategory: null,
                    isSystem: false);
                StampTenantId(appointmentType, TenantsTestData.TenantARef);
                await _appointmentTypeRepository.InsertAsync(appointmentType, autoSave: true);

                await _doctorAvailabilityRepository.InsertAsync(
                    new DoctorAvailability(
                        id: fixture.CurrentSlotId,
                        locationId: LocationsTestData.Location1Id,
                        availableDate: DateTime.UtcNow.Date.AddDays(currentSlotDayOffset),
                        fromTime: NextFromTime(),
                        toTime: new TimeOnly(23, 59),
                        bookingStatusId: BookingStatus.Booked)
                    {
                        // Set rather than left to ABP's stamp: the appointment FKs to this row and
                        // the reschedule flow reads it back inside the office, so an accidentally
                        // host-scoped slot would fail somewhere far from its cause.
                        TenantId = TenantsTestData.TenantARef,
                    },
                    autoSave: true);

                await _patientRepository.InsertAsync(
                    new Patient(
                        id: fixture.PatientId,
                        stateId: null,
                        appointmentLanguageId: null,
                        // No identity user: closes the guard's patient-identity pathway so the
                        // named-party Facts cannot pass through it by accident.
                        identityUserId: null,
                        tenantId: TenantsTestData.TenantARef,
                        firstName: "TEST-Pat",
                        lastName: $"TEST-{token}",
                        // Empty is legal (Check.Length with minLength 0) and makes the recipient
                        // resolver skip the patient. See the remarks above.
                        email: string.Empty,
                        genderId: Gender.Male,
                        dateOfBirth: PatientsTestData.FixedDateOfBirth,
                        phoneNumberTypeId: PhoneNumberType.Work),
                    autoSave: true);

                var appointment = new Appointment(
                    id: fixture.AppointmentId,
                    patientId: fixture.PatientId,
                    identityUserId: null,
                    appointmentTypeId: fixture.AppointmentTypeId,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: fixture.CurrentSlotId,
                    appointmentDate: DateTime.UtcNow.Date.AddDays(currentSlotDayOffset),
                    // (TenantId, RequestConfirmationNumber) is a hard unique index and these rows
                    // accumulate for the whole run, so the token goes in the number.
                    requestConfirmationNumber: $"A9CR{token}",
                    appointmentStatus: status)
                {
                    TenantId = TenantsTestData.TenantARef,
                    PatientEmail = partyEmails ? fixture.PartyEmail : null,
                    DefenseAttorneyEmail = partyEmails ? $"TEST-da-{token}@test.local" : null,
                };
                await _appointmentRepository.InsertAsync(appointment, autoSave: true);
            }
        });

        return fixture;
    }

    /// <summary>
    /// A free slot the reschedule flow can propose. Separate from the fixture because the two
    /// horizon Facts vary only this date.
    /// </summary>
    private async Task<Guid> SeedFreeSlotAsync(int dayOffset)
    {
        var slotId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _doctorAvailabilityRepository.InsertAsync(
                    new DoctorAvailability(
                        id: slotId,
                        locationId: LocationsTestData.Location1Id,
                        availableDate: DateTime.UtcNow.Date.AddDays(dayOffset),
                        fromTime: NextFromTime(),
                        toTime: new TimeOnly(23, 59),
                        bookingStatusId: BookingStatus.Available)
                    {
                        TenantId = TenantsTestData.TenantARef,
                    },
                    autoSave: true);
            }
        });
        return slotId;
    }

    /// <summary>
    /// Grants an explicit Edit accessor row. Used where an external caller needs access WITHOUT
    /// becoming a notification recipient: <c>AppointmentRecipientResolver</c> never reads the
    /// accessor table, so this admits a caller while leaving the zero-recipient shape intact.
    ///
    /// <para>The user id must be a real seeded login -- AppointmentAccessor.IdentityUserId is a
    /// required FK to AbpUsers and the SQLite rig enforces foreign keys.</para>
    /// </summary>
    private Task GrantEditAccessAsync(Guid appointmentId, Guid identityUserId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _accessorRepository.InsertAsync(
                    new AppointmentAccessor(Guid.NewGuid(), identityUserId, appointmentId, AccessType.Edit)
                    {
                        TenantId = TenantsTestData.TenantARef,
                    },
                    autoSave: true);
            }
        });

    /// <summary>Reads the single change-request row for an appointment, after the submit UoW has completed.</summary>
    private Task<AppointmentChangeRequest?> FindRequestForAsync(Guid appointmentId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _changeRequestRepository.FirstOrDefaultAsync(x => x.AppointmentId == appointmentId);
            }
        });

    private Task<Appointment> ReadAppointmentAsync(Guid appointmentId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _appointmentRepository.GetAsync(appointmentId);
            }
        });

    private Task<DoctorAvailability> ReadSlotAsync(Guid slotId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _doctorAvailabilityRepository.GetAsync(slotId);
            }
        });

    /// <summary>
    /// Inserts a change request directly, with a chosen <c>CreationTime</c>.
    ///
    /// <para><c>CreationTime</c> has a protected setter, so it is reflected in the same way
    /// <c>ChangeRequestListFilterUnitTests</c> does. ABP's audit setter only assigns it when the
    /// value is still <c>default</c>, so a pre-set value survives the insert -- the ordering Fact
    /// below asserts that it did, rather than assuming it.</para>
    /// </summary>
    private async Task SeedRequestAsync(
        Guid id,
        Guid appointmentId,
        string reason,
        RequestStatusType status,
        DateTime creationTimeUtc)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var request = new AppointmentChangeRequest(
                    id: id,
                    tenantId: TenantsTestData.TenantARef,
                    appointmentId: appointmentId,
                    changeRequestType: ChangeRequestType.Cancel,
                    cancellationReason: reason,
                    reScheduleReason: null,
                    newDoctorAvailabilityId: null);

                if (status != RequestStatusType.Pending)
                {
                    // Status is protected; a decision is the only way out of Pending, and it is
                    // how production gets there too.
                    request.MarkDecided(status, Guid.NewGuid(), DateTime.UtcNow);
                }

                typeof(AppointmentChangeRequest)
                    .GetProperty(nameof(AppointmentChangeRequest.CreationTime), BindingFlags.Public | BindingFlags.Instance)
                    ?.GetSetMethod(nonPublic: true)
                    ?.Invoke(request, new object?[] { creationTimeUtc });

                await _changeRequestRepository.InsertAsync(request, autoSave: true);
            }
        });
    }

    // ========================================================================
    // RequestCancellationAsync
    //
    // THE TEMPLATE WALL, and why the consent-ISSUING branches get no Fact.
    //
    // A cancellation submit publishes AppointmentChangeRequestSubmittedEto, and
    // ChangeRequestSubmittedEmailHandler renders the AppointmentCancelledRequest template with NO
    // catch. NotificationTemplateDataSeedContributor seeds only four HOST-scoped codes (the rig
    // calls IDataSeeder.SeedAsync() with no tenant), so any render inside an office throws
    // BusinessException("CaseEvaluation:NotificationTemplate.NotFound") out of CompleteAsync.
    //
    // The handler returns early when the appointment resolves zero recipients -- which is why the
    // fixture above is built to resolve none. But issuing consent requires at LEAST one resolvable
    // recipient (ChangeRequestSideResolver reads the same IAppointmentRecipientResolver), and one
    // recipient is exactly what makes the email handler render and throw. The two requirements are
    // mutually exclusive, and the consent ETO cannot be caught instead: local unit-of-work events
    // dispatch FIFO and the submitted ETO is queued first, so the throw aborts the rest.
    //
    // So the party-initiated auto-grant/token branch and the staff-initiated both-sides branch are
    // NOT asserted here. Seeding a template to force them green would be asserting against invented
    // scaffolding. End-to-end cancel-consent coverage needs per-tenant template seeding, the same
    // fixture work already recorded at EfCoreExternalAccountAppServiceTests.cs:237 and
    // AppointmentsAppServiceTests.cs:1629.
    //
    // The two consent-SKIP branches ARE live, ARE distinguishable from one another, and are pinned.
    // ========================================================================

    [Fact]
    public async Task RequestCancellationAsync_WithAnEmptyAppointmentId_IsRefusedBeforeAnyLookup()
    {
        // Reachable: appointmentId is a bare Guid, and ABP's MethodInvocationValidator finds no
        // annotations on a primitive-extended parameter, so it arrives at the method body.
        //
        // The exception TYPE is the assertion. Deleting the guard does not make the call succeed --
        // it makes EnsureCanEditAsync run GetAsync(Guid.Empty) and throw EntityNotFoundException,
        // a different type from a different layer. Asserting "it threw something" would survive
        // that mutation.
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _changeRequestsAppService.RequestCancellationAsync(
                Guid.Empty,
                new RequestCancellationDto { Reason = "TEST-empty-id" }));
    }

    [Fact]
    public async Task RequestCancellationAsync_ByAnExternalCallerOnAPendingAppointment_IsRefused()
    {
        // B1 (2026-07-01), the EXTERNAL half: allowPendingSource is false for an external caller,
        // so a not-yet-approved appointment stays uncancellable by the party.
        //
        // Access comes from the Edit accessor row, NOT from a party-email column: the fixture is
        // seeded without party emails, so the caller's address matches nothing on the appointment
        // and the row still resolves zero notification recipients. That is deliberate. It means a
        // mutation hardcoding the flag true makes the submit SUCCEED outright, so this Fact fails
        // on a missing exception rather than trading one exception for another.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Pending, currentSlotDayOffset: 30);
        await GrantEditAccessAsync(fixture.AppointmentId, IdentityUsersTestData.Patient1UserId);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.Patient1UserId,
                   fixture.PartyEmail,
                   IdentityUsersTestData.PatientRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _changeRequestsAppService.RequestCancellationAsync(
                    fixture.AppointmentId,
                    new RequestCancellationDto { Reason = $"TEST-external-pending-{fixture.Token}" }));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved);
        }

        (await FindRequestForAsync(fixture.AppointmentId)).ShouldBeNull();
    }

    [Fact]
    public async Task RequestCancellationAsync_ByInternalStaffOnAPendingAppointment_IsAcceptedAndSolicitsNoConsent()
    {
        // Two rules in one Fact because one fixture proves both, and they are the same caller's
        // two consequences:
        //   1. B1, the INTERNAL half -- allowPendingSource is true, so a Pending appointment is
        //      cancellable by staff. Hardcode it false and this throws NotApproved.
        //   2. The staff-initiated consent shape -- neither side is the requestor, so the row is
        //      stamped with a submitter but NO requesting side, and with no representative on
        //      either side both stay NotRequired.
        //
        // The ambient principal is the internal admin, so no impersonation is needed; that IS the
        // internal caller. The zero-recipient fixture is what makes the submit survive (template
        // wall, above) AND what puts both sides out of reach of a representative.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Pending, currentSlotDayOffset: 30);
        var reason = $"TEST-staff-cancel-{fixture.Token}";

        Guid? expectedSubmitter;
        AppointmentChangeRequestDto dto;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            expectedSubmitter = _currentUser.Id;
            dto = await _changeRequestsAppService.RequestCancellationAsync(
                fixture.AppointmentId,
                new RequestCancellationDto { Reason = reason });
        }

        expectedSubmitter.ShouldNotBeNull(
            "The ambient principal carries no user id, so the submitter assertion below could not "
            + "distinguish the staff branch from the party branch.");

        dto.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
        dto.RequestStatus.ShouldBe(RequestStatusType.Pending);
        dto.CancellationReason.ShouldBe(reason);

        // Read the entity, not the DTO: SubmittedByUserId is not on the DTO, and it is the field
        // that separates this branch from the party branch below.
        var row = await FindRequestForAsync(fixture.AppointmentId);
        row.ShouldNotBeNull();
        row!.RequestingSide.ShouldBeNull();
        row!.SubmittedByUserId.ShouldBe(expectedSubmitter);
        row!.SideAConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row!.SideBConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
    }

    [Fact]
    public async Task RequestCancellationAsync_WhenTheSubmittersSideCannotBeResolved_StillCreatesTheRequest()
    {
        // The documented defensive path: an EXTERNAL submitter whose side cannot be resolved leaves
        // consent NotRequired so a Staff Supervisor mediates, rather than failing the submit.
        //
        // The role places the caller on Side A, but Side A's OPPOSING representative does not exist
        // on a zero-recipient appointment, so ChangeRequestSideResolver returns null and the service
        // bails BEFORE InitiateConsent. That is the whole difference from the staff branch above,
        // and SubmittedByUserId is where it shows: null here, set there. Move the null-return below
        // InitiateConsent and this Fact fails while the staff one still passes.
        //
        // Approved (not Pending) so the B1 rule is satisfied and cannot mask the assertion.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);
        await GrantEditAccessAsync(fixture.AppointmentId, IdentityUsersTestData.Patient1UserId);
        var reason = $"TEST-unresolved-side-{fixture.Token}";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(
                   _principalAccessor,
                   IdentityUsersTestData.Patient1UserId,
                   IdentityUsersTestData.PatientRoleName))
        {
            var dto = await _changeRequestsAppService.RequestCancellationAsync(
                fixture.AppointmentId,
                new RequestCancellationDto { Reason = reason });

            dto.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
            dto.RequestStatus.ShouldBe(RequestStatusType.Pending);
        }

        var row = await FindRequestForAsync(fixture.AppointmentId);
        row.ShouldNotBeNull();
        row!.CancellationReason.ShouldBe(reason);
        row!.RequestingSide.ShouldBeNull();
        row!.SubmittedByUserId.ShouldBeNull();
        row!.SideAConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row!.SideBConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
    }

    [Fact]
    public async Task RequestCancellationAsync_ByAnUnrelatedCaller_IsRefusedWithTheChangeRequestErrorCode()
    {
        // The CODE is the point, not the refusal. EnsureCanEditAsync deliberately calls the guard's
        // non-throwing predicate and raises its OWN ChangeRequestEditAccessRequired, so the
        // change-request contract stays stable even though the rule is shared with the read gate.
        // Swapping the body for the guard's own EnsureCanEditAsync would still refuse this caller,
        // but with AppointmentAccessDenied -- a contract change the client would see. Deleting the
        // gate outright lets the submit succeed and this fails on the missing exception.
        //
        // The caller is a real seeded login holding a real external role, matched by NOTHING on the
        // appointment: not the creator, no accessor row, no party-email column, and the patient row
        // has no identity user.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.ClaimExaminer1UserId,
                   IdentityUsersTestData.ClaimExaminer1Email,
                   IdentityUsersTestData.ClaimExaminerRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _changeRequestsAppService.RequestCancellationAsync(
                    fixture.AppointmentId,
                    new RequestCancellationDto { Reason = $"TEST-stranger-{fixture.Token}" }));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestEditAccessRequired);
        }

        (await FindRequestForAsync(fixture.AppointmentId)).ShouldBeNull();
    }

    // ========================================================================
    // RequestRescheduleAsync
    //
    // No template wall on this path: ChangeRequestSubmittedEmailHandler returns early for a
    // reschedule (phase 4c), the clinical-staff email is cancel-only, and the parent's
    // Approved -> RescheduleRequested transition is not a status StatusChangeEmailHandler handles.
    // These are therefore the strongest Facts in the file -- full end-to-end submits.
    // ========================================================================

    [Fact]
    public async Task RequestRescheduleAsync_WithAnEmptyAppointmentId_IsRefusedBeforeAnyLookup()
    {
        // Guard-line coverage, same shape and same reasoning as the cancellation equivalent: the
        // exception TYPE distinguishes the guard from the EntityNotFoundException that replaces it
        // when the guard is deleted.
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _changeRequestsAppService.RequestRescheduleAsync(
                Guid.Empty,
                new RequestRescheduleDto { ReScheduleReason = "TEST-empty-id" }));
    }

    [Fact]
    public async Task RequestRescheduleAsync_ByAnExternalCaller_IsBoundByTheSixtyDayHorizon()
    {
        // The role-aware horizon (2026-06-11), external half. A reschedule re-picks a date, so an
        // external requester is held to the same per-type cap the booking flow enforces (OTHER = 60
        // by default) rather than the internal ceiling (90).
        //
        // 75 days is chosen to sit between the two with 15 days of margin on the near side and 14 on
        // the far side, so the Pacific-vs-UTC anchor -- which can move "today" by one day -- cannot
        // reach either boundary. Hardcode isInternalCaller true and the slot becomes legal and this
        // Fact fails on the missing exception.
        var fixture = await SeedFixtureAsync(
            AppointmentStatusType.Approved, currentSlotDayOffset: 30, partyEmails: true);
        var proposedSlotId = await SeedFreeSlotAsync(dayOffset: 75);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.Patient1UserId,
                   fixture.PartyEmail,
                   IdentityUsersTestData.PatientRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _changeRequestsAppService.RequestRescheduleAsync(
                    fixture.AppointmentId,
                    new RequestRescheduleDto
                    {
                        NewDoctorAvailabilityId = proposedSlotId,
                        ReScheduleReason = $"TEST-external-horizon-{fixture.Token}",
                    }));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentBookingDatePastMaxHorizon);
        }

        // Nothing was written: no request row, the proposed slot was not held, and the appointment
        // did not move. A gate that threw after mutating would be worse than one that did not throw.
        (await FindRequestForAsync(fixture.AppointmentId)).ShouldBeNull();
        (await ReadSlotAsync(proposedSlotId)).BookingStatusId.ShouldBe(BookingStatus.Available);
        (await ReadAppointmentAsync(fixture.AppointmentId)).AppointmentStatus
            .ShouldBe(AppointmentStatusType.Approved);
    }

    [Fact]
    public async Task RequestRescheduleAsync_ByInternalStaff_MayReachPastTheExternalHorizon()
    {
        // The same horizon rule from the other side, on the same 75-day slot the external caller was
        // refused: internal staff are bound by the 90-day ceiling instead, so this succeeds. Without
        // BOTH directions, hardcoding the flag either way would still leave one Fact green.
        //
        // Ambient principal = the internal admin.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);
        var proposedSlotId = await SeedFreeSlotAsync(dayOffset: 75);
        var reason = $"TEST-internal-horizon-{fixture.Token}";

        AppointmentChangeRequestDto dto;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            dto = await _changeRequestsAppService.RequestRescheduleAsync(
                fixture.AppointmentId,
                new RequestRescheduleDto
                {
                    NewDoctorAvailabilityId = proposedSlotId,
                    ReScheduleReason = reason,
                });
        }

        dto.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
        dto.RequestStatus.ShouldBe(RequestStatusType.Pending);
        dto.NewDoctorAvailabilityId.ShouldBe(proposedSlotId);
        dto.ReScheduleReason.ShouldBe(reason);

        // The two side effects the endpoint's contract promises: the proposed slot is HELD, and the
        // parent appointment moves out of Approved.
        (await ReadSlotAsync(proposedSlotId)).BookingStatusId.ShouldBe(BookingStatus.Reserved);
        (await ReadAppointmentAsync(fixture.AppointmentId)).AppointmentStatus
            .ShouldBe(AppointmentStatusType.RescheduleRequested);
    }

    [Fact]
    public async Task RequestRescheduleAsync_SubmittedByAParty_SolicitsNoConsent()
    {
        // Phase 4b (2026-08-04): a reschedule submit issues NO consent, because after 4b there is no
        // date at submit time to consent TO -- the consent round is issued when staff confirm a date.
        //
        // A NEGATIVE GUARANTEE NEEDS THE THING IT WITHHOLDS TO EXIST. This fixture names a
        // representative on BOTH sides (patient email = Side A, defense attorney email = Side B), so
        // ChangeRequestSideResolver would resolve a requesting side AND an opposing representative
        // if the service asked it to. Against the zero-recipient fixture used elsewhere, adding an
        // IssueConsentAndNotifyAsync call to the reschedule path would change nothing and this Fact
        // would pass with the guarantee broken.
        //
        // The mutation is observable here precisely because the consent email handler CATCHES
        // NotificationTemplateNotFound, so the added call would complete rather than blowing up the
        // unit of work.
        var fixture = await SeedFixtureAsync(
            AppointmentStatusType.Approved, currentSlotDayOffset: 30, partyEmails: true);
        var reason = $"TEST-party-reschedule-{fixture.Token}";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.Patient1UserId,
                   fixture.PartyEmail,
                   IdentityUsersTestData.PatientRoleName))
        {
            // No slot proposed: the normal external path since 4b.
            var dto = await _changeRequestsAppService.RequestRescheduleAsync(
                fixture.AppointmentId,
                new RequestRescheduleDto { ReScheduleReason = reason });

            dto.NewDoctorAvailabilityId.ShouldBeNull();
        }

        var row = await FindRequestForAsync(fixture.AppointmentId);
        row.ShouldNotBeNull();
        row!.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
        row!.ReScheduleReason.ShouldBe(reason);
        // If consent were issued: RequestingSide would be SideA, SideA would be auto-granted to
        // Approved, SideB would be tokened to Pending, and SubmittedByUserId would be stamped.
        row!.RequestingSide.ShouldBeNull();
        row!.SubmittedByUserId.ShouldBeNull();
        row!.SideAConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row!.SideBConsentStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row!.SideAConsentTokenHash.ShouldBeNull();
        row!.SideBConsentTokenHash.ShouldBeNull();
    }

    [Fact]
    public async Task RequestRescheduleAsync_ByANamedPartyWhoIsNotTheBooker_IsAllowed()
    {
        // F-013 (2026-06-23): the flow composes the BROAD CanRequestChangeAsync, not the slim
        // CanEditAsync. The slim gate admits only the internal caller, the booker and an
        // Edit-accessor, and on a paralegal-booked appointment that 403'd the patient and the
        // attorney-of-record off their own case.
        //
        // This caller is admitted by ONE pathway only -- the email+role rule against the appointment's
        // patient-email column. The fixture closes every other door on purpose: the creator is the
        // ambient admin (the fixture seeds outside impersonation), no accessor row exists, and the
        // patient row has no identity user. AppointmentReadAccessGuardTests already pins that the
        // slim gate DENIES exactly this caller, so swapping the gate back makes this fail.
        var fixture = await SeedFixtureAsync(
            AppointmentStatusType.Approved, currentSlotDayOffset: 30, partyEmails: true);
        var reason = $"TEST-named-party-{fixture.Token}";

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.Patient1UserId,
                   fixture.PartyEmail,
                   IdentityUsersTestData.PatientRoleName))
        {
            var dto = await _changeRequestsAppService.RequestRescheduleAsync(
                fixture.AppointmentId,
                new RequestRescheduleDto { ReScheduleReason = reason });

            dto.AppointmentId.ShouldBe(fixture.AppointmentId);
            dto.RequestStatus.ShouldBe(RequestStatusType.Pending);
        }

        (await ReadAppointmentAsync(fixture.AppointmentId)).AppointmentStatus
            .ShouldBe(AppointmentStatusType.RescheduleRequested);
    }

    // ========================================================================
    // GetActiveForAppointmentAsync
    // ========================================================================

    [Fact]
    public async Task GetActiveForAppointmentAsync_WithAnEmptyAppointmentId_ReturnsNullWithoutConsultingTheReadGate()
    {
        // The only endpoint of the three that answers an empty id with null rather than an
        // exception, because the UI polls it for the consent indicator. Deleting the guard sends
        // Guid.Empty into EnsureCanReadAsync, whose GetAsync throws EntityNotFoundException -- so
        // this returns nothing rather than null.
        var result = await _changeRequestsAppService.GetActiveForAppointmentAsync(Guid.Empty);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveForAppointmentAsync_WithOnlyADecidedRequest_ReturnsNull()
    {
        // "Active" means Pending. A NEGATIVE GUARANTEE NEEDS THE EXCLUDED ROW TO EXIST: against an
        // appointment with no requests at all this would return null with the status filter deleted,
        // and prove nothing. The Rejected row is seeded precisely so that dropping
        // `&& cr.RequestStatus == RequestStatusType.Pending` returns it and fails this.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);
        await SeedRequestAsync(
            id: Guid.NewGuid(),
            appointmentId: fixture.AppointmentId,
            reason: $"TEST-decided-{fixture.Token}",
            status: RequestStatusType.Rejected,
            creationTimeUtc: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        AppointmentChangeRequestDto? result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _changeRequestsAppService.GetActiveForAppointmentAsync(fixture.AppointmentId);
        }

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveForAppointmentAsync_WithSeveralRequests_ReturnsTheNewestPendingOne()
    {
        // Two rules at once, and the fixture is built so each has its own failing mutation:
        //   - ordering: flipping OrderByDescending to ascending returns the OLDER pending row;
        //   - filtering: dropping the Pending filter returns the NEWEST row, which is Rejected.
        // Neither mutation can hide behind the other, because the newest row overall is the one the
        // status filter is meant to exclude.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);
        var olderPendingId = Guid.NewGuid();
        var newerPendingId = Guid.NewGuid();
        var newestRejectedId = Guid.NewGuid();

        var older = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 1, 2, 12, 0, 0, DateTimeKind.Utc);
        var newest = new DateTime(2026, 1, 3, 12, 0, 0, DateTimeKind.Utc);

        await SeedRequestAsync(olderPendingId, fixture.AppointmentId, $"TEST-older-{fixture.Token}", RequestStatusType.Pending, older);
        await SeedRequestAsync(newerPendingId, fixture.AppointmentId, $"TEST-newer-{fixture.Token}", RequestStatusType.Pending, newer);
        await SeedRequestAsync(newestRejectedId, fixture.AppointmentId, $"TEST-newest-{fixture.Token}", RequestStatusType.Rejected, newest);

        // FIXTURE PRECONDITION, not a redundant assertion. The whole Fact rests on the reflected
        // CreationTime surviving the insert (ABP's audit setter only writes it when it is still
        // default). If that ever stops holding, all three rows get near-identical timestamps, the
        // ordering becomes arbitrary, and this Fact would go intermittently green for the wrong
        // reason. Assert it instead, so the failure names the cause.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                (await _changeRequestRepository.GetAsync(olderPendingId)).CreationTime.ShouldBe(
                    older, "The seeded CreationTime did not survive the insert; the ordering assertion below would be meaningless.");
                (await _changeRequestRepository.GetAsync(newerPendingId)).CreationTime.ShouldBe(newer);
                (await _changeRequestRepository.GetAsync(newestRejectedId)).CreationTime.ShouldBe(newest);
            }
        });

        AppointmentChangeRequestDto? result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _changeRequestsAppService.GetActiveForAppointmentAsync(fixture.AppointmentId);
        }

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(newerPendingId);
        // Asserting further mapped fields as well proves the mapper ran rather than the endpoint
        // returning a default-constructed DTO.
        result!.AppointmentId.ShouldBe(fixture.AppointmentId);
        result!.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
        result!.RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public async Task GetActiveForAppointmentAsync_ByACallerWhoCannotReadTheAppointment_IsRefused()
    {
        // This endpoint answers "who is contesting this appointment, and where does each side
        // stand" -- so it must not answer for a stranger. Deleting the EnsureCanReadAsync call
        // returns the DTO to a caller matched by nothing on the appointment.
        var fixture = await SeedFixtureAsync(AppointmentStatusType.Approved, currentSlotDayOffset: 30);
        await SeedRequestAsync(
            id: Guid.NewGuid(),
            appointmentId: fixture.AppointmentId,
            reason: $"TEST-gated-read-{fixture.Token}",
            status: RequestStatusType.Pending,
            creationTimeUtc: new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.RunWithEmail(
                   _principalAccessor,
                   IdentityUsersTestData.ClaimExaminer1UserId,
                   IdentityUsersTestData.ClaimExaminer1Email,
                   IdentityUsersTestData.ClaimExaminerRoleName))
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _changeRequestsAppService.GetActiveForAppointmentAsync(fixture.AppointmentId));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentAccessDenied);
        }
    }
}
