using System;
using HealthcareSupport.CaseEvaluation.Branding;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The owning office's letterhead facts, so the Case Tracker can print them on its own letters
/// (ADDED 2026-11-15, contract section A <c>data.office</c>). Intake bodies only; a document update
/// never carries it.
///
/// <para>Each value is the EFFECTIVE one the patient packet prints, from
/// <see cref="OfficeLetterheadResolver"/>, so the packet and the Case Tracker cannot disagree about
/// an office's own address. A blank is sent as <c>null</c>, never as an empty string. In practice
/// <see cref="PracticeName"/> is never blank: it falls back to the office display name, then to the
/// doctor's name (<see cref="OfficeLetterhead.Compose"/>).</para>
///
/// <para>A letterhead edit by itself queues nothing. Each intake body carries the letterhead as it
/// was when that body was built, and the appointment's NEXT intake carries any newer values.</para>
/// </summary>
public class IntakeOfficeSection
{
    /// <summary>The practice name the packet prints, e.g. above the records-release address.</summary>
    public string? PracticeName { get; set; }

    /// <summary>One address row per line, separated by <c>"\n"</c> (never <c>"\r\n"</c>).</summary>
    public string? RecordsReleaseAddress { get; set; }

    /// <summary>As entered on the letterhead; not reformatted.</summary>
    public string? Phone { get; set; }

    /// <summary>As entered on the letterhead; not reformatted.</summary>
    public string? Fax { get; set; }

    /// <summary>
    /// Maps the effective letterhead to the wire section. Line endings in the records-release
    /// address are normalised to <c>"\n"</c>: nothing on the save path does it, the packet's HTML
    /// hides a stray <c>"\r"</c>, and the JSON would not.
    /// </summary>
    public static IntakeOfficeSection From(OfficeLetterhead letterhead)
    {
        ArgumentNullException.ThrowIfNull(letterhead);

        return new IntakeOfficeSection
        {
            PracticeName = NullIfBlank(letterhead.PracticeName),
            RecordsReleaseAddress = NullIfBlank(letterhead.RecordsReleaseAddress)?.ReplaceLineEndings("\n"),
            Phone = NullIfBlank(letterhead.Phone),
            Fax = NullIfBlank(letterhead.Fax),
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
