using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// QuestPDF document replicating OLD's <c>PATIENT PACKET NEW.docx</c> as a
/// PDF. Section order, labels, and token positions follow the audit at
/// <c>docs/parity/packet-generation-audit.md</c> -- the audit is the contract;
/// this template implements it.
///
/// <para>OLD source DOCX:
/// <c>P:\PatientPortalOld\PatientAppointment.Api\wwwroot\Documents\documentBluePrint\patientpacketnew\PATIENT PACKET NEW.docx</c>
/// (343 KB, 822 paragraphs, 13 tables). OLD-rendered text stream:
/// <c>%TEMP%\patient_packet.paragraphs.txt</c>.</para>
///
/// <para>Phase 1 first-cut scope: full section ordering + page header
/// repeat + the cover-letter section (token-heavy). Clinical-form tables
/// (ADL checkbox grid, Pain Questionnaire 0-10 scales, body diagrams) are
/// rendered as structured placeholders pending visual sign-off; once
/// Adrian approves the section structure we iterate per-section.</para>
/// </summary>
public class PatientPacketTemplate : IDocument
{
    /// <summary>
    /// Approximate signature stamp size matching OLD's
    /// <c>InsertAPicture(... 880000L, 880000L ...)</c> in
    /// <c>AppointmentDocumentDomain.cs:965</c>. 880000 EMU = 0.96 in.; OLD
    /// width and height are equal (square).
    /// </summary>
    private const float SignatureSizePoints = 60f; // ~0.83 in. at 72 dpi

    private readonly PacketTokenContext _ctx;

    public PatientPacketTemplate(PacketTokenContext ctx)
    {
        _ctx = ctx;
    }

    public DocumentMetadata GetMetadata()
    {
        return new DocumentMetadata
        {
            Title = $"Patient Packet - {_ctx.RequestConfirmationNumber}",
            Author = "West Coast Spine Institute",
            Subject = "Patient Intake Packet",
            CreationDate = System.DateTimeOffset.Now,
        };
    }

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(0.5f, Unit.Inch);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(t => t.FontSize(10).FontFamily(Fonts.Calibri));

