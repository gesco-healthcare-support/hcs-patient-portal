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

    /// <summary>
    /// What a booking does with the attorney master, given what was typed, the master the booking
    /// named by id (if any) and the master its typed email matched (looked up only when none was named).
    /// The same rules for the applicant and the defense side.
    /// </summary>
    internal static AttorneyMasterWrite Decide(
        bool isInternalCaller,
        Guid? callerId,
        AttorneyMasterValues typed,
        string? typedConcurrencyStamp,
        AttorneyMasterRecord? named,
        AttorneyMasterRecord? matchedByEmail)
    {
        if (named != null)
        {
            // Another firm's record is linked as it is; the typed details go onto the appointment.
            return MayChangeMaster(isInternalCaller, named.Values.IdentityUserId, callerId)
                ? new AttorneyMasterWrite(named.Id, typed with { FirmAddress = named.Values.FirmAddress }, typedConcurrencyStamp, false)
                : new AttorneyMasterWrite(named.Id, null, null, true);
        }

        if (matchedByEmail == null)
        {
            return new AttorneyMasterWrite(null, typed, null, false);
        }

        // R2-2 (2026-06-22): email is the authoritative identity for a party, so an email match reuses
        // that master rather than creating a duplicate. Another party's attorney only has its blanks
        // filled, and an account attached only when it has none.
        return MayChangeMaster(isInternalCaller, matchedByEmail.Values.IdentityUserId, callerId)
            ? new AttorneyMasterWrite(matchedByEmail.Id, Overlay(typed, matchedByEmail.Values), matchedByEmail.ConcurrencyStamp, false)
            : new AttorneyMasterWrite(matchedByEmail.Id, FillBlanks(matchedByEmail.Values, typed), matchedByEmail.ConcurrencyStamp, true);
    }

    /// <summary>The booker's own or staff's update of an email-matched master: typed values win, blanks keep the record's.</summary>
    internal static AttorneyMasterValues Overlay(AttorneyMasterValues typed, AttorneyMasterValues current)
    {
        return new AttorneyMasterValues(
            typed.StateId ?? current.StateId,
            typed.IdentityUserId ?? current.IdentityUserId,
            typed.FirmName ?? current.FirmName,
            current.FirmAddress,
            typed.PhoneNumber ?? current.PhoneNumber,
            typed.WebAddress ?? current.WebAddress,
            typed.FaxNumber ?? current.FaxNumber,
            typed.Street ?? current.Street,
            typed.City ?? current.City,
            typed.ZipCode ?? current.ZipCode,
            typed.Email ?? current.Email,
            typed.FirstName ?? current.FirstName,
            typed.LastName ?? current.LastName);
    }

    /// <summary>Another party's email-matched master: what it holds is kept; only its blanks take the typed values.</summary>
    internal static AttorneyMasterValues FillBlanks(AttorneyMasterValues current, AttorneyMasterValues typed)
    {
        return new AttorneyMasterValues(
            current.StateId ?? typed.StateId,
            current.IdentityUserId ?? typed.IdentityUserId,
            FillBlank(current.FirmName, typed.FirmName),
            current.FirmAddress,
            FillBlank(current.PhoneNumber, typed.PhoneNumber),
            FillBlank(current.WebAddress, typed.WebAddress),
            FillBlank(current.FaxNumber, typed.FaxNumber),
            FillBlank(current.Street, typed.Street),
            FillBlank(current.City, typed.City),
            FillBlank(current.ZipCode, typed.ZipCode),
            current.Email ?? typed.Email,
            FillBlank(current.FirstName, typed.FirstName),
            FillBlank(current.LastName, typed.LastName));
    }
}

/// <summary>The fields of an applicant or defense attorney master a booking writes, in the managers' order.</summary>
internal sealed record AttorneyMasterValues(
    Guid? StateId,
    Guid? IdentityUserId,
    string? FirmName,
    string? FirmAddress,
    string? PhoneNumber,
    string? WebAddress,
    string? FaxNumber,
    string? Street,
    string? City,
    string? ZipCode,
    string? Email,
    string? FirstName,
    string? LastName);

/// <summary>An existing attorney master as the booking found it.</summary>
internal sealed record AttorneyMasterRecord(Guid Id, string? ConcurrencyStamp, AttorneyMasterValues Values);

/// <summary>
/// The outcome of <see cref="AttorneyMasterWritePolicy.Decide"/>. <see cref="Values"/> null: link
/// <see cref="TargetId"/> unchanged. Otherwise write <see cref="Values"/> to <see cref="TargetId"/>, or
/// create a new master when <see cref="TargetId"/> is null. <see cref="TypedOnAppointmentOnly"/>: the
/// typed details belong on the appointment's own copy, not the shared record.
/// </summary>
internal sealed record AttorneyMasterWrite(
    Guid? TargetId,
    AttorneyMasterValues? Values,
    string? ConcurrencyStamp,
    bool TypedOnAppointmentOnly);
