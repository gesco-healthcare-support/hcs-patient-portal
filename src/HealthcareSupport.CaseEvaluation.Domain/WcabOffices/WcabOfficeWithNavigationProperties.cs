using HealthcareSupport.CaseEvaluation.States;
using System;
using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.WcabOffices;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeWithNavigationProperties
{
    public WcabOffice WcabOffice { get; set; } = null!;
    public State? State { get; set; }
}