using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Localization;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-05-13 (Issue #114) -- centralised read-gate that composes
/// <see cref="AppointmentAccessRules.CanRead"/> with live state.
///
/// Extracted from <c>AppointmentsAppService.EnsureCanReadAppointmentAsync</c>
/// so the same 7-pathway rule (Internal / Creator / Patient / AA-link /
/// DA-link / CE-email / AppointmentAccessor) can be reused by other
/// AppServices that operate on a specific appointment -- starting with
/// <c>AppointmentDocumentsAppService</c>, which previously gated only by
/// permission + tenant and so let any same-tenant external party
/// read/upload/delete documents on an appointment they were not a party to.
///
/// Internal-role callers bypass the gate (returns immediately). External
/// callers go through hydration of the 5 join sources used by the
/// production list query (<c>ComputeExternalPartyVisibilityAsync</c>) so
/// the row-level read and the per-appointment read agree.
/// </summary>
public class AppointmentReadAccessGuard : ITransientDependency
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly IAppointmentApplicantAttorneyRepository _applicantAttorneyLinkRepository;
    private readonly IAppointmentDefenseAttorneyRepository _defenseAttorneyLinkRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryDetailRepository;
    private readonly IRepository<AppointmentClaimExaminer, Guid> _claimExaminerRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    public AppointmentReadAccessGuard(
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentAccessor, Guid> accessorRepository,
        IAppointmentApplicantAttorneyRepository applicantAttorneyLinkRepository,
        IAppointmentDefenseAttorneyRepository defenseAttorneyLinkRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryDetailRepository,
        IRepository<AppointmentClaimExaminer, Guid> claimExaminerRepository,
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _appointmentRepository = appointmentRepository;
        _accessorRepository = accessorRepository;
        _applicantAttorneyLinkRepository = applicantAttorneyLinkRepository;
        _defenseAttorneyLinkRepository = defenseAttorneyLinkRepository;
        _patientRepository = patientRepository;
        _injuryDetailRepository = injuryDetailRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _currentUser = currentUser;
        _asyncExecuter = asyncExecuter;
        _l = localizer;
    }

    /// <summary>
    /// Load the appointment and gate. Throws <see cref="EntityNotFoundException"/>
    /// (via repo.GetAsync) if the appointmentId does not resolve.
    /// </summary>
    public async Task EnsureCanReadAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        await EnsureCanReadAsync(appointment);
    }

    /// <summary>
    /// Gate against an already-loaded appointment. Use this overload from
    /// callers that already have the entity to avoid a redundant DB hit.
    /// </summary>
    public async Task EnsureCanReadAsync(Appointment appointment)
    {
        var callerRoles = _currentUser.Roles ?? Array.Empty<string>();
        if (BookingFlowRoles.IsInternalUserCaller(callerRoles))
        {
            return;
        }

        // Hydration order matches AppointmentsAppService.GetListAsync's
        // ComputeExternalPartyVisibilityAsync so the two paths agree.
        var accessorQuery = await _accessorRepository.GetQueryableAsync();
        var accessorEntries = await _asyncExecuter.ToListAsync(
            accessorQuery
                .Where(a => a.AppointmentId == appointment.Id)
                .Select(a => new AppointmentAccessRules.AccessorEntry(a.IdentityUserId, a.AccessTypeId)));

        // Patient: resolve IdentityUserId via PatientId on the appointment.
        // Patient is IMultiTenant; the auto-filter scopes this to the
        // current tenant. Patient row may not exist (rare data inconsistency)
        // -- treat as missing.
        Guid? patientIdentityUserId = null;
        var patient = await _patientRepository.FindAsync(appointment.PatientId);
        if (patient != null)
        {
            patientIdentityUserId = patient.IdentityUserId;
        }

        // AA / DA link rows allow a null IdentityUserId (attorney named
        // before they registered). Filter to populated rows so the rule
        // receives only non-null Guids.
        var aaLinkQuery = await _applicantAttorneyLinkRepository.GetQueryableAsync();
        var aaIdentityUserIds = await _asyncExecuter.ToListAsync(
            aaLinkQuery
                .Where(l => l.AppointmentId == appointment.Id && l.IdentityUserId.HasValue)
                .Select(l => l.IdentityUserId!.Value));

        var daLinkQuery = await _defenseAttorneyLinkRepository.GetQueryableAsync();
        var daIdentityUserIds = await _asyncExecuter.ToListAsync(
            daLinkQuery
                .Where(l => l.AppointmentId == appointment.Id && l.IdentityUserId.HasValue)
                .Select(l => l.IdentityUserId!.Value));

        // ClaimExaminer two-hop: AppointmentClaimExaminer rows are linked
        // via AppointmentInjuryDetail.AppointmentId. Match by email
        // (case-insensitive in the rule itself) since CE has no
        // IdentityUser join.
        var injuryQuery = await _injuryDetailRepository.GetQueryableAsync();
        var injuryIds = await _asyncExecuter.ToListAsync(
            injuryQuery.Where(i => i.AppointmentId == appointment.Id).Select(i => i.Id));
        var ceEmails = new List<string>();
        if (injuryIds.Count > 0)
        {
            var ceQuery = await _claimExaminerRepository.GetQueryableAsync();
            ceEmails = await _asyncExecuter.ToListAsync(
                ceQuery
                    .Where(c => injuryIds.Contains(c.AppointmentInjuryDetailId) && c.Email != null)
                    .Select(c => c.Email!));
        }

        var accessResult = AppointmentAccessRules.CanRead(
            callerUserId: _currentUser.Id,
            callerEmail: _currentUser.Email,
            callerIsInternalUser: false,
            appointmentCreatorId: appointment.CreatorId,
            patientIdentityUserId: patientIdentityUserId,
            applicantAttorneyIdentityUserIds: aaIdentityUserIds,
            defenseAttorneyIdentityUserIds: daIdentityUserIds,
            claimExaminerEmails: ceEmails,
            accessorEntries: accessorEntries);

        if (!accessResult.allowed)
        {
            // UserFriendlyException so the localized message reaches the
            // client unchanged. BusinessException's MapCodeNamespace
            // auto-localization is not resolving in this codebase --
            // returns "An internal error occurred during your request!".
            throw new UserFriendlyException(
                code: CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
                message: _l["Appointment:AccessDenied"]);
        }
    }
}
