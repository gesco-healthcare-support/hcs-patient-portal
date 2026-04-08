using System;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class WcabOfficeExcelDto
{
    public string Name { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;
    public string? Address { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public bool IsActive { get; set; }
}