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

        // ClaimExaminer (CI1 2026-06-05): now a single appointment-level row, so
        // the link is direct (CE.AppointmentId). Match by email
        // (case-insensitive in the rule itself) since CE has no IdentityUser join.
        var ceQuery = await _claimExaminerRepository.GetQueryableAsync();
        var ceEmails = await _asyncExecuter.ToListAsync(
            ceQuery
                .Where(c => c.AppointmentId == appointment.Id && c.Email != null)
                .Select(c => c.Email!));

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

    /// <summary>
    /// Edit-access predicate using the SLIM <see cref="AppointmentAccessRules.CanEdit"/>
    /// rule (internal user / appointment creator / Edit-accessor). This is the same
    /// rule the appointment change-request flow uses, centralised here so callers do
    /// not duplicate the accessor hydration. Returns a bool so each caller throws its
    /// own exception (e.g. the change-request keeps its own error code).
    ///
    /// NOTE: the full 7-pathway CanEdit (which also admits patient / AA / DA / CE) is
    /// deliberately NOT used here -- gating the core appointment Update with the right
    /// rule is a separate deferred slice (UpdateAsync is currently un-gated).
    /// </summary>
    public async Task<bool> CanEditAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        return await CanEditAsync(appointment);
    }

    public async Task<bool> CanEditAsync(Appointment appointment)
    {
        var callerRoles = _currentUser.Roles ?? Array.Empty<string>();
        var isInternal = BookingFlowRoles.IsInternalUserCaller(callerRoles);

        var accessorQuery = await _accessorRepository.GetQueryableAsync();
        var accessorEntries = await _asyncExecuter.ToListAsync(
            accessorQuery
                .Where(a => a.AppointmentId == appointment.Id)
                .Select(a => new AppointmentAccessRules.AccessorEntry(a.IdentityUserId, a.AccessTypeId)));

        return AppointmentAccessRules.CanEdit(
            callerUserId: _currentUser.Id,
            callerIsInternalUser: isInternal,
            appointmentCreatorId: appointment.CreatorId,
            accessorEntries: accessorEntries);
    }

    /// <summary>
    /// Throwing variant of <see cref="CanEditAsync(Guid)"/> for callers that just want
    /// deny-by-default (e.g. accessor mutations). Throws the shared access-denied error.
    /// </summary>
    public async Task EnsureCanEditAsync(Guid appointmentId)
    {
        if (!await CanEditAsync(appointmentId))
        {
            throw new UserFriendlyException(
                code: CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
                message: _l["Appointment:AccessDenied"]);
        }
    }

    /// <summary>
    /// Accessor-management predicate (2026-06-10, Workstream B). Composes the
    /// dedicated <see cref="AppointmentAccessRules.CanManageAccessors"/> rule:
    /// internal users pass; external callers must be the appointment creator AND
    /// hold an authorized accessor-managing role (Applicant / Defense Attorney
    /// today). Deliberately STRICTER than <see cref="CanEditAsync(Appointment)"/>
    /// -- the Edit-accessor pathway is not admitted, so an Edit-accessor cannot
    /// self-propagate accessors. The rule ignores accessor rows, so no accessor
    /// hydration is needed (cheaper than CanEditAsync).
    /// </summary>
    public async Task<bool> CanManageAccessorsAsync(Guid appointmentId)
    {
        var appointment = await _appointmentRepository.GetAsync(appointmentId);
        return await CanManageAccessorsAsync(appointment);
    }

    public Task<bool> CanManageAccessorsAsync(Appointment appointment)
    {
        var callerRoles = _currentUser.Roles ?? Array.Empty<string>();
        var allowed = AppointmentAccessRules.CanManageAccessors(
            callerUserId: _currentUser.Id,
            callerIsInternalUser: BookingFlowRoles.IsInternalUserCaller(callerRoles),
            callerIsAuthorizedExternalAccessorManager: BookingFlowRoles.IsExternalAccessorManager(callerRoles),
            appointmentCreatorId: appointment.CreatorId);
        return Task.FromResult(allowed);
    }

    /// <summary>
    /// Throwing variant of <see cref="CanManageAccessorsAsync(Guid)"/> for the
    /// accessor mutation endpoints (Create / Update / Delete). Throws the same
    /// shared localized access-denied error as the read / edit gates.
    /// </summary>
    public async Task EnsureCanManageAccessorsAsync(Guid appointmentId)
    {
        if (!await CanManageAccessorsAsync(appointmentId))
        {
            throw new UserFriendlyException(
                code: CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
                message: _l["Appointment:AccessDenied"]);
        }
    }
}
