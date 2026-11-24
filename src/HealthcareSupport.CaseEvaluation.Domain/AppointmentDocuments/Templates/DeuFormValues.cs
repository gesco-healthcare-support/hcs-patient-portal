using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// The 19 values pre-filled on the DWC Employee's Disability Questionnaire (DEU form 100), the two pages the patient
/// packet ends with for a Panel QME patient with no applicant attorney (<c>patient-deu</c>, 2026-11-24). Rendered as
/// the <c>##Deu.*##</c> tokens.
///
/// <para><b>Empty means unknown.</b> The template prints a value when it is not empty, and renders an empty fillable
/// field under the field's old name when it is (the Case Tracker fills the packet digitally, so a printed blank line
/// could never be filled). So every value here is TRIMMED, and a value made only of spaces is <c>""</c>.</para>
///
/// <para>Values are upper-cased like every other per-case packet value, except the doctor's name, which is the
/// office's own letterhead text and is printed as the office wrote it.</para>
/// </summary>
public sealed record DeuFormValues
{
    /// <summary>The claim lines on the form. A fifth line carries any further claims, so none is dropped.</summary>
    public const int ClaimLines = 5;

    /// <summary>Between the two dates of a cumulative injury: an EN DASH with spaces, as the Case Tracker asked.</summary>
    public const string RangeSeparator = " \u2013 ";

    /// <summary>Between the dates of several injuries.</summary>
    public const string InjurySeparator = "; ";

    /// <summary>Between the claims that share the fifth line.</summary>
    public const string OverflowSeparator = ", ";

