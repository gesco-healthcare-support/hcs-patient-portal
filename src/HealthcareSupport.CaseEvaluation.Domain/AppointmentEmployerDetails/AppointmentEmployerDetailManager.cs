using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailManager : DomainService
{
    protected IAppointmentEmployerDetailRepository _appointmentEmployerDetailRepository;

    public AppointmentEmployerDetailManager(IAppointmentEmployerDetailRepository appointmentEmployerDetailRepository)
    {
        _appointmentEmployerDetailRepository = appointmentEmployerDetailRepository;
    }

    public virtual async Task<AppointmentEmployerDetail> CreateAsync(
        Guid appointmentId,
        Guid? stateId,
        string employerName,
        string occupation,
        string? phoneNumber = null,
        string? street = null,
        string? city = null,
        string? zipCode = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(employerName, nameof(employerName));
        Check.Length(employerName, nameof(employerName), AppointmentEmployerDetailConsts.EmployerNameMaxLength);
        Check.NotNullOrWhiteSpace(occupation, nameof(occupation));
        Check.Length(occupation, nameof(occupation), AppointmentEmployerDetailConsts.OccupationMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentEmployerDetailConsts.PhoneNumberMaxLength);
        Check.Length(street, nameof(street), AppointmentEmployerDetailConsts.StreetMaxLength);
        Check.Length(city, nameof(city), AppointmentEmployerDetailConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), AppointmentEmployerDetailConsts.ZipCodeMaxLength);
        var appointmentEmployerDetail = new AppointmentEmployerDetail(GuidGenerator.Create(), appointmentId, stateId, employerName, occupation);
        appointmentEmployerDetail.PhoneNumber = phoneNumber;
        appointmentEmployerDetail.Street = street;
        appointmentEmployerDetail.City = city;
        appointmentEmployerDetail.ZipCode = zipCode;
        return await _appointmentEmployerDetailRepository.InsertAsync(appointmentEmployerDetail);
    }

    public virtual async Task<AppointmentEmployerDetail> UpdateAsync(
        Guid id,
        Guid appointmentId,
        Guid? stateId,
        string employerName,
        string occupation,
        string? phoneNumber = null,
        string? street = null,
        string? city = null,
        string? zipCode = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(employerName, nameof(employerName));
        Check.Length(employerName, nameof(employerName), AppointmentEmployerDetailConsts.EmployerNameMaxLength);
        Check.NotNullOrWhiteSpace(occupation, nameof(occupation));
        Check.Length(occupation, nameof(occupation), AppointmentEmployerDetailConsts.OccupationMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), AppointmentEmployerDetailConsts.PhoneNumberMaxLength);
        Check.Length(street, nameof(street), AppointmentEmployerDetailConsts.StreetMaxLength);
        Check.Length(city, nameof(city), AppointmentEmployerDetailConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), AppointmentEmployerDetailConsts.ZipCodeMaxLength);
        var appointmentEmployerDetail = await _appointmentEmployerDetailRepository.GetAsync(id);
        appointmentEmployerDetail.AppointmentId = appointmentId;
        appointmentEmployerDetail.StateId = stateId;
        appointmentEmployerDetail.EmployerName = employerName;
        appointmentEmployerDetail.Occupation = occupation;
        appointmentEmployerDetail.PhoneNumber = phoneNumber;
        appointmentEmployerDetail.Street = street;
        appointmentEmployerDetail.City = city;
        appointmentEmployerDetail.ZipCode = zipCode;
        appointmentEmployerDetail.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _appointmentEmployerDetailRepository.UpdateAsync(appointmentEmployerDetail);
    }
}