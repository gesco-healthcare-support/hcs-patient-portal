using System.Globalization;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthcareSupport.CaseEvaluation.Reports.Pdf;

/// <summary>
/// G-08-04 (2026-06-07) -- the per-appointment Patient Demographics intake sheet,
/// rendered with QuestPDF (reuses the G-08-03 groundwork). Mirrors the legacy
/// sheet's sections from the already-masked appointment nav graph: SSN arrives
/// masked (last-4) on the DTO; this document additionally renders the date of
/// birth as the birth year only (Adrian's HIPAA call). It only formats -- it
/// never un-masks.
///
/// <para>Two legacy sections are intentionally omitted in this version, each
/// flagged in <c>docs/parity/_parity-flags.md</c>: the Appointment Accessors
/// section (the read DTO exposes only access rights + a user id, not the
/// invitee name/email) and the Custom Form section (custom-field values are
/// not on the read graph). Both await a read-DTO extension.</para>
/// </summary>
internal sealed class AppointmentDemographicsPdfDocument : IDocument
{
    // QuestPDF's Material Green 500 == the legacy header (#4CAF50).
    private static readonly Color HeaderColor = Colors.Green.Medium;

    private readonly AppointmentWithNavigationPropertiesDto _appointment;

    public AppointmentDemographicsPdfDocument(AppointmentWithNavigationPropertiesDto appointment)
    {
        _appointment = appointment;
    }

    public DocumentMetadata GetMetadata() => new() { Title = "Patient Demographics" };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(text => text.FontSize(9));

            page.Header().Column(header =>
            {
                header.Item().Text("Patient Demographics").FontSize(16).Bold();
                var confirmation = _appointment.Appointment?.RequestConfirmationNumber;
                if (!string.IsNullOrWhiteSpace(confirmation))
                {
                    header.Item().Text($"Confirmation No: {confirmation}").FontSize(10);
                }
            });

            page.Content().PaddingVertical(8).Column(body =>
            {
                body.Spacing(8);
                ComposeSections(body);
            });

            page.Footer().AlignRight().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private void ComposeSections(ColumnDescriptor body)
    {
        var appointment = _appointment.Appointment;
        var patient = _appointment.Patient;

        Section(body, "Appointment Details",
            ("Appointment Type", _appointment.AppointmentType?.Name),
            ("Location", _appointment.Location?.Name),
            ("Appointment Date", Format(appointment?.AppointmentDate)),
            ("Status", appointment?.AppointmentStatus.ToString()),
            ("Panel Number", appointment?.PanelNumber),
            ("Confirmation No", appointment?.RequestConfirmationNumber),
            ("Due Date", Format(appointment?.DueDate)),
            ("Approved Date", Format(appointment?.AppointmentApproveDate)));

        Section(body, "Patient Details",
            ("Name", ComposePatientName(patient)),
            ("Date of Birth", DobVisibility.ToYearOnly(patient?.DateOfBirth)),
            ("Social Security Number", patient?.SocialSecurityNumber),
            ("Email", patient?.Email ?? appointment?.PatientEmail),
            ("Phone", patient?.PhoneNumber),
            ("Cell Phone", patient?.CellPhoneNumber),
            ("Address", ComposeAddress(patient?.Street ?? patient?.Address, patient?.City, patient?.ZipCode)),
            ("Other Language", patient?.OthersLanguageName),
            ("Interpreter Vendor", patient?.InterpreterVendorName),
            ("Referred By", appointment?.RefferedBy));

        var employer = _appointment.AppointmentEmployerDetail?.AppointmentEmployerDetail;
        if (employer != null)
        {
            Section(body, "Employer Details",
                ("Employer", employer.EmployerName),
                ("Occupation", employer.Occupation),
                ("Phone", employer.PhoneNumber),
                ("Address", ComposeAddress(employer.Street, employer.City, employer.ZipCode)));
        }

        ComposeInjuries(body);

        // #296 (2026-06-07 merge) moved Claim Examiner + Primary Insurance from
        // per-injury to per-appointment, so they render once here.
        var insurance = _appointment.PrimaryInsurance;
        if (insurance != null)
        {
            Section(body, "Insurance",
                ("Company", insurance.Name),
                ("Phone", insurance.PhoneNumber),
                ("Fax", insurance.FaxNumber),
                ("Address", ComposeAddress(insurance.Street, insurance.City, insurance.Zip)));
        }

        var examiner = _appointment.ClaimExaminer;
        if (examiner != null)
        {
            Section(body, "Claim Examiner",
                ("Name", examiner.Name),
                ("Email", examiner.Email),
                ("Phone", examiner.PhoneNumber),
                ("Fax", examiner.Fax),
                ("Address", ComposeAddress(examiner.Street, examiner.City, examiner.Zip)));
        }

        var applicant = _appointment.AppointmentApplicantAttorney?.ApplicantAttorney;
        if (applicant != null)
        {
            Section(body, "Applicant Attorney",
                ("Name", ComposeName(applicant.FirstName, applicant.LastName)),
                ("Firm", applicant.FirmName),
                ("Email", appointment?.ApplicantAttorneyEmail),
                ("Phone", applicant.PhoneNumber),
                ("Fax", applicant.FaxNumber),
                ("Web", applicant.WebAddress),
                ("Address", applicant.FirmAddress ?? ComposeAddress(applicant.Street, applicant.City, applicant.ZipCode)));
        }

        var defense = _appointment.AppointmentDefenseAttorney?.DefenseAttorney;
        if (defense != null)
        {
            Section(body, "Defense Attorney",
                ("Name", ComposeName(defense.FirstName, defense.LastName)),
                ("Firm", defense.FirmName),
                ("Email", appointment?.DefenseAttorneyEmail),
                ("Phone", defense.PhoneNumber),
                ("Fax", defense.FaxNumber),
                ("Web", defense.WebAddress),
                ("Address", defense.FirmAddress ?? ComposeAddress(defense.Street, defense.City, defense.ZipCode)));
        }
    }

