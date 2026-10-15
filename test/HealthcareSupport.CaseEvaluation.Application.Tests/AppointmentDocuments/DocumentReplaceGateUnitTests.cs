using System;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// 2026-10-14 -- the gates behind "Upload replacement" on a rejected document: only a rejected
/// row may be replaced that way, the package path only takes package rows, and a request that is
/// back with the requester (InfoRequested) accepts uploads, because a staff send-back can flag
/// "documents" precisely to ask for one. Pure, no harness. All values are synthetic.
/// </summary>
public class DocumentReplaceGateUnitTests
{
    [Fact]
    public void An_appointment_back_with_the_requester_accepts_uploads()
    {
        var appointment = NewAppointment(AppointmentStatusType.InfoRequested);

        Should.NotThrow(() => DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate(appointment));
    }

    [Fact]
    public void Only_a_rejected_document_passes_the_replace_gate()
    {
        Should.NotThrow(() => DocumentUploadGate.EnsureRejected(NewDocument(DocumentStatus.Rejected)));
    }

    [Theory]
    [InlineData(DocumentStatus.Pending)]
    [InlineData(DocumentStatus.Uploaded)]
    [InlineData(DocumentStatus.Accepted)]
    public void Any_other_status_is_refused_by_the_replace_gate(DocumentStatus status)
    {
        Should.Throw<BusinessException>(() => DocumentUploadGate.EnsureRejected(NewDocument(status)))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentReplaceRequiresRejected);
    }

    [Fact]
    public void A_queued_package_row_passes_the_package_gate()
    {
        Should.NotThrow(() => DocumentUploadGate.EnsurePackageRow(NewDocument(DocumentStatus.Pending)));
    }

    [Fact]
    public void An_ad_hoc_upload_is_refused_by_the_package_gate()
    {
        var document = NewDocument(DocumentStatus.Rejected);
        document.IsAdHoc = true;

        Should.Throw<BusinessException>(() => DocumentUploadGate.EnsurePackageRow(document))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentNotAPackageRow);
    }

    [Fact]
    public void A_joint_declaration_is_refused_by_the_package_gate()
    {
        var document = NewDocument(DocumentStatus.Rejected);
        document.IsJointDeclaration = true;

        Should.Throw<BusinessException>(() => DocumentUploadGate.EnsurePackageRow(document))
            .Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentNotAPackageRow);
    }

    [Fact]
    public void Both_new_gates_refuse_a_null_document()
    {
        Should.Throw<ArgumentNullException>(() => DocumentUploadGate.EnsureRejected(null!));
        Should.Throw<ArgumentNullException>(() => DocumentUploadGate.EnsurePackageRow(null!));
    }

    private static Appointment NewAppointment(AppointmentStatusType status) =>
        new(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Unspecified),
            requestConfirmationNumber: "A00001",
            appointmentStatus: status,
            dueDate: DateTime.UtcNow.AddDays(7));

    private static AppointmentDocument NewDocument(DocumentStatus status)
    {
        var document = new AppointmentDocument(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: Guid.NewGuid(),
            documentName: "Synthetic document",
            fileName: "synthetic.pdf",
            blobName: "host/x/y",
            contentType: "application/pdf",
            fileSize: 1024,
            uploadedByUserId: Guid.NewGuid());
        document.Status = status;
        return document;
    }
}
