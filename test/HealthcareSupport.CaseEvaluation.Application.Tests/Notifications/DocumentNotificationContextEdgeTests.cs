using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Edge cases of <see cref="DocumentNotificationContext"/> that neither
/// <c>DocumentNotificationContextUnitTests</c> nor the document email handler tests reach.
///
/// <para><b>WHAT IS PINNED.</b> A patient with only a first name or only a last name gets that one
/// name as their full name, trimmed, with no stray space; and an unknown document email kind is
/// refused with <see cref="ArgumentOutOfRangeException"/> on each of the three template routes, rather
/// than silently picking a template. The results asserted are the returned variable and the thrown
/// error. Synthetic data only (HIPAA).</para>
/// </summary>
public class DocumentNotificationContextEdgeTests
{
    [Theory]
    [InlineData("  TEST-First  ", null, "TEST-First")]
    [InlineData(null, "  TEST-Last  ", "TEST-Last")]
    [InlineData("TEST-First", "   ", "TEST-First")]
    [InlineData("   ", "TEST-Last", "TEST-Last")]
    public void BuildVariables_WithOnlyOneNamePart_UsesThatPartAloneAsTheFullName(
        string? first, string? last, string expectedFullName)
    {
        var variables = DocumentNotificationContext.BuildVariables(
            patientFirstName: first,
            patientLastName: last,
            patientEmail: null,
            requestConfirmationNumber: "TEST-N0001",
            appointmentDate: null,
            claimNumber: null,
            wcabAdj: null,
            documentName: null,
            rejectionNotes: null,
            clinicName: null,
            portalUrl: null);

        variables["PatientFullName"].ShouldBe(expectedFullName);
    }

    /// <summary>
    /// Each of the three routes -- Joint Declaration, ad-hoc, package -- has its own switch, and each
    /// refuses a kind it does not know.
    /// </summary>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void ClassifyDocumentTemplateCode_AnUnknownKind_IsRefused(bool isAdHoc, bool isJointDeclaration)
    {
        var thrown = Should.Throw<ArgumentOutOfRangeException>(
            () => DocumentNotificationContext.ClassifyDocumentTemplateCode((DocumentEmailKind)99, isAdHoc, isJointDeclaration));

        thrown.ParamName.ShouldBe("kind");
    }
}
