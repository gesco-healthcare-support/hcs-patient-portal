using System;
using HealthcareSupport.CaseEvaluation.Data;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// The real-authorization harness's stand-in for <see cref="TenantConnectionStringProvider"/>: an office
/// slug becomes that office's named in-memory SQLite database. The production
/// <see cref="OfficeConnectionStringResolver"/> still reads the database name from each office row.
///
/// <para>A map rather than a pattern, because the grant-cleanup tests put TWO offices in one database on
/// purpose (see <see cref="RealAuthorizationTestDatabase.CleanupOfficesConnectionString"/>). An unknown slug
/// throws, so a new test office that was not given a database fails loudly instead of opening an empty
/// one.</para>
/// </summary>
public sealed class RealAuthorizationConnectionStringProvider : ITenantConnectionStringProvider
{
    public const string OfficeSlug = "f2-authz-office";
    public const string SecondOfficeSlug = "f2-authz-office-b";
    public const string CleanupOfficeSlugPrefix = "f2-cleanup-";

    public string BuildConnectionString(string slug)
    {
        if (slug == OfficeSlug)
        {
            return RealAuthorizationTestDatabase.OfficeConnectionString;
        }

        if (slug == SecondOfficeSlug)
        {
            return RealAuthorizationTestDatabase.SecondOfficeConnectionString;
        }

        if (slug.StartsWith(CleanupOfficeSlugPrefix, StringComparison.Ordinal))
        {
            return RealAuthorizationTestDatabase.CleanupOfficesConnectionString;
        }

        throw new InvalidOperationException(
            $"The real-authorization harness has no database for office '{slug}'. Add it to this map.");
    }
}
