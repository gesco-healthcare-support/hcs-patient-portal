using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
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
    private readonly IAppointmentDocumentTypeRepository _documentTypeRepository;
    // Tenant-aware URL composition via the centralized builder.
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public DocumentEmailContextResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryDetailRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IAppointmentDocumentTypeRepository documentTypeRepository,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _documentRepository = documentRepository;
        _injuryDetailRepository = injuryDetailRepository;
        _identityUserRepository = identityUserRepository;
        _documentTypeRepository = documentTypeRepository;
        _accountUrlBuilder = accountUrlBuilder;
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

        // IP6 (2026-06-05): IdentityUserId is nullable for unclaimed patient
        // records; downstream BookerEmail/BookerFullName degrade to null.
        var bookerUser = appointment.IdentityUserId.HasValue
            ? await _identityUserRepository.FindAsync(appointment.IdentityUserId.Value)
            : null;

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

        // 2026-06-09: resolve the uploader's display name (so document emails
        // greet the addressee) and the document "label" -- the admin category
        // name, else the free-text "Other" label, else the raw uploaded file
        // name. Templates show the label, not the file name.
        var uploaderUser = document?.UploadedByUserId is { } uploaderId && uploaderId != Guid.Empty
            ? await _identityUserRepository.FindAsync(uploaderId)
            : null;
        var uploaderName = JoinName(uploaderUser?.Name, uploaderUser?.Surname);

        string? documentLabel = null;
        if (document != null)
        {
            if (document.AppointmentDocumentTypeId is { } typeId && typeId != Guid.Empty)
            {
                var documentType = await _documentTypeRepository.FindAsync(typeId);
                documentLabel = documentType?.Name;
            }
            documentLabel ??= string.IsNullOrWhiteSpace(document.OtherDocumentTypeName)
                ? document.DocumentName
                : document.OtherDocumentTypeName;
        }

        // Route through IAccountUrlBuilder. Tenant comes from the
        // appointment row's TenantId (the source of truth) rather than
        // _currentTenant.Name (which is null inside the
        // Change(eventData.TenantId) scope opened by the calling handlers).
        var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(appointment.TenantId);

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
            DocumentLabel = documentLabel,
            UploaderFullName = !string.IsNullOrWhiteSpace(uploaderName)
                ? uploaderName
                : JoinName(patient?.FirstName, patient?.LastName),
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

    /// <summary>2026-06-09: the document's display label -- the admin category
    /// name, else the free-text "Other" label, else the uploaded file name.
    /// Templates show this instead of the raw file name.</summary>
    public string? DocumentLabel { get; set; }

    /// <summary>2026-06-09: the uploader's display name, so document emails greet
    /// the addressee. Falls back to the patient's name when the upload was
    /// anonymous or the uploader has no display name.</summary>
    public string UploaderFullName { get; set; } = string.Empty;

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
