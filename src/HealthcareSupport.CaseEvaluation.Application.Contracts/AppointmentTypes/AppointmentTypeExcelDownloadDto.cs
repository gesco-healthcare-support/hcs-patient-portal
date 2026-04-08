using Volo.Abp.Application.Dtos;
using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public class AppointmentTypeExcelDownloadDto
{
    public string DownloadToken { get; set; } = null!;
    public string? FilterText { get; set; }

    public string? Name { get; set; }

    public AppointmentTypeExcelDownloadDto()
    {
    }
}