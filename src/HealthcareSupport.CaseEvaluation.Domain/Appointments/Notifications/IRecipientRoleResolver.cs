namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// Wave 3 / #17.3 (2026-05-07) -- classifies a recipient email against an
/// expected role so the booking-time submission email handler can pick the
/// correct call-to-action (Login vs Register).
///
/// <para><b>Why this exists:</b> the booker types an email into one of the
/// four appointment-level party-email columns (Patient / AA / DA / CE). The
/// <see cref="AppointmentRecipientResolver"/>'s pre-Wave-3 implementation
/// did a single check -- "does an IdentityUser exist with this email?" -- and
/// flipped <c>IsRegistered</c> from that. It did NOT verify that the
/// IdentityUser actually held the role the booker assigned. Symptom: a
/// patient registered as <c>Role.Patient</c> typed by the booker into the
/// "Defense Attorney email" column got <c>IsRegistered=true</c> + the
/// "Open patient portal" Login CTA. They logged in, saw their Patient
/// dashboard with no DA accessor binding, and the appointment was invisible.
/// </para>
///
/// <para><b>Option A semantics</b> (chosen 2026-05-07): when the email
/// resolves to a user whose roles do NOT include the expected role, treat
/// as "not registered for THIS role" (<c>IsRegistered=false</c>). The
/// handler then renders the Register CTA, the recipient creates a
/// dedicated account for the new role, and accessor binding goes through
/// the normal registration flow. Matches OLD's "no role coupling" model
/// where each party held a single-role account.</para>
///
/// <para>Bypassed for <see cref="RecipientRole.OfficeAdmin"/>: that
/// recipient is a tenant mailbox setting, not an IdentityUser, so the
/// classifier returns <c>IsRegistered=true, MatchesRole=true</c>
/// unconditionally and the handler renders the OfficeAdmin branch.</para>
/// </summary>
public interface IRecipientRoleResolver
{
    /// <summary>
    /// Looks up the email in the current tenant's IdentityUser table and
    /// reports whether (a) any user matches and (b) that user holds the
    /// <paramref name="expectedRole"/>'s ABP role name. Per Option A,
    /// callers should treat <c>!MatchesRole</c> as "not registered for
    /// this role" -- i.e., the recipient gets the Register CTA, not the
    /// Login CTA, even if a same-email account exists under a different role.
    /// </summary>
    Task<RecipientRoleClassification> ClassifyAsync(string email, RecipientRole expectedRole);
}

/// <summary>
/// Result of <see cref="IRecipientRoleResolver.ClassifyAsync"/>.
///
/// <para><b>IsRegistered</b> -- per Option A this is the value handlers
/// should consume directly: <c>true</c> only when the email resolves to a
/// user AND that user holds the expected role; <c>false</c> in every other
/// case (no matching user OR matching user is off-role). Combines the two
/// signals in a single boolean so the handler does not need to special-case
/// off-role registrations -- it sends the Register CTA exactly when
/// <c>IsRegistered</c> is false.</para>
///
/// <para><b>MatchesRole</b> + <b>UserId</b> are surfaced for diagnostics
/// (e.g., logging the off-role conflict) and for future flows that may
/// want to distinguish "no user" from "wrong-role user" -- e.g., to mute
/// the email entirely or fire a staff-side "ambiguous role" notification.
/// Today's only consumer (<see cref="AppointmentRecipientResolver"/>)
/// reads <c>IsRegistered</c>.</para>
/// </summary>
public sealed record RecipientRoleClassification(
    bool IsRegistered,
    bool MatchesRole,
    Guid? UserId);
