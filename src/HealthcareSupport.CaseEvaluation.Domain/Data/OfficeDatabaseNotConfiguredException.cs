using System;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Thrown when a request runs as an office that has no usable office-database setting. The request
/// fails rather than being given any other database (see <see cref="OfficeConnectionStringResolver"/>).
///
/// <para>The message names the office id and the reason only, never the stored value.</para>
/// </summary>
public class OfficeDatabaseNotConfiguredException : AbpException
{
    public Guid OfficeId { get; }

    public OfficeDatabaseNotConfiguredException(Guid officeId, string reason)
        : base($"Office {officeId} has {reason}, so its request was refused rather than given another database.")
    {
        OfficeId = officeId;
    }
}
