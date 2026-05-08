using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.States;
using HealthcareSupport.CaseEvaluation.AppointmentLanguages;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Patients;

// FEAT-09 (ADR-006 T4, 2026-05-05): Patient implements IMultiTenant so
// ABP's automatic tenant filter scopes queries by CurrentTenant.Id. Was
// previously host-only with a manual TenantId column but no auto-filter,
// which let any caller with the Patients permission read every tenant's
// patients. Skipped test
// PatientsAppServiceTests.GetListAsync_WhenCallerIsTenantScoped_ReturnsOnlyTheirTenantPatients
// flips green once the interface is added; the framework filter does the
// rest -- no AppService change needed.
public class Patient : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    [NotNull]
    public virtual string FirstName { get; set; } = null!;

    [NotNull]
    public virtual string LastName { get; set; } = null!;

    [CanBeNull]
    public virtual string? MiddleName { get; set; }

    [NotNull]
    public virtual string Email { get; set; } = null!;

    public virtual Gender GenderId { get; set; }

    public virtual DateTime DateOfBirth { get; set; }

    [CanBeNull]
    public virtual string? PhoneNumber { get; set; }

    [CanBeNull]
    public virtual string? SocialSecurityNumber { get; set; }

    [CanBeNull]
    public virtual string? Address { get; set; }

    [CanBeNull]
    public virtual string? City { get; set; }

    [CanBeNull]
    public virtual string? ZipCode { get; set; }

    [CanBeNull]
    public virtual string? RefferedBy { get; set; }

    [CanBeNull]
    public virtual string? CellPhoneNumber { get; set; }

    public virtual PhoneNumberType PhoneNumberTypeId { get; set; }

    [CanBeNull]
    public virtual string? Street { get; set; }

    [CanBeNull]
    public virtual string? InterpreterVendorName { get; set; }

    [CanBeNull]
    public virtual string? ApptNumber { get; set; }

    [CanBeNull]
    public virtual string? OthersLanguageName { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentLanguageId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid? TenantId { get; set; }

    protected Patient()
    {
    }

    public Patient(Guid id, Guid? stateId, Guid? appointmentLanguageId, Guid identityUserId, Guid? tenantId, string firstName, string lastName, string email, Gender genderId, DateTime dateOfBirth, PhoneNumberType phoneNumberTypeId, string? middleName = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? othersLanguageName = null)
    {
        Id = id;
        Check.NotNull(firstName, nameof(firstName));
        Check.Length(firstName, nameof(firstName), PatientConsts.FirstNameMaxLength, 0);
        Check.NotNull(lastName, nameof(lastName));
        Check.Length(lastName, nameof(lastName), PatientConsts.LastNameMaxLength, 0);
        Check.NotNull(email, nameof(email));
        Check.Length(email, nameof(email), PatientConsts.EmailMaxLength, 0);
        Check.Length(middleName, nameof(middleName), PatientConsts.MiddleNameMaxLength, 0);
        Check.Length(phoneNumber, nameof(phoneNumber), PatientConsts.PhoneNumberMaxLength, 0);
        Check.Length(socialSecurityNumber, nameof(socialSecurityNumber), PatientConsts.SocialSecurityNumberMaxLength, 0);
        Check.Length(address, nameof(address), PatientConsts.AddressMaxLength, 0);
        Check.Length(city, nameof(city), PatientConsts.CityMaxLength, 0);
        Check.Length(zipCode, nameof(zipCode), PatientConsts.ZipCodeMaxLength, 0);
        Check.Length(refferedBy, nameof(refferedBy), PatientConsts.RefferedByMaxLength, 0);
        Check.Length(cellPhoneNumber, nameof(cellPhoneNumber), PatientConsts.CellPhoneNumberMaxLength, 0);
        Check.Length(street, nameof(street), PatientConsts.StreetMaxLength, 0);
        Check.Length(interpreterVendorName, nameof(interpreterVendorName), PatientConsts.InterpreterVendorNameMaxLength, 0);
        Check.Length(apptNumber, nameof(apptNumber), PatientConsts.ApptNumberMaxLength, 0);
        Check.Length(othersLanguageName, nameof(othersLanguageName), PatientConsts.OthersLanguageNameMaxLength, 0);
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        GenderId = genderId;
        DateOfBirth = dateOfBirth;
        PhoneNumberTypeId = phoneNumberTypeId;
        MiddleName = middleName;
        PhoneNumber = phoneNumber;
        SocialSecurityNumber = socialSecurityNumber;
        Address = address;
        City = city;
        ZipCode = zipCode;
        RefferedBy = refferedBy;
        CellPhoneNumber = cellPhoneNumber;
        Street = street;
        InterpreterVendorName = interpreterVendorName;
        ApptNumber = apptNumber;
        OthersLanguageName = othersLanguageName;
        StateId = stateId;
        AppointmentLanguageId = appointmentLanguageId;
        IdentityUserId = identityUserId;
        TenantId = tenantId;
    }
}