    public string FirstName { get; init; } = string.Empty;
    public string MiddleInitial { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    /// <summary>Digits only: the form says "SSN (Numbers Only)".</summary>
    public string Ssn { get; init; } = string.Empty;

    public string Street1 { get; init; } = string.Empty;

    /// <summary>The unit ("Unit #" on the booking form; the snapshot's <c>Unit</c>).</summary>
    public string Street2 { get; init; } = string.Empty;

    public string City { get; init; } = string.Empty;

    /// <summary>The state's NAME: the portal stores no state code.</summary>
    public string State { get; init; } = string.Empty;

    public string Zip { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string DateOfInjury { get; init; } = string.Empty;
    public string Employer { get; init; } = string.Empty;
    public string Claim1 { get; init; } = string.Empty;
    public string Claim2 { get; init; } = string.Empty;
    public string Claim3 { get; init; } = string.Empty;
    public string Claim4 { get; init; } = string.Empty;
    public string Claim5 { get; init; } = string.Empty;
    public string DoctorName { get; init; } = string.Empty;
    public string ExamScheduled { get; init; } = string.Empty;

    /// <summary>
    /// Builds the values from the records the packet resolver already loaded.
    /// </summary>
    /// <param name="patient">The booked-time patient snapshot; null leaves the patient values unknown.</param>
    /// <param name="stateName">The patient's state name as resolved for the other packet tokens.</param>
    /// <param name="injuries">The appointment's live injuries, in any order.</param>
    /// <param name="employerName">The newest live employer row's name, or null.</param>
    /// <param name="physicianName">The office letterhead's physician name.</param>
    /// <param name="examDate">The exam date, already formatted MM/dd/yyyy, or "".</param>
    /// <param name="examTime">The exam time, already formatted h:mm tt, or "".</param>
    public static DeuFormValues Compose(
        AppointmentPatientSnapshot? patient,
        string? stateName,
        IEnumerable<AppointmentInjuryDetail> injuries,
        string? employerName,
        string? physicianName,
        string? examDate,
        string? examTime)
    {
        var ordered = OrderInjuries(injuries);
        var claims = ClaimLinesOf(ordered.Select(i => i.ClaimNumber));
        return new DeuFormValues
        {
            FirstName = Upper(patient?.FirstName),
            MiddleInitial = Initial(patient?.MiddleName),
            LastName = Upper(patient?.LastName),
            Ssn = DigitsOnly(patient?.SocialSecurityNumber),
            Street1 = Upper(patient?.Street),
            Street2 = Upper(patient?.Unit),
            City = Upper(patient?.City),
            State = Upper(stateName),
            Zip = Upper(patient?.ZipCode),
            DateOfBirth = PacketDateStamp.Format(patient?.DateOfBirth).Trim(),
            DateOfInjury = DateOfInjuryLine(ordered),
            Employer = Upper(employerName),
            Claim1 = claims[0],
            Claim2 = claims[1],
            Claim3 = claims[2],
            Claim4 = claims[3],
            Claim5 = claims[4],
            DoctorName = (physicianName ?? string.Empty).Trim(),
            ExamScheduled = ExamScheduledLine(examDate, examTime),
        };
    }

    /// <summary>
    /// The order both the claim lines and the dates follow: by date of injury, then by when the row was created, then
    /// by id, so two injuries on one day always come out the same way. The query that loads them has no ORDER BY.
    /// </summary>
    public static IReadOnlyList<AppointmentInjuryDetail> OrderInjuries(IEnumerable<AppointmentInjuryDetail> injuries) =>
        injuries
            .OrderBy(i => i.DateOfInjury)
            .ThenBy(i => i.CreationTime)
            .ThenBy(i => i.Id)
            .ToList();

    /// <summary>
    /// Exactly <see cref="ClaimLines"/> lines. Distinct claim numbers (trimmed, case-insensitive; the first spelling
    /// wins), in the order given; lines 1-4 take one each, line 5 takes the fifth and every one after it joined with
    /// <see cref="OverflowSeparator"/>; unused lines are "".
    /// </summary>
    public static string[] ClaimLinesOf(IEnumerable<string?> claimNumbers)
    {
        var distinct = new List<string>();
        foreach (var claim in claimNumbers.Select(Upper).Where(c => c.Length > 0))
        {
            if (!distinct.Contains(claim, StringComparer.OrdinalIgnoreCase))
            {
                distinct.Add(claim);
            }
        }

        var lines = new string[ClaimLines];
        for (var i = 0; i < ClaimLines - 1; i++)
        {
            lines[i] = i < distinct.Count ? distinct[i] : string.Empty;
        }

        lines[ClaimLines - 1] = string.Join(OverflowSeparator, distinct.Skip(ClaimLines - 1));
        return lines;
    }

    /// <summary>
    /// One date per injury, in the order given, joined with <see cref="InjurySeparator"/>. A cumulative injury with an
    /// end date prints as "from" + <see cref="RangeSeparator"/> + "to".
    /// </summary>
    public static string DateOfInjuryLine(IEnumerable<AppointmentInjuryDetail> orderedInjuries) =>
        string.Join(InjurySeparator, orderedInjuries.Select(i =>
            i.IsCumulativeInjury && i.ToDateOfInjury.HasValue
                ? PacketDateStamp.Format(i.DateOfInjury) + RangeSeparator + PacketDateStamp.Format(i.ToDateOfInjury)
                : PacketDateStamp.Format(i.DateOfInjury)));

    /// <summary>"12/18/2026 at 8:30 AM"; the date alone when there is no time; "" when there is no date.</summary>
    public static string ExamScheduledLine(string? examDate, string? examTime)
    {
        var date = (examDate ?? string.Empty).Trim();
        var time = (examTime ?? string.Empty).Trim();
        if (date.Length == 0)
        {
            return string.Empty;
        }

        return time.Length == 0 ? date : $"{date} at {time}";
    }

    /// <summary>The first character of the trimmed middle name, upper-cased; "" when there is none.</summary>
    public static string Initial(string? middleName)
    {
        var trimmed = (middleName ?? string.Empty).Trim();
        return trimmed.Length == 0 ? string.Empty : Upper(trimmed[..1]);
    }

    /// <summary>The digits of a stored SSN; "" when it has none.</summary>
    public static string DigitsOnly(string? value) =>
        new((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    private static string Upper(string? value) =>
        (value ?? string.Empty).Trim().ToUpper(CultureInfo.InvariantCulture);
}
