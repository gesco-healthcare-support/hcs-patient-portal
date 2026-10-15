using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Shared by the accepted- and rejected-document handler tests, which assert the same encoding
/// guarantee against the same list builder.
/// </summary>
/// <remarks>
/// The two handlers build <c>RemainingDocumentList</c> with identical code, so the test is
/// identical too. It lives here rather than being written twice: duplicated in both files it
/// tripped SonarCloud's new-code duplication gate, and duplication is a MEASURE rather than an
/// issue, so an exclusion cannot clear it -- only extraction can.
/// </remarks>
internal static class OutstandingDocumentListFixture
{
    /// <summary>
    /// Two outstanding documents whose names carry markup: a tag pair and an ampersand with
    /// quotes. Names are the uploaded file's own, so they are caller-supplied.
    /// </summary>
    public static MissingRequiredDocument[] NamesWithMarkup() =>
    [
        new(Guid.NewGuid(), "<b>TEST-Bold</b>", RequiredDocumentState.NotUploaded),
        new(Guid.NewGuid(), "TEST & \"quoted\"", RequiredDocumentState.Rejected),
    ];

    /// <summary>
    /// What <see cref="NamesWithMarkup"/> must render as once encoded. Unencoded, the first name
    /// would inject a tag into every recipient's email body.
    /// </summary>
    public const string EncodedList =
        "<li>&lt;b&gt;TEST-Bold&lt;/b&gt;</li><li>TEST &amp; &quot;quoted&quot;</li>";
}
