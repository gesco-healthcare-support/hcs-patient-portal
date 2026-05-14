using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- pure unit tests for the
/// <see cref="DocumentUploadGate"/> helpers. Replicates OLD's
/// <c>UpdateValidation</c> + <c>GetValidation</c> +
/// JDF-availability + accepted-immutability semantics from
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:64-75
/// + 90-107 + JDF parallels.
///
/// <para>Test appointments are constructed via
/// <c>new Appointment(...)</c> directly per the user's repeating
/// directive. Documents via <c>new AppointmentDocument(...)</c>.</para>
/// </summary>
public class DocumentUploadGateUnitTests
{
    // ------------------------------------------------------------------
    // EnsureAppointmentApprovedAndNotPastDueDate
    // OLD: AppointmentDocumentDomain.cs:90-107
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureAppointmentApprovedAndNotPastDueDate_Approved_FutureDueDate_DoesNotThrow()
    {
        var appointment = NewAppointment(
            status: AppointmentStatusType.Approved,
            dueDate: DateTime.UtcNow.AddDays(7));

        Should.NotThrow(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
    }

    [Fact]
    public void EnsureAppointmentApprovedAndNotPastDueDate_RescheduleRequested_FutureDueDate_DoesNotThrow()
    {
        // OLD allows upload during RescheduleRequested too (line 95).
        var appointment = NewAppointment(
            status: AppointmentStatusType.RescheduleRequested,
            dueDate: DateTime.UtcNow.AddDays(3));

        Should.NotThrow(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    public void EnsureAppointmentApprovedAndNotPastDueDate_NonApprovedStatus_Throws(
        AppointmentStatusType status)
    {
        var appointment = NewAppointment(status, dueDate: DateTime.UtcNow.AddDays(7));

        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUploadAfterApproval);
    }

    [Fact]
    public void EnsureAppointmentApprovedAndNotPastDueDate_PastDueDate_Throws()
    {
        var appointment = NewAppointment(
            status: AppointmentStatusType.Approved,
            dueDate: DateTime.UtcNow.AddDays(-1));

        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUploadAfterDueDate);
    }

    [Fact]
    public void EnsureAppointmentApprovedAndNotPastDueDate_NullDueDate_DoesNotThrow()
    {
        // No DueDate means no cutoff to enforce; OLD's check at
        // line 97 uses `appointment.DueDate < DateTime.Now` which
        // throws on null DueDate (NRE). NEW's null-guard is the
        // OLD-bug-fix.
        var appointment = NewAppointment(
            status: AppointmentStatusType.Approved,
            dueDate: null);

        Should.NotThrow(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
    }

    [Fact]
    public void EnsureAppointmentApprovedAndNotPastDueDate_NullAppointment_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(null!));
    }

    // ------------------------------------------------------------------
    // EnsureAme
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureAme_AmeAppointmentTypeId_DoesNotThrow()
    {
        Should.NotThrow(() =>
            DocumentUploadGate.EnsureAme(CaseEvaluationSeedIds.AppointmentTypes.Ame));
    }

