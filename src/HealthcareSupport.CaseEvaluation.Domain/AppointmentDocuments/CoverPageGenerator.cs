using System;
using System.IO;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// W2-11: builds the merged-PDF cover page using MigraDoc.
///
/// Adopts OLD's per-recipient DOCX template field set (confirmation
/// number, appointment date, patient name, appointment type, location,
/// claim/injury details, parties on case, responsible signature) but
/// renders as a single PDF page authored at run time -- not as a
/// pre-authored template. Per-recipient packets with token mail-merge
/// are deferred (logged in the W2-11 ledger) until the manager confirms
/// the exact format.
/// </summary>
public class CoverPageGenerator : ITransientDependency
{
    /// <summary>
    /// Renders the cover page to a fresh in-memory PDF byte array.
    /// </summary>
    public byte[] RenderCoverPagePdf(
        Appointment appointment,
        Patient? patient,
        AppointmentType? appointmentType,
        Location? location,
        string? claimNumber,
        string? bodyPartsSummary,
        string? wcabAdj)
    {
        var doc = BuildDocument(appointment, patient, appointmentType, location, claimNumber, bodyPartsSummary, wcabAdj);
        using var ms = new MemoryStream();
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static Document BuildDocument(
        Appointment appointment,
        Patient? patient,
        AppointmentType? appointmentType,
        Location? location,
        string? claimNumber,
        string? bodyPartsSummary,
        string? wcabAdj)
    {
        var doc = new Document();
        var section = doc.AddSection();
        section.PageSetup.TopMargin = Unit.FromCentimeter(2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var title = section.AddParagraph("CASE FILE PACKET");
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.Alignment = ParagraphAlignment.Center;
        title.Format.SpaceAfter = Unit.FromCentimeter(0.5);

        var sub = section.AddParagraph(
            "This packet contains the cover sheet plus all documents approved by the office for this appointment.");
        sub.Format.Font.Size = 10;
        sub.Format.Font.Italic = true;
        sub.Format.Alignment = ParagraphAlignment.Center;
        sub.Format.SpaceAfter = Unit.FromCentimeter(1);

        var table = section.AddTable();
        table.Borders.Width = 0.5;
        table.AddColumn(Unit.FromCentimeter(5));
        table.AddColumn(Unit.FromCentimeter(11));

        AddRow(table, "Confirmation #", appointment.RequestConfirmationNumber);
        AddRow(table, "Appointment Date", appointment.AppointmentDate.ToString("MMM d, yyyy h:mm tt"));
        AddRow(table, "Patient Name", $"{patient?.FirstName} {patient?.LastName}".Trim());
        AddRow(table, "Patient Email", patient?.Email ?? string.Empty);
        AddRow(table, "Appointment Type", appointmentType?.Name ?? "");
        AddRow(table, "Location", location?.Name ?? "");
        AddRow(table, "Claim #", claimNumber ?? "(not on file)");
        AddRow(table, "WCAB ADJ #", wcabAdj ?? "(not on file)");
        AddRow(table, "Body Parts", bodyPartsSummary ?? "(not on file)");

        section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(1);

        var generatedAt = section.AddParagraph($"Generated: {DateTime.UtcNow:MMM d, yyyy h:mm tt} UTC");
        generatedAt.Format.Font.Size = 9;
        generatedAt.Format.Font.Color = Colors.DarkGray;

        var footer = section.AddParagraph(
            "Confidential -- patient health information. Handle per HIPAA + applicable workers' compensation rules.");
        footer.Format.Font.Size = 9;
        footer.Format.Font.Italic = true;
        footer.Format.Font.Color = Colors.DarkGray;

        return doc;
    }

    private static void AddRow(Table table, string label, string value)
    {
        var row = table.AddRow();
        var labelP = row.Cells[0].AddParagraph(label);
        labelP.Format.Font.Bold = true;
        row.Cells[1].AddParagraph(string.IsNullOrWhiteSpace(value) ? "-" : value);
    }
}
