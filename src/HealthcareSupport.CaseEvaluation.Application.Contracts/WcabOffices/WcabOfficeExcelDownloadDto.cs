using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeExcelDownloadDto
{
    public string DownloadToken { get; set; } = null!;
    public string? FilterText { get; set; }

    public string? Name { get; set; }

    public string? Abbreviation { get; set; }

    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public bool? IsActive { get; set; }

    public Guid? StateId { get; set; }

    public WcabOfficeExcelDownloadDto()
    {
    }
}