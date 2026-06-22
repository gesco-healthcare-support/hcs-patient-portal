using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
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
/// so the same rule can be reused by other AppServices that operate on a
/// specific appointment -- starting with <c>AppointmentDocumentsAppService</c>,
/// which previously gated only by permission + tenant and so let any
/// same-tenant external party read/upload/delete documents on an appointment
/// they were not a party to.
///
/// Internal-role callers bypass the gate (returns immediately). External
/// callers are allowed via: Creator, the patient identity, an explicit
/// AppointmentAccessor grant, OR the #2 / Phase 5 "email + role" rule (a
/// party-email column equals the caller's email AND the caller holds that
/// column's role). This is the SAME set the production list query
/// (<c>ComputeExternalPartyVisibilityAsync</c>) applies, so the row-level read
/// and the per-appointment read agree. The earlier id-based AA/DA link +
/// role-agnostic CE-email pathways were dropped (Option A) to avoid surfacing a
/// column to a user who lacks its role once linking keys by email.
/// </summary>
public class AppointmentReadAccessGuard : ITransientDependency
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<CaseEvaluationResource> _l;

    public AppointmentReadAccessGuard(
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentAccessor, Guid> accessorRepository,
        IRepository<Patient, Guid> patientRepository,
        ICurrentUser currentUser,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _appointmentRepository = appointmentRepository;
        _accessorRepository = accessorRepository;
        _patientRepository = patientRepository;
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

        // Phase 5 (#2 / Option A): hydrate only the two role-correct id-based
        // sources the rule still uses -- explicit accessor grants + the patient
        // identity. The AA/DA link + CE-email pathways were dropped: once an
        // account is associated with an appointment by email alone, those
        // id/agnostic paths would surface a party column to a user who lacks that
        // column's role. The email+role rule below replaces them and mirrors the
        // list query (ComputeExternalPartyVisibilityAsync) exactly, so a row that
        // shows in the list never 403s on click and a hidden row is never openable.
        var accessorQuery = await _accessorRepository.GetQueryableAsync();
        var accessorEntries = await _asyncExecuter.ToListAsync(
            accessorQuery
                .Where(a => a.AppointmentId == appointment.Id)
                .Select(a => new AppointmentAccessRules.AccessorEntry(a.IdentityUserId, a.AccessTypeId)));

        // Patient: resolve IdentityUserId via PatientId on the appointment.
        // Patient is IMultiTenant; the auto-filter scopes this to the current
        // tenant. Patient row may not exist (rare data inconsistency) -- treat as
        // missing. Role-correct: IdentityUserId is stamped only for the actual
        // patient, so this pathway cannot surface another role's appointment.
        Guid? patientIdentityUserId = null;
        var patient = await _patientRepository.FindAsync(appointment.PatientId);
        if (patient != null)
        {
            patientIdentityUserId = patient.IdentityUserId;
        }

        var byCoreRules = AppointmentAccessRules.CanRead(
            callerUserId: _currentUser.Id,
            callerEmail: _currentUser.Email,
            callerIsInternalUser: false,
            // R2-2: BookedByUserId is the reliable booker; coalesce with CreatorId so
            // the booker can read/edit their own (possibly null-creator) booking. Same
            // coalesce as the list query (ComputeExternalPartyVisibilityAsync).
            appointmentCreatorId: appointment.CreatorId ?? appointment.BookedByUserId,
            patientIdentityUserId: patientIdentityUserId,
            applicantAttorneyIdentityUserIds: null,
            defenseAttorneyIdentityUserIds: null,
            claimExaminerEmails: null,
            accessorEntries: accessorEntries).allowed;

        // #2 / Phase 5: email + role row-level visibility -- the SAME rule the
        // list query applies, against this appointment's denormalized party-email
        // columns + the caller's roles.
        var byEmailRole = AppointmentAccessRules.IsAppointmentEmailRoleVisible(
            callerEmail: _currentUser.Email,
            callerRoles: callerRoles,
            patientEmail: appointment.PatientEmail,
            applicantAttorneyEmail: appointment.ApplicantAttorneyEmail,
            defenseAttorneyEmail: appointment.DefenseAttorneyEmail,
            claimExaminerEmail: appointment.ClaimExaminerEmail);

        if (!byCoreRules && !byEmailRole)
        {
            // UserFriendlyException so the localized message reaches the client
            // unchanged (BusinessException's MapCodeNamespace auto-localization is
            // not resolving in this codebase).
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
            // R2-2: coalesce booker with CreatorId (see EnsureCanReadAsync).
            appointmentCreatorId: appointment.CreatorId ?? appointment.BookedByUserId,
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
            // R2-2: coalesce booker with CreatorId (see EnsureCanReadAsync).
            appointmentCreatorId: appointment.CreatorId ?? appointment.BookedByUserId);
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
