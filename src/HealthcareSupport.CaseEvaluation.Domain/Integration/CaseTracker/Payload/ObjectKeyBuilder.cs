using System;
using System.Globalization;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Turns a stored blob name into the fully-qualified MinIO object key the Case Tracker can fetch.
///
/// <para>Why this exists: <c>AppointmentDocument.BlobName</c> and <c>AppointmentPacket.BlobName</c>
/// hold only the LOGICAL key. ABP's MinIO provider prepends a scope segment at save time, so the
/// real object key inside bucket <c>case-evaluation-documents</c> carries a <c>tenants/{id}/</c>
/// (or <c>host/</c>) prefix that is absent from the database value. The Case Tracker reads objects
/// in place, so publishing the unprefixed name would 404 on every fetch.</para>
///
/// <para>Mirrors ABP v10 <c>DefaultMinioBlobNameCalculator.Calculate</c> exactly -- host when no
/// tenant is in scope, otherwise <c>tenants/{tenantId:D}/</c>. Confirmed empirically against the
/// live bucket, whose root contains exactly <c>host/</c> and <c>tenants/</c>. The container name is
/// NOT part of the key because every container shares one bucket.</para>
/// </summary>
public static class ObjectKeyBuilder
{
    private const string HostSegment = "host";
    private const string TenantsSegment = "tenants";

    /// <summary>
    /// Prefixes <paramref name="blobName"/> with its scope segment. The blob name is passed through
    /// verbatim -- packet keys use dashed GUIDs and document keys use the no-dash ("N") form, and
    /// normalising either would break the fetch.
    /// </summary>
    public static string BuildFullyQualifiedKey(Guid? tenantId, string blobName)
    {
        Check.NotNullOrWhiteSpace(blobName, nameof(blobName));

        return tenantId.HasValue
            ? string.Concat(TenantsSegment, "/", tenantId.Value.ToString("D", CultureInfo.InvariantCulture), "/", blobName)
            : string.Concat(HostSegment, "/", blobName);
    }
}
