using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class PatientManager : DomainService
{
    protected IPatientRepository _patientRepository;

    public PatientManager(IPatientRepository patientRepository)
    {
        _patientRepository = patientRepository;
    }

    public virtual async Task<Patient> CreateAsync(Guid? stateId, Guid? appointmentLanguageId, Guid identityUserId, Guid? tenantId, string firstName, string lastName, string email, Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId, string? middleName = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? othersLanguageName = null)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), PatientConsts.FirstNameMaxLength);
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), PatientConsts.LastNameMaxLength);
        Check.NotNullOrWhiteSpace(email, nameof(email));
        Check.Length(email, nameof(email), PatientConsts.EmailMaxLength);
        Check.NotNull(genderId, nameof(genderId));
        Check.NotNull(dateOfBirth, nameof(dateOfBirth));
        Check.NotNull(phoneNumberTypeId, nameof(phoneNumberTypeId));
        Check.Length(middleName, nameof(middleName), PatientConsts.MiddleNameMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), PatientConsts.PhoneNumberMaxLength);
        Check.Length(socialSecurityNumber, nameof(socialSecurityNumber), PatientConsts.SocialSecurityNumberMaxLength);
        Check.Length(address, nameof(address), PatientConsts.AddressMaxLength);
        Check.Length(city, nameof(city), PatientConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), PatientConsts.ZipCodeMaxLength);
        Check.Length(refferedBy, nameof(refferedBy), PatientConsts.RefferedByMaxLength);
        Check.Length(cellPhoneNumber, nameof(cellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength);
        Check.Length(street, nameof(street), PatientConsts.StreetMaxLength);
        Check.Length(interpreterVendorName, nameof(interpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength);
        Check.Length(apptNumber, nameof(apptNumber), PatientConsts.ApptNumberMaxLength);
        Check.Length(othersLanguageName, nameof(othersLanguageName), PatientConsts.OthersLanguageNameMaxLength);
        var patient = new Patient(GuidGenerator.Create(), stateId, appointmentLanguageId, identityUserId, tenantId, firstName, lastName, email, genderId, dateOfBirth, phoneNumberTypeId, middleName, phoneNumber, socialSecurityNumber, address, city, zipCode, refferedBy, cellPhoneNumber, street, interpreterVendorName, apptNumber, othersLanguageName);
        return await _patientRepository.InsertAsync(patient);
    }

    public virtual async Task<Patient> UpdateAsync(Guid id, Guid? stateId, Guid? appointmentLanguageId, Guid identityUserId, Guid? tenantId, string firstName, string lastName, string email, Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId, string? middleName = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? othersLanguageName = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), PatientConsts.FirstNameMaxLength);
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), PatientConsts.LastNameMaxLength);
        Check.NotNullOrWhiteSpace(email, nameof(email));
        Check.Length(email, nameof(email), PatientConsts.EmailMaxLength);
        Check.NotNull(genderId, nameof(genderId));
        Check.NotNull(dateOfBirth, nameof(dateOfBirth));
        Check.NotNull(phoneNumberTypeId, nameof(phoneNumberTypeId));
        Check.Length(middleName, nameof(middleName), PatientConsts.MiddleNameMaxLength);
        Check.Length(phoneNumber, nameof(phoneNumber), PatientConsts.PhoneNumberMaxLength);
        Check.Length(socialSecurityNumber, nameof(socialSecurityNumber), PatientConsts.SocialSecurityNumberMaxLength);
        Check.Length(address, nameof(address), PatientConsts.AddressMaxLength);
        Check.Length(city, nameof(city), PatientConsts.CityMaxLength);
        Check.Length(zipCode, nameof(zipCode), PatientConsts.ZipCodeMaxLength);
        Check.Length(refferedBy, nameof(refferedBy), PatientConsts.RefferedByMaxLength);
        Check.Length(cellPhoneNumber, nameof(cellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength);
        Check.Length(street, nameof(street), PatientConsts.StreetMaxLength);
        Check.Length(interpreterVendorName, nameof(interpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength);
        Check.Length(apptNumber, nameof(apptNumber), PatientConsts.ApptNumberMaxLength);
        Check.Length(othersLanguageName, nameof(othersLanguageName), PatientConsts.OthersLanguageNameMaxLength);
        var patient = await _patientRepository.GetAsync(id);
        patient.StateId = stateId;
        patient.AppointmentLanguageId = appointmentLanguageId;
        patient.IdentityUserId = identityUserId;
        patient.TenantId = tenantId;
        patient.FirstName = firstName;
        patient.LastName = lastName;
        patient.Email = email;
        patient.GenderId = genderId;
        patient.DateOfBirth = dateOfBirth;
        patient.PhoneNumberTypeId = phoneNumberTypeId;
        patient.MiddleName = middleName;
        patient.PhoneNumber = phoneNumber;
        patient.SocialSecurityNumber = socialSecurityNumber;
        patient.Address = address;
        patient.City = city;
        patient.ZipCode = zipCode;
        patient.RefferedBy = refferedBy;
        patient.CellPhoneNumber = cellPhoneNumber;
        patient.Street = street;
        patient.InterpreterVendorName = interpreterVendorName;
        patient.ApptNumber = apptNumber;
        patient.OthersLanguageName = othersLanguageName;
        patient.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _patientRepository.UpdateAsync(patient);
    }
}