using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Phase 14b (2026-05-04) -- reusable context resolver for document
/// email handlers. Pulls the appointment + patient + first injury
/// detail + branding setting and packages them into a flat record the
/// per-event handlers consume. Lives at the Application layer so the
/// per-feature handlers can stay one-screen-of-code.
/// </summary>
public class DocumentEmailContextResolver : ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryDetailRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly ISettingProvider _settingProvider;

    public DocumentEmailContextResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryDetailRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        ISettingProvider settingProvider)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _documentRepository = documentRepository;
        _injuryDetailRepository = injuryDetailRepository;
        _identityUserRepository = identityUserRepository;
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Resolves the read-only snapshot the handlers need. Returns null
    /// when the appointment is missing (handlers log + skip).
    /// </summary>
    public virtual async Task<DocumentEmailContext?> ResolveAsync(Guid appointmentId, Guid? appointmentDocumentId)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            return null;
        }

        var patient = appointment.PatientId == Guid.Empty
            ? null
            : await _patientRepository.FindAsync(appointment.PatientId);

        var bookerUser = await _identityUserRepository.FindAsync(appointment.IdentityUserId);

        // Take the first injury detail for the appointment -- mirrors
        // OLD's `appointmentInjury = ...FirstOrDefault()` at
        // AppointmentDocumentDomain.cs:453.
        var injuryQueryable = await _injuryDetailRepository.GetQueryableAsync();
        var injury = injuryQueryable
            .Where(i => i.AppointmentId == appointmentId)
            .Select(i => new { i.ClaimNumber, i.WcabAdj })
            .FirstOrDefault();

        AppointmentDocument? document = null;
        if (appointmentDocumentId.HasValue && appointmentDocumentId.Value != Guid.Empty)
        {
            document = await _documentRepository.FindAsync(appointmentDocumentId.Value);
        }

        var portalUrl = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);

        return new DocumentEmailContext
        {
            AppointmentId = appointment.Id,
            TenantId = appointment.TenantId,
            RequestConfirmationNumber = appointment.RequestConfirmationNumber,
            AppointmentDate = appointment.AppointmentDate,
            DueDate = appointment.DueDate,
            BookerEmail = bookerUser?.Email,
            BookerFullName = JoinName(bookerUser?.Name, bookerUser?.Surname),
            ResponsibleUserId = appointment.PrimaryResponsibleUserId,
            PatientFirstName = patient?.FirstName,
            PatientLastName = patient?.LastName,
            PatientEmail = patient?.Email ?? appointment.PatientEmail,
            ClaimNumber = injury?.ClaimNumber,
            WcabAdj = injury?.WcabAdj,
            DocumentId = document?.Id,
            DocumentName = document?.DocumentName,
            DocumentUploadedByUserId = document?.UploadedByUserId,
            DocumentRejectionReason = document?.RejectionReason,
            PortalBaseUrl = portalUrl,
            IsAdHoc = document?.IsAdHoc ?? false,
            IsJointDeclaration = document?.IsJointDeclaration ?? false,
        };
    }

    /// <summary>
    /// Looks up the responsible user's email when set. Null when not.
    /// Separated from <see cref="ResolveAsync"/> so the caller can
    /// branch (skip the responsible-user leg when null).
    /// </summary>
    public virtual async Task<string?> ResolveResponsibleUserEmailAsync(Guid? responsibleUserId)
    {
        if (!responsibleUserId.HasValue || responsibleUserId.Value == Guid.Empty)
        {
            return null;
        }
        var user = await _identityUserRepository.FindAsync(responsibleUserId.Value);
        return user?.Email;
    }

    /// <summary>
    /// Resolves the original uploader's email so accept/reject handlers
    /// can email the right person. Falls back to the booker's email if
    /// the uploader is anonymous (verification-code path) or missing.
    /// </summary>
    public virtual async Task<string?> ResolveUploaderEmailAsync(Guid? uploaderUserId, string? fallbackEmail)
    {
        if (uploaderUserId.HasValue && uploaderUserId.Value != Guid.Empty)
        {
            var user = await _identityUserRepository.FindAsync(uploaderUserId.Value);
            if (!string.IsNullOrWhiteSpace(user?.Email))
            {
                return user.Email;
            }
        }
        return fallbackEmail;
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast)
        {
            return first!.Trim() + " " + last!.Trim();
        }
        if (hasFirst)
        {
            return first!.Trim();
        }
        if (hasLast)
        {
            return last!.Trim();
        }
        return string.Empty;
    }
}

/// <summary>
/// Read-only snapshot consumed by the document email handlers.
/// </summary>
public class DocumentEmailContext
{
    public Guid AppointmentId { get; set; }
    public Guid? TenantId { get; set; }
    public string RequestConfirmationNumber { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? BookerEmail { get; set; }
    public string BookerFullName { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }
    public string? PatientFirstName { get; set; }
    public string? PatientLastName { get; set; }
    public string? PatientEmail { get; set; }
    public string? ClaimNumber { get; set; }
    public string? WcabAdj { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentName { get; set; }
    public Guid? DocumentUploadedByUserId { get; set; }
    public string? DocumentRejectionReason { get; set; }
    public string? PortalBaseUrl { get; set; }

    /// <summary>
    /// Phase 6.A (Category 6, 2026-05-08): true when the document was
    /// uploaded outside the post-approval package list (a free-form
    /// document the patient or staff added to the appointment record).
    /// Drives the <c>PatientNewDocument*</c> template branch in the
    /// document email handlers per Adrian Decision 6.1 (strict OLD parity:
    /// 3 paths -- PatientDocument*, PatientNewDocument*, JointAgreementLetter*).
    /// Default <c>false</c> when no document is in scope.
    /// </summary>
    public bool IsAdHoc { get; set; }

    /// <summary>
    /// Phase 6.A (Category 6, 2026-05-08): true when the document is a
    /// Joint Declaration (AME-only). Drives the
    /// <c>JointAgreementLetter*</c> template branch -- takes precedence
    /// over <see cref="IsAdHoc"/> when both happen to be true.
    /// </summary>
    public bool IsJointDeclaration { get; set; }
}
