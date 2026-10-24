using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-23: who may change an attorney MASTER record (the shared applicant or defense attorney
/// row that every appointment naming that attorney points at, and that each new booking copies its
/// snapshot from).
///
/// <para>Staff may change any master, as before. An external caller (patient, attorney, claim
/// examiner) may change only a master already linked to their own account -- an attorney keeping
/// their own details current. Any other master an external booking names is LINKED as it is: the
/// details the booker typed go onto that appointment's own copy (the attorney snapshot), never onto
/// the shared record, so one party cannot rewrite another firm's address, phone or email, or attach
/// another firm's record to their own login.</para>
/// </summary>
internal static class AttorneyMasterWritePolicy
{
    /// <summary>
    /// The account id honoured for an attorney. Staff may name any. For an external caller the id is
    /// ignored and the account is found from the typed email instead: that is the attorney's own
    /// login when they have one, and an attorney booking for themselves types their own email, so
    /// they still land on their own account. Honouring even the caller's OWN id would let a patient
    /// attach their account to a not-yet-registered attorney's record, and own it from then on.
    /// </summary>
    internal static Guid AccountIdToHonour(bool isInternalCaller, Guid requested)
    {
        return isInternalCaller ? requested : Guid.Empty;
    }

    /// <summary>Whether the caller may write to a master whose linked account is <paramref name="masterAccountId"/>.</summary>
    internal static bool MayChangeMaster(bool isInternalCaller, Guid? masterAccountId, Guid? callerId)
    {
        return isInternalCaller || (callerId.HasValue && masterAccountId == callerId);
    }

    /// <summary>Keeps a value already on the master; fills it from the booking only when it is blank.</summary>
    internal static string? FillBlank(string? current, string? typed)
    {
        return string.IsNullOrWhiteSpace(current) ? typed : current;
    }

    /// <summary>What the appointment's own copy shows: what the booker typed, else what it already holds.</summary>
    internal static string? TypedOr(string? typed, string? current)
    {
        return string.IsNullOrWhiteSpace(typed) ? current : typed;
    }
}
