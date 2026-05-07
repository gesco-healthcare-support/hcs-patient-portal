using System;
using System.IO;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;

/// <summary>
/// Phase 1 sample-PDF generator. Run via
/// <c>dotnet test --filter "FullyQualifiedName~PatientPacketSampleGenerator"</c>;
/// emits <c>docs/parity/samples/patient-packet-sample.pdf</c> off the
/// repo root for visual side-by-side review against
/// <c>P:\PatientPortalOld\...\PATIENT PACKET NEW.docx</c>.
///
/// <para>NOT a verification test -- this generates an artifact for human
/// review. There is no Assert beyond a non-empty file size sanity check.</para>
///
/// <para>Synthetic data ONLY (per <c>~/.claude/rules/hipaa-data.md</c>);
/// every field uses obviously-fake values that nonetheless exercise the
/// full token surface so layout deltas surface in the side-by-side.</para>
/// </summary>
public class PatientPacketSampleGenerator
{
    static PatientPacketSampleGenerator()
    {
        // The Domain module sets this in ConfigureServices, but this test
        // calls QuestPDF.Document.Create directly without booting the
        // ABP host. License must be set before any Generate* call.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Generate_PatientPacket_Sample()
    {
        var ctx = BuildSyntheticContext();

        var bytes = Document
            .Create(c => new PatientPacketTemplate(ctx).Compose(c))
            .GeneratePdf();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, "PDF should be at least 1 KB");

        var samplePath = ResolveSamplePath("patient-packet-sample.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(samplePath)!);
        File.WriteAllBytes(samplePath, bytes);
    }

    /// <summary>
    /// Builds a context that exercises every token slot so visual deltas
    /// surface in the side-by-side review. All values are upper-case to
    /// match the OLD <c>.ToUpper()</c> behavior at
    /// <c>AppointmentDocumentDomain.cs:1070</c> -- the resolver applies
    /// ToUpper, and tests provide values as the resolver would supply them.
    /// </summary>
    private static PacketTokenContext BuildSyntheticContext()
    {
        return new PacketTokenContext
        {
            // Patients
            PatientFirstName = "JANE",
            PatientLastName = "SAMPLE",
            PatientMiddleName = "Q",
            PatientDateOfBirth = "01/15/1980",
            PatientSocialSecurityNumber = "XXX-XX-1234",
            PatientStreet = "123 SAMPLE ST",
            PatientCity = "LOS ANGELES",
            PatientState = "CALIFORNIA",
            PatientZipCode = "90001",

            // Appointments
            RequestConfirmationNumber = "APT-100001",
            AvailableDate = "06/15/2026",
            AppointmentTime = "9:00 AM",
            AppointmentType = "AME",
            LocationName = "DEMO CLINIC",
            LocationAddress = "456 DEMO BLVD",
            LocationCity = "ENCINO",
            LocationState = "CALIFORNIA",
            LocationZipCode = "91426",
            LocationParkingFee = "10",
            PrimaryResponsibleUserName = "DR. SAMPLE STAFF",
            ResponsibleUserSignature = null, // OLD silent-skip when null

            // Employer
            EmployerName = "DEMO EMPLOYER LLC",
            EmployerStreet = "789 EMPLOYER WAY",
            EmployerCity = "BURBANK",
            EmployerState = "CALIFORNIA",
            EmployerZip = "91505",

            // Patient (Applicant) attorney
            PatientAttorneyName = "ATTORNEY APPLICANT",
            PatientAttorneyStreet = "100 APPLICANT LN",
            PatientAttorneyCity = "GLENDALE",
            PatientAttorneyState = "CALIFORNIA",
            PatientAttorneyZip = "91201",

            // Defense attorney
            DefenseAttorneyName = "ATTORNEY DEFENSE",
            DefenseAttorneyStreet = "200 DEFENSE DR",
            DefenseAttorneyCity = "PASADENA",
            DefenseAttorneyState = "CALIFORNIA",
            DefenseAttorneyZip = "91101",

            // Injury -- single injury so concat string has trailing space
            InjuryClaimNumber = "CLM-555555 ",
            InjuryDateOfInjury = "03/01/2026 ",
            InjuryWcabAdj = "ADJ-9999 ",
            InjuryWcabOfficeName = "VAN NUYS WCAB ",
            InjuryWcabOfficeAddress = "6150 VAN NUYS BLVD ",
            InjuryWcabOfficeCity = "VAN NUYS ",
            InjuryWcabOfficeState = "CALIFORNIA ",
            InjuryWcabOfficeZipCode = "91401 ",
            InjuryPrimaryInsuranceName = "DEMO INSURANCE CO ",
            InjuryPrimaryInsuranceStreet = "300 INSURANCE PKWY ",
            InjuryPrimaryInsuranceCity = "LONG BEACH ",
            InjuryPrimaryInsuranceState = "CALIFORNIA ",
            InjuryPrimaryInsuranceZip = "90802 ",
            InjuryClaimExaminerName = "EXAMINER SAMPLE ",

            // Others
            DateNow = DateTime.Today.ToString("MM/dd/yyyy").ToUpper(),
        };
    }

    /// <summary>
    /// Walks up from the test assembly location to the repo root
    /// (the directory containing the <c>docs/</c> folder), then resolves
    /// <c>docs/parity/samples/{fileName}</c>. Allows the test to run from
    /// any CWD without a hard-coded absolute path.
    /// </summary>
    private static string ResolveSamplePath(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
        {
            dir = dir.Parent;
        }
        if (dir == null)
        {
            throw new InvalidOperationException(
                "Could not locate repo root (no ancestor contains a 'docs' folder).");
        }
        return Path.Combine(dir.FullName, "docs", "parity", "samples", fileName);
    }
}
