using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
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

    public AppointmentsAppService(IAppointmentRepository appointmentRepository, AppointmentManager appointmentManager, IRepository<HealthcareSupport.CaseEvaluation.Patients.Patient, Guid> patientRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<HealthcareSupport.CaseEvaluation.AppointmentTypes.AppointmentType, Guid> appointmentTypeRepository, IRepository<HealthcareSupport.CaseEvaluation.Locations.Location, Guid> locationRepository, IRepository<HealthcareSupport.CaseEvaluation.DoctorAvailabilities.DoctorAvailability, Guid> doctorAvailabilityRepository, IRepository<HealthcareSupport.CaseEvaluation.Doctors.Doctor, Guid> doctorRepository, IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository, IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository, ApplicantAttorneyManager applicantAttorneyManager, AppointmentApplicantAttorneyManager appointmentApplicantAttorneyManager)
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
    }
    [Authorize]
    public virtual async Task<PagedResultDto<AppointmentWithNavigationPropertiesDto>> GetListAsync(GetAppointmentsInput input)
    {
        var totalCount = await _appointmentRepository.GetCountAsync(input.FilterText, input.PanelNumber, input.AppointmentDateMin, input.AppointmentDateMax, input.IdentityUserId, input.AccessorIdentityUserId, input.AppointmentTypeId, input.LocationId);
        var items = await _appointmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.PanelNumber, input.AppointmentDateMin, input.AppointmentDateMax, input.IdentityUserId, input.AccessorIdentityUserId, input.AppointmentTypeId, input.LocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<AppointmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<AppointmentWithNavigationProperties>, List<AppointmentWithNavigationPropertiesDto>>(items)
        };
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

    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetPatientLookupAsync(LookupRequestDto input)
    {
        var query = (await _patientRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));
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
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Email != null && x.Email.Contains(input.Filter!));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }
    [Authorize]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        var queryable = (await _doctorRepository.GetQueryableAsync())
            .SelectMany(x => x.AppointmentTypes)
            .Select(x => x.AppointmentType);

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
        var queryable = (await _doctorRepository.GetQueryableAsync())
            .SelectMany(x => x.Locations)
            .Select(x => x.Location);
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
        await _appointmentRepository.DeleteAsync(id);
    }

    [Authorize]
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

        var requestConfirmationNumber = await GenerateNextRequestConfirmationNumberAsync();

        // W1-1: per T11 lifecycle, every booker submission lands at Pending. The
        // client-supplied AppointmentStatus on AppointmentCreateDto used to be
        // honored as-is (a known gap from the gap-analysis -- track 02). Force
        // Pending so external bookers cannot self-approve. The state machine
        // still allows the office to transition forward via the Approve / Reject
        // / SendBack endpoints exposed on AppointmentManager.
        var appointment = await _appointmentManager.CreateAsync(input.PatientId, input.IdentityUserId, input.AppointmentTypeId, input.LocationId, input.DoctorAvailabilityId, input.AppointmentDate, requestConfirmationNumber, AppointmentStatusType.Pending, input.PanelNumber, input.DueDate);

        // W1-1: per T11 slot-sync, submission moves the slot Available -> Reserved
        // (NOT Booked). The slot cascade flips Reserved -> Booked when the office
        // approves, or Reserved -> Available when the office rejects.
        doctorAvailability.BookingStatusId = BookingStatus.Reserved;
        await _doctorAvailabilityRepository.UpdateAsync(doctorAvailability);

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

        var appointment = await _appointmentManager.UpdateAsync(id, input.PatientId, input.IdentityUserId, input.AppointmentTypeId, input.LocationId, input.DoctorAvailabilityId, input.AppointmentDate, input.PanelNumber, input.DueDate, input.ConcurrencyStamp);
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

    [Authorize(CaseEvaluationPermissions.Appointments.Edit)]
    public virtual async Task<AppointmentDto> SendBackAsync(Guid id, SendBackAppointmentInput input)
    {
        var fields = input?.FlaggedFields ?? new List<string>();
        var appointment = await _appointmentManager.SendBackAsync(id, fields, input?.Note, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize]
    public virtual async Task<AppointmentDto> RespondAsync(Guid id)
    {
        var appointment = await _appointmentManager.RespondAsync(id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }
}