            page.Header().Element(ComposePageHeader);
            page.Content().Element(ComposeContent);
            page.Footer().AlignCenter().Text(t =>
            {
                t.CurrentPageNumber();
                t.Span(" of ");
                t.TotalPages();
            });
        });
    }

    /// <summary>
    /// OLD page-header repeat (paras 0002, 0635, 0665, 2067 in the
    /// audit's paragraph stream). Format: NAME / ACCT / DATE on one row.
    /// </summary>
    private void ComposePageHeader(IContainer container)
    {
        container.PaddingBottom(8).BorderBottom(0.5f).BorderColor(Colors.Black).Row(row =>
        {
            row.RelativeItem(2).Text(t =>
            {
                t.Span("NAME: ").Bold();
                t.Span($"{_ctx.PatientFirstName} {_ctx.PatientLastName}".Trim());
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("ACCT: ").Bold();
                t.Span(_ctx.RequestConfirmationNumber);
            });
            row.RelativeItem(1).Text(t =>
            {
                t.Span("DATE: ").Bold();
                t.Span(_ctx.AvailableDate);
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(8).Column(col =>
        {
            col.Spacing(14);

            ComposeActivitiesOfDailyLiving(col);
            ComposeReleaseOfMedicalRecords(col);
            ComposePresentComplaints(col);
            ComposeAmaGuidelinesPainQuestionnaire(col);
            ComposePatientSignaturePage(col);
            ComposeCoverLetterToPatient(col);
            ComposeCaseInfoBlock(col);
            ComposeResponsibleUserSignOff(col);
            ComposeSecondaryClaimSummary(col);
        });
    }

    // -- Section 1: ACTIVITIES OF DAILY LIVING FORM (paras 0-599) ---------

    private void ComposeActivitiesOfDailyLiving(ColumnDescriptor col)
    {
        col.Item().Element(SectionHeading("ACTIVITIES OF DAILY LIVING FORM"));

        col.Item().Text("Please rate each activity using one of the four columns:")
            .FontSize(9).Italic();

        col.Item().Row(row =>
        {
            row.RelativeItem().Text("[ ] Without difficulty").FontSize(9);
            row.RelativeItem().Text("[ ] With some difficulty").FontSize(9);
            row.RelativeItem().Text("[ ] With much difficulty").FontSize(9);
            row.RelativeItem().Text("[ ] Unable to do").FontSize(9);
        });

        ComposeAdlSubsection(col, "Self-Care, Personal Hygiene",
            "Urinating, Defecating, Brushing Teeth, Combing Hair, Bathing, Dressing Oneself, Eating");
        ComposeAdlSubsection(col, "Communication",
            "Writing, Typing, Seeing, Hearing, Speaking");
        ComposeAdlSubsection(col, "Physical Activity",
            "Standing, Sitting, Reclining, Walking, Climbing Stairs");
        ComposeAdlSubsection(col, "Sensory Function",
            "Hearing, Seeing, Tactile Feeling, Tasting, Smelling");
        ComposeAdlSubsection(col, "Nonspecialized Hand Activities",
            "Grasping, Lifting, Tactile Discrimination");
        ComposeAdlSubsection(col, "Travel",
            "Riding, Driving, Flying");
        ComposeAdlSubsection(col, "Sleep / Sexual Function",
            "Restful, Nocturnal Sleep Pattern, Orgasm, Ejaculation, Lubrication, Erection");
    }

    private static void ComposeAdlSubsection(ColumnDescriptor col, string label, string examples)
    {
        col.Item().PaddingTop(6).Column(inner =>
        {
            inner.Item().Text(t =>
            {
                t.Span($"{label}: ").Bold().FontSize(9);
                t.Span($"(Example -- {examples})").FontSize(8).Italic();
            });
            inner.Item().PaddingTop(2).Row(row =>
            {
                row.RelativeItem().Text("[ ] ___________").FontSize(8);
                row.RelativeItem().Text("[ ] ___________").FontSize(8);
                row.RelativeItem().Text("[ ] ___________").FontSize(8);
                row.RelativeItem().Text("[ ] ___________").FontSize(8);
            });
        });
    }

    // -- Section 2: RELEASE OF MEDICAL RECORDS (paras 603-633) ------------

    private void ComposeReleaseOfMedicalRecords(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().Element(SectionHeading("RELEASE OF MEDICAL RECORDS"));

        col.Item().Text("West Coast Spine Institute").Bold().FontSize(12);
        col.Item().Text("FELLOW, AMERICAN ACADEMY OF ORTHOPAEDIC SURGEONS").FontSize(9);

        col.Item().PaddingTop(8).Column(inner =>
        {
            inner.Spacing(4);
            inner.Item().Text(t =>
            {
                t.Span("Date of Birth: ").Bold();
                t.Span(_ctx.PatientDateOfBirth);
            });
            inner.Item().Text(t =>
            {
                t.Span("Date of Injury: ").Bold();
                t.Span(_ctx.InjuryDateOfInjury);
            });
            inner.Item().Text(t =>
            {
                t.Span("Social Security Number:  ").Bold();
                t.Span(_ctx.PatientSocialSecurityNumber);
            });
        });

        col.Item().PaddingTop(8).Column(inner =>
        {
            inner.Spacing(4);
            ComposeBoldHeading(inner, "Privacy Policy Statement");
            inner.Item().Text("[Privacy Policy Statement body -- TBD per OLD DOCX paragraphs 619-620]")
                .FontSize(9).Italic();

            ComposeBoldHeading(inner, "Notice of Privacy Practice");
            ComposeBoldHeading(inner, "Assigning Privacy");
            ComposeBoldHeading(inner, "Minimum Use and Disclosures of Protected Health Information");
            ComposeBoldHeading(inner, "Marketing");
            ComposeBoldHeading(inner, "Complaints");
            ComposeBoldHeading(inner, "Responsibility and Identification");
        });

        col.Item().PaddingTop(8).Text("West Coast Spine Institute Acknowledgement of Receipt of Notice of Privacy Practices")
            .Bold().FontSize(10);
        col.Item().Text(t =>
        {
            t.Span("Patient: ").Bold();
            t.Span($"{_ctx.PatientFirstName}  {_ctx.PatientLastName}".Trim());
        });
    }

    private static void ComposeBoldHeading(ColumnDescriptor col, string text)
    {
        col.Item().PaddingTop(2).Text(text).Bold().FontSize(10);
    }

    // -- Section 3: PRESENT COMPLAINTS (paras 664-732) --------------------

    private void ComposePresentComplaints(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().Element(SectionHeading("PRESENT COMPLAINTS"));

        col.Item().Text("Please mark all symptoms that apply on the body diagram below:").FontSize(9);

        col.Item().PaddingTop(6).Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("Ache").Bold(); t.Span(" (Dolor)");
            });
            row.RelativeItem().Text(t =>
            {
                t.Span("Burning").Bold(); t.Span(" (Ardor)");
            });
            row.RelativeItem().Text(t =>
            {
                t.Span("Numbness").Bold(); t.Span(" (Entumecimiento)");
            });
        });
        col.Item().Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("Pins and Needles").Bold(); t.Span(" (Hormigueo)");
            });
            row.RelativeItem().Text(t =>
            {
                t.Span("Stabbing").Bold(); t.Span(" (Punalada)");
            });
            row.RelativeItem().Text(t =>
            {
                t.Span("Bruises").Bold(); t.Span(" (Moretones)");
            });
        });

        col.Item().PaddingTop(10).Row(row =>
        {
            row.RelativeItem().Border(0.5f).Padding(10).AlignCenter().Column(inner =>
            {
                inner.Item().Text("BACK").Bold();
                inner.Item().Text("(Parte Posterior)").Italic().FontSize(8);
                inner.Item().PaddingTop(40).Text("[Body Diagram -- BACK view; OLD renders an image here]")
                    .FontSize(8).Italic();
                inner.Item().PaddingBottom(120);
            });
            row.ConstantItem(15);
            row.RelativeItem().Border(0.5f).Padding(10).AlignCenter().Column(inner =>
            {
                inner.Item().Text("FRONT").Bold();
                inner.Item().Text("(Parte Anterior)").Italic().FontSize(8);
                inner.Item().PaddingTop(40).Text("[Body Diagram -- FRONT view; OLD renders an image here]")
                    .FontSize(8).Italic();
                inner.Item().PaddingBottom(120);
            });
        });
    }

    // -- Section 4: AMA GUIDELINES (5TH EDITION) -- PAIN QUESTIONNAIRE ----

    private void ComposeAmaGuidelinesPainQuestionnaire(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().Element(SectionHeading("AMA GUIDELINES (5TH EDITION)"));
        col.Item().Element(SectionSubheading("ACTIVITIES OF DAILY LIVING / PAIN QUESTIONNAIRE"));

        col.Item().PaddingTop(4).Text("I. Pain (Self-report of Severity)").Bold();
        col.Item().Text("Mark the number that best describes your pain level (0 = no pain, 10 = worst pain imaginable):")
            .FontSize(9).Italic();
        for (var sub = 'A'; sub <= 'E'; sub++)
        {
            ComposePainScale(col, $"{sub}.");
        }
        col.Item().PaddingTop(4).Text("Sum score of Section I:").Bold();
        col.Item().Text("A-E = Total pain severity / 5: ____________________").FontSize(9);

        col.Item().PaddingTop(8).Text("II. Activity Limitation of Interference").Bold();
        col.Item().Text("Mark the number that best describes how much pain interferes with each activity:")
            .FontSize(9).Italic();
        for (var sub = 'A'; sub <= 'E'; sub++)
        {
            ComposePainScale(col, $"{sub}.");
        }
        col.Item().PaddingTop(4).Text("Total interference score:").Bold();
    }

    private static void ComposePainScale(ColumnDescriptor col, string label)
    {
        col.Item().PaddingTop(4).Row(row =>
        {
            row.ConstantItem(20).Text(label).FontSize(9);
            row.RelativeItem().Row(scale =>
            {
                for (var i = 0; i <= 10; i++)
                {
                    scale.RelativeItem().AlignCenter().Text($"[ ] {i}").FontSize(9);
                }
            });
        });
    }

    // -- Section 5: Patient signature page (paras 2066-2067) --------------

    private void ComposePatientSignaturePage(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().Row(row =>
        {
            row.RelativeItem(3).Text(t =>
            {
                t.Span("PATIENT NAME (Print)  ").Bold();
                t.Span($"{_ctx.PatientFirstName}   {_ctx.PatientLastName}".Trim());
            });
            row.RelativeItem(2).Text(t =>
            {
                t.Span("DATE: ").Bold();
                t.Span(_ctx.AvailableDate);
            });
        });
        col.Item().PaddingTop(20).Text("Patient Signature: ____________________________________________");
        col.Item().PaddingTop(20).Text("Witness Signature: ____________________________________________");
    }

    // -- Section 6: Cover letter to patient (paras 3040-3050) -------------
    // Token-heavy section -- detailed rendering for Phase 1 sample.

    private void ComposeCoverLetterToPatient(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().AlignRight().Text(_ctx.DateNow);
        col.Item().PaddingTop(20).Text(t =>
        {
            t.Span($"{_ctx.PatientFirstName}  {_ctx.PatientLastName}".Trim());
        });
        col.Item().Text(_ctx.PatientStreet);
        col.Item().Text($"{_ctx.PatientCity},  {_ctx.PatientState} {_ctx.PatientZipCode}".Trim());

        col.Item().PaddingTop(16).Text(t =>
        {
            t.Span("Dear : ").Bold();
            t.Span($"{_ctx.PatientFirstName}  {_ctx.PatientLastName}".Trim());
        });

        col.Item().PaddingTop(8).Text(t =>
        {
            t.Span("Your appointment has been scheduled to see Yuri Falkinstein, M.D. on ");
            t.Span(_ctx.AvailableDate).Bold();
            t.Span(" at ");
            t.Span(_ctx.AppointmentTime).Bold();
            t.Span(". Your appointment will be held at:");
        });

        col.Item().PaddingTop(6).PaddingLeft(20).Column(inner =>
        {
            inner.Item().Text(t =>
            {
                t.Span(_ctx.LocationName).Bold();
                t.Span(" ");
                t.Span(_ctx.LocationAddress);
            });
            inner.Item().Text(
                $"{_ctx.LocationCity},  {_ctx.LocationState} {_ctx.LocationZipCode}".Trim());
        });

        col.Item().PaddingTop(8).Text(t =>
        {
            t.Span("Please be advised that this location has a parking fee of ");
            t.Span(_ctx.LocationParkingFee).Bold();
            t.Span(". Please make sure you keep this letter and bring it with you to your appointment.");
        });
    }

    // -- Section 7: Case-info block (paras 3060-3083) ---------------------

    private void ComposeCaseInfoBlock(ColumnDescriptor col)
    {
        col.Item().PaddingTop(20).Column(inner =>
        {
            inner.Spacing(4);
            inner.Item().Text(t =>
            {
                t.Span("Case Name : ").Bold();
                t.Span($"{_ctx.PatientFirstName} {_ctx.PatientLastName}".Trim());
            });
            inner.Item().Text(t =>
            {
                t.Span("EMPLOYER/PIV: ").Bold();
                t.Span(_ctx.EmployerName);
            });
            inner.Item().Text(t =>
            {
                t.Span("Claim No: ").Bold();
                t.Span(_ctx.InjuryClaimNumber);
            });
            inner.Item().Text(t =>
            {
                t.Span("WC/MS or WCAB Case No. (if any): ").Bold();
                t.Span(_ctx.InjuryWcabAdj);
            });
        });

        col.Item().PaddingTop(12).Text(t =>
        {
            t.Span("I, ").Bold();
            t.Span(_ctx.PrimaryResponsibleUserName).Bold();
            t.Span(", certify the foregoing.");
        });
    }

    // -- Section 8: Sign-off (paras 3098-3102) ----------------------------

    private void ComposeResponsibleUserSignOff(ColumnDescriptor col)
    {
        col.Item().PaddingTop(20).Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("Date: ").Bold();
                t.Span(_ctx.DateNow);
            });
            row.RelativeItem().AlignRight().Text("Please See Attached").Italic().FontSize(9);
        });

        // Signature image + responsible-user-name beneath.
        col.Item().PaddingTop(16).Column(inner =>
        {
            inner.Item().Container().Height(SignatureSizePoints).Width(SignatureSizePoints * 1.5f)
                .Element(c =>
                {
                    if (_ctx.ResponsibleUserSignature != null && _ctx.ResponsibleUserSignature.Length > 0)
                    {
                        c.Image(_ctx.ResponsibleUserSignature).FitArea();
                    }
                    else
                    {
                        // OLD silent-skip per AppointmentDocumentDomain.cs:657:
                        // when the responsible user has no signature on file,
                        // OLD InsertAPicture is never called and the placeholder
                        // text is left in the doc. NEW renders a blank space of
                        // the same size to preserve layout.
                        c.AlignCenter().AlignMiddle().Text("").FontSize(8);
                    }
                });
            inner.Item().PaddingTop(2).Text(_ctx.PrimaryResponsibleUserName).Bold();
        });
    }

    // -- Section 9: Secondary claim summary (paras 3110-3145) -------------

    private void ComposeSecondaryClaimSummary(ColumnDescriptor col)
    {
        col.Item().PageBreak();
        col.Item().Element(SectionHeading("CLAIM SUMMARY"));

        col.Item().PaddingTop(4).Column(inner =>
        {
            inner.Spacing(3);
            inner.Item().Text(t =>
            {
                t.Span("Case Name : ").Bold();
                t.Span($"{_ctx.PatientFirstName} {_ctx.PatientLastName}".Trim());
            });
            inner.Item().Text(t =>
            {
                t.Span("EMPLOYER/PIV: ").Bold();
                t.Span(_ctx.EmployerName);
            });
            inner.Item().Text(t =>
            {
                t.Span("Claim No: ").Bold();
                t.Span(_ctx.InjuryClaimNumber);
            });
            inner.Item().Text(t =>
            {
                t.Span("WC/MS or WCAB Case No. (if any): ").Bold();
                t.Span(_ctx.InjuryWcabAdj);
            });
        });

        col.Item().PaddingTop(10).Element(SectionSubheading("PATIENT"));
        col.Item().Text($"{_ctx.PatientFirstName} {_ctx.PatientLastName}".Trim());
        col.Item().Text(_ctx.PatientStreet);
        col.Item().Text($"{_ctx.PatientCity}, {_ctx.PatientState} {_ctx.PatientZipCode}".Trim());

        col.Item().PaddingTop(10).Element(SectionSubheading("PRIMARY INSURANCE"));
        col.Item().Text(_ctx.InjuryPrimaryInsuranceName);
        col.Item().Text(_ctx.InjuryPrimaryInsuranceStreet);
        col.Item().Text($"{_ctx.InjuryPrimaryInsuranceCity}, {_ctx.InjuryPrimaryInsuranceState} {_ctx.InjuryPrimaryInsuranceZip}".Trim());

        col.Item().PaddingTop(10).Element(SectionSubheading("APPLICANT ATTORNEY"));
        col.Item().Text(_ctx.PatientAttorneyName);
        col.Item().Text(_ctx.PatientAttorneyStreet);
        col.Item().Text($"{_ctx.PatientAttorneyCity}, {_ctx.PatientAttorneyState}  {_ctx.PatientAttorneyZip}".Trim());

        col.Item().PaddingTop(10).Element(SectionSubheading("DEFENSE ATTORNEY"));
        col.Item().Text(_ctx.DefenseAttorneyName);
        col.Item().Text(_ctx.DefenseAttorneyStreet);
        col.Item().Text($"{_ctx.DefenseAttorneyCity}, {_ctx.DefenseAttorneyState} {_ctx.DefenseAttorneyZip}".Trim());

        col.Item().PaddingTop(10).Element(SectionSubheading("WCAB OFFICE"));
        col.Item().Text(_ctx.InjuryWcabOfficeName);
        col.Item().Text(_ctx.InjuryWcabOfficeAddress);
        col.Item().Text($"{_ctx.InjuryWcabOfficeCity},{_ctx.InjuryWcabOfficeState} {_ctx.InjuryWcabOfficeZipCode}".Trim());
    }

    // -- Helpers ----------------------------------------------------------

    /// <summary>Bold + centered + larger font, used for major section headings.</summary>
    private static System.Action<IContainer> SectionHeading(string text)
    {
        return container => container.AlignCenter()
            .Text(text).Bold().FontSize(14);
    }

    /// <summary>Bold + centered + medium font, for sub-section headings.</summary>
    private static System.Action<IContainer> SectionSubheading(string text)
    {
        return container => container.AlignCenter()
            .Text(text).Bold().FontSize(11);
    }
}
