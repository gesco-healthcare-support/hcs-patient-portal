using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.Identity;

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

    // 2026-11-04: a paralegal booker is tagged with its side's attorney role (RecipientRole has
    // no paralegal value), not the claim-examiner fallback.
    private static RecipientRole? RoleOf(System.Collections.Generic.IEnumerable<string> roles)
    {
        if (roles.Any(r => ExternalRoleConsts.ApplicantLawOffice.Contains((r ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)))
        {
            return RecipientRole.ApplicantAttorney;
        }
        if (roles.Any(r => ExternalRoleConsts.DefenseLawOffice.Contains((r ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)))
        {
            return RecipientRole.DefenseAttorney;
        }
        return RecipientRole.ClaimExaminer;
    }
}
