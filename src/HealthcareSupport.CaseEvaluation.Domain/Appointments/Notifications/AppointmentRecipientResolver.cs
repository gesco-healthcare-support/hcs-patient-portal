using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// W2-10 default impl. Loads the appointment + every linked party row, and
/// returns one <see cref="SendAppointmentEmailArgs"/> per addressable
/// recipient. Caller fills <c>Subject</c> + <c>Body</c> + <c>Context</c>
/// per-recipient before enqueuing the job.
///
/// Tenant scope: caller is responsible for ensuring the right
/// <c>CurrentTenant</c> is active. The repository queries observe ABP's
/// automatic <c>IMultiTenant</c> filter, so a wrong tenant returns nothing.
///
/// Dedup: returns at most one row per <c>To</c> email address even if the
/// same email is reachable through multiple paths (e.g. booker is also the
/// applicant attorney's IdentityUser). The MVP role tag picked first-wins.
/// </summary>
public class AppointmentRecipientResolver : IAppointmentRecipientResolver, ITransientDependency
{
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    private readonly IAppointmentInjuryDetailRepository _appointmentInjuryDetailRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly IRepository<AppointmentPrimaryInsurance, Guid> _primaryInsuranceRepository;
    private readonly IRepository<AppointmentEmployerDetail, Guid> _employerDetailRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AppointmentRecipientResolver> _logger;

