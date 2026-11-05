using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// One case follower (2026-11-04): an external account that follows an appointment without being a
/// named party -- the external booker, or an accessor (Authorized User). <see cref="IsBooker"/> marks the
/// account that submitted the request; <see cref="IsPatientRole"/> marks an account holding the Patient
/// role; <see cref="Role"/> is the recipient tag for the mail (null when no party role applies);
/// <see cref="RoleNames"/> is what the account holds, for the packet-visibility check.
/// <see cref="AdderRoleNames"/> (2026-11-04) is what the account that ADDED the accessor row holds (null for
/// the booker, and for a row whose adder is unknown); it decides whether a law-office accessor may answer a
/// consent request for its side (<see cref="ConsentSideFollowerRule"/>).
/// </summary>
public sealed record CaseFollower(
    string Email,
    Guid UserId,
    bool IsBooker,
    bool IsPatientRole,
    RecipientRole? Role,
    IReadOnlyList<string> RoleNames,
    IReadOnlyList<string>? AdderRoleNames = null);

/// <summary>
/// Case followers in email routing (2026-11-04). The followers of an appointment are its external booker
/// and every accessor; they receive every case email the parties receive, once per person per event, except
/// the notices that are deliberately kept to the people they were written for (the consent link, the
/// decline notice, the day reminder, packets, the office copies and the staff-only mail).
///
/// <para>Followers are loaded once, by <see cref="DocumentEmailContextResolver"/>, and handed to each
/// handler on <see cref="DocumentEmailContext.Followers"/>. A handler appends them to its recipient list
/// BEFORE its own de-duplication and link-holder filtering, so a follower who is also a party, the booker
/// or a link holder is mailed once, or not at all, by the rules that already exist.</para>
/// </summary>
public static class CaseFollowers
{
    /// <summary>
    /// The ONE switch for packets to accessors (office decision 4, 2026-11-04: ON). False: no accessor gets
    /// a packet (the booker still does, through <see cref="AttorneyPacketBookerRecipient"/>). True: an
    /// accessor gets the attorney / claim examiner packet when <c>PacketVisibility</c> allows one of its
    /// roles, so a Patient-role accessor never gets it. Nothing else changes when it is flipped.
    /// </summary>
    public const bool AccessorsReceivePackets = true;

    /// <summary>The recipient tag for an account's roles; null when it holds no party role.</summary>
    public static RecipientRole? RoleOf(IEnumerable<string?> roleNames)
    {
        var roles = Trim(roleNames);
        if (HoldsRole(roles, ExternalRoleConsts.ApplicantAttorney, ExternalRoleConsts.ApplicantParalegal))
        {
            return RecipientRole.ApplicantAttorney;
        }
        if (HoldsRole(roles, ExternalRoleConsts.DefenseAttorney, ExternalRoleConsts.DefenseParalegal))
        {
            return RecipientRole.DefenseAttorney;
        }
        if (HoldsRole(roles, ExternalRoleConsts.ClaimExaminer))
        {
            return RecipientRole.ClaimExaminer;
        }
        return HoldsRole(roles, ExternalRoleConsts.Patient) ? RecipientRole.Patient : null;
    }

    /// <summary>True when <paramref name="roleNames"/> holds the Patient role.</summary>
    public static bool IsPatientRole(IEnumerable<string?> roleNames) =>
        HoldsRole(Trim(roleNames), ExternalRoleConsts.Patient);

