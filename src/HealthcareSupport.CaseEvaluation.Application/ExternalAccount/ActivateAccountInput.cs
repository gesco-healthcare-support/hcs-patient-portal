using System;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>What the activation page posts: the link's two halves and the chosen password.</summary>
public class ActivateAccountInput
{
    public Guid UserId { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
