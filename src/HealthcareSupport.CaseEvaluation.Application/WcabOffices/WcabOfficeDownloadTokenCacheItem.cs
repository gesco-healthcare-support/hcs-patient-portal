using System;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

[Serializable]
public class WcabOfficeDownloadTokenCacheItem
{
    public string Token { get; set; } = null!;
}