using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentAccessors;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-06-22 -- the single source of truth for "which appointments may this
/// caller see". Extracted from <c>AppointmentsAppService</c>'s private
/// <c>ComputeExternalPartyVisibilityAsync</c> so the appointment list and the
/// external-user lookup share one definition and cannot drift apart (the lookup
/// must scope its results to the caller's co-parties, which are exactly the
/// parties named on these appointments).
///
/// <para>Returns <c>null</c> for an internal-role caller (no narrowing -- they
/// see the whole tenant) or the set of visible appointment ids for an
/// external-only caller. The set is the union of four pathways: booker
/// (<c>CreatorId ?? BookedByUserId</c>), patient identity, explicit accessor
/// grants, and the leak-free email+role rule
/// (<see cref="AppointmentAccessRules.IsAppointmentEmailRoleVisible"/>). The
/// per-appointment read guard (<see cref="AppointmentReadAccessGuard"/>) applies
/// the same rule set so list and click agree.</para>
/// </summary>
public class AppointmentVisibilityService : ITransientDependency
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentAccessor, Guid> _accessorRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public AppointmentVisibilityService(
        IAppointmentRepository appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IRepository<AppointmentAccessor, Guid> accessorRepository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _accessorRepository = accessorRepository;
        _currentUser = currentUser;
        _currentTenant = currentTenant;
        _asyncExecuter = asyncExecuter;
    }

    /// <summary>
    /// Null = internal caller (no narrowing). Otherwise the union of appointment
    /// ids the external-only caller is a party to. See class docs for pathways.
    /// </summary>
    public async Task<IReadOnlyCollection<Guid>?> GetVisibleAppointmentIdsAsync()
    {
        if (!_currentUser.Id.HasValue)
        {
            return Array.Empty<Guid>();
        }

        // Internal-role check: anyone with a non-external role bypasses the
        // narrowing. Use the canonical role names from
        // ExternalUserRoleDataSeedContributor.
        var externalRoles = new[] { "Patient", "Applicant Attorney", "Defense Attorney", "Claim Examiner" };
        var roles = _currentUser.Roles ?? Array.Empty<string>();
        var hasOnlyExternalRoles = roles.Length > 0
            && roles.All(r => externalRoles.Any(er => string.Equals(r, er, StringComparison.OrdinalIgnoreCase)));
        if (!hasOnlyExternalRoles)
        {
            // Internal user (admin / Intake Staff / Staff Supervisor / Doctor)
            // OR a multi-role user with at least one internal role.
            return null;
        }

        var userId = _currentUser.Id.Value;
        var userEmail = _currentUser.Email;

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var patientQuery = await _patientRepository.GetQueryableAsync();
        var accessorQuery = await _accessorRepository.GetQueryableAsync();

        // 1. Booker. R2-2: BookedByUserId is the reliable booker (stamped at create);
        // CreatorId is the legacy/audit fallback. Coalesce so a record-only booking
        // (null CreatorId) still surfaces to whoever booked it. The read guard applies
        // the same coalesce so list and click agree.
        var bookerIds = await _asyncExecuter.ToListAsync(
            appointmentQuery.Where(a => (a.CreatorId ?? a.BookedByUserId) == userId).Select(a => a.Id));

        // 2. Patient identity (Patient.IdentityUserId). Patient is IMultiTenant;
        // constrain by TenantId manually since the home query may run outside an
        // explicit tenant scope. Role-correct: IdentityUserId is stamped only for
        // the actual patient (claimed by email at registration), so this cannot
        // surface another role's appointment.
        var patientIds = await _asyncExecuter.ToListAsync(
            patientQuery
                .Where(p => p.TenantId == _currentTenant.Id && p.IdentityUserId == userId)
                .Select(p => p.Id));
        var patientAppointmentIds = patientIds.Count == 0
            ? new List<Guid>()
            : await _asyncExecuter.ToListAsync(
                appointmentQuery.Where(a => patientIds.Contains(a.PatientId)).Select(a => a.Id));

        // 3. Appointment accessor grants (explicit per-appointment access).
        // Phase 5 FIX: previously keyed on CreatorId (a no-op duplicate of #1), so
        // an accessor-invited user never saw the appointment in their list. Use the
        // real AppointmentAccessor table so a D9 accessor (e.g. an opposing-side
        // firm granted access) sees it. Role-agnostic by design -- an accessor was
        // explicitly granted access.
        var accessorAppointmentIds = await _asyncExecuter.ToListAsync(
            accessorQuery.Where(a => a.IdentityUserId == userId).Select(a => a.AppointmentId));

        // 4. Email + role visibility (#2 / Phase 5). Surface an appointment ONLY
        // where the caller's email is a party-email column AND the caller holds
        // that column's role. This REPLACES the prior role-AGNOSTIC email match
        // plus the id-based AA/DA link and CE-email unions: those would reveal a
        // column to a user who lacks that column's role (a latent over-show today;
        // a hard leak once linking keys purely by email). Reuses the shared
        // AppointmentAccessRules.IsAppointmentEmailRoleVisible rule so this list and
        // the per-appointment read guard agree exactly. The candidate set is first
        // narrowed in SQL to appointments naming the caller's email, then the pure
        // rule applies the role gate in memory against CurrentUser.Roles.
        var emailRoleAppointmentIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var callerEmailLower = userEmail.Trim().ToLower();
            var candidates = await _asyncExecuter.ToListAsync(
                appointmentQuery
                    .Where(a =>
                        (a.PatientEmail != null && a.PatientEmail.ToLower() == callerEmailLower) ||
                        (a.ApplicantAttorneyEmail != null && a.ApplicantAttorneyEmail.ToLower() == callerEmailLower) ||
                        (a.DefenseAttorneyEmail != null && a.DefenseAttorneyEmail.ToLower() == callerEmailLower) ||
                        (a.ClaimExaminerEmail != null && a.ClaimExaminerEmail.ToLower() == callerEmailLower))
                    .Select(a => new
                    {
                        a.Id,
                        a.PatientEmail,
                        a.ApplicantAttorneyEmail,
                        a.DefenseAttorneyEmail,
                        a.ClaimExaminerEmail,
                    }));
            foreach (var c in candidates)
            {
                if (AppointmentAccessRules.IsAppointmentEmailRoleVisible(
                        userEmail,
                        roles,
                        c.PatientEmail,
                        c.ApplicantAttorneyEmail,
                        c.DefenseAttorneyEmail,
                        c.ClaimExaminerEmail))
                {
                    emailRoleAppointmentIds.Add(c.Id);
                }
            }
        }

        var union = new HashSet<Guid>(bookerIds);
        union.UnionWith(patientAppointmentIds);
        union.UnionWith(accessorAppointmentIds);
        union.UnionWith(emailRoleAppointmentIds);
        return union.ToList();
    }
}
