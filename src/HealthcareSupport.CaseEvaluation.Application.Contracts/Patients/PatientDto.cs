using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class PatientDto : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? MiddleName { get; set; }

    public string Email { get; set; } = null!;
    public Gender GenderId { get; set; }

    public DateTime DateOfBirth { get; set; }

    public string? PhoneNumber { get; set; }

    public string? SocialSecurityNumber { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public string? RefferedBy { get; set; }

    public string? CellPhoneNumber { get; set; }

    public PhoneNumberType PhoneNumberTypeId { get; set; }

    public string? Street { get; set; }

    public string? InterpreterVendorName { get; set; }

    public string? ApptNumber { get; set; }

    public string? OthersLanguageName { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentLanguageId { get; set; }

    public Guid IdentityUserId { get; set; }

    public Guid? TenantId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}