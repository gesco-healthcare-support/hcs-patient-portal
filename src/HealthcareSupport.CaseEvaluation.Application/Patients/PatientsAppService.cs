using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Saas.Tenants;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Patients;

[RemoteService(IsEnabled = false)]
public class PatientsAppService : CaseEvaluationAppService, IPatientsAppService
{
    protected IPatientRepository _patientRepository;
    protected PatientManager _patientManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage, Guid> _appointmentLanguageRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> _tenantRepository;
    protected IRepository<Appointment, Guid> _appointmentRepository;

    // FEAT-09 (ADR-006 T4): Patient is now IMultiTenant. ABP applies a
    // WHERE TenantId = CurrentTenant.Id filter automatically. In host
    // context (CurrentTenant.Id == null) the filter generates
    // WHERE TenantId IS NULL, which excludes every tenant-scoped row.
    // Admin / IT-Admin paths run in host context but must see every
    // tenant's patients, so we wrap the read in `_dataFilter.Disable()`
    // when no tenant is current. Mirrors DoctorsAppService (since Doctor
    // is also IMultiTenant). Booking + profile paths run inside a tenant
    // OAuth context in production; the filter applies and scopes
    // correctly. The same disable() pattern is used for booking-flow
    // reads to keep tests that simulate the booking call from host
    // context (without an OAuth-resolved tenant) working without any
    // production-correctness compromise.
    private readonly IDataFilter<IMultiTenant> _dataFilter;
    private readonly PatientBookingReadAccess _bookingReadAccess;

    // #598: one message for every external refusal of the booking edit, whether the record belongs to
    // someone else or does not exist, so the two cannot be told apart.
    private const string NotAuthorizedToEditPatientMessage = "Not authorized to edit this patient.";

    // The same idea for the booking read: one message whether the record is someone else's or missing.
    private const string NotAuthorizedToReadPatientMessage = "Not authorized to view this patient.";

    // 2026-08-17: renders the *.InUse delete guards as their real message. Without it the
    // raw BusinessException reaches the SPA with no message and the toast falls back to
    // ABP's generic "An internal error occurred during your request!".
    public PatientsAppService(IPatientRepository patientRepository, PatientManager patientManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage, Guid> appointmentLanguageRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository, IRepository<Appointment, Guid> appointmentRepository, IDataFilter<IMultiTenant> dataFilter, PatientBookingReadAccess bookingReadAccess)
    {
        _patientRepository = patientRepository;
        _patientManager = patientManager;
        _stateRepository = stateRepository;
        _appointmentLanguageRepository = appointmentLanguageRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _appointmentRepository = appointmentRepository;
        _dataFilter = dataFilter;
        _bookingReadAccess = bookingReadAccess;
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PagedResultDto<PatientWithNavigationPropertiesDto>> GetListAsync(GetPatientsInput input)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var totalCount = await _patientRepository.GetCountAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId);
            var items = await _patientRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
            var dtoItems = ObjectMapper.Map<List<PatientWithNavigationProperties>, List<PatientWithNavigationPropertiesDto>>(items);
            ApplySsnVisibilityToList(dtoItems);
            return new PagedResultDto<PatientWithNavigationPropertiesDto>
            {
                TotalCount = totalCount,
                Items = dtoItems
            };
        }
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual Task<PatientWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return GetMaskedPatientAsync(id);
    }

    // Not a named permission, because the booking wizard fetches a selected patient's profile from
    // this route while serving an external booker who does not hold CaseEvaluation.Patients: a
    // permission here returns 403 and "book for an existing patient" stops working for attorneys and
    // claim examiners. But a session alone is not enough either -- that let any signed-in caller read
    // any patient in their office. So the check is per record (PatientBookingReadAccess): staff with
    // the permission, the patient's own login, or a party under the typeahead's own rule.
    //
    // THE CALL IS BY RAW URL, NOT THROUGH THE GENERATED PROXY, so searching for the proxy method
    // name finds nothing and the route looks unused. It is reached through RestService from
    // onPatientSelected in appointment-add.component.ts, behind the demographics typeahead that
    // renders only for isExternalUserNonPatient. Search this route's callers with the URL instead:
    //
    //     git grep -n "for-appointment-booking" -- angular/src/app ':!*.spec.ts'
    //
    // A refusal is a 403 (AbpAuthorizationException), and an EXTERNAL caller gets that same refusal
    // for an id that does not exist, so a 404 cannot tell them which ids are real. Staff keep the
    // not-found: they may see every patient in the office, so there is nothing for them to learn.
    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetPatientForAppointmentBookingAsync(Guid id)
    {
        await EnsureInternalCallerMayReadPatientsAsync();

        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var patientWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(id);
            if (patientWithNav?.Patient == null && BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
            {
                throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Patient), id);
            }

            if (patientWithNav?.Patient == null || !await _bookingReadAccess.CanReadAsync(patientWithNav.Patient))
            {
                throw new AbpAuthorizationException(NotAuthorizedToReadPatientMessage);
            }

            var dto = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(patientWithNav);
            ApplySsnVisibility(dto);
            return dto;
        }
    }

    /// <summary>
    /// Staff may read any patient in the office through the booking route, so hold them to the
    /// permission the regular read uses (<c>GetWithNavigationPropertiesAsync</c>). External callers
    /// are not asked for it; the per-record rule that follows admits only records they are entitled
    /// to. Runs before the lookup, so a refused caller learns nothing about the id.
    /// </summary>
    private async Task EnsureInternalCallerMayReadPatientsAsync()
    {
        if (!BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
        {
            return;
        }

        if (!await AuthorizationService.IsGrantedAsync(CaseEvaluationPermissions.Patients.Default))
        {
            throw new AbpAuthorizationException("Not authorized to view patients.");
        }
    }

    // The permission-gated read's body. The booking read above no longer shares it, because it adds a
    // per-record check this one does not need. The authorization lives on the public method, never here.
    private async Task<PatientWithNavigationPropertiesDto> GetMaskedPatientAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var dto = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>((await _patientRepository.GetWithNavigationPropertiesAsync(id))!);
            ApplySsnVisibility(dto);
            return dto;
        }
    }

    // Gated, unlike the three siblings, because this one is not reached at all. Its only UI call
    // site is loadPatientByEmail (appointment-add.component.ts:2892) and nothing invokes
    // loadPatientByEmail -- verified by URL as well as by proxy name. So the permission costs no
    // caller, and the method otherwise returns the same patient record as the gated GetList.
    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PatientWithNavigationPropertiesDto?> GetPatientByEmailForAppointmentBookingAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var patients = await _patientRepository.GetListWithNavigationPropertiesAsync(
                email: email.Trim(),
                maxResultCount: 1,
                skipCount: 0);
            var existing = patients.FirstOrDefault();
            if (existing?.Patient == null)
            {
                return null;
            }
            var dto = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing);
            ApplySsnVisibility(dto);
            return dto;
        }
    }

    // Returns the first de-duplication candidate the 3-of-6 rule matches, mapped and masked
    // for the caller, or null when none does.
    private async Task<PatientWithNavigationPropertiesDto?> FindDedupMatchAsync(
        CreatePatientForAppointmentBookingInput input,
        string? email,
        List<Patient> dedupCandidates)
    {
        var incoming = new HealthcareSupport.CaseEvaluation.Appointments.PatientDeduplicationCandidate
        {
            LastName = input.LastName,
            DateOfBirth = input.DateOfBirth,
            PhoneNumber = input.PhoneNumber,
            Email = email ?? string.Empty,
            SocialSecurityNumber = input.SocialSecurityNumber,
            ClaimNumber = null,
        };

        foreach (var candidate in dedupCandidates)
        {
            var candidateBag = new HealthcareSupport.CaseEvaluation.Appointments.PatientDeduplicationCandidate
            {
                LastName = candidate.LastName,
                DateOfBirth = candidate.DateOfBirth,
                PhoneNumber = candidate.PhoneNumber,
                Email = candidate.Email,
                SocialSecurityNumber = candidate.SocialSecurityNumber,
                ClaimNumber = null,
            };

            if (HealthcareSupport.CaseEvaluation.Appointments.AppointmentBookingValidators
                .IsPatientDuplicate(incoming, candidateBag))
            {
                var matchedWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(candidate.Id);
                if (matchedWithNav != null)
                {
                    // R2 (2026-05-04): 3-of-6 dedup matched an existing patient.
                    var dtoMatched = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(matchedWithNav);
                    dtoMatched.IsExisting = true;
                    ApplySsnVisibility(dtoMatched);
                    return dtoMatched;
                }
            }
        }
        return null;
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetOrCreatePatientForAppointmentBookingAsync(CreatePatientForAppointmentBookingInput input)
    {
        // task_d5407b22 (2026-07-21): patient email is OPTIONAL (injured workers often lack
        // one). Normalize blank -> null so we skip the email fast-path + email-based dedup
        // pull; the 3-of-6 name/DOB/SSN/phone/ZIP match still catches duplicates, and the
        // stored value falls back to "" (Patient.Email is NOT NULL but has no unique index).
        // The notification pipeline drops blank recipients, so a no-email patient simply gets
        // no patient-targeted mail (booker / applicant attorney still do).
        var email = string.IsNullOrWhiteSpace(input.Email) ? null : input.Email.Trim();

        var isHost = CurrentTenant.Id == null;
        // The using block wraps every Patient read in this method (email
        // fast-path, 3-of-6 dedup candidate scan, and the final
        // GetWithNavigationProperties echo) so the IMultiTenant filter
        // is consistently disabled for host-context callers. Tests run
        // booking from host context; production runs it inside an
        // OAuth-resolved tenant context, where the filter applies
        // naturally.
        using (isHost ? _dataFilter.Disable() : null)
        {
            if (email != null)
            {
                var existingPatients = await _patientRepository.GetListWithNavigationPropertiesAsync(
                    email: email,
                    maxResultCount: 1,
                    skipCount: 0);
                var existing = existingPatients.FirstOrDefault();
                if (existing?.Patient != null)
                {
                    // R2 (2026-05-04): email-fast-path resolved an existing patient.
                    var dtoExisting = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing);
                    dtoExisting.IsExisting = true;
                    ApplySsnVisibility(dtoExisting);
                    return dtoExisting;
                }
            }
            // Audit closeout 2026-05-04 -- OLD-parity 3-of-6 dedup
            // (Phase 11k repo method `GetDeduplicationCandidatesAsync`
            // wired here per the Phase 11k audit-doc commitment).
            // Mirrors OLD `AppointmentDomain.cs:732-780` IsPatientRegistered:
            // pull rows matching ANY of LastName / DOB / Phone / Email /
            // SSN, then count 3-of-6 matches via the pure helper. Email
            // match alone hit the fast path above; here we catch the
            // re-registration-under-different-email case the email
            // pre-check missed. NEW's PatientManager.FindOrCreateAsync
            // below remains as a safety net using its own (different)
            // 3-of-6 field set.
            var dedupCandidates = await _patientRepository.GetDeduplicationCandidatesAsync(
                tenantId: CurrentTenant.Id,
                lastName: input.LastName,
                dateOfBirth: input.DateOfBirth,
                phone: input.PhoneNumber,
                email: email,
                ssn: input.SocialSecurityNumber,
                // Patient does not carry ClaimNumber in NEW (per Phase 11k
                // audit doc) -- the column lives on AppointmentInjuryDetail
                // and is unavailable at Patient creation time. Pass null;
                // the predicate counts the remaining 5 fields.
                claimNumbers: null);

            var dedupMatch = await FindDedupMatchAsync(input, email, dedupCandidates);
            if (dedupMatch != null)
            {
                return dedupMatch;
            }

            // IP6 (2026-06-05): record-only model. Booking inserts a Patient
            // record with NO login -- it no longer mints an IdentityUser, grants
            // the Patient role, or sets any password. The SEC-05 shared-password
            // defect is removed by deletion, not patched. The patient claims a
            // login later via the appointment-request email's register link;
            // ExternalSignupAppService.RegisterAsync then links the record by
            // email and assigns the Patient role at that point.

            // W1-0 (W0-8 carry-over): delegate to PatientManager.FindOrCreateAsync so the
            // 3-of-6 fuzzy match (FirstName, LastName, DOB, SSN, Phone, ZipCode) catches
            // re-registration under a different email. Email pre-check above stays as the
            // fast-path; FindOrCreateAsync is the safety net.
            // R2 (2026-05-04): capture wasFound from PatientManager.FindOrCreateAsync
            // so we can echo the existence signal to the caller. wasFound=true means
            // FindOrCreate's own 3-of-6 (different field set than the dedup repo
            // method above) hit an existing row; wasFound=false means a brand-new
            // patient was inserted by FindOrCreate.
            var (patient, wasFound) = await _patientManager.FindOrCreateAsync(
                tenantId: CurrentTenant.Id,
                identityUserId: null,
                firstName: input.FirstName,
                lastName: input.LastName,
                email: email ?? string.Empty,
                genderId: input.GenderId,
                dateOfBirth: input.DateOfBirth,
                phoneNumberTypeId: input.PhoneNumberTypeId,
                stateId: input.StateId,
                appointmentLanguageId: input.AppointmentLanguageId,
                phoneNumber: input.PhoneNumber,
                socialSecurityNumber: input.SocialSecurityNumber,
                zipCode: input.ZipCode,
                middleName: input.MiddleName,
                address: input.Address,
                city: input.City,
                cellPhoneNumber: input.CellPhoneNumber,
                street: input.Street,
                interpreterVendorName: input.InterpreterVendorName,
                apptNumber: input.ApptNumber,
                othersLanguageName: input.OthersLanguageName);

            if (CurrentUnitOfWork != null)
            {
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            var createdWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(patient.Id);
            if (createdWithNav == null)
            {
                createdWithNav = new PatientWithNavigationProperties
                {
                    Patient = patient
                };
            }

            var dtoFinal = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(createdWithNav);
            dtoFinal.IsExisting = wasFound;
            ApplySsnVisibility(dtoFinal);
            return dtoFinal;
        }
    }

    /// <summary>
    /// Booking-flow patient edit. Stays a bare <c>[Authorize]</c> so external bookers can still
    /// reach their OWN record through it; who may edit WHICH record is decided in code, because it
    /// depends on the record (#598).
    /// <list type="bullet">
    ///   <item><description>Internal staff must hold <c>Patients.Edit</c>, checked before the lookup
    ///   -- the same bar as the regular <c>UpdateAsync</c>.</description></item>
    ///   <item><description>Everyone must pass <see cref="PatientBookingEditAccess.CanEdit"/>:
    ///   internal, or the patient's own login.</description></item>
    /// </list>
    /// A refusal is a 403 (<see cref="AbpAuthorizationException"/>), matching the SSN reveal.
    ///
    /// <para>An EXTERNAL caller gets that same refusal when the id does not exist. Were a missing id a
    /// 404 while another person's record is a 403, the difference alone would tell a caller which ids
    /// are real. Staff keep the not-found: they may see every patient in the office, so there is
    /// nothing for them to learn from it.</para>
    /// </summary>
    [Authorize]
    public virtual async Task<PatientDto> UpdatePatientForAppointmentBookingAsync(Guid id, PatientUpdateDto input)
    {
        await EnsureInternalCallerMayEditPatientsAsync();

        var isHost = CurrentTenant.Id == null;
        PatientWithNavigationProperties? patientWithNav;
        using (isHost ? _dataFilter.Disable() : null)
        {
            patientWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(id);
        }
        var currentPatient = patientWithNav?.Patient;
        if (currentPatient == null && BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Patient), id);
        }

        if (currentPatient == null
            || !PatientBookingEditAccess.CanEdit(CurrentUser.Roles, CurrentUser.Id, currentPatient.IdentityUserId))
        {
            throw new AbpAuthorizationException(NotAuthorizedToEditPatientMessage);
        }

        var patient = await _patientManager.UpdateAsync(
            id,
            input.StateId ?? currentPatient.StateId,
            input.AppointmentLanguageId ?? currentPatient.AppointmentLanguageId,
            currentPatient.IdentityUserId,
            currentPatient.TenantId,
            input.FirstName ?? currentPatient.FirstName,
            input.LastName ?? currentPatient.LastName,
            input.Email ?? currentPatient.Email,
            currentPatient.GenderId,
            currentPatient.DateOfBirth,
            currentPatient.PhoneNumberTypeId,
            input.MiddleName ?? currentPatient.MiddleName,
            input.PhoneNumber ?? currentPatient.PhoneNumber,
            input.SocialSecurityNumber ?? currentPatient.SocialSecurityNumber,
            input.Address ?? currentPatient.Address,
            input.City ?? currentPatient.City,
            input.ZipCode ?? currentPatient.ZipCode,
            input.CellPhoneNumber ?? currentPatient.CellPhoneNumber,
            input.Street ?? currentPatient.Street,
            input.InterpreterVendorName ?? currentPatient.InterpreterVendorName,
            input.ApptNumber ?? currentPatient.ApptNumber,
            input.OthersLanguageName ?? currentPatient.OthersLanguageName,
            input.ConcurrencyStamp ?? currentPatient.ConcurrencyStamp
        );

        return MapToMaskedDto(patient);
    }

    /// <summary>
    /// #598: an internal caller edits any patient in the office, so hold them to the permission the
    /// regular edit uses. External callers are not asked for it -- the owner rule that follows admits
    /// only their own record. Runs before the lookup so a refused caller learns nothing about the id.
    /// </summary>
    private async Task EnsureInternalCallerMayEditPatientsAsync()
    {
        if (!BookingFlowRoles.IsInternalUserCaller(CurrentUser.Roles))
        {
            return;
        }

        if (!await AuthorizationService.IsGrantedAsync(CaseEvaluationPermissions.Patients.Edit))
        {
            throw new AbpAuthorizationException("Not authorized to edit patients.");
        }
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetMyProfileAsync()
    {
        var patientWithNav = await GetCurrentPatientWithNavigationAsync();
        var dto = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(patientWithNav);
        // Caller is always the record owner here; the helper still applies for symmetry.
        ApplySsnVisibility(dto);
        return dto;
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PatientDto> GetAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var dto = ObjectMapper.Map<Patient, PatientDto>(await _patientRepository.GetAsync(id));
            ApplySsnVisibility(dto);
            return dto;
        }
    }

    // F1 / Design B (2026-05-29) -- dedicated, audited SSN reveal endpoint.
    // Standard payloads carry only the masked last-4 (see ApplySsnVisibility);
    // this returns the full value. Two gates: the Patients.RevealSsn permission
    // (declarative) AND the internal-or-owner check in SsnRevealAccess (so a
    // Patient can reveal only their OWN SSN, while internal staff may reveal
    // any). ABP's HTTP audit log records each call (caller + patient id in the
    // route: GET api/app/patients/{id}/ssn).
    [Authorize(CaseEvaluationPermissions.Patients.RevealSsn)]
    public virtual async Task<SsnRevealDto> GetFullSsnAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        Patient patient;
        using (isHost ? _dataFilter.Disable() : null)
        {
            patient = await _patientRepository.GetAsync(id);
        }

        if (!SsnRevealAccess.CanReveal(CurrentUser.Roles, CurrentUser.Id, patient.IdentityUserId))
        {
            throw new AbpAuthorizationException("Not authorized to reveal this patient's SSN.");
        }

        return new SsnRevealDto { SocialSecurityNumber = patient.SocialSecurityNumber };
    }

    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        // Issue 2.2 (2026-05-12): OrderBy Name asc for alphabetical
        // state list. OLD parity (AppointmentRequestLookupsController.cs:93).
        var query = (await _stateRepository.GetQueryableAsync())
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!))
            .OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.States.State>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.States.State>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLanguageLookupAsync(LookupRequestDto input)
    {
        // Issue 2.2 (2026-05-12): OrderBy Name asc.
        var query = (await _appointmentLanguageRepository.GetQueryableAsync())
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!))
            .OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = IdentityUserLookupScope.ForCaller((await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name), CurrentUser);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input)
    {
        var query = (await _tenantRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!)).OrderBy(x => x.Name);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Saas.Tenants.Tenant>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Saas.Tenants.Tenant>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(CaseEvaluationPermissions.Patients.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        // Prompt 15 / item 32: block delete while any Appointment references this
        // patient (Appointment.PatientId). Database-per-office: Patient and Appointment
        // are IMultiTenant in the office's own database, so the in-use probe runs in the
        // office's context where the filter already scopes it to that office.
        if (await _appointmentRepository.AnyAsync(a => a.PatientId == id))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.PatientInUse);
        }
        await _patientRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Create)]
    public virtual async Task<PatientDto> CreateAsync(PatientCreateDto input)
    {
        var patient = await _patientManager.CreateAsync(input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName);
        return MapToMaskedDto(patient);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Edit)]
    public virtual async Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto input)
    {
        var isHost = CurrentTenant.Id == null;
        // PatientManager.UpdateAsync internally calls GetAsync(id) which
        // is subject to the IMultiTenant filter; wrap the call so admin
        // (host-context) edits resolve the row.
        Patient patient;
        using (isHost ? _dataFilter.Disable() : null)
        {
            patient = await _patientManager.UpdateAsync(id, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName, input.ConcurrencyStamp);
        }
        return MapToMaskedDto(patient);
    }

    [Authorize]
    public virtual async Task<PatientDto> UpdateMyProfileAsync(PatientUpdateDto input)
    {
        var patientWithNav = await GetCurrentPatientWithNavigationAsync();
        var currentPatient = patientWithNav.Patient;

        var patient = await _patientManager.UpdateAsync(
            currentPatient.Id,
            input.StateId,
            input.AppointmentLanguageId,
            currentPatient.IdentityUserId,
            currentPatient.TenantId,
            input.FirstName,
            input.LastName,
            input.Email,
            input.GenderId,
            input.DateOfBirth,
            input.PhoneNumberTypeId,
            input.MiddleName,
            input.PhoneNumber,
            input.SocialSecurityNumber,
            input.Address,
            input.City,
            input.ZipCode,
            input.CellPhoneNumber,
            input.Street,
            input.InterpreterVendorName,
            input.ApptNumber,
            input.OthersLanguageName,
            input.ConcurrencyStamp
        );

        return MapToMaskedDto(patient);
    }

    private async Task<PatientWithNavigationProperties> GetCurrentPatientWithNavigationAsync()
    {
        var identityUserId = CurrentUser.Id;
        if (!identityUserId.HasValue)
        {
            throw new AbpAuthorizationException("Current user is not authenticated.");
        }

        // Self-service profile lookup keys off identityUserId (which is
        // unique). Disable the IMultiTenant filter when CurrentTenant.Id
        // is null so test harnesses that simulate the principal without
        // entering tenant scope still resolve the row. Production OAuth
        // sets both principal and CurrentTenant, so the filter applies
        // and returns the same single row.
        var isHost = CurrentTenant.Id == null;
        List<PatientWithNavigationProperties> records;
        using (isHost ? _dataFilter.Disable() : null)
        {
            records = await _patientRepository.GetListWithNavigationPropertiesAsync(
                identityUserId: identityUserId.Value,
                maxResultCount: 1,
                skipCount: 0
            );
        }

        var current = records.FirstOrDefault();
        if (current?.Patient == null)
        {
            throw new EntityNotFoundException(typeof(Patient), identityUserId.Value);
        }

        return current;
    }

    // F4-01 (2026-05-25) origin; F1 / Design B (2026-05-29) -- every patient
    // read- AND write-path return now masks SSN to the last 4 for ALL callers
    // (internal staff and the record owner included). The full value crosses
    // the wire only via GetFullSsnAsync (the audited reveal endpoint), whose
    // internal-or-owner authorization lives in the pure SsnRevealAccess helper.
    // See docs/plans/2026-05-29-ssn-redact-on-type.md.
    private static void ApplySsnVisibility(PatientDto? dto)
    {
        SsnVisibility.MaskToLast4(dto);
    }

    private static void ApplySsnVisibility(PatientWithNavigationPropertiesDto? dto)
    {
        SsnVisibility.MaskToLast4(dto);
    }

    private static void ApplySsnVisibilityToList(IEnumerable<PatientWithNavigationPropertiesDto> dtos)
    {
        foreach (var dto in dtos)
        {
            SsnVisibility.MaskToLast4(dto);
        }
    }

    // F1 / Design B (2026-05-29) -- the write-path returns (Create / Update /
    // UpdateMyProfile / UpdatePatientForAppointmentBooking) previously echoed
    // the full SSN to every caller because they mapped straight from the
    // entity (the F4-01 write-back gap). Mask them on the way out, same as the
    // read paths.
    private PatientDto MapToMaskedDto(Patient patient)
    {
        var dto = ObjectMapper.Map<Patient, PatientDto>(patient);
        SsnVisibility.MaskToLast4(dto);
        return dto;
    }
}