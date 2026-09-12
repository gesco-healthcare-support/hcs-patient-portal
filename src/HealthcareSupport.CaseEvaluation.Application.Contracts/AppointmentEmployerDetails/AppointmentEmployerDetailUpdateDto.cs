using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;
using HealthcareSupport.CaseEvaluation.Validation;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class AppointmentEmployerDetailUpdateDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(AppointmentEmployerDetailConsts.EmployerNameMaxLength)]
    public string EmployerName { get; set; } = null!;

    [Required]
    [StringLength(AppointmentEmployerDetailConsts.OccupationMaxLength)]
    public string Occupation { get; set; } = null!;

    [StringLength(AppointmentEmployerDetailConsts.PhoneNumberMaxLength)]
    [PhoneNumber]
    public string? PhoneNumber { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(AppointmentEmployerDetailConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public Guid AppointmentId { get; set; }

    public Guid? StateId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}