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

    /// <summary>
    /// Single entry point for "turn incoming Patient-shaped input into a Patient row".
    /// Runs the 3-of-6 fuzzy match against the calling tenant's existing rows; returns
    /// the existing match if found, otherwise delegates to <see cref="CreateAsync"/>.
    ///
    /// Patient is NOT IMultiTenant -- the repository applies a manual <c>TenantId</c>
    /// filter to avoid cross-tenant PHI leak (FEAT-09 context).
    ///
    /// Match keys (any 3 of 6 must equal): FirstName (lowercased), LastName (lowercased),
    /// DateOfBirth (date-only), SocialSecurityNumber (digits-only), PhoneNumber
    /// (digits-only), ZipCode (lowercased trim). OLD reference: <c>IsPatientRegistered</c>
    /// in <c>AppointmentDomain.cs:732-780</c>; ZipCode substitutes for OLD's ClaimNumber
    /// because <c>AppointmentInjuryDetail</c> is a Wave 1 capability.
    ///
    /// Concurrency note: Wave 0 ships without an <c>IDistributedLockProvider</c> guard.
    /// A first-write-wins race between two concurrent matching submissions is rare and
    /// acceptable in dev; the post-MVP "Wave 0 hardening" tail adds the lock.
    /// </summary>
    public virtual async Task<(Patient Patient, bool WasExisting)> FindOrCreateAsync(
        Guid? tenantId,
        Guid identityUserId,
        string firstName,
        string lastName,
        string email,
        Gender genderId,
        DateTime dateOfBirth,
        PhoneNumberType phoneNumberTypeId,
        Guid? stateId = null,
        Guid? appointmentLanguageId = null,
        string? phoneNumber = null,
        string? socialSecurityNumber = null,
        string? zipCode = null,
        string? middleName = null,
        string? address = null,
        string? city = null,
        string? refferedBy = null,
        string? cellPhoneNumber = null,
        string? street = null,
        string? interpreterVendorName = null,
        string? apptNumber = null,
        string? othersLanguageName = null)
    {
        var fn = PatientMatching.Normalise(firstName) ?? string.Empty;
        var ln = PatientMatching.Normalise(lastName) ?? string.Empty;
        var ssn = PatientMatching.NormaliseSsn(socialSecurityNumber);
        var phone = PatientMatching.NormalisePhone(phoneNumber);
        var zip = PatientMatching.Normalise(zipCode);

        var match = await _patientRepository.FindBestMatchAsync(
            tenantId,
            fn,
            ln,
            dateOfBirth.Date,
            ssn,
            phone,
            zip);

        if (match != null)
        {
            var existing = await _patientRepository.GetAsync(match.Id);
            return (existing, true);
        }

        var created = await CreateAsync(
            stateId,
            appointmentLanguageId,
            identityUserId,
            tenantId,
            firstName,
            lastName,
            email,
            genderId,
            dateOfBirth,
            phoneNumberTypeId,
            middleName,
            phoneNumber,
            socialSecurityNumber,
            address,
            city,
            zipCode,
            refferedBy,
            cellPhoneNumber,
            street,
            interpreterVendorName,
            apptNumber,
            othersLanguageName);

        return (created, false);
    }
}