    private void ComposeInjuries(ColumnDescriptor body)
    {
        var injuries = _appointment.AppointmentInjuryDetails ?? new();
        var index = 0;
        foreach (var injuryNav in injuries)
        {
            index++;
            var injury = injuryNav.AppointmentInjuryDetail;
            var bodyParts = injuryNav.BodyParts != null && injuryNav.BodyParts.Count > 0
                ? string.Join(", ", injuryNav.BodyParts.Select(b => b.BodyPartDescription))
                : injury?.BodyPartsSummary;

            Section(body, $"Injury {index}",
                ("Cumulative Trauma", injury == null ? null : (injury.IsCumulativeInjury ? "Yes" : "No")),
                ("WCAB / ADJ", injury?.WcabAdj),
                ("Date of Injury", Format(injury?.DateOfInjury)),
                ("To Date of Injury", Format(injury?.ToDateOfInjury)),
                ("Claim Number", injury?.ClaimNumber),
                ("WCAB Office", injuryNav.WcabOffice?.Name),
                ("Body Parts", bodyParts));
        }
    }

    // Renders a green section header + a label/value grid, skipping empty values.
    // The whole section is omitted when it has no populated fields.
    private static void Section(ColumnDescriptor body, string title, params (string Label, string? Value)[] fields)
    {
        var populated = fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
        if (populated.Count == 0)
        {
            return;
        }

        body.Item().Column(section =>
        {
            section.Item().Background(HeaderColor).Padding(4).Text(title).FontColor(Colors.White).Bold();
            section.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                foreach (var (label, value) in populated)
                {
                    table.Cell().Padding(3).Text(label).SemiBold();
                    table.Cell().Padding(3).Text(value);
                }
            });
        });
    }

    private static string? ComposePatientName(PatientDto? patient)
    {
        if (patient == null)
        {
            return null;
        }

        var name = $"{patient.LastName}, {patient.FirstName}";
        return string.IsNullOrWhiteSpace(patient.MiddleName) ? name : $"{name} {patient.MiddleName}";
    }

    private static string? ComposeName(string? first, string? last)
    {
        var name = $"{first} {last}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static string? ComposeAddress(string? street, string? city, string? zip)
    {
        var parts = new[] { street, city, zip }.Where(p => !string.IsNullOrWhiteSpace(p));
        var address = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(address) ? null : address;
    }

    private static string? Format(System.DateTime? value)
    {
        return value?.ToString("g", CultureInfo.InvariantCulture);
    }
}
