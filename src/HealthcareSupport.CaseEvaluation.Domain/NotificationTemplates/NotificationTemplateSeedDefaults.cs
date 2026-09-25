namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// The shipped (seed) subject + bodies for a notification-template code.
/// </summary>
public sealed class NotificationTemplateSeedDefault
{
    public NotificationTemplateSeedDefault(string subject, string bodyEmail, string bodySms)
    {
        Subject = subject;
        BodyEmail = bodyEmail;
        BodySms = bodySms;
    }

    public string Subject { get; }

    public string BodyEmail { get; }

    public string BodySms { get; }
}

/// <summary>
/// Single source of truth for the "shipped default" content of every
/// notification template code. Resolves the subject from
/// <see cref="EmailSubjects.ByCode"/>, the email body from
/// <see cref="EmailBodyResources"/> (embedded <c>EmailBodies\*.html</c>), and
/// the SMS body from the standard stub, applying the same fallbacks the
/// <see cref="NotificationTemplateDataSeedContributor"/> uses when a code has
/// no curated subject / resource body yet.
///
/// <para>Extracted (B-B2, 2026-06-16) so two callers share one definition of
/// "default": the seed contributor (which inserts / overwrites rows) and the
/// <see cref="NotificationTemplateVariableCatalog.IsCustomized"/> derivation
/// (which reports whether a tenant has edited a template away from its
/// shipped content). Keeping these in sync by hand was the failure mode this
/// removes.</para>
/// </summary>
public static class NotificationTemplateSeedDefaults
{
    /// <summary>
    /// Returns the shipped subject + email body + SMS body for
    /// <paramref name="code"/>. Unknown / unwired codes fall back to the stub
    /// strings (matching the seed contributor) so the result is never null.
    /// </summary>
    public static NotificationTemplateSeedDefault GetSeedDefaults(string code)
    {
        var subject = EmailSubjects.ByCode.TryGetValue(code, out var s)
            ? s
            : $"[{code}] -- TODO: parity-correct subject";

        var bodyEmail = EmailBodyResources.TryLoadBody(code)
            ?? $"<p>Stub body for {code}. Per-feature phases will replace with parity-correct content.</p>";

        var bodySms = $"Stub SMS for {code}.";

        return new NotificationTemplateSeedDefault(subject, bodyEmail, bodySms);
    }

    /// <summary>
    /// True when <paramref name="code"/> has a curated embedded HTML body
    /// (versus a stub). The seed contributor only overwrites existing rows for
    /// resource-backed codes, so this drives that branch.
    /// </summary>
    public static bool HasResourceBackedBody(string code) =>
        EmailBodyResources.TryLoadBody(code) != null;
}
