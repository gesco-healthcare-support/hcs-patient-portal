using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Appointments;

[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentsAppService : CaseEvaluationAppService, IAppointmentsAppService
{
    private const string RequestConfirmationPrefix = "A";
    private const int RequestConfirmationDigits = 5;

    protected IAppointmentRepository _appointmentRepository;
    protected AppointmentManager _appointmentManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid> _patientRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> _appointmentTypeRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> _locationRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> _doctorAvailabilityRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.Doctors.Doctor, Guid> _doctorRepository;
    protected IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    protected IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;
    protected ApplicantAttorneyManager _applicantAttorneyManager;
    protected AppointmentApplicantAttorneyManager _appointmentApplicantAttorneyManager;
    protected IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    protected IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;
    protected DefenseAttorneyManager _defenseAttorneyManager;
    protected AppointmentDefenseAttorneyManager _appointmentDefenseAttorneyManager;
    protected IRepository<AppointmentInjuryDetail, Guid> _appointmentInjuryDetailRepository;
    protected IRepository<AppointmentClaimExaminer, Guid> _appointmentClaimExaminerRepository;
    protected ILocalEventBus _localEventBus;
    // Phase 11b (2026-05-04) -- lead-time + per-type max-time gates.
    protected BookingPolicyValidator _bookingPolicyValidator;

    public AppointmentsAppService(IAppointmentRepository appointmentRepository, AppointmentManager appointmentManager, IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid> patientRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository, IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository, IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> doctorAvailabilityRepository, IRepository<HealthcareSupport.CaseEvaluation.Doctors.Doctor, Guid> doctorRepository, IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository, IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository, ApplicantAttorneyManager applicantAttorneyManager, AppointmentApplicantAttorneyManager appointmentApplicantAttorneyManager, IRepository<DefenseAttorney, Guid> defenseAttorneyRepository, IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository, DefenseAttorneyManager defenseAttorneyManager, AppointmentDefenseAttorneyManager appointmentDefenseAttorneyManager, IRepository<AppointmentInjuryDetail, Guid> appointmentInjuryDetailRepository, IRepository<AppointmentClaimExaminer, Guid> appointmentClaimExaminerRepository, ILocalEventBus localEventBus, BookingPolicyValidator bookingPolicyValidator)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentManager = appointmentManager;
        _patientRepository = patientRepository;
        _identityUserRepository = identityUserRepository;
        _appointmentTypeRepository = appointmentTypeRepository;
        _locationRepository = locationRepository;
        _doctorAvailabilityRepository = doctorAvailabilityRepository;
        _doctorRepository = doctorRepository;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _applicantAttorneyManager = applicantAttorneyManager;
        _appointmentApplicantAttorneyManager = appointmentApplicantAttorneyManager;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _defenseAttorneyManager = defenseAttorneyManager;
        _appointmentDefenseAttorneyManager = appointmentDefenseAttorneyManager;
        _appointmentInjuryDetailRepository = appointmentInjuryDetailRepository;
        _appointmentClaimExaminerRepository = appointmentClaimExaminerRepository;
        _localEventBus = localEventBus;
        _bookingPolicyValidator = bookingPolicyValidator;
    }
    [Authorize]
    public virtual async Task<PagedResultDto<AppointmentWithNavigationPropertiesDto>> GetListAsync(GetAppointmentsInput input)
    {
        // S-NEW-2 (Adrian 2026-04-30): when an external party is on the call,
        // narrow the result to appointments where the caller is involved
        // (booker, patient, AA, DA, CE) -- regardless of which role they
        // hold. Internal users (admin / Clinic Staff / Staff Supervisor /
        // Doctor) are not narrowed; they continue to see every appointment
        // in the tenant. The narrowing complements ABP's automatic
        // IMultiTenant filter, which still ensures the caller never sees
        // another tenant's data.
        var visibleIds = await ComputeExternalPartyVisibilityAsync();

        var totalCount = await _appointmentRepository.GetCountAsync(input.FilterText, input.PanelNumber, input.AppointmentDateMin, input.AppointmentDateMax, input.IdentityUserId, input.AccessorIdentityUserId, input.AppointmentTypeId, input.LocationId, input.AppointmentStatus, visibleIds);
        var items = await _appointmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.PanelNumber, input.AppointmentDateMin, input.AppointmentDateMax, input.IdentityUserId, input.AccessorIdentityUserId, input.AppointmentTypeId, input.LocationId, input.AppointmentStatus, input.Sorting, input.MaxResultCount, input.SkipCount, visibleIds);
        return new PagedResultDto<AppointmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentWithNavigationProperties>, List<AppointmentWithNavigationPropertiesDto>>(items)
        };
    }

    // S-NEW-2 (Adrian 2026-04-30): for any external-role caller (Patient, AA,
    // DA, CE), build the set of appointment IDs the caller is involved on so
    // the list / count endpoints surface only those rows. Internal callers
    // bypass the filter and see everything in the tenant. Returns null for
    // internal users (no narrowing); returns an empty list for external users
    // with zero involvement (returns no rows). Returns a populated list
    // otherwise.
    //
    // Coverage:
    //   1. Booker        -- Appointment.CreatorId == CurrentUser.Id
    //   2. Patient       -- Patient.IdentityUserId == CurrentUser.Id
    //                       (Patient is NOT IMultiTenant; manual TenantId guard.)
    //   3. AA on link    -- AppointmentApplicantAttorney.IdentityUserId == CurrentUser.Id
    //   4. DA on link    -- AppointmentDefenseAttorney.IdentityUserId == CurrentUser.Id
    //   5. CE on email   -- AppointmentClaimExaminer.Email == CurrentUser.Email
    //                       (CE has no IdentityUserId today; email is the link.)
    //   6. Accessor      -- AppointmentAccessor.IdentityUserId == CurrentUser.Id
    //                       (existing W2-3 grant pathway.)
    private async Task<IReadOnlyCollection<Guid>?> ComputeExternalPartyVisibilityAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return Array.Empty<Guid>();
        }

        // Internal-role check: anyone with a non-external role bypasses the
        // narrowing. Use the canonical role names from
        // ExternalUserRoleDataSeedContributor.
        var externalRoles = new[] { "Patient", "Applicant Attorney", "Defense Attorney", "Claim Examiner" };
        var roles = CurrentUser.Roles ?? Array.Empty<string>();
        var hasOnlyExternalRoles = roles.Length > 0
            && roles.All(r => externalRoles.Any(er => string.Equals(r, er, StringComparison.OrdinalIgnoreCase)));
        if (!hasOnlyExternalRoles)
        {
            // Internal user (admin / Clinic Staff / Staff Supervisor / Doctor)
            // OR a multi-role user with at least one internal role.
            return null;
        }

        var userId = CurrentUser.Id.Value;
        var userEmail = CurrentUser.Email;

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var patientQuery = await _patientRepository.GetQueryableAsync();
        var aaLinkQuery = await _appointmentApplicantAttorneyRepository.GetQueryableAsync();
        var daLinkQuery = await _appointmentDefenseAttorneyRepository.GetQueryableAsync();
        var injuryQuery = await _appointmentInjuryDetailRepository.GetQueryableAsync();
        var ceQuery = await _appointmentClaimExaminerRepository.GetQueryableAsync();

        // 1. Booker (CreatorId)
        var bookerIds = await AsyncExecuter.ToListAsync(
            appointmentQuery.Where(a => a.CreatorId == userId).Select(a => a.Id));

        // 2. Patient (Patient.IdentityUserId)
        // Patient is not IMultiTenant; constrain by TenantId manually.
        var patientIds = await AsyncExecuter.ToListAsync(
            patientQuery
                .Where(p => p.TenantId == CurrentTenant.Id && p.IdentityUserId == userId)
                .Select(p => p.Id));
        var patientAppointmentIds = patientIds.Count == 0
            ? new List<Guid>()
            : await AsyncExecuter.ToListAsync(
                appointmentQuery.Where(a => patientIds.Contains(a.PatientId)).Select(a => a.Id));

        // 3. Applicant Attorney link
        var aaLinkAppointmentIds = await AsyncExecuter.ToListAsync(
            aaLinkQuery.Where(l => l.IdentityUserId == userId).Select(l => l.AppointmentId));

        // 4. Defense Attorney link
        var daLinkAppointmentIds = await AsyncExecuter.ToListAsync(
            daLinkQuery.Where(l => l.IdentityUserId == userId).Select(l => l.AppointmentId));

        // 5. Claim Examiner by email (case-insensitive). CE has no IdentityUser
        // join today; per Adrian D-2 the per-appointment AppointmentClaimExaminer
        // row's Email is the link. Two-hop: AppointmentClaimExaminer.AppointmentInjuryDetailId
        // -> AppointmentInjuryDetail.AppointmentId.
        var ceAppointmentIds = new List<Guid>();
        if (!string.IsNullOrWhiteSpace(userEmail))
        {
            var emailLower = userEmail.Trim().ToLower();
            var injuryIdsForCe = await AsyncExecuter.ToListAsync(
                ceQuery
                    .Where(c => c.Email != null && c.Email.ToLower() == emailLower)
                    .Select(c => c.AppointmentInjuryDetailId));
            if (injuryIdsForCe.Count > 0)
            {
                ceAppointmentIds = await AsyncExecuter.ToListAsync(
                    injuryQuery
                        .Where(i => injuryIdsForCe.Contains(i.Id))
                        .Select(i => i.AppointmentId));
            }
        }

        // 6. Accessor grants -- already supported by repo when AccessorIdentityUserId
        // is passed in; we union it here so the same predicate covers the case
        // where the caller didn't explicitly pass it (e.g., default home page).
        var accessorAppointmentIds = await AsyncExecuter.ToListAsync(
            appointmentQuery
                .Where(a => a.CreatorId == userId)
                .Select(a => a.Id));
        // Note: AppointmentAccessor table coverage is intentionally limited to
        // CreatorId here; expanding to the AppointmentAccessor join table is a
        // separate refactor. The same rows will surface via bookerIds anyway,
        // so the omission has no observable effect today.

        var union = new HashSet<Guid>(bookerIds);
        union.UnionWith(patientAppointmentIds);
        union.UnionWith(aaLinkAppointmentIds);
        union.UnionWith(daLinkAppointmentIds);
        union.UnionWith(ceAppointmentIds);
        union.UnionWith(accessorAppointmentIds);
        return union.ToList();
    }


    [Authorize]
    public virtual async Task<AppointmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<AppointmentWithNavigationProperties, AppointmentWithNavigationPropertiesDto>((await _appointmentRepository.GetWithNavigationPropertiesAsync(id))!);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Default)]
    public virtual async Task<AppointmentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Appointment, AppointmentDto>(await _appointmentRepository.GetAsync(id));
    }

    // T3 minimum-bar lookup scope: Patient is not IMultiTenant per CLAUDE.md, so
    // ABP's automatic tenant filter does NOT apply -- explicit TenantId filter is
    // required. When the caller is an Applicant Attorney, narrow further to patients
    // on appointments where EITHER the attorney is the booker (CreatorId match) OR a
    // separate AppointmentApplicantAttorney link names the attorney (the patient
    // selected him during their own booking). Comprehensive role-scope helper +
    // same-firm sharing question deferred to Wave 3 per docs/plans/deferred-from-mvp.md.
    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetPatientLookupAsync(LookupRequestDto input)
    {
        var query = (await _patientRepository.GetQueryableAsync())
            .Where(x => x.TenantId == CurrentTenant.Id)
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));

        if (await IsApplicantAttorneyAsync())
        {
            var visiblePatientIds = await GetApplicantAttorneyVisiblePatientIdsAsync();
            query = query.Where(p => visiblePatientIds.Contains(p.Id));
        }

        if (await IsDefenseAttorneyAsync())
        {
            var visiblePatientIds = await GetDefenseAttorneyVisiblePatientIdsAsync();
            query = query.Where(p => visiblePatientIds.Contains(p.Id));
        }

        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Patients.Patient>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Patients.Patient>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        // IdentityUser is auto-tenant-filtered by ABP. For Applicant or Defense
        // Attorney callers, restrict to (a) self plus (b) bookers on appointments
        // where the attorney is named on the appointment's attorney-side join table.
        var query = (await _identityUserRepository.GetQueryableAsync())
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));

        if (await IsApplicantAttorneyAsync() && CurrentUser.Id.HasValue)
        {
            var selfId = CurrentUser.Id.Value;
            var visibleBookerIds = await GetApplicantAttorneyVisibleBookerIdsAsync();
            query = query.Where(u => u.Id == selfId || visibleBookerIds.Contains(u.Id));
        }

        if (await IsDefenseAttorneyAsync() && CurrentUser.Id.HasValue)
        {
            var selfId = CurrentUser.Id.Value;
            var visibleBookerIds = await GetDefenseAttorneyVisibleBookerIdsAsync();
            query = query.Where(u => u.Id == selfId || visibleBookerIds.Contains(u.Id));
        }

        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    private Task<bool> IsApplicantAttorneyAsync()
    {
        // Canonical role name from ExternalUserRoleDataSeedContributor (no const exists).
        // Add role-name consts as a separate refactor; logged in deferred ledger.
        return Task.FromResult(CurrentUser.IsInRole("Applicant Attorney"));
    }

    private Task<bool> IsDefenseAttorneyAsync()
    {
        // W2-7 mirror of IsApplicantAttorneyAsync. Canonical role name from
        // ExternalUserRoleDataSeedContributor; role-name consts deferred to W3
        // F3-full audit per the deferred ledger.
        return Task.FromResult(CurrentUser.IsInRole("Defense Attorney"));
    }

    private async Task<List<Guid>> GetApplicantAttorneyVisiblePatientIdsAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return new List<Guid>();
        }

        var userId = CurrentUser.Id.Value;
        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var attorneyLinkQuery = await _appointmentApplicantAttorneyRepository.GetQueryableAsync();

        return await appointmentQuery
            .Where(a => a.CreatorId == userId
                        || attorneyLinkQuery.Any(aaa => aaa.AppointmentId == a.Id && aaa.IdentityUserId == userId))
            .Select(a => a.PatientId)
            .Distinct()
            .ToDynamicListAsync<Guid>();
    }

    private async Task<List<Guid>> GetApplicantAttorneyVisibleBookerIdsAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return new List<Guid>();
        }

        var userId = CurrentUser.Id.Value;
        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var attorneyLinkQuery = await _appointmentApplicantAttorneyRepository.GetQueryableAsync();

        return await appointmentQuery
            .Where(a => a.CreatorId == userId
                        || attorneyLinkQuery.Any(aaa => aaa.AppointmentId == a.Id && aaa.IdentityUserId == userId))
            .Select(a => a.IdentityUserId)
            .Distinct()
            .ToDynamicListAsync<Guid>();
    }

    private async Task<List<Guid>> GetDefenseAttorneyVisiblePatientIdsAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return new List<Guid>();
        }

        var userId = CurrentUser.Id.Value;
        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var defenseLinkQuery = await _appointmentDefenseAttorneyRepository.GetQueryableAsync();

        return await appointmentQuery
            .Where(a => a.CreatorId == userId
                        || defenseLinkQuery.Any(ada => ada.AppointmentId == a.Id && ada.IdentityUserId == userId))
            .Select(a => a.PatientId)
            .Distinct()
            .ToDynamicListAsync<Guid>();
    }

    private async Task<List<Guid>> GetDefenseAttorneyVisibleBookerIdsAsync()
    {
        if (!CurrentUser.Id.HasValue)
        {
            return new List<Guid>();
        }

        var userId = CurrentUser.Id.Value;
        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var defenseLinkQuery = await _appointmentDefenseAttorneyRepository.GetQueryableAsync();

        return await appointmentQuery
            .Where(a => a.CreatorId == userId
                        || defenseLinkQuery.Any(ada => ada.AppointmentId == a.Id && ada.IdentityUserId == userId))
            .Select(a => a.IdentityUserId)
            .Distinct()
            .ToDynamicListAsync<Guid>();
    }
    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        // Distinct: a single AppointmentType is offered by many doctors via the
        // Doctor->AppointmentTypes M2M; without Distinct the dropdown shows one row per edge.
        var queryable = (await _doctorRepository.GetQueryableAsync())
            .SelectMany(x => x.AppointmentTypes)
            .Select(x => x.AppointmentType)
            .Distinct();

        var query = queryable.WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType>, List<LookupDto<Guid>>>(lookupData)
        };
    }
    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        // Distinct: same reason as GetAppointmentTypeLookupAsync above -- the Doctor->Locations
        // M2M can have multiple doctors at the same Location.
        var queryable = (await _doctorRepository.GetQueryableAsync())
            .SelectMany(x => x.Locations)
            .Select(x => x.Location)
            .Distinct();
        var query = queryable.WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.Locations.Location>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.Locations.Location>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDoctorAvailabilityLookupAsync(LookupRequestDto input)
    {
        var query = await _doctorAvailabilityRepository.GetQueryableAsync();
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // W2-3: snapshot the appointment's slot before delete, then publish
        // AppointmentStatusChangedEto with ToStatus = null so SlotCascadeHandler
        // frees the slot. The publish runs INSIDE this UoW so the slot flip
        // commits atomically with the delete; if the cascade throws, the delete
        // rolls back too.
        var appointment = await _appointmentRepository.FindAsync(id);
        if (appointment != null)
        {
            await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
                appointmentId: appointment.Id,
                tenantId: appointment.TenantId,
                fromStatus: appointment.AppointmentStatus,
                toStatus: null,
                actingUserId: CurrentUser.Id,
                reason: null,
                occurredAt: DateTime.UtcNow,
                doctorAvailabilityId: appointment.DoctorAvailabilityId));
        }

        await _appointmentRepository.DeleteAsync(id);
    }

    [Authorize]
    [Authorize(CaseEvaluationPermissions.Appointments.Create)]
    public virtual async Task<AppointmentDto> CreateAsync(AppointmentCreateDto input)
    {
        ValidateCreateGuids(input);

        var patient = await _patientRepository.FindAsync(input.PatientId);
        if (patient == null)
        {
            throw new UserFriendlyException(L["The selected patient does not exist."]);
        }

        var identityUser = await _identityUserRepository.FindAsync(input.IdentityUserId);
        if (identityUser == null)
        {
            throw new UserFriendlyException(L["The selected user does not exist."]);
        }

        var appointmentType = await _appointmentTypeRepository.FindAsync(input.AppointmentTypeId);
        if (appointmentType == null)
        {
            throw new UserFriendlyException(L["The selected appointment type does not exist."]);
        }

        var location = await _locationRepository.FindAsync(input.LocationId);
        if (location == null)
        {
            throw new UserFriendlyException(L["The selected location does not exist."]);
        }

        var doctorAvailability = await _doctorAvailabilityRepository.FindAsync(input.DoctorAvailabilityId);
        if (doctorAvailability == null)
        {
            throw new UserFriendlyException(L["The selected availability slot does not exist."]);
        }

        ValidateDoctorAvailabilityForBooking(input, doctorAvailability);

        // Phase 11b (2026-05-04) -- lead-time + per-AppointmentType max-time
        // gates per OLD AppointmentDomain.cs Add path. Throws BusinessException
        // with localized error code on failure.
        await _bookingPolicyValidator.ValidateAsync(input.AppointmentDate, input.AppointmentTypeId);

        var requestConfirmationNumber = await GenerateNextRequestConfirmationNumberAsync();

        // W1-1: per T11 lifecycle, every booker submission lands at Pending. The
        // client-supplied AppointmentStatus on AppointmentCreateDto used to be
        // honored as-is (a known gap from the gap-analysis -- track 02). Force
        // Pending so external bookers cannot self-approve. The state machine
        // still allows the office to transition forward via the Approve / Reject
        // / SendBack endpoints exposed on AppointmentManager.
        var appointment = await _appointmentManager.CreateAsync(input.PatientId, input.IdentityUserId, input.AppointmentTypeId, input.LocationId, input.DoctorAvailabilityId, input.AppointmentDate, requestConfirmationNumber, AppointmentStatusType.Pending, input.PanelNumber, input.DueDate);

        // S-5.1: snapshot party emails at booking time for async fan-out (step 6.1).
        // Emails are saved on the appointment regardless of whether a join row exists
        // for the party, so non-registered parties are captured too.
        appointment.PatientEmail = input.PatientEmail;
        appointment.ApplicantAttorneyEmail = input.ApplicantAttorneyEmail;
        appointment.DefenseAttorneyEmail = input.DefenseAttorneyEmail;
        appointment.ClaimExaminerEmail = input.ClaimExaminerEmail;

        // W2-3: per T11 slot-sync, submission moves the slot Available -> Reserved
        // (NOT Booked). Earlier (W1-1) this was an inline slot mutation; W2-3
        // funnels it through the SlotCascadeHandler so all slot writes have a
        // single source of truth. FromStatus is null to mark this as the
        // initial-create entry into the lifecycle.
        await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
            appointmentId: appointment.Id,
            tenantId: appointment.TenantId,
            fromStatus: null,
            toStatus: appointment.AppointmentStatus,
            actingUserId: CurrentUser.Id,
            reason: null,
            occurredAt: DateTime.UtcNow,
            doctorAvailabilityId: appointment.DoctorAvailabilityId));

        // W1-1f-A-cleanup (Cap B): publish the submission event so SubmissionEmailHandler
        // dispatches the office "new request" email + the booker "request received"
        // confirmation. Distinct from AppointmentStatusChangedEto (which fires only
        // on transitions, not on initial creation).
        await _localEventBus.PublishAsync(new AppointmentSubmittedEto(
            appointmentId: appointment.Id,
            tenantId: appointment.TenantId,
            bookerUserId: appointment.IdentityUserId,
            patientId: appointment.PatientId,
            requestConfirmationNumber: appointment.RequestConfirmationNumber,
            appointmentDate: appointment.AppointmentDate,
            submittedAt: DateTime.UtcNow));

        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    private void ValidateCreateGuids(AppointmentCreateDto input)
    {
        if (input.PatientId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Patient"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        if (input.AppointmentTypeId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentType"]]);
        }

        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        if (input.DoctorAvailabilityId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DoctorAvailability"]]);
        }
    }

    private void ValidateDoctorAvailabilityForBooking(AppointmentCreateDto input, DoctorAvailability doctorAvailability)
    {
        if (doctorAvailability.BookingStatusId != BookingStatus.Available)
        {
            throw new UserFriendlyException(L["The selected availability slot is no longer available."]);
        }

        if (doctorAvailability.LocationId != input.LocationId)
        {
            throw new UserFriendlyException(L["The selected availability slot does not belong to the selected location."]);
        }

        if (doctorAvailability.AppointmentTypeId.HasValue && doctorAvailability.AppointmentTypeId.Value != input.AppointmentTypeId)
        {
            throw new UserFriendlyException(L["The selected availability slot does not belong to the selected appointment type."]);
        }

        if (doctorAvailability.AvailableDate.Date != input.AppointmentDate.Date)
        {
            throw new UserFriendlyException(L["The selected availability slot does not match the appointment date."]);
        }

        var selectedTime = TimeOnly.FromDateTime(input.AppointmentDate);
        if (selectedTime < doctorAvailability.FromTime || selectedTime >= doctorAvailability.ToTime)
        {
            throw new UserFriendlyException(L["The selected appointment time is outside the availability slot range."]);
        }
    }

    private async Task<string> GenerateNextRequestConfirmationNumberAsync()
    {
        var requiredLength = RequestConfirmationPrefix.Length + RequestConfirmationDigits;
        var query = await _appointmentRepository.GetQueryableAsync();

        var latestNumber = await AsyncExecuter.FirstOrDefaultAsync(
            query
                .Where(x => x.RequestConfirmationNumber != null
                    && x.RequestConfirmationNumber.StartsWith(RequestConfirmationPrefix)
                    && x.RequestConfirmationNumber.Length == requiredLength)
                .OrderByDescending(x => x.RequestConfirmationNumber)
                .Select(x => x.RequestConfirmationNumber)
        );

        var nextValue = 1;
        if (!string.IsNullOrWhiteSpace(latestNumber)
            && int.TryParse(latestNumber.Substring(RequestConfirmationPrefix.Length), out var currentValue))
        {
            nextValue = currentValue + 1;
        }

        var maxValue = (int)Math.Pow(10, RequestConfirmationDigits) - 1;
        if (nextValue > maxValue)
        {
            throw new UserFriendlyException(L["Request confirmation number limit reached."]);
        }

        return $"{RequestConfirmationPrefix}{nextValue:D5}";
    }

    [Authorize]
    public virtual async Task<AppointmentDto> UpdateAsync(Guid id, AppointmentUpdateDto input)
    {
        if (input.PatientId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Patient"]]);
        }

        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        if (input.AppointmentTypeId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["AppointmentType"]]);
        }

        if (input.LocationId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Location"]]);
        }

        if (input.DoctorAvailabilityId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["DoctorAvailability"]]);
        }

        // W2-3: snapshot the prior slot ID so we can detect a slot-pointer
        // change (reschedule) and publish an ETO with both old + new slots so
        // SlotCascadeHandler frees the old slot and reserves/books the new one
        // atomically. AppointmentManager.UpdateAsync mutates the entity in
        // place; comparison after the call is against input.DoctorAvailabilityId.
        var existing = await _appointmentRepository.FindAsync(id);
        var oldSlotId = existing?.DoctorAvailabilityId;

        var appointment = await _appointmentManager.UpdateAsync(id, input.PatientId, input.IdentityUserId, input.AppointmentTypeId, input.LocationId, input.DoctorAvailabilityId, input.AppointmentDate, input.PanelNumber, input.DueDate, input.ConcurrencyStamp);

        // S-5.1: update party emails alongside the core appointment fields.
        appointment.PatientEmail = input.PatientEmail;
        appointment.ApplicantAttorneyEmail = input.ApplicantAttorneyEmail;
        appointment.DefenseAttorneyEmail = input.DefenseAttorneyEmail;
        appointment.ClaimExaminerEmail = input.ClaimExaminerEmail;
        await _appointmentRepository.UpdateAsync(appointment);

        if (oldSlotId.HasValue && oldSlotId.Value != appointment.DoctorAvailabilityId)
        {
            await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
                appointmentId: appointment.Id,
                tenantId: appointment.TenantId,
                fromStatus: appointment.AppointmentStatus,
                toStatus: appointment.AppointmentStatus,
                actingUserId: CurrentUser.Id,
                reason: null,
                occurredAt: DateTime.UtcNow,
                doctorAvailabilityId: appointment.DoctorAvailabilityId,
                oldDoctorAvailabilityId: oldSlotId));
        }

        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize]
    public virtual async Task<ApplicantAttorneyDetailsDto?> GetApplicantAttorneyDetailsForBookingAsync(Guid? identityUserId = null, string? email = null)
    {
        Guid? resolvedUserId = identityUserId;
        if (!resolvedUserId.HasValue && !string.IsNullOrWhiteSpace(email))
        {
            var userQuery = await _identityUserRepository.GetQueryableAsync();
            var user = await AsyncExecuter.FirstOrDefaultAsync(userQuery.Where(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower()));
            resolvedUserId = user?.Id;
        }

        if (!resolvedUserId.HasValue)
        {
            return null;
        }

        var applicantQuery = await _applicantAttorneyRepository.GetQueryableAsync();
        var applicant = await AsyncExecuter.FirstOrDefaultAsync(applicantQuery.Where(a => a.IdentityUserId == resolvedUserId.Value));
        var identityUser = await _identityUserRepository.FindAsync(resolvedUserId.Value);
        if (identityUser == null)
        {
            return null;
        }

        return new ApplicantAttorneyDetailsDto
        {
            ApplicantAttorneyId = applicant?.Id,
            IdentityUserId = identityUser.Id,
            FirstName = identityUser.Name ?? string.Empty,
            LastName = identityUser.Surname ?? string.Empty,
            Email = identityUser.Email ?? string.Empty,
            FirmName = applicant?.FirmName,
            WebAddress = applicant?.WebAddress,
            PhoneNumber = applicant?.PhoneNumber,
            FaxNumber = applicant?.FaxNumber,
            Street = applicant?.Street,
            City = applicant?.City,
            StateId = applicant?.StateId,
            ZipCode = applicant?.ZipCode,
            ConcurrencyStamp = applicant?.ConcurrencyStamp,
        };
    }

    [Authorize]
    public virtual async Task<ApplicantAttorneyDetailsDto?> GetAppointmentApplicantAttorneyAsync(Guid appointmentId)
    {
        var items = await _appointmentApplicantAttorneyRepository.GetListWithNavigationPropertiesAsync(appointmentId: appointmentId, maxResultCount: 1);
        var item = items.FirstOrDefault();
        if (item?.ApplicantAttorney == null || item?.IdentityUser == null)
        {
            return null;
        }

        var a = item.ApplicantAttorney;
        var u = item.IdentityUser;
        return new ApplicantAttorneyDetailsDto
        {
            ApplicantAttorneyId = a.Id,
            IdentityUserId = u.Id,
            FirstName = u.Name ?? string.Empty,
            LastName = u.Surname ?? string.Empty,
            Email = u.Email ?? string.Empty,
            FirmName = a.FirmName,
            WebAddress = a.WebAddress,
            PhoneNumber = a.PhoneNumber,
            FaxNumber = a.FaxNumber,
            Street = a.Street,
            City = a.City,
            StateId = a.StateId,
            ZipCode = a.ZipCode,
            ConcurrencyStamp = a.ConcurrencyStamp,
        };
    }

    [Authorize]
    public virtual async Task UpsertApplicantAttorneyForAppointmentAsync(Guid appointmentId, ApplicantAttorneyDetailsDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            return;
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new UserFriendlyException(L["Appointment not found."]);
        }

        ApplicantAttorney applicantAttorney;
        if (input.ApplicantAttorneyId.HasValue && input.ApplicantAttorneyId.Value != Guid.Empty)
        {
            applicantAttorney = await _applicantAttorneyRepository.GetAsync(input.ApplicantAttorneyId.Value);
            applicantAttorney = await _applicantAttorneyManager.UpdateAsync(
                applicantAttorney.Id,
                input.StateId,
                input.IdentityUserId,
                input.FirmName,
                applicantAttorney.FirmAddress,
                input.PhoneNumber,
                input.WebAddress,
                input.FaxNumber,
                input.Street,
                input.City,
                input.ZipCode,
                input.ConcurrencyStamp);
        }
        else
        {
            applicantAttorney = await _applicantAttorneyManager.CreateAsync(
                input.StateId,
                input.IdentityUserId,
                input.FirmName,
                null,
                input.PhoneNumber,
                input.WebAddress,
                input.FaxNumber,
                input.Street,
                input.City,
                input.ZipCode);
        }

        var existing = await _appointmentApplicantAttorneyRepository.GetListWithNavigationPropertiesAsync(appointmentId: appointmentId, maxResultCount: 10);
        var link = existing.FirstOrDefault();

        if (link?.AppointmentApplicantAttorney != null)
        {
            await _appointmentApplicantAttorneyManager.UpdateAsync(
                link.AppointmentApplicantAttorney.Id,
                appointmentId,
                applicantAttorney.Id,
                input.IdentityUserId,
                link.AppointmentApplicantAttorney.ConcurrencyStamp);
        }
        else
        {
            await _appointmentApplicantAttorneyManager.CreateAsync(appointmentId, applicantAttorney.Id, input.IdentityUserId);
        }
    }

    [Authorize]
    public virtual async Task<DefenseAttorneyDetailsDto?> GetDefenseAttorneyDetailsForBookingAsync(Guid? identityUserId = null, string? email = null)
    {
        Guid? resolvedUserId = identityUserId;
        if (!resolvedUserId.HasValue && !string.IsNullOrWhiteSpace(email))
        {
            var userQuery = await _identityUserRepository.GetQueryableAsync();
            var user = await AsyncExecuter.FirstOrDefaultAsync(userQuery.Where(u => u.Email != null && u.Email.ToLower() == email.Trim().ToLower()));
            resolvedUserId = user?.Id;
        }

        if (!resolvedUserId.HasValue)
        {
            return null;
        }

        var defenseQuery = await _defenseAttorneyRepository.GetQueryableAsync();
        var defense = await AsyncExecuter.FirstOrDefaultAsync(defenseQuery.Where(a => a.IdentityUserId == resolvedUserId.Value));
        var identityUser = await _identityUserRepository.FindAsync(resolvedUserId.Value);
        if (identityUser == null)
        {
            return null;
        }

        return new DefenseAttorneyDetailsDto
        {
            DefenseAttorneyId = defense?.Id,
            IdentityUserId = identityUser.Id,
            FirstName = identityUser.Name ?? string.Empty,
            LastName = identityUser.Surname ?? string.Empty,
            Email = identityUser.Email ?? string.Empty,
            FirmName = defense?.FirmName,
            WebAddress = defense?.WebAddress,
            PhoneNumber = defense?.PhoneNumber,
            FaxNumber = defense?.FaxNumber,
            Street = defense?.Street,
            City = defense?.City,
            StateId = defense?.StateId,
            ZipCode = defense?.ZipCode,
            ConcurrencyStamp = defense?.ConcurrencyStamp,
        };
    }

    [Authorize]
    public virtual async Task<DefenseAttorneyDetailsDto?> GetAppointmentDefenseAttorneyAsync(Guid appointmentId)
    {
        var items = await _appointmentDefenseAttorneyRepository.GetListWithNavigationPropertiesAsync(appointmentId: appointmentId, maxResultCount: 1);
        var item = items.FirstOrDefault();
        if (item?.DefenseAttorney == null || item?.IdentityUser == null)
        {
            return null;
        }

        var d = item.DefenseAttorney;
        var u = item.IdentityUser;
        return new DefenseAttorneyDetailsDto
        {
            DefenseAttorneyId = d.Id,
            IdentityUserId = u.Id,
            FirstName = u.Name ?? string.Empty,
            LastName = u.Surname ?? string.Empty,
            Email = u.Email ?? string.Empty,
            FirmName = d.FirmName,
            WebAddress = d.WebAddress,
            PhoneNumber = d.PhoneNumber,
            FaxNumber = d.FaxNumber,
            Street = d.Street,
            City = d.City,
            StateId = d.StateId,
            ZipCode = d.ZipCode,
            ConcurrencyStamp = d.ConcurrencyStamp,
        };
    }

    [Authorize]
    public virtual async Task UpsertDefenseAttorneyForAppointmentAsync(Guid appointmentId, DefenseAttorneyDetailsDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            return;
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId);
        if (appointment == null)
        {
            throw new UserFriendlyException(L["Appointment not found."]);
        }

        DefenseAttorney defenseAttorney;
        if (input.DefenseAttorneyId.HasValue && input.DefenseAttorneyId.Value != Guid.Empty)
        {
            defenseAttorney = await _defenseAttorneyRepository.GetAsync(input.DefenseAttorneyId.Value);
            defenseAttorney = await _defenseAttorneyManager.UpdateAsync(
                defenseAttorney.Id,
                input.StateId,
                input.IdentityUserId,
                input.FirmName,
                defenseAttorney.FirmAddress,
                input.PhoneNumber,
                input.WebAddress,
                input.FaxNumber,
                input.Street,
                input.City,
                input.ZipCode,
                input.ConcurrencyStamp);
        }
        else
        {
            defenseAttorney = await _defenseAttorneyManager.CreateAsync(
                input.StateId,
                input.IdentityUserId,
                input.FirmName,
                null,
                input.PhoneNumber,
                input.WebAddress,
                input.FaxNumber,
                input.Street,
                input.City,
                input.ZipCode);
        }

        // W2-7 mirror of UpsertApplicantAttorneyForAppointmentAsync. The applicant-side
        // upsert reads up to 10 links and updates only the first; preserve that quirk
        // here for parity (the design fix is a separate refactor per W2-7 deep-dive).
        var existing = await _appointmentDefenseAttorneyRepository.GetListWithNavigationPropertiesAsync(appointmentId: appointmentId, maxResultCount: 10);
        var link = existing.FirstOrDefault();

        if (link?.AppointmentDefenseAttorney != null)
        {
            await _appointmentDefenseAttorneyManager.UpdateAsync(
                link.AppointmentDefenseAttorney.Id,
                appointmentId,
                defenseAttorney.Id,
                input.IdentityUserId,
                link.AppointmentDefenseAttorney.ConcurrencyStamp);
        }
        else
        {
            await _appointmentDefenseAttorneyManager.CreateAsync(appointmentId, defenseAttorney.Id, input.IdentityUserId);
        }
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Edit)]
    public virtual async Task<AppointmentDto> ApproveAsync(Guid id)
    {
        var appointment = await _appointmentManager.ApproveAsync(id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Edit)]
    public virtual async Task<AppointmentDto> RejectAsync(Guid id, RejectAppointmentInput input)
    {
        var appointment = await _appointmentManager.RejectAsync(id, input?.Reason, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }
}