    /// <summary>
    /// The recipients plus the followers whose address is not already present (case-insensitive). A
    /// follower is never tagged <see cref="RecipientRole.OfficeAdmin"/> and is always registered. When
    /// <paramref name="patientRoleAllowed"/> is false a follower holding the Patient role is skipped,
    /// unless it is the booker (the booker submitted the request, so it is never dropped by role).
    /// </summary>
    public static List<SendAppointmentEmailArgs> Append(
        IEnumerable<SendAppointmentEmailArgs> recipients,
        DocumentEmailContext ctx,
        bool patientRoleAllowed,
        bool requestReceipt = false)
    {
        var result = recipients
            .Where(r => !IsExcluded(ctx, r.To, requestReceipt))
            .ToList();
        var present = new HashSet<string>(
            result.Select(r => (r.To ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var follower in Eligible(ctx, patientRoleAllowed, patientNever: false))
        {
            if (present.Add(follower.Email))
            {
                result.Add(new SendAppointmentEmailArgs
                {
                    To = follower.Email,
                    Role = follower.Role,
                    IsRegistered = true,
                });
            }
        }
        return result;
    }

    /// <summary>The same append for a <see cref="NotificationRecipient"/> list.</summary>
    public static List<NotificationRecipient> Append(
        IEnumerable<NotificationRecipient> recipients,
        DocumentEmailContext ctx,
        bool patientRoleAllowed)
    {
        return AppendCore(recipients, ctx, patientRoleAllowed, patientNever: false);
    }

    /// <summary>
    /// True when <paramref name="email"/> must get NO mail about this appointment (2026-11-04): the
    /// lawyer's address while the "send my mail to the paralegal instead" redirect applies, and -- except
    /// for the request receipt (<paramref name="requestReceipt"/>) -- the address of a booker who opted
    /// out of following the case and is not also a named party.
    /// </summary>
    public static bool IsExcluded(DocumentEmailContext ctx, string? email, bool requestReceipt = false)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        var address = email.Trim();
        if (ctx.RedirectedAddresses.Contains(address, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }
        return !requestReceipt
            && !string.IsNullOrWhiteSpace(ctx.OptedOutBookerAddress)
            && string.Equals(ctx.OptedOutBookerAddress.Trim(), address, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The case followers who get the consent LINK for <paramref name="side"/> besides its representative
    /// (office decision 3, 2026-11-04): those <see cref="ConsentSideFollowerRule.MaySpeakForSide"/> allows. The
    /// same link goes to all of them; the first answer wins. A booker who opted out is not a follower.
    /// </summary>
    public static List<NotificationRecipient> ConsentLinkFollowers(DocumentEmailContext ctx, ChangeRequestSide side)
    {
        return ctx.Followers
            .Where(f => ConsentSideFollowerRule.MaySpeakForSide(side, f.RoleNames, f.IsBooker, f.AdderRoleNames))
            .Select(f => new NotificationRecipient(email: f.Email, role: f.Role, isRegistered: true))
            .ToList();
    }

    /// <summary>The recipients minus every address that must get nothing (see <see cref="IsExcluded"/>).</summary>
    public static List<NotificationRecipient> Exclude(
        IEnumerable<NotificationRecipient> recipients, DocumentEmailContext ctx) =>
        recipients.Where(r => !IsExcluded(ctx, r.Email)).ToList();

    /// <summary>
    /// The append for the consent INFO notice: the patient never gets it, so a follower with a patient
    /// address or the Patient role is skipped, the booker included (the patient hears only the confirmed
    /// date when the change finishes).
    /// </summary>
    public static List<NotificationRecipient> AppendWithoutPatient(
        IEnumerable<NotificationRecipient> recipients, DocumentEmailContext ctx)
    {
        return AppendCore(recipients, ctx, patientRoleAllowed: false, patientNever: true);
    }

    /// <summary>
    /// The accessors who get the attorney / claim examiner packet: none while
    /// <see cref="AccessorsReceivePackets"/> is false; otherwise each non-booker follower whose roles
    /// <c>PacketVisibility</c> allows that packet. The booker is handled by
    /// <see cref="AttorneyPacketBookerRecipient"/>. <paramref name="enabled"/> defaults to the switch; it
    /// is a parameter so the gate can be tested without flipping the constant.
    /// </summary>
    public static List<NotificationRecipient> AccessorPacketRecipients(
        DocumentEmailContext ctx, bool enabled = AccessorsReceivePackets)
    {
        if (!enabled)
        {
            return new List<NotificationRecipient>();
        }

        return ctx.Followers
            .Where(f => !f.IsBooker && PacketVisibility.IsAllowed(f.RoleNames, PacketKind.AttorneyClaimExaminer))
            .Select(f => new NotificationRecipient(
                email: f.Email,
                role: f.Role ?? RecipientRole.ClaimExaminer,
                isRegistered: true))
            .ToList();
    }

    private static List<NotificationRecipient> AppendCore(
        IEnumerable<NotificationRecipient> recipients,
        DocumentEmailContext ctx,
        bool patientRoleAllowed,
        bool patientNever)
    {
        var result = Exclude(recipients, ctx);
        var present = new HashSet<string>(
            result.Select(r => (r.Email ?? string.Empty).Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var follower in Eligible(ctx, patientRoleAllowed, patientNever))
        {
            if (present.Add(follower.Email))
            {
                result.Add(new NotificationRecipient(
                    email: follower.Email, role: follower.Role, isRegistered: true));
            }
        }
        return result;
    }

    private static IEnumerable<CaseFollower> Eligible(
        DocumentEmailContext ctx, bool patientRoleAllowed, bool patientNever)
    {
        foreach (var follower in ctx.Followers)
        {
            if (patientNever)
            {
                if (follower.IsPatientRole
                    || ctx.PatientAddresses.Contains(follower.Email, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }
            }
            else if (!patientRoleAllowed && follower.IsPatientRole && !follower.IsBooker)
            {
                continue;
            }
            yield return follower;
        }
    }

    private static List<string> Trim(IEnumerable<string?> roleNames) =>
        roleNames.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r!.Trim()).ToList();

    private static bool HoldsRole(IReadOnlyCollection<string> roles, params string[] names) =>
        roles.Any(r => names.Any(n => string.Equals(r, n, StringComparison.OrdinalIgnoreCase)));
}