    [Theory]
    [InlineData("Qme")]
    [InlineData("PanelQme")]
    [InlineData("RecordReview")]
    [InlineData("Deposition")]
    public void EnsureAme_NonAmeAppointmentTypeId_Throws(string nonAmeTypeName)
    {
        var nonAmeId = nonAmeTypeName switch
        {
            "Qme" => CaseEvaluationSeedIds.AppointmentTypes.Qme,
            "PanelQme" => CaseEvaluationSeedIds.AppointmentTypes.PanelQme,
            "RecordReview" => CaseEvaluationSeedIds.AppointmentTypes.RecordReview,
            "Deposition" => CaseEvaluationSeedIds.AppointmentTypes.Deposition,
            _ => Guid.NewGuid(),
        };

        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureAme(nonAmeId));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfRequiresAmeAppointment);
    }

    [Fact]
    public void EnsureAme_RandomGuid_Throws()
    {
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureAme(Guid.NewGuid()));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfRequiresAmeAppointment);
    }

    // ------------------------------------------------------------------
    // EnsureCreatorIsAttorney
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureCreatorIsAttorney_CreatorMatchAndApplicantAttorneyRole_DoesNotThrow()
    {
        var creatorId = Guid.NewGuid();
        var appointment = NewAppointmentWithCreator(creatorId);

        Should.NotThrow(() => DocumentUploadGate.EnsureCreatorIsAttorney(
            appointment, creatorId, new[] { "Applicant Attorney" }));
    }

    [Fact]
    public void EnsureCreatorIsAttorney_CreatorMatchAndDefenseAttorneyRole_DoesNotThrow()
    {
        var creatorId = Guid.NewGuid();
        var appointment = NewAppointmentWithCreator(creatorId);

        Should.NotThrow(() => DocumentUploadGate.EnsureCreatorIsAttorney(
            appointment, creatorId, new[] { "Defense Attorney" }));
    }

    [Fact]
    public void EnsureCreatorIsAttorney_CreatorMismatch_Throws()
    {
        var appointment = NewAppointmentWithCreator(Guid.NewGuid());
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureCreatorIsAttorney(
                appointment, Guid.NewGuid(), new[] { "Applicant Attorney" }));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfUploaderMustBeBookingAttorney);
    }

    [Fact]
    public void EnsureCreatorIsAttorney_NullCurrentUserId_Throws()
    {
        var appointment = NewAppointmentWithCreator(Guid.NewGuid());
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureCreatorIsAttorney(
                appointment, null, new[] { "Applicant Attorney" }));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfUploaderMustBeBookingAttorney);
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Adjuster")]
    [InlineData("Clinic Staff")]
    [InlineData("IT Admin")]
    public void EnsureCreatorIsAttorney_NonAttorneyRole_Throws(string roleName)
    {
        var creatorId = Guid.NewGuid();
        var appointment = NewAppointmentWithCreator(creatorId);

        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureCreatorIsAttorney(
                appointment, creatorId, new[] { roleName }));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfUploaderMustBeBookingAttorney);
    }

    [Fact]
    public void EnsureCreatorIsAttorney_RoleNameCaseInsensitive_DoesNotThrow()
    {
        var creatorId = Guid.NewGuid();
        var appointment = NewAppointmentWithCreator(creatorId);

        Should.NotThrow(() => DocumentUploadGate.EnsureCreatorIsAttorney(
            appointment, creatorId, new[] { "applicant attorney" }));
    }

    // ------------------------------------------------------------------
    // EnsureNotImmutable
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(DocumentStatus.Pending, false)]
    [InlineData(DocumentStatus.Uploaded, false)]
    [InlineData(DocumentStatus.Rejected, false)]
    public void EnsureNotImmutable_NonAcceptedExternalUser_DoesNotThrow(
        DocumentStatus status,
        bool isInternalUser)
    {
        var doc = NewDocument(status);
        Should.NotThrow(() => DocumentUploadGate.EnsureNotImmutable(doc, isInternalUser));
    }

    [Fact]
    public void EnsureNotImmutable_AcceptedExternalUser_Throws()
    {
        var doc = NewDocument(DocumentStatus.Accepted);
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureNotImmutable(doc, isInternalUser: false));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentImmutableForExternalUser);
    }

    [Fact]
    public void EnsureNotImmutable_AcceptedInternalUser_DoesNotThrow()
    {
        // Internal staff bypass: clinic staff can correct an Accepted
        // document on behalf of the patient.
        var doc = NewDocument(DocumentStatus.Accepted);
        Should.NotThrow(() => DocumentUploadGate.EnsureNotImmutable(doc, isInternalUser: true));
    }

    // ------------------------------------------------------------------
    // EnsureVerificationCodeMatches
    // OLD: AppointmentDocumentDomain.cs:64-75
    // ------------------------------------------------------------------

    [Fact]
    public void EnsureVerificationCodeMatches_MatchingCode_DoesNotThrow()
    {
        var code = Guid.NewGuid();
        var doc = NewDocumentWithCode(code);
        Should.NotThrow(() => DocumentUploadGate.EnsureVerificationCodeMatches(doc, code));
    }

    [Fact]
    public void EnsureVerificationCodeMatches_NullDocument_Throws()
    {
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureVerificationCodeMatches(null!, Guid.NewGuid()));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
    }

    [Fact]
    public void EnsureVerificationCodeMatches_MismatchedCode_Throws()
    {
        var doc = NewDocumentWithCode(Guid.NewGuid());
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureVerificationCodeMatches(doc, Guid.NewGuid()));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
    }

    [Fact]
    public void EnsureVerificationCodeMatches_EmptyGuidSupplied_Throws()
    {
        // OLD line 66 explicitly skips the lookup when verificationCode
        // == Guid.Empty (treats it as "not provided"). NEW's gate
        // throws on Empty supply because the anonymous endpoint should
        // never be reached without a code.
        var doc = NewDocumentWithCode(Guid.NewGuid());
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureVerificationCodeMatches(doc, Guid.Empty));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
    }

    [Fact]
    public void EnsureVerificationCodeMatches_DocumentWithoutCode_Throws()
    {
        var doc = NewDocument(DocumentStatus.Pending);
        // No VerificationCode set.
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureVerificationCodeMatches(doc, Guid.NewGuid()));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);
    }

    [Fact]
    public void EnsureVerificationCodeMatches_AcceptedDocument_ThrowsImmutable()
    {
        var code = Guid.NewGuid();
        var doc = NewDocumentWithCode(code);
        doc.Status = DocumentStatus.Accepted;
        var ex = Should.Throw<BusinessException>(() =>
            DocumentUploadGate.EnsureVerificationCodeMatches(doc, code));
        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentImmutableForExternalUser);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Appointment NewAppointment(AppointmentStatusType status, DateTime? dueDate)
    {
        return new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00001",
            appointmentStatus: status,
            panelNumber: null,
            dueDate: dueDate);
    }

    private static Appointment NewAppointmentWithCreator(Guid creatorIdentityUserId)
    {
        // The "creator" of the appointment in NEW is the IdentityUserId
        // (the IdentityUser who initiated the booking). The audit's
        // "Appointment.CreatedById == CurrentUser.Id" maps to NEW's
        // IdentityUserId field.
        return new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: creatorIdentityUserId,
            appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00001",
            appointmentStatus: AppointmentStatusType.Approved,
            dueDate: DateTime.UtcNow.AddDays(7));
    }

    private static AppointmentDocument NewDocument(DocumentStatus status)
    {
        var doc = new AppointmentDocument(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: Guid.NewGuid(),
            documentName: "Test Document",
            fileName: "test.pdf",
            blobName: "host/x/y",
            contentType: "application/pdf",
            fileSize: 1024,
            uploadedByUserId: Guid.NewGuid());
        doc.Status = status;
        return doc;
    }

    private static AppointmentDocument NewDocumentWithCode(Guid code)
    {
        var doc = NewDocument(DocumentStatus.Pending);
        doc.VerificationCode = code;
        return doc;
    }
}
