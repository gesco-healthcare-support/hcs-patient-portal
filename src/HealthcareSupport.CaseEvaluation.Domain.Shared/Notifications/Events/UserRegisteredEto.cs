using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Raised when an external user finishes self-service registration via
/// <c>IExternalSignupAppService.RegisterAsync</c>. Mirrors OLD's call to
/// <c>UserDomain.SendEmail(user, isNewUser: true)</c> at
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs</c>:314-333,
/// which fires a single email to the new user with a verify-link.
///
/// <para>The <c>UserRegisteredEmailHandler</c> consumes this event and
/// dispatches the <see cref="NotificationTemplates.NotificationTemplateConsts.Codes.UserRegistered"/>
/// template. NEW deviates from OLD on the verify mechanism: OLD wrote a
/// <c>VerificationCode Guid</c> column on <c>User</c> and built a
/// <c>/verify-email/{userId}?query={code}</c> link; NEW relies on ABP's
/// <c>IdentityUser.SetEmailConfirmed</c> token flow, with the link
/// pointing at the AuthServer's stock <c>/Account/ConfirmEmail</c>
/// page. Token + URL are constructed in the handler so the Eto stays
/// minimal (no token-leak risk in the published event).</para>
/// </summary>
public class UserRegisteredEto
{
    /// <summary>The freshly-registered user's IdentityUser id.</summary>
    public Guid UserId { get; set; }

    /// <summary>Tenant the user registered into. Null for host signups (not used in NEW today).</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Email address the verify-link is sent to. Same as <c>IdentityUser.Email</c>.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Stamped from the user's <c>IdentityUser.Name</c> for the body's "Hello, {first}" line.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Stamped from the user's <c>IdentityUser.Surname</c>; templates do not reference it today, kept for forward-compat.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Role the user signed up as (Patient / Adjuster / Applicant Attorney /
    /// Defense Attorney). Future per-role copy can branch on this; the
    /// MVP body is role-agnostic.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; }
}
