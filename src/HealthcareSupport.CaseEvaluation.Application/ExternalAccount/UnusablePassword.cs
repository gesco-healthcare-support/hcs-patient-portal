using System;
using System.Security.Cryptography;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// The password an account is created with when the person will choose their own from an
/// activation link. Identity needs SOME password to create an account; this one is never shown,
/// stored or sent anywhere, and stops mattering the moment the link is used, because activation
/// replaces it. It still has to pass the password policy, so it carries one of each required
/// character class in front of 32 random bytes.
/// </summary>
public static class UnusablePassword
{
    public static string Generate() =>
        "Aa1!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