    public AppointmentRecipientResolver(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository,
        IAppointmentInjuryDetailRepository appointmentInjuryDetailRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        IRepository<AppointmentPrimaryInsurance, Guid> primaryInsuranceRepository,
        IRepository<AppointmentEmployerDetail, Guid> employerDetailRepository,
        ISettingProvider settingProvider,
        ICurrentTenant currentTenant,
        ILogger<AppointmentRecipientResolver> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _appointmentInjuryDetailRepository = appointmentInjuryDetailRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _primaryInsuranceRepository = primaryInsuranceRepository;
        _employerDetailRepository = employerDetailRepository;
        _settingProvider = settingProvider;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<List<SendAppointmentEmailArgs>> ResolveAsync(Guid appointmentId, NotificationKind kind)
    {
        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            _logger.LogWarning("AppointmentRecipientResolver: appointment {AppointmentId} not found.", appointmentId);
            return new List<SendAppointmentEmailArgs>();
        }

        // S-6.1: tenant name carried on every recipient so the
        // SubmissionEmailHandler can build register-URL links with
        // `?__tenant=<TenantName>` without a separate per-recipient lookup.
        var tenantName = _currentTenant.Name;

        // Build a (email -> args) dictionary so duplicates collapse to first-wins.
        var byEmail = new Dictionary<string, SendAppointmentEmailArgs>(StringComparer.OrdinalIgnoreCase);

        void AddIfPresent(string? email, RecipientRole role, string contextSuffix, bool isRegistered = true)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogDebug(
                    "AppointmentRecipientResolver: skipped {Role} for appointment {AppointmentId} (empty email).",
                    role, appointmentId);
                return;
            }
            if (byEmail.ContainsKey(email))
            {
                return;
            }
            byEmail[email] = new SendAppointmentEmailArgs
            {
                To = email,
                Role = role,
                Context = $"Resolver/{kind}/{role}/{appointmentId}/{contextSuffix}",
                IsRegistered = isRegistered,
                TenantName = tenantName,
            };
        }

        // 1. Office mailbox -- per-tenant ABP setting (W1-2 OfficeEmail key).
        var officeEmail = await _settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail);
        AddIfPresent(officeEmail, RecipientRole.OfficeAdmin, "office");

        // 2. Booker / Patient -- whoever logged in to create the request.
        var bookerUser = await _identityUserRepository.FindAsync(appointment.IdentityUserId);
        AddIfPresent(bookerUser?.Email, RecipientRole.Patient, "booker");

        // 2b. Patient row email (if different from the booker's IdentityUser).
        var patient = await _patientRepository.FindAsync(appointment.PatientId);
        AddIfPresent(patient?.Email, RecipientRole.Patient, "patient");

        // 3. Applicant Attorney -- via the AppointmentApplicantAttorney join.
        var applicantLinkQueryable = await _appointmentApplicantAttorneyRepository.GetQueryableAsync();
        var applicantLinks = applicantLinkQueryable.Where(x => x.AppointmentId == appointmentId).Take(10).ToList();
        foreach (var link in applicantLinks)
        {
            var aa = await _applicantAttorneyRepository.FindAsync(link.ApplicantAttorneyId);
            var aaUser = await _identityUserRepository.FindAsync(link.IdentityUserId);
            AddIfPresent(aaUser?.Email, RecipientRole.ApplicantAttorney, $"aa/{link.Id}");
        }

        // 4. Defense Attorney -- via the AppointmentDefenseAttorney join (W2-7).
        var defenseLinkQueryable = await _appointmentDefenseAttorneyRepository.GetQueryableAsync();
        var defenseLinks = defenseLinkQueryable.Where(x => x.AppointmentId == appointmentId).Take(10).ToList();
        foreach (var link in defenseLinks)
        {
            var da = await _defenseAttorneyRepository.FindAsync(link.DefenseAttorneyId);
            var daUser = await _identityUserRepository.FindAsync(link.IdentityUserId);
            AddIfPresent(daUser?.Email, RecipientRole.DefenseAttorney, $"da/{link.Id}");
        }

        // 5. Claim Examiner + Primary Insurance contact -- via injury details (W2-8).
        var injuries = await _appointmentInjuryDetailRepository.GetListAsync(appointmentId: appointmentId, maxResultCount: 10);
        foreach (var injury in injuries)
        {
            var examinerQuery = await _claimExaminerRepository.GetQueryableAsync();
            var examiners = examinerQuery
                .Where(x => x.AppointmentInjuryDetailId == injury.Id && x.IsActive)
                .ToList();
            foreach (var examiner in examiners)
            {
                AddIfPresent(examiner.Email, RecipientRole.ClaimExaminer, $"examiner/{examiner.Id}");
            }

            // PrimaryInsurance has no Email column today (OLD didn't either; carriers
            // are usually contacted through the claim examiner or the "Attention"
            // field). Future schema enhancement adds an Email; until then the
            // InsuranceCarrierContact branch is a no-op for this injury row.
            var insuranceQuery = await _primaryInsuranceRepository.GetQueryableAsync();
            var insurances = insuranceQuery
                .Where(x => x.AppointmentInjuryDetailId == injury.Id && x.IsActive)
                .ToList();
            foreach (var _ in insurances)
            {
                // No email column. Skip silently. Logged at Debug once per call to avoid noise.
            }
        }

        // 6. Employer -- AppointmentEmployerDetail has no Email today; per intent
        //    self-insured employers WOULD receive notifications. Schema enhancement
        //    pending. Branch reserved for forward-compat.

        // 7. S-6.1: walk the 4 appointment-level party-email columns added by
        //    Step 5.1 (PatientEmail / ApplicantAttorneyEmail / DefenseAttorneyEmail
        //    / ClaimExaminerEmail). These capture booker-supplied emails for
        //    parties that have no JOIN row yet (typically because they have not
        //    registered). Look up each by email; if an IdentityUser already
        //    exists in this tenant, mark the recipient as registered (the
        //    handler will send the "log in to view" template). Otherwise mark
        //    as not registered (handler will send the "register as [role]"
        //    template with a tenant-pre-filled register URL). The earlier
        //    JOIN-based passes already added registered AAs/DAs/CEs by
        //    IdentityUser email; this loop only fires for emails that did NOT
        //    surface through a JOIN (i.e., dedup-skipped or never-seen).
        await AddPartyEmailIfNotKnownAsync(
            byEmail, AddIfPresent, appointment.PatientEmail, RecipientRole.Patient, "patient-email-col");
        await AddPartyEmailIfNotKnownAsync(
            byEmail, AddIfPresent, appointment.ApplicantAttorneyEmail, RecipientRole.ApplicantAttorney, "aa-email-col");
        await AddPartyEmailIfNotKnownAsync(
            byEmail, AddIfPresent, appointment.DefenseAttorneyEmail, RecipientRole.DefenseAttorney, "da-email-col");
        await AddPartyEmailIfNotKnownAsync(
            byEmail, AddIfPresent, appointment.ClaimExaminerEmail, RecipientRole.ClaimExaminer, "ce-email-col");

        return byEmail.Values.ToList();
    }

    // S-6.1: helper for the email-column walk. Looks up the email against the
    // current-tenant IdentityUser table to decide IsRegistered, then delegates
    // to AddIfPresent. Skips silently when the email is empty or already
    // present in the dict (first-wins dedup matches the rest of the resolver).
    private async Task AddPartyEmailIfNotKnownAsync(
        Dictionary<string, SendAppointmentEmailArgs> byEmail,
        Action<string?, RecipientRole, string, bool> addIfPresent,
        string? email,
        RecipientRole role,
        string contextSuffix)
    {
        if (string.IsNullOrWhiteSpace(email) || byEmail.ContainsKey(email))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLower();
        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var hasRegisteredUser = userQuery
            .Any(u => u.Email != null && u.Email.ToLower() == normalizedEmail);

        addIfPresent(email, role, contextSuffix, hasRegisteredUser);
    }
}
