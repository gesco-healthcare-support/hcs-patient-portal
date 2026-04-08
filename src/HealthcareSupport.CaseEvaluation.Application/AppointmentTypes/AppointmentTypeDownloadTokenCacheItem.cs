using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

[Serializable]
public class AppointmentTypeDownloadTokenCacheItem
{
    public string Token { get; set; } = null!;
}