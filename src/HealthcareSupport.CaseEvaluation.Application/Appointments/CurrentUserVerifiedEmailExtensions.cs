using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The caller's email for ACCESS decisions: the address only when the identity
/// provider reports it as confirmed, otherwise null. The email-matching access
/// pathways grant visibility from a match against an appointment's party-email
/// columns, so an unconfirmed (for example just-changed) address must never
/// count. Null makes those pathways refuse; they already treat a blank email as
/// no match.
/// </summary>
public static class CurrentUserVerifiedEmailExtensions
{
    public static string? GetVerifiedEmail(this ICurrentUser currentUser)
        => currentUser.EmailVerified ? currentUser.Email : null;
}
