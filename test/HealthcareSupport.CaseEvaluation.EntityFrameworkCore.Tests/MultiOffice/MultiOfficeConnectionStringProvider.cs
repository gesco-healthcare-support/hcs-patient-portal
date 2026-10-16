using HealthcareSupport.CaseEvaluation.Data;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// The multi-office harness's stand-in for <see cref="TenantConnectionStringProvider"/>: an office slug
/// becomes that office's named in-memory SQLite database rather than a SQL Server catalog. Everything
/// above it -- reading the database name from the office row, refusing an office that has none -- is the
/// production <see cref="OfficeConnectionStringResolver"/>.
/// </summary>
public sealed class MultiOfficeConnectionStringProvider : ITenantConnectionStringProvider
{
    public string BuildConnectionString(string slug) => MultiOfficeTestDatabase.OfficeConnectionStringFor(slug);
}
