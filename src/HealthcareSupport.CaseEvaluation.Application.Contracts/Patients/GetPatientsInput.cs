using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class GetPatientsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? MiddleName { get; set; }

    public string? Email { get; set; }

    public Gender? GenderId { get; set; }

    public DateTime? DateOfBirthMin { get; set; }

    public DateTime? DateOfBirthMax { get; set; }

    public string? PhoneNumber { get; set; }

    public string? SocialSecurityNumber { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public string? RefferedBy { get; set; }

    public string? CellPhoneNumber { get; set; }

    public string? Street { get; set; }

    public string? InterpreterVendorName { get; set; }

    public string? ApptNumber { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentLanguageId { get; set; }

    public Guid? IdentityUserId { get; set; }

    public GetPatientsInput()
    {
    }
}