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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
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

    public PatientsAppService(IPatientRepository patientRepository, PatientManager patientManager, IdentityUserManager userManager, IdentityRoleManager roleManager, IRepository<HealthcareSupport.CaseEvaluation.States.State, Guid> stateRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentLanguages.AppointmentLanguage, Guid> appointmentLanguageRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<Volo.Saas.Tenants.Tenant, Guid> tenantRepository)
    {
        _patientRepository = patientRepository;
        _patientManager = patientManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _stateRepository = stateRepository;
        _appointmentLanguageRepository = appointmentLanguageRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PagedResultDto<PatientWithNavigationPropertiesDto>> GetListAsync(GetPatientsInput input)
    {
        var totalCount = await _patientRepository.GetCountAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId);
        var items = await _patientRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FirstName, input.LastName, input.MiddleName, input.Email, input.GenderId, input.DateOfBirthMin, input.DateOfBirthMax, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<PatientWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<PatientWithNavigationProperties>, List<PatientWithNavigationPropertiesDto>>(items)
        };
    }

    [Authorize(CaseEvaluationPermissions.Patients.Default)]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(await _patientRepository.GetWithNavigationPropertiesAsync(id));
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetPatientForAppointmentBookingAsync(Guid id)
    {
        return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(await _patientRepository.GetWithNavigationPropertiesAsync(id));
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto?> GetPatientByEmailForAppointmentBookingAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var patients = await _patientRepository.GetListWithNavigationPropertiesAsync(
            email: email.Trim(),
            maxResultCount: 1,
            skipCount: 0);
        var existing = patients.FirstOrDefault();
        return existing?.Patient != null
            ? ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing)
            : null;
    }

    [Authorize]
    public virtual async Task<PatientWithNavigationPropertiesDto> GetOrCreatePatientForAppointmentBookingAsync(CreatePatientForAppointmentBookingInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Email))
        {
            throw new UserFriendlyException(L["Email is required."]);
        }

        var existingPatients = await _patientRepository.GetListWithNavigationPropertiesAsync(
            email: input.Email.Trim(),
            maxResultCount: 1,
            skipCount: 0);
        var existing = existingPatients.FirstOrDefault();
        if (existing?.Patient != null)
        {
            return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(existing);
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

        var patient = await _patientManager.CreateAsync(
            input.StateId,
            input.AppointmentLanguageId,
            identityUser.Id,
            CurrentTenant.Id,
            input.FirstName,
            input.LastName,
            input.Email.Trim(),
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
            input.OthersLanguageName);

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

        return ObjectMapper.Map<PatientWithNavigationProperties, PatientWithNavigationPropertiesDto>(createdWithNav);
    }

    [Authorize]
    public virtual async Task<PatientDto> UpdatePatientForAppointmentBookingAsync(Guid id, PatientUpdateDto input)
    {
        var patientWithNav = await _patientRepository.GetWithNavigationPropertiesAsync(id);
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
        return ObjectMapper.Map<Patient, PatientDto>(await _patientRepository.GetAsync(id));
    }

    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        var query = (await _stateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
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
        var query = (await _appointmentLanguageRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
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
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
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
        var query = (await _tenantRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
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
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var patient = await _patientManager.CreateAsync(input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName);
        return ObjectMapper.Map<Patient, PatientDto>(patient);
    }

    [Authorize(CaseEvaluationPermissions.Patients.Edit)]
    public virtual async Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto input)
    {
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var patient = await _patientManager.UpdateAsync(id, input.StateId, input.AppointmentLanguageId, input.IdentityUserId, input.TenantId, input.FirstName, input.LastName, input.Email, input.GenderId, input.DateOfBirth, input.PhoneNumberTypeId, input.MiddleName, input.PhoneNumber, input.SocialSecurityNumber, input.Address, input.City, input.ZipCode, input.RefferedBy, input.CellPhoneNumber, input.Street, input.InterpreterVendorName, input.ApptNumber, input.OthersLanguageName, input.ConcurrencyStamp);
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

        var records = await _patientRepository.GetListWithNavigationPropertiesAsync(
            identityUserId: identityUserId.Value,
            maxResultCount: 1,
            skipCount: 0
        );

        var current = records.FirstOrDefault();
        if (current?.Patient == null)
        {
            throw new EntityNotFoundException(typeof(Patient), identityUserId.Value);
        }

        return current;
    }
}