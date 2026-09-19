namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// Phase 4.1 (2026-09-09) -- masks an email address for diagnostic context, closing CodeQL alert
/// 211.
///
/// <para><b>What was wrong.</b> <see cref="CaseEvaluationAccountEmailer"/> built its context tag as
/// <c>$"AccountEmailer/ConfirmationCode/{emailAddress}"</c>, and that tag reaches a
/// <c>LogWarning</c> on the template-missing branch -- past the empty-recipient guard, so the
/// address is non-empty by construction. A full patient email address in a log breaches the
/// redaction rule in <c>~/.claude/rules/hipaa.md</c>.</para>
///
/// <para><b>Why masking rather than dropping the tag.</b> Adrian's ruling, 2026-09-09, chosen over
/// removing the tag entirely: the tag exists so an operator can tell WHICH emailer path failed and
/// roughly for whom. Dropping it blinds the log; masking keeps the correlation and removes the
/// identifier. <b>The domain is retained deliberately</b> -- that is what preserves the
/// correlation. The trade-off was weighed: on a small tenant a domain can narrow identity. Changing
/// that is a new decision, not a tidy-up.</para>
///
/// <para><b>Fixing at the construction site is deliberate.</b> The tag has TWO sinks -- the Warning
/// logs, and <c>Context</c> on the enqueued <c>SendAppointmentEmailArgs</c>. Masking where the tag
/// is built cleans both from one edit. (The job already carries the real address in <c>To</c>, so
/// the job sink was never the exposure; it is simply the sink a test can observe.)</para>
///
/// <para>Mirrors <see cref="Patients.SsnVisibility"/>: internal static, pure, no DI, a const mask,
/// null in / null out, and a fallback when the input cannot be masked meaningfully. Treats the
/// value as opaque -- it does not validate that the input is a well-formed address, because a
/// logger must never be the thing that rejects input.</para>
/// </summary>
internal static class EmailAddressVisibility
{
    internal const string Mask3 = "***";

    /// <summary>
    /// Keeps the first character of the local part and the whole domain, replacing the rest of the
    /// local part with <see cref="Mask3"/>. Returns <paramref name="emailAddress"/> unchanged when
    /// it is null or empty, and <see cref="Mask3"/> alone when there is no domain to keep.
    /// </summary>
    internal static string? Mask(string? emailAddress)
    {
        if (string.IsNullOrEmpty(emailAddress))
        {
            return emailAddress;
        }

        // The LAST '@' delimits the domain: a quoted local part may legally contain one, and
        // splitting on the first would push the remainder of the local part into the "domain"
        // half, which is the part we keep verbatim.
        var at = emailAddress.LastIndexOf('@');
        if (at < 0)
        {
            // Not an address. There is no domain worth keeping and no way to know which part is
            // safe, so reveal nothing rather than guess.
            return Mask3;
        }

        var domain = emailAddress.Substring(at);
        return at == 0
            ? Mask3 + domain
            : emailAddress.Substring(0, 1) + Mask3 + domain;
    }
}
