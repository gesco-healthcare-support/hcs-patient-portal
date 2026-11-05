using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// 2026-10-10 -- the in-portal way for a party to agree to or decline a reschedule date or a
/// cancellation (see <see cref="IPartyChangeRequestConsentAppService"/>). Until now the only way
/// was the single-use link in the consent email; that link keeps working, and whichever answers
/// first wins because both write through <see cref="ChangeRequestConsentManager"/>, which refuses
/// a second answer for a side.
///
/// <para>WHO MAY ANSWER FOR A SIDE. Exactly the person the link was emailed to: the side's
/// representative as <see cref="ChangeRequestSideResolver.ResolveBothSidesAsync"/> resolves it
/// (applicant attorney, else patient; defense attorney, else claim examiner), matched on the
/// caller's VERIFIED email. A token proves possession of that inbox; here a verified email on a
/// signed-in account proves the same thing. The caller must ALSO pass the appointment read guard,
/// and internal staff are refused outright -- staff answering for a party would defeat the point
/// of asking the party. Every refusal is the same <c>ConsentNotAvailable</c>, so the endpoint
/// cannot be used to learn which requests exist.</para>
///
/// <para>CASE FOLLOWERS (office decision 3, 2026-11-04). A paralegal may answer for their side too: the
/// caller is also admitted for side S when they follow the appointment -- the booker (while the booker
/// still follows, <see cref="AppointmentMailPreferences.BookerFollowsCase"/>) or an ACTIVE, non-staff
/// Authorized User -- and <see cref="ConsentSideFollowerRule.MaySpeakForSide"/> allows it: they hold a
/// law-office role of side S, and an Authorized User was added by staff or by an account of side S
/// (the creator of the accessor row; unknown refuses). The same refusal covers every "not allowed", so no
/// new information leaks, and the first answer still wins in <see cref="ChangeRequestConsentManager"/>.
/// The answer is stored with the answering account's verified email, as for a representative.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class PartyChangeRequestConsentAppService : CaseEvaluationAppService, IPartyChangeRequestConsentAppService
{
    /// <summary>Bounds the scan of an office's pending requests when no appointment is named.</summary>
    public const int MaxPendingScanned = 100;

    private readonly IAppointmentChangeRequestRepository _changeRequestRepository;
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly AppointmentReadAccessGuard _readAccessGuard;
    private readonly ChangeRequestSideResolver _sideResolver;
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly ChangeRequestConsentCompletion _consentCompletion;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly IdentityUserManager _userManager;

    public PartyChangeRequestConsentAppService(
        IAppointmentChangeRequestRepository changeRequestRepository,
        IChangeRequestConsentRoundRepository roundRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<DoctorAvailability, Guid> slotRepository,
        AppointmentReadAccessGuard readAccessGuard,
        ChangeRequestSideResolver sideResolver,
        ChangeRequestConsentManager consentManager,
        ChangeRequestConsentCompletion consentCompletion,
        IRepository<AppointmentAccessor, Guid> accessorRepository,
        IdentityUserManager userManager)
    {
        _changeRequestRepository = changeRequestRepository;
        _roundRepository = roundRepository;
        _appointmentRepository = appointmentRepository;
        _slotRepository = slotRepository;
        _readAccessGuard = readAccessGuard;
        _sideResolver = sideResolver;
        _consentManager = consentManager;
        _consentCompletion = consentCompletion;
        _accessorRepository = accessorRepository;
        _userManager = userManager;
    }

    public virtual async Task<ListResultDto<PartyConsentRequestDto>> GetMyPendingAsync(Guid? appointmentId)
    {
        var items = new List<PartyConsentRequestDto>();
        if (CallerEmail() == null)
        {
            return new ListResultDto<PartyConsentRequestDto>(items);
        }

        var pending = await _changeRequestRepository.GetListAsync(cr =>
            cr.RequestStatus == RequestStatusType.Pending
            && (appointmentId == null || cr.AppointmentId == appointmentId.Value));

        foreach (var request in pending.OrderByDescending(cr => cr.CreationTime).Take(MaxPendingScanned))
        {
            var ask = await FindAskAsync(request);
            if (ask != null)
            {
                items.Add(await ToDtoAsync(ask));
            }
        }

        return new ListResultDto<PartyConsentRequestDto>(items);
    }

    public virtual async Task<PartyConsentRequestDto> SubmitMyDecisionAsync(
        Guid changeRequestId,
        SubmitChangeRequestConsentDto input)
    {
        Check.NotNull(input, nameof(input));

        var request = await _changeRequestRepository.FindAsync(changeRequestId);
        var ask = request == null ? null : await FindAskAsync(request);
        if (ask == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotAvailable);
        }

        foreach (var side in ask.Sides)
        {
            await _consentManager.RecordDecisionAsync(
                new ChangeRequestConsentMatch(ask.Request, ask.Round, side), input.Approved, CallerEmail());
        }

        if (input.Approved)
        {
            // The LAST approval may complete the request on its own (2026-10-09). Scheduled for
            // after this unit of work commits, in its own unit, failures swallowed and logged: a
            // failure to complete must never undo the consent just recorded.
            await _consentCompletion.RequestCompletionAsync(request!.Id);
        }

        return await ToDtoAsync(ask);
    }

    /// <summary>The caller's verified email, or null when they cannot be a party here.</summary>
    private string? CallerEmail()
    {
        if (BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles ?? Array.Empty<string>()))
        {
            return null;
        }

        var email = CurrentUser.GetVerifiedEmail();
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    /// <summary>
    /// The sides of <paramref name="request"/> waiting for the CALLER's answer, or null when there
    /// are none for any reason (not pending, not the caller's side, not readable, expired).
    /// </summary>
    private async Task<PartyConsentAsk?> FindAskAsync(AppointmentChangeRequest request)
    {
        var email = CallerEmail();
        if (email == null || request.RequestStatus != RequestStatusType.Pending)
        {
            return null;
        }

        var appointment = await _appointmentRepository.FindAsync(request.AppointmentId);
        if (appointment == null || !await _readAccessGuard.CanReadAsync(appointment))
        {
            return null;
        }

        // Reschedule consent lives on the CURRENT round (a staff-confirmed date); with no round
        // there is no date to agree to yet. Cancellation consent lives on the request itself.
        ChangeRequestConsentRound? round = null;
        if (request.ChangeRequestType == ChangeRequestType.Reschedule)
        {
            round = await _roundRepository.GetCurrentAsync(request.Id);
            if (round == null)
            {
                return null;
            }
        }

        var reps = await _sideResolver.ResolveBothSidesAsync(request.AppointmentId);
        var nowUtc = Clock.Now.ToUniversalTime();
        var follower = await LoadFollowerAsync(appointment);
        var sides = new[]
            {
                (Side: ChangeRequestSide.SideA, RepEmail: reps.SideARepEmail),
                (Side: ChangeRequestSide.SideB, RepEmail: reps.SideBRepEmail),
            }
            .Where(s => string.Equals(s.RepEmail, email, StringComparison.OrdinalIgnoreCase)
                || (follower != null && follower.MaySpeakFor(s.Side)))
            .Where(s => IsAwaitingAnswer(request, round, s.Side, nowUtc))
            .Select(s => s.Side)
            .ToList();

        return sides.Count == 0 ? null : new PartyConsentAsk(request, round, appointment, sides);
    }

    /// <summary>
    /// The caller as a case follower of <paramref name="appointment"/>, or null when they are neither the
    /// following booker nor an active Authorized User. Their roles are read from the account, not the token,
    /// like the mail path; an Authorized User's adder is the account that created the accessor row.
    /// </summary>
    private async Task<CallerAsFollower?> LoadFollowerAsync(Appointment appointment)
    {
        if (CurrentUser.Id is not { } callerId)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(callerId.ToString());
        if (user == null || !user.IsActive)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (BookingFlowRoles.IsInternalUserCaller(roles))
        {
            return null;
        }

        if (AppointmentSubmitter.IdOf(appointment) == callerId
            && AppointmentMailPreferences.BookerFollowsCase(appointment))
        {
            return new CallerAsFollower(roles.ToList(), IsBooker: true, new List<IList<string>>());
        }

        var adders = new List<IList<string>>();
        var rows = await _accessorRepository.GetListAsync(a =>
            a.AppointmentId == appointment.Id && a.IdentityUserId == callerId);
        foreach (var row in rows)
        {
            var adder = row.CreatorId is { } adderId
                ? await _userManager.FindByIdAsync(adderId.ToString())
                : null;
            if (adder != null)
            {
                adders.Add(await _userManager.GetRolesAsync(adder));
            }
        }

        return rows.Count == 0 ? null : new CallerAsFollower(roles.ToList(), IsBooker: false, adders);
    }

    private sealed record CallerAsFollower(
        IReadOnlyList<string> Roles,
        bool IsBooker,
        IReadOnlyList<IList<string>> AdderRoles)
    {
        public bool MaySpeakFor(ChangeRequestSide side) =>
            IsBooker
                ? ConsentSideFollowerRule.MaySpeakForSide(side, Roles, isBooker: true, adderRoles: null)
                : AdderRoles.Any(adder => ConsentSideFollowerRule.MaySpeakForSide(side, Roles, isBooker: false, adder));
    }

    private static bool IsAwaitingAnswer(
        AppointmentChangeRequest request,
        ChangeRequestConsentRound? round,
        ChangeRequestSide side,
        DateTime nowUtc)
    {
        return round != null
            ? round.SideConsentStatus(side) == ChangeRequestConsentStatus.Pending && !round.IsSideExpired(side, nowUtc)
            : request.SideConsentStatus(side) == ChangeRequestConsentStatus.Pending && !request.IsSideExpired(side, nowUtc);
    }

    private async Task<PartyConsentRequestDto> ToDtoAsync(PartyConsentAsk ask)
    {
        var side = ask.Sides[0];
        string? newDateTime = null;
        if (ask.Round != null)
        {
            var slot = await _slotRepository.FindAsync(ask.Round.ProposedDoctorAvailabilityId);
            newDateTime = slot == null ? null : ConsentSlotLabel.Format(slot);
        }

        return new PartyConsentRequestDto
        {
            ChangeRequestId = ask.Request.Id,
            AppointmentId = ask.Appointment.Id,
            ConfirmationNumber = ask.Appointment.RequestConfirmationNumber,
            PatientName = string.Join(
                " ",
                new[] { ask.Appointment.PatientFirstName, ask.Appointment.PatientLastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part))),
            AppointmentDate = ask.Appointment.AppointmentDate,
            ChangeRequestType = ask.Request.ChangeRequestType,
            Reason = ask.Request.ChangeRequestType == ChangeRequestType.Cancel
                ? ask.Request.CancellationReason
                : ask.Request.ReScheduleReason,
            RequestedNewDateTime = newDateTime,
            ConsentStatus = ask.Round?.SideConsentStatus(side) ?? ask.Request.SideConsentStatus(side),
        };
    }

    private sealed record PartyConsentAsk(
        AppointmentChangeRequest Request,
        ChangeRequestConsentRound? Round,
        Appointment Appointment,
        IReadOnlyList<ChangeRequestSide> Sides);
}
