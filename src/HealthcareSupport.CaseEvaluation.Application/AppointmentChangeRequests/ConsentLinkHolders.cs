using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Who holds a consent link for a just-submitted change request (2026-11-03). The consent email is
/// that person's one email for the event, so the "request submitted" notice must not also go to them.
///
/// <para>Mirrors how <c>AppointmentChangeRequestsAppService.IssueConsentAndNotifyAsync</c> chooses
/// recipients, and reads the same <see cref="ChangeRequestSideResolver"/>: a party-initiated request
/// asks the OPPOSING side's single representative; a staff-initiated one asks BOTH sides'
/// representatives (a side with none is not asked). Nobody holds a link when consent gating is off or
/// the submitter's side cannot be resolved.</para>
/// </summary>
public class ConsentLinkHolders : ITransientDependency
{
    private readonly ChangeRequestSideResolver _sideResolver;
    private readonly IdentityUserManager _userManager;
    private readonly DocumentEmailContextResolver _contextResolver;

    public ConsentLinkHolders(
        ChangeRequestSideResolver sideResolver,
        IdentityUserManager userManager,
        DocumentEmailContextResolver contextResolver)
    {
        _sideResolver = sideResolver;
        _userManager = userManager;
        _contextResolver = contextResolver;
    }

    /// <summary>The addresses the consent emails for this submission are sent to.</summary>
    public virtual async Task<IReadOnlyList<string>> ResolveEmailsAsync(Guid appointmentId, Guid submittedByUserId)
    {
        if (!AppointmentChangeRequestConsts.ConsentGatingEnabled || appointmentId == Guid.Empty)
        {
            return Array.Empty<string>();
        }

        var submitter = submittedByUserId == Guid.Empty
            ? null
            : await _userManager.FindByIdAsync(submittedByUserId.ToString());
        IList<string> roles = submitter == null ? new List<string>() : await _userManager.GetRolesAsync(submitter);

        if (AppointmentSubmitter.HoldsStaffRole(roles))
        {
            var both = await _sideResolver.ResolveBothSidesAsync(appointmentId);
            var asked = new List<ChangeRequestSide>();
            if (!string.IsNullOrWhiteSpace(both.SideARepEmail))
            {
                asked.Add(ChangeRequestSide.SideA);
            }
            if (!string.IsNullOrWhiteSpace(both.SideBRepEmail))
            {
                asked.Add(ChangeRequestSide.SideB);
            }
            var reps = new[] { both.SideARepEmail, both.SideBRepEmail }
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .ToList();
            return await WithSideFollowersAsync(appointmentId, reps, asked);
        }

        // Matched on the CONFIRMED email only, as the consent issuer does.
        var verifiedEmail = submitter is { EmailConfirmed: true } ? submitter.Email : null;
        var side = await _sideResolver.ResolveAsync(appointmentId, verifiedEmail, roles);
        if (side == null)
        {
            return Array.Empty<string>();
        }
        var opposing = side.RequestingSide == ChangeRequestSide.SideA ? ChangeRequestSide.SideB : ChangeRequestSide.SideA;
        return await WithSideFollowersAsync(appointmentId, new[] { side.OpposingRepEmail }, new[] { opposing });
    }

    /// <summary>
    /// 2026-11-04 (office decision 3): the case followers who may answer for an asked side get that side's
    /// link too, so they hold a link as well and stay off the request-submitted notice.
    /// </summary>
    private async Task<IReadOnlyList<string>> WithSideFollowersAsync(
        Guid appointmentId, IEnumerable<string> representatives, IReadOnlyCollection<ChangeRequestSide> askedSides)
    {
        var holders = new List<string>(representatives);
        var ctx = await _contextResolver.ResolveAsync(appointmentId, appointmentDocumentId: null);
        if (ctx != null)
        {
            foreach (var side in askedSides)
            {
                holders.AddRange(CaseFollowers.ConsentLinkFollowers(ctx, side).Select(f => f.Email));
            }
        }
        return holders;
    }
}
