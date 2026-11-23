using System;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <c>data.employer</c>: the patient's employer as recorded on the appointment. One per appointment, not per
/// injury, matching <see cref="AppointmentEmployerDetail"/>.
///
/// <para><b>The key names are FIXED.</b> The Case Tracker reads exactly <c>name</c>, <c>occupation</c>,
/// <c>phoneNumber</c>, <c>street</c>, <c>city</c>, <c>state</c> and <c>zipCode</c> (agreed 2026-11-23). Renaming,
/// adding or moving one is a contract change: tell the Case Tracker first. A test pins the exact set.</para>
///
/// <para><b>Values.</b> <see cref="Name"/> and <see cref="Occupation"/> are required on the row and are sent as
/// stored; a blank one is sent blank, never invented. The optional fields are sent raw, or <c>null</c> when blank
/// or whitespace, never <c>""</c> (the same rule as <see cref="IntakeOfficeSection"/>). <see cref="State"/> is the
/// State's NAME, as in the other party blocks. Nothing is upper-cased; only the packets do that.</para>
/// </summary>
public class IntakeEmployerSection
{
    public string Name { get; set; } = string.Empty;

    public string Occupation { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    /// <summary>The State's name, or null when the row has no state or it no longer resolves.</summary>
    public string? State { get; set; }

    public string? ZipCode { get; set; }

    /// <summary>Maps one employer row; <paramref name="stateName"/> is the resolved name of its StateId, or null.</summary>
    public static IntakeEmployerSection From(AppointmentEmployerDetail row, string? stateName)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new IntakeEmployerSection
        {
            Name = row.EmployerName,
            Occupation = row.Occupation,
            PhoneNumber = NullIfBlank(row.PhoneNumber),
            Street = NullIfBlank(row.Street),
            City = NullIfBlank(row.City),
            State = NullIfBlank(stateName),
            ZipCode = NullIfBlank(row.ZipCode),
        };
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
