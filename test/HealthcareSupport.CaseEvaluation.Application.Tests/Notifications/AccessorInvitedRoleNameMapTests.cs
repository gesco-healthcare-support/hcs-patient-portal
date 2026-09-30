using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// <c>AccessorInvitedEmailHandler.MapRoleName</c> turns the invite's free-text role into the typed
/// role that picks the email's wording. Every known name maps; a blank or unknown name maps to
/// null so the role-agnostic body is used rather than a wrong role's.
/// </summary>
public class AccessorInvitedRoleNameMapTests
{
    [Theory]
    [InlineData("Patient", RecipientRole.Patient)]
    [InlineData("Applicant Attorney", RecipientRole.ApplicantAttorney)]
    [InlineData("Defense Attorney", RecipientRole.DefenseAttorney)]
    [InlineData("Claim Examiner", RecipientRole.ClaimExaminer)]
    [InlineData("  Claim Examiner  ", RecipientRole.ClaimExaminer)]
    public void KnownRoleNames_Map(string roleName, RecipientRole expected)
    {
        AccessorInvitedEmailHandler.MapRoleName(roleName).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("TEST-not-a-role")]
    public void BlankOrUnknownRoleNames_MapToNull(string? roleName)
    {
        AccessorInvitedEmailHandler.MapRoleName(roleName).ShouldBeNull();
    }
}
