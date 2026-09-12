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

    public virtual async Task<Patient> CreateAsync(Guid? stateId, Guid? appointmentLanguageId, Guid? identityUserId, Guid? tenantId, string firstName, string lastName, string email, Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId, string? middleName = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? othersLanguageName = null)
    {
        // identityUserId is now nullable (IP6 2026-06-05): a Patient may exist
        // as a record with no login. Booking inserts null; the claim flow links
        // an identity later. Admin/profile callers still pass a real id.
        // firstName / lastName accept empty string at create-time. The minimal
        // register form does not collect names; the booker fills them later
        // through the booking form's patient section. Keep length validation
        // so admin-CRUD callers still get a 50-char ceiling.
        // Adrian (2026-04-30, register-form simplification).
        firstName ??= string.Empty;
        lastName ??= string.Empty;
        Check.Length(firstName, nameof(firstName), PatientConsts.FirstNameMaxLength);
        Check.Length(lastName, nameof(lastName), PatientConsts.LastNameMaxLength);
        // task_d5407b22 (2026-07-21): patient email is optional (injured workers often
        // lack one); accept empty at create time like firstName / lastName above.
        email ??= string.Empty;
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
        Check.Length(cellPhoneNumber, nameof(cellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength);
        Check.Length(street, nameof(street), PatientConsts.StreetMaxLength);
        Check.Length(interpreterVendorName, nameof(interpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength);
        Check.Length(apptNumber, nameof(apptNumber), PatientConsts.ApptNumberMaxLength);
        Check.Length(othersLanguageName, nameof(othersLanguageName), PatientConsts.OthersLanguageNameMaxLength);
        EnsureOwningTenant(tenantId);
        var patient = new Patient(GuidGenerator.Create(), stateId, appointmentLanguageId, identityUserId, tenantId, firstName, lastName, email, genderId, dateOfBirth, phoneNumberTypeId, middleName, phoneNumber, socialSecurityNumber, address, city, zipCode, cellPhoneNumber, street, interpreterVendorName, apptNumber, othersLanguageName);
        return await _patientRepository.InsertAsync(patient);
    }

    /// <summary>
    /// Refuses to create a <see cref="Patient"/> that belongs to no practice.
    ///
    /// <para><b>Why this needs a guard at all.</b> <see cref="Patient"/> is
    /// <see cref="Volo.Abp.MultiTenancy.IMultiTenant"/>, but its TenantId arrives as a CALLER
    /// ARGUMENT rather than from ABP, so ABP's usual guarantee does not apply -- whatever the
    /// caller passes is what gets written, including null.</para>
    ///
    /// <para><b>What a null costs.</b> Nothing throws. The row is inserted and the appointment
    /// points at it, but the multi-tenancy filter then hides it from every tenant-scoped read: no
    /// patient name in the appointment list, blank demographics on the detail view, no way for
    /// staff to edit it, and -- worst -- the duplicate search cannot see it either, so the next
    /// booking for the same person creates a SECOND record. Two patients reached production this
    /// way on 2026-08-19, and the rows had to be repaired in the database because the UI cannot
    /// reach them.</para>
    ///
    /// <para><b>Why it refuses rather than inferring.</b> An earlier version fell back to
    /// <c>CurrentTenant.Id</c> when the caller passed nothing. That was convenience nobody asked
    /// for: every caller already passes a value explicitly -- the two seed contributors pass a
    /// real practice, <c>ExternalSignupAppService</c> passes <c>CurrentTenant.Id</c>, and
    /// <c>PatientsAppService.CreateAsync</c> passes the client DTO's value. Inferring a practice
    /// from request context would only ever mask the caller's bug, and could attach a patient to
    /// whichever practice happened to be in scope. Refusing is safe because the product has no
    /// host-level patients: the host database contains none, so a patient with no practice is
    /// always a defect, and a visible failure beats an invisible row.</para>
    /// </summary>
    protected virtual void EnsureOwningTenant(Guid? tenantId)
    {
        if (tenantId == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.PatientTenantRequired);
        }
    }

    public virtual async Task<Patient> UpdateAsync(Guid id, Guid? stateId, Guid? appointmentLanguageId, Guid? identityUserId, Guid? tenantId, string firstName, string lastName, string email, Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId, string? middleName = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? othersLanguageName = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), PatientConsts.FirstNameMaxLength);
        Check.NotNullOrWhiteSpace(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), PatientConsts.LastNameMaxLength);
        // task_d5407b22 (2026-07-21): patient email is optional; accept empty on update too.
        email ??= string.Empty;
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
        // F1 / Design B (2026-05-29): the SSN field is never pre-filled into any
        // edit/booking form, so an update that carries no SSN means "leave the
        // stored value unchanged" -- NOT "clear it". Only overwrite when a value
        // is actually provided. This guards all three update callers
        // (admin UpdateAsync, UpdateMyProfileAsync, UpdatePatientForAppointment
        // BookingAsync). A typed SSN still overwrites. SSN is the only field
        // with this rule because it is the only never-pre-filled field.
        if (!string.IsNullOrEmpty(socialSecurityNumber))
        {
            patient.SocialSecurityNumber = socialSecurityNumber;
        }
        patient.Address = address;
        patient.City = city;
        patient.ZipCode = zipCode;
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
        Guid? identityUserId,
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
            cellPhoneNumber,
            street,
            interpreterVendorName,
            apptNumber,
            othersLanguageName);

        return (created, false);
    }
}