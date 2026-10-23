namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// 2026-10-23 -- the token behind an activate-your-account link.
///
/// <para>An account created by someone else (New Practice's office admin, a user made on the identity
/// screen, an invited appointment accessor) gets ONE email. Its link opens a page where the person
/// chooses a password, and choosing it is what confirms the email. Before this, such an account was
/// created unconfirmed with a password nobody knew: it could not sign in, and forgot-password refused
/// it behind a generic success, so the only way in was a resend-verification page nobody knew about.</para>
///
/// <para>The token comes from Identity's password-reset provider (Default: data protection, one day)
/// with its OWN purpose. A shared purpose would make the two link kinds interchangeable: an activation
/// link would also open the reset page, which sets a password without confirming the email (so sign-in
/// would still refuse), and any forgot-password link would activate. Setting the password rotates the
/// security stamp the token is bound to, so a link works once.</para>
/// </summary>
public static class AccountActivationTokens
{
    public const string Purpose = "ActivateAccount";

    /// <summary>
    /// What the email tells the reader. It must match the provider's lifetime; nothing in src/ sets
    /// <c>DataProtectionTokenProviderOptions.TokenLifespan</c>, so that is Identity's default of one
    /// day. Change both together.
    /// </summary>
    public const int LifetimeHours = 24;
}
