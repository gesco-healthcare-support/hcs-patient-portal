using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The booker as a recipient of the attorney / claim examiner packet (2026-11-03). The account that
/// SUBMITTED the appointment gets the same packet email as the named attorneys, so a firm login whose
/// address differs from the attorney typed on the appointment is not left out. The booker is external
/// only (staff never are), is not the patient, is an active login with an address, and holds a role
/// that may see the attorney packet (<see cref="PacketVisibility"/>); anyone else gets nothing here.
/// Must run inside the appointment's office scope, like the handler that calls it.
/// </summary>
public class AttorneyPacketBookerRecipient : ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public AttorneyPacketBookerRecipient(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    public virtual async Task<NotificationRecipient?> ResolveAsync(DocumentEmailContext ctx)
    {
        // BookerUserId is already null for a staff submitter (DocumentEmailContextResolver).
        if (ctx.BookerUserId is not { } bookerId)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(bookerId.ToString());
        if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        if (PatientBeforeApprovalRule.IsPatientAccount(ctx, user.Id, user.EmailConfirmed ? user.Email : null))
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (!PacketVisibility.IsAllowed(roles, PacketKind.AttorneyClaimExaminer))
        {
            return null;
        }

        return new NotificationRecipient(email: user.Email.Trim(), role: RoleOf(roles), isRegistered: true);
    }

    private static RecipientRole? RoleOf(System.Collections.Generic.IEnumerable<string> roles)
    {
        if (roles.Any(r => string.Equals(r?.Trim(), "Applicant Attorney", StringComparison.OrdinalIgnoreCase)))
        {
            return RecipientRole.ApplicantAttorney;
        }
        if (roles.Any(r => string.Equals(r?.Trim(), "Defense Attorney", StringComparison.OrdinalIgnoreCase)))
        {
            return RecipientRole.DefenseAttorney;
        }
        return RecipientRole.ClaimExaminer;
    }
}
