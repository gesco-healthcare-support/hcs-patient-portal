using System;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>Which party-email column on the appointment changed.</summary>
public enum PartyEmailRole
{
    Patient,
    ApplicantAttorney,
    DefenseAttorney,
    ClaimExaminer,
}

/// <summary>One party-email column that now holds a different address than before.</summary>
public sealed record PartyEmailChange(PartyEmailRole Role, string? Before, string After);

/// <summary>
/// The four appointment party-email columns, before and after a staff party change (2026-10-21).
/// Pure, so the "who is new" decision is unit-tested directly: only an address that is now on the
/// appointment AND was not there before for that role counts. A removal, or the same address with
/// different capitalisation, is not a new party.
/// </summary>
public static class PartyEmails
{
    public sealed record Snapshot(
        string? Patient,
        string? ApplicantAttorney,
        string? DefenseAttorney,
        string? ClaimExaminer);

    public static Snapshot Of(Appointment appointment) => new(
        appointment.PatientEmail,
        appointment.ApplicantAttorneyEmail,
        appointment.DefenseAttorneyEmail,
        appointment.ClaimExaminerEmail);

    public static IReadOnlyList<PartyEmailChange> ChangedRoles(Snapshot before, Snapshot after)
    {
        var changes = new List<PartyEmailChange>();
        Add(changes, PartyEmailRole.Patient, before.Patient, after.Patient);
        Add(changes, PartyEmailRole.ApplicantAttorney, before.ApplicantAttorney, after.ApplicantAttorney);
        Add(changes, PartyEmailRole.DefenseAttorney, before.DefenseAttorney, after.DefenseAttorney);
        Add(changes, PartyEmailRole.ClaimExaminer, before.ClaimExaminer, after.ClaimExaminer);
        return changes;
    }

    private static void Add(List<PartyEmailChange> changes, PartyEmailRole role, string? before, string? after)
    {
        var now = after?.Trim();
        if (string.IsNullOrEmpty(now))
        {
            return;
        }

        if (string.Equals(before?.Trim(), now, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        changes.Add(new PartyEmailChange(role, before, now));
    }
}
