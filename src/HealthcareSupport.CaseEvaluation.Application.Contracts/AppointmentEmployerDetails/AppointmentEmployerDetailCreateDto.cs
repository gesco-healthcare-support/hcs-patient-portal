using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailCreateDto
{
    [Required]
    [StringLength(AppointmentEmployerDetailConsts.EmployerNameMaxLength)]
    public string EmployerName { get; set; } = null!;
    [Required]
    [StringLength(AppointmentEmployerDetailConsts.OccupationMaxLength)]
    public string Occupation { get; set; } = null!;

    [StringLength(AppointmentEmployerDetailConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid? StateId { get; set; }
}