using HealthcareSupport.CaseEvaluation.States;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeWithNavigationPropertiesDto
{
    public WcabOfficeDto WcabOffice { get; set; } = null!;
    public StateDto? State { get; set; }
}