using System;
using System.Collections.Generic;
using System.Text;
using HealthcareSupport.CaseEvaluation.AppointmentBodyParts;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Reports.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// The demographics PDF's per-appointment party sections the existing smoke tests leave out: the
/// Insurance, Claim Examiner and Defense Attorney sections, and an injury whose body parts are
/// listed individually rather than as a summary. QuestPDF compresses its content streams, so the
/// test cannot read the text back; it asserts a valid PDF that is LARGER than the same appointment
/// rendered without those sections, which is only true if the sections were composed.
/// </summary>
public class AppointmentDemographicsPdfPartySectionsTests
{
    static AppointmentDemographicsPdfPartySectionsTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void Renders_the_insurance_claim_examiner_defense_and_listed_body_part_sections()
    {
        var bare = new AppointmentDemographicsPdfDocument(Appointment()).GeneratePdf();

        var full = Appointment();
        full.PrimaryInsurance = new AppointmentPrimaryInsuranceDto
        {
            Name = "TEST-Insurer",
            PhoneNumber = "555-0110",
            FaxNumber = "555-0111",
            Street = "TEST-1 Policy Way",
            City = "TEST-Town",
            Zip = "90001",
        };
        full.ClaimExaminer = new AppointmentClaimExaminerDto
        {
            Name = "TEST-Examiner",
            Email = "examiner@test.local",
            PhoneNumber = "555-0120",
            Fax = "555-0121",
            Street = "TEST-2 Claim St",
            City = "TEST-Town",
            Zip = "90002",
        };
        full.AppointmentDefenseAttorney = new AppointmentDefenseAttorneyWithNavigationPropertiesDto
        {
            AppointmentDefenseAttorney = new AppointmentDefenseAttorneyDto(),
            DefenseAttorney = new DefenseAttorneyDto
            {
                FirstName = "TEST-Dana",
                LastName = "TEST-Defense",
                FirmName = "TEST-Defense LLP",
                PhoneNumber = "555-0130",
                FaxNumber = "555-0131",
                WebAddress = "https://defense.example.test",
                Street = "TEST-3 Court Rd",
                City = "TEST-Town",
                ZipCode = "90003",
            },
        };
        full.AppointmentInjuryDetails = new List<AppointmentInjuryDetailWithNavigationPropertiesDto>
        {
            new()
            {
                AppointmentInjuryDetail = new AppointmentInjuryDetailDto
                {
                    DateOfInjury = new DateTime(2025, 1, 15), ClaimNumber = "TEST-CLM-1", IsCumulativeInjury = false,
                    WcabAdj = "TEST-ADJ-1", BodyPartsSummary = "TEST-summary that the listed parts replace",
                },
                BodyParts = new List<AppointmentBodyPartDto>
                {
                    new() { BodyPartDescription = "TEST-left knee" },
                    new() { BodyPartDescription = "TEST-lower back" },
                },
            },
        };

        var bytes = new AppointmentDemographicsPdfDocument(full).GeneratePdf();

        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        bytes.Length.ShouldBeGreaterThan(bare.Length);
    }

    private static AppointmentWithNavigationPropertiesDto Appointment() => new()
    {
        Appointment = new AppointmentDto
        {
            RequestConfirmationNumber = "A90003",
            AppointmentDate = new DateTime(2026, 6, 11, 9, 30, 0),
            AppointmentStatus = AppointmentStatusType.Approved,
        },
    };
}
