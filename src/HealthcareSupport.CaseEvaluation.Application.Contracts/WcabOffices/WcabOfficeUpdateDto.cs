using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeUpdateDto : IHasConcurrencyStamp
{
    [Required]
    [StringLength(WcabOfficeConsts.NameMaxLength)]
    public string Name { get; set; } = null!;
    [Required]
    [StringLength(WcabOfficeConsts.AbbreviationMaxLength)]
    public string Abbreviation { get; set; } = null!;
    [StringLength(WcabOfficeConsts.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(WcabOfficeConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(WcabOfficeConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    public bool IsActive { get; set; }

    public Guid? StateId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}