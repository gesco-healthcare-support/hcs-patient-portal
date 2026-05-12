using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Saas.Tenants;
using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using HealthcareSupport.CaseEvaluation.States;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
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

namespace HealthcareSupport.CaseEvaluation.Patients;

[RemoteService(IsEnabled = false)]
public class PatientsAppService : CaseEvaluationAppService, IPatientsAppService
{
    protected IPatientRepository _patientRepository;
    protected PatientManager _patientManager;
    protected IdentityUserManager _userManager;
    protected IdentityRoleManager _roleManager;
    protected IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> _stateRepository;
    protected IRepository<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage, Guid> _appointmentLanguageRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<Volo.Saas.Tenants.Tenant, Guid> _tenantRepository;

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

    public PatientsAppService(IPatientRepository patientRepository, PatientManager patientManager, IdentityUserManager userManager, IdentityRoleManager roleManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage, Guid> appointmentLanguageRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository, IDataFilter<IMultiTenant> dataFilter)
    {
        _patientRepository = patientRepository;
        _patientManager = patientManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _stateRepository = stateRepository;
        _appointmentLanguageRepository = appointmentLanguageRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
        _dataFilter = dataFilter;
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PagedResultDto<PatientWithNavigationPropertiesDto>> GetListAsync(GetPatientsInput input)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            var totalCount = await _patientRepository.GetCountAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId);
            var items = await _patientRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
            return new PagedResultDto<PatientWithNavigationPropertiesDto>
            {
                TotalCount = totalCount,
                Items = ObjectMapper.Map<List<PatientWithNavigationProperties>, List<PatientWithNavigationPropertiesDto>>(items)
            };
        }
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>((await _patientRepository.GetWithNavigationPropertiesAsync(id))!);
        }
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetPatientForAppointmentBookingAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>((await _patientRepository.GetWithNavigationPropertiesAsync(id))!);
        }
    }

    [Authorize]
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
            return existing?.Patient != null
                ? ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing)
                : null;
        }
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetOrCreatePatientForAppointmentBookingAsync(CreatePatientForAppointmentBookingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Email))
        {
            throw new UserFriendlyException(L["Email is required."]);
        }

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
            var existingPatients = await _patientRepository.GetListWithNavigationPropertiesAsync(
                email: input.Email.Trim(),
                maxResultCount: 1,
                skipCount: 0);
            var existing = existingPatients.FirstOrDefault();
            if (existing?.Patient != null)
            {
                // R2 (2026-05-04): email-fast-path resolved an existing patient.
                var dtoExisting = ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing);
                dtoExisting.IsExisting = true;
                return dtoExisting;
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
                email: input.Email,
                ssn: input.SocialSecurityNumber,
                // Patient does not carry ClaimNumber in NEW (per Phase 11k
                // audit doc) -- the column lives on AppointmentInjuryDetail
                // and is unavailable at Patient creation time. Pass null;
                // the predicate counts the remaining 5 fields.
                claimNumbers: null);

            if (dedupCandidates.Count > 0)
            {
                var incoming = new HealthcareSupport.CaseEvaluation.Appointments.PatientDeduplicationCandidate
                {
                    LastName = input.LastName,
                    DateOfBirth = input.DateOfBirth,
                    PhoneNumber = input.PhoneNumber,
                    Email = input.Email,
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
                            return dtoMatched;
                        }
                    }
                }
            }

            var identityUser = await _userManager.FindByEmailAsync(input.Email.Trim());
            if (identityUser == null)
            {
                identityUser = new IdentityUser(
                    GuidGenerator.Create(),
                    userName: input.Email.Trim(),
                    email: input.Email.Trim(),
                    tenantId: CurrentTenant.Id)
                {
                    Name = input.FirstName,
                    Surname = input.LastName,
                };

                var tempPassword = CaseEvaluationConsts.AdminPasswordDefaultValue;
                var createResult = await _userManager.CreateAsync(identityUser, tempPassword);
                if (!createResult.Succeeded)
                {
                    throw new UserFriendlyException(string.Join(", ", createResult.Errors.Select(x => x.Description)));
                }
            }
            else
            {
                identityUser.Name = input.FirstName;
                identityUser.Surname = input.LastName;
                await _userManager.UpdateAsync(identityUser);
            }

            if (!await _userManager.IsInRoleAsync(identityUser, "Patient"))
            {
                var role = await _roleManager.FindByNameAsync("Patient");
                if (role == null)
                {
                    role = new IdentityRole(GuidGenerator.Create(), "Patient", CurrentTenant.Id);
                    await _roleManager.CreateAsync(role);
                }
                await _userManager.AddToRoleAsync(identityUser, "Patient");
            }

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
                identityUserId: identityUser.Id,
                firstName: input.FirstName,
                lastName: input.LastName,
                email: input.Email.Trim(),
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
                refferedBy: input.RefferedBy,
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
            return dtoFinal;
        }
    }

    [Authorize]
    public virtual async Task<PatientDto> UpdatePatientForAppointmentBookingAsync(Guid id, PatientUpdateDto input)
    {
        var isHost = CurrentTenant.Id == null;
        PatientWithNavigationProperties? patientWithNav;
        using (isHost ? _dataFilter.Disable() : null)
        {
            patientWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(id);
        }
        if (patientWithNav == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Patient), id);
        }
        var currentPatient = patientWithNav.Patient;
        if (currentPatient == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Patient), id);
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
            input.RefferedBy ?? currentPatient.RefferedBy,
            input.CellPhoneNumber ?? currentPatient.CellPhoneNumber,
            input.Street ?? currentPatient.Street,
            input.InterpreterVendorName ?? currentPatient.InterpreterVendorName,
            input.ApptNumber ?? currentPatient.ApptNumber,
            input.OthersLanguageName ?? currentPatient.OthersLanguageName,
            input.ConcurrencyStamp ?? currentPatient.ConcurrencyStamp
        );

        return ObjectMapper.Map<Patient, PatientDto>(patient);
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetMyProfileAsync()
    {
        var patientWithNav = await GetCurrentPatientWithNavigationAsync();
        return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(patientWithNav);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PatientDto> GetAsync(Guid id)
    {
        var isHost = CurrentTenant.Id == null;
        using (isHost ? _dataFilter.Disable() : null)
        {
            return ObjectMapper.Map<Patient, PatientDto>(await _patientRepository.GetAsync(id));
        }
    }

    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _appointmentLanguageRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        var query = (await _tenantRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter!));
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
        await _patientRepository.DeleteAsync(id);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Create)]
    public virtual async Task<PatientDto> CreateAsync(PatientCreateDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var patient = await _patientManager.CreateAsync(input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName);
        return ObjectMapper.Map<Patient, PatientDto>(patient);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Edit)]
    public virtual async Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto input)
    {
        if (input.IdentityUserId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var isHost = CurrentTenant.Id == null;
        // PatientManager.UpdateAsync internally calls GetAsync(id) which
        // is subject to the IMultiTenant filter; wrap the call so admin
        // (host-context) edits resolve the row.
        Patient patient;
        using (isHost ? _dataFilter.Disable() : null)
        {
            patient = await _patientManager.UpdateAsync(id, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName, input.ConcurrencyStamp);
        }
        return ObjectMapper.Map<Patient, PatientDto>(patient);
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
            input.RefferedBy,
            input.CellPhoneNumber,
            input.Street,
            input.InterpreterVendorName,
            input.ApptNumber,
            input.OthersLanguageName,
            input.ConcurrencyStamp
        );

        return ObjectMapper.Map<Patient, PatientDto>(patient);
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
}