using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
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
    // T17 (paralegal-on-behalf-of-attorney, 2026-06-10): master attorney rows are the
    // source for the represented attorney's display name when a paralegal books (the
    // attorney may be unregistered, so IdentityUser is not reliable).
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;

    public DocumentEmailContextResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryDetailRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IAppointmentDocumentTypeRepository documentTypeRepository,
        IAccountUrlBuilder accountUrlBuilder,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _documentRepository = documentRepository;
        _injuryDetailRepository = injuryDetailRepository;
        _identityUserRepository = identityUserRepository;
        _documentTypeRepository = documentTypeRepository;
        _accountUrlBuilder = accountUrlBuilder;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
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

        // T17 (paralegal-on-behalf-of-attorney): when the booker is a paralegal, the
        // email greeting names the represented attorney (the principal), not the
        // paralegal. Match the booker's email against the denormalized per-side
        // paralegal emails; if it matches, resolve that side's attorney display name
        // from the master attorney row. Null for non-paralegal bookers, so the
        // handlers keep their existing booker/patient greeting unchanged.
        string? principalAttorneyName = null;
        var bookerEmailForGreeting = bookerUser?.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(bookerEmailForGreeting))
        {
            if (!string.IsNullOrWhiteSpace(appointment.ApplicantParalegalEmail)
                && string.Equals(bookerEmailForGreeting, appointment.ApplicantParalegalEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                principalAttorneyName = await ResolveApplicantAttorneyDisplayNameAsync(appointment.ApplicantAttorneyEmail);
            }
            else if (!string.IsNullOrWhiteSpace(appointment.DefenseParalegalEmail)
                && string.Equals(bookerEmailForGreeting, appointment.DefenseParalegalEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                principalAttorneyName = await ResolveDefenseAttorneyDisplayNameAsync(appointment.DefenseAttorneyEmail);
            }
        }

        return new DocumentEmailContext
        {
            AppointmentId = appointment.Id,
            TenantId = appointment.TenantId,
            RequestConfirmationNumber = appointment.RequestConfirmationNumber,
            AppointmentDate = appointment.AppointmentDate,
            DueDate = appointment.DueDate,
            BookerEmail = bookerUser?.Email,
            BookerFullName = JoinName(bookerUser?.Name, bookerUser?.Surname),
            // T17: the represented attorney's name when a paralegal booked (else null).
            PrincipalAttorneyName = principalAttorneyName,
            // Paralegal-on-behalf-of-attorney (2026-06-10): denormalized per-side
            // attorney + paralegal emails, so the addressing layer can promote the
            // represented attorney to the email To when a paralegal is the booker.
            ApplicantAttorneyEmail = appointment.ApplicantAttorneyEmail,
            ApplicantParalegalEmail = appointment.ApplicantParalegalEmail,
            DefenseAttorneyEmail = appointment.DefenseAttorneyEmail,
            DefenseParalegalEmail = appointment.DefenseParalegalEmail,
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

    /// <summary>
    /// T17: resolve the applicant attorney's display name from the master row by the
    /// denormalized attorney email. Sync execution mirrors the injury-detail read above.
    /// Returns null when no email / no match / no name (caller falls back to the booker).
    /// </summary>
    private async Task<string?> ResolveApplicantAttorneyDisplayNameAsync(string? attorneyEmail)
    {
        if (string.IsNullOrWhiteSpace(attorneyEmail))
        {
            return null;
        }
        var trimmed = attorneyEmail.Trim().ToLower();
        var query = await _applicantAttorneyRepository.GetQueryableAsync();
        var match = query
            .Where(a => a.Email != null && a.Email.ToLower() == trimmed)
            .Select(a => new { a.FirstName, a.LastName })
            .FirstOrDefault();
        var name = JoinName(match?.FirstName, match?.LastName);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>T17: defense-side mirror of <see cref="ResolveApplicantAttorneyDisplayNameAsync"/>.</summary>
    private async Task<string?> ResolveDefenseAttorneyDisplayNameAsync(string? attorneyEmail)
    {
        if (string.IsNullOrWhiteSpace(attorneyEmail))
        {
            return null;
        }
        var trimmed = attorneyEmail.Trim().ToLower();
        var query = await _defenseAttorneyRepository.GetQueryableAsync();
        var match = query
            .Where(a => a.Email != null && a.Email.ToLower() == trimmed)
            .Select(a => new { a.FirstName, a.LastName })
            .FirstOrDefault();
        var name = JoinName(match?.FirstName, match?.LastName);
        return string.IsNullOrWhiteSpace(name) ? null : name;
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

    /// <summary>
    /// T17 (paralegal-on-behalf-of-attorney): the represented attorney's display name
    /// when the booker is a paralegal; null otherwise. When set, the status + reminder
    /// email greetings name the attorney instead of the booking paralegal.
    /// </summary>
    public string? PrincipalAttorneyName { get; set; }

    // Paralegal-on-behalf-of-attorney (2026-06-10): denormalized per-side attorney +
    // paralegal emails (from the appointment). Consumed by the addressing layer's
    // principal-promotion (BookerCcDispatcher.ResolvePrincipalEmail).
    public string? ApplicantAttorneyEmail { get; set; }
    public string? ApplicantParalegalEmail { get; set; }
    public string? DefenseAttorneyEmail { get; set; }
    public string? DefenseParalegalEmail { get; set; }
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
