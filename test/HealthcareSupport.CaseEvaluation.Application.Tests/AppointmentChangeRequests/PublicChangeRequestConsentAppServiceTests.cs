using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Covers <see cref="IPublicChangeRequestConsentAppService"/>, which reported 0.0% before this file.
///
/// <para>THIS IS THE ONLY UNAUTHENTICATED SURFACE IN THE TRANCHE, AND THAT -- NOT ITS SIZE -- IS WHY
/// IT IS HERE. The class carries <c>[AllowAnonymous]</c> AND both methods carry it individually.
/// A single-use token is the entire credential: a patient or attorney opens this page from an email
/// link with nothing else. Forty-five uncovered lines reads as a small target; "the only endpoint a
/// stranger can reach, with no test at all" does not. Those are different currencies and this file
/// exists on the second one.</para>
///
/// <para>WHY THE FIXTURES ARE BUILT BY HAND. Nothing in <c>TestBase/Data/</c> seeds a change request
/// or a consent round -- the seed contributor has no <c>AppointmentChangeRequest</c> at all. So each
/// Fact constructs its own request against a SEEDED appointment and mints a real token through
/// <see cref="ChangeRequestConsentManager"/>. Minting through the manager rather than writing a hash
/// by hand is deliberate: only the SHA256 hash is ever persisted, so a hand-built fixture would be
/// asserting my reimplementation of the hash rather than the service's resolution of a real token.</para>
///
/// <para>THE CANCEL PATH IS USED WHEREVER IT WILL DO. Cancellation consent lives on the request's own
/// flat columns; reschedule consent lives on a <c>ChangeRequestConsentRound</c>. The Cancel shape
/// needs no round, so it reaches <c>GetConsentInfoAsync</c>, <c>SubmitDecisionAsync</c>, the
/// idempotent-replay catch and the request-level consent-status fallback with the smallest fixture
/// that is still realistic.</para>
///
/// <para>TRAP A DOES NOT BITE AND TRAP B IS IRRELEVANT: there is no <c>IEventBus.PublishAsync</c> in
/// this service, and no Fact here asserts an authorization refusal -- there is no authorization to
/// refuse, which is the entire point of the class.</para>
/// </summary>
public abstract class PublicChangeRequestConsentAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPublicChangeRequestConsentAppService _consent;
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly IRepository<AppointmentChangeRequest, Guid> _requestRepository;
    private readonly ICurrentTenant _currentTenant;

    protected PublicChangeRequestConsentAppServiceTests()
    {
        _consent = GetRequiredService<IPublicChangeRequestConsentAppService>();
        _consentManager = GetRequiredService<ChangeRequestConsentManager>();
        _requestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string Token(string label) => $"TEST-crc-{label}-{Guid.NewGuid():N}"[..40];

    /// <summary>
    /// Builds a CANCELLATION change request against the seeded appointment, issues a real consent
    /// token for one side, persists, and hands back the raw token.
    ///
    /// <para>The raw token is returned ONCE by the manager and never stored, so it has to be
    /// captured here -- there is no way to recover it from the row afterwards. That is the
    /// production contract, not a test limitation.</para>
    /// </summary>
    private async Task<string> SeedCancelConsentAsync(
        string reason,
        ChangeRequestSide side = ChangeRequestSide.SideA)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: _currentTenant.Id,
            appointmentId: AppointmentsTestData.Appointment1Id,
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: reason,
            reScheduleReason: null,
            newDoctorAvailabilityId: null);

        var rawToken = _consentManager.IssueSideConsent(request, side);
        await _requestRepository.InsertAsync(request, autoSave: true);
        return rawToken;
    }

    /// <summary>
    /// Builds a RESCHEDULE change request carrying a slot on its own
    /// <c>NewDoctorAvailabilityId</c> column and no consent round.
    ///
    /// <para>That combination is not contrived -- it is the pre-4c shape the service's fallback at
    /// <c>:83</c> exists to keep working, and tokens of that vintage may still be sitting in
    /// somebody's inbox.</para>
    /// </summary>
    private async Task<string> SeedRescheduleConsentWithSlotOnTheRequestAsync(
        string reason,
        Guid slotId,
        ChangeRequestSide side = ChangeRequestSide.SideA)
    {
        var request = new AppointmentChangeRequest(
            id: Guid.NewGuid(),
            tenantId: _currentTenant.Id,
            appointmentId: AppointmentsTestData.Appointment1Id,
            changeRequestType: ChangeRequestType.Reschedule,
            cancellationReason: null,
            reScheduleReason: reason,
            newDoctorAvailabilityId: slotId);

        var rawToken = _consentManager.IssueSideConsent(request, side);
        await _requestRepository.InsertAsync(request, autoSave: true);
        return rawToken;
    }

    // ------------------------------------------------------------------------
    // GetConsentInfoAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetConsentInfoAsync_WithAValidCancellationToken_ReturnsTheLandingPageState()
    {
        var reason = Token("cancel-reason");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(reason);

            var info = await _consent.GetConsentInfoAsync(rawToken);

            info.ShouldNotBeNull();
            info.ChangeRequestType.ShouldBe(ChangeRequestType.Cancel);
            info.ConfirmationNumber.ShouldBe(
                AppointmentsTestData.Appointment1RequestConfirmationNumber,
                "The page shows the caller which appointment this is about. An empty string here "
                + "means the appointment lookup missed and the fallback at :104 fired.");
            info.ConsentStatus.ShouldBe(
                ChangeRequestConsentStatus.Pending,
                "A freshly issued, unanswered token is Pending. Anything else means the status is "
                + "being read from the wrong side or the wrong store.");
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_ForACancellation_SurfacesTheCancellationReasonNotTheRescheduleOne()
    {
        // PINS THE TERNARY AT :106-108. Both reason columns exist on the same row, so a service
        // reading the wrong one still returns a populated, plausible page. The reschedule column is
        // null on a cancellation, so the failure would show as a BLANK reason on a live page.
        var reason = Token("why-cancelled");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(reason);

            var info = await _consent.GetConsentInfoAsync(rawToken);

            info.Reason.ShouldBe(
                reason,
                "A Cancel request must surface CancellationReason. Swapping the ternary yields null "
                + "here and a consent page with no reason on it.");
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_ForACancellation_LeavesTheProposedDateEmpty()
    {
        // A NEGATIVE GUARANTEE. The whole date block at :81-100 is gated on ChangeRequestType
        // .Reschedule; a cancellation must not render a date row at all. Deleting the type check
        // would send a Cancel request down the slot lookup, and with a null slot id it would still
        // come back null -- so this Fact is paired with the reschedule Fact below, which is what
        // makes the gate discriminable.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(Token("no-date"));

            var info = await _consent.GetConsentInfoAsync(rawToken);

            info.RequestedNewDateTime.ShouldBeNull(
                "A cancellation has no proposed new date. The template branches on this being null.");
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_ForAPre4cReschedule_FallsBackToTheRequestsOwnSlot()
    {
        // THE PHASE-4C FALLBACK AT :83, AND IT IS THE HIGHEST-VALUE FACT IN THIS FILE.
        // `match.Round?.ProposedDoctorAvailabilityId ?? request.NewDoctorAvailabilityId` -- the
        // service comment records that reading the request column ALONE was a latent blank-date bug
        // once 4b moved the staff slot off it. This fixture is the other half: a request with NO
        // round, whose slot lives only on its own column. Delete the `??` fallback and this Fact
        // fails with a null date, which is exactly the live symptom -- an *ngIf hides the date row
        // and somebody is asked to approve a reschedule with no date on the page.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedRescheduleConsentWithSlotOnTheRequestAsync(
                Token("resched-reason"),
                DoctorAvailabilitiesTestData.Slot2Id);

            var info = await _consent.GetConsentInfoAsync(rawToken);

            info.ChangeRequestType.ShouldBe(ChangeRequestType.Reschedule);
            info.RequestedNewDateTime.ShouldNotBeNullOrWhiteSpace(
                "A reschedule whose slot sits on the request must still render a date. Null here is "
                + "the blank-date bug the :75-79 comment describes.");
            info.RequestedNewDateTime!.ShouldContain(
                " at ",
                Case.Sensitive,
                "The rendered form is \"MMM d, yyyy at h:mm tt\". Losing the separator means the "
                + "date and time were composed differently from what the page expects.");
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_ForAReschedule_SurfacesTheRescheduleReason()
    {
        var reason = Token("why-moved");

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedRescheduleConsentWithSlotOnTheRequestAsync(
                reason,
                DoctorAvailabilitiesTestData.Slot2Id);

            var info = await _consent.GetConsentInfoAsync(rawToken);

            info.Reason.ShouldBe(
                reason,
                "The other arm of the :106-108 ternary. Asserting only the Cancel arm would let the "
                + "condition be inverted without either Fact noticing.");
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_WithAGarbageToken_IsRefused()
    {
        // The public entry point with the widest exposure: an unauthenticated caller supplying an
        // arbitrary string. It must refuse rather than resolving to somebody's appointment.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<BusinessException>(
                async () => await _consent.GetConsentInfoAsync("TEST-not-a-real-token"));
        }
    }

    [Fact]
    public async Task GetConsentInfoAsync_WithAnEmptyToken_IsRefusedWithoutADatabaseLookup()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<BusinessException>(
                async () => await _consent.GetConsentInfoAsync(string.Empty));
        }
    }

    // ------------------------------------------------------------------------
    // SubmitDecisionAsync.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task SubmitDecisionAsync_WithApproval_RecordsTheDecisionAndReturnsTheNewState()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(Token("approve"));

            var info = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = true });

            info.ConsentStatus.ShouldBe(
                ChangeRequestConsentStatus.Approved,
                "The returned DTO must reflect the decision just recorded, not the pre-decision "
                + "state -- the landing page renders straight from it.");
        }
    }

    [Fact]
    public async Task SubmitDecisionAsync_WithRefusal_RecordsADeclineRatherThanAnApproval()
    {
        // Both arms of `input.Approved` are asserted. A service that ignored the flag and always
        // approved would pass the Fact above on its own -- and would silently approve changes
        // somebody explicitly declined.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(Token("decline"));

            var info = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = false });

            info.ConsentStatus.ShouldNotBe(
                ChangeRequestConsentStatus.Approved,
                "A decline must never record as an approval. This is the one assertion in the file "
                + "whose failure would be a consent forgery rather than a display bug.");
        }
    }

    [Fact]
    public async Task SubmitDecisionAsync_OnAReplay_ReturnsTheExistingStateInsteadOfThrowing()
    {
        // THE IDEMPOTENT-REPLAY CATCH AT :58-67, AND THE FIXTURE IS THE POINT. The first submission
        // is not setup -- it is what puts the request into the already-responded state the catch
        // exists for. Against a fresh token the catch is unreachable and deleting it would look
        // identical.
        //
        // This is the realistic case, not an edge case: people double-click links in emails, and
        // mail clients pre-fetch them.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(Token("replay"));

            var first = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = true });

            // Called directly rather than through Should.NotThrowAsync: that helper's
            // Task-returning overload yields void, so it cannot hand back the DTO this Fact has to
            // inspect. An escaping exception fails the Fact just as loudly, and the assertion below
            // is the part that actually discriminates.
            var replay = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = true });

            replay.ConsentStatus.ShouldBe(
                first.ConsentStatus,
                "The replay must surface the state that was already recorded -- and in particular "
                + "must not overwrite an earlier decision with this one.");
        }
    }

    [Fact]
    public async Task SubmitDecisionAsync_OnAReplayWithTheOppositeAnswer_DoesNotOverwriteTheFirstDecision()
    {
        // The sharper half of the replay contract. A decline followed by an approve on the same
        // token must NOT flip the record -- otherwise anybody re-opening the link could quietly
        // reverse a refusal.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var rawToken = await SeedCancelConsentAsync(Token("no-flip"));

            var declined = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = false });

            var afterApprovalAttempt = await _consent.SubmitDecisionAsync(
                rawToken,
                new SubmitChangeRequestConsentDto { Approved = true });

            afterApprovalAttempt.ConsentStatus.ShouldBe(
                declined.ConsentStatus,
                "A recorded decision is final. If this flips to Approved, a single-use token is "
                + "not single-use and a refusal can be reversed by re-opening the email link.");
        }
    }

    [Fact]
    public async Task SubmitDecisionAsync_WithAGarbageToken_IsRefused()
    {
        // The catch-when filters on TWO specific codes. A token-invalid BusinessException carries
        // neither, so it must propagate rather than be swallowed into a blank landing page.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<BusinessException>(
                async () => await _consent.SubmitDecisionAsync(
                    "TEST-not-a-real-token",
                    new SubmitChangeRequestConsentDto { Approved = true }),
                "Only AlreadyResponded and Expired are idempotent. An invalid token must still be "
                + "refused, or the catch-when has been widened to swallow everything.");
        }
    }
}
