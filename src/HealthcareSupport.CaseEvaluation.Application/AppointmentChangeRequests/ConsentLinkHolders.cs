using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
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

    public ConsentLinkHolders(ChangeRequestSideResolver sideResolver, IdentityUserManager userManager)
    {
        _sideResolver = sideResolver;
        _userManager = userManager;
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
            return new[] { both.SideARepEmail, both.SideBRepEmail }
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e!)
                .ToList();
        }

        // Matched on the CONFIRMED email only, as the consent issuer does.
        var verifiedEmail = submitter is { EmailConfirmed: true } ? submitter.Email : null;
        var side = await _sideResolver.ResolveAsync(appointmentId, verifiedEmail, roles);
        return side == null ? Array.Empty<string>() : new[] { side.OpposingRepEmail };
    }
}
