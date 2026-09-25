using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// An opaque, office-scoped token that is equal for two appointments belonging to the same patient.
///
/// <para>Why a hash and not <c>Patient.Id</c>: the receiver's only use is equality, and our patient row
/// key means nothing in CalMed's world, where patient identity is actually minted. Publishing the raw key
/// would invite something downstream to store or display it as a patient identifier -- the precise
/// confusion that keeping CalMed's ids and our surrogate keys apart is meant to avoid. An obviously
/// opaque value enforces that by construction instead of by everyone remembering the rule.</para>
///
/// <para>Why salted with the office: patient deduplication in the portal runs per office, so the same
/// human at two offices is two unrelated rows. Mixing the office into the digest makes a cross-office
/// false match impossible rather than merely discouraged.</para>
///
/// <para>Why the office id and not a configured secret: the digest already contains a GUID, so it cannot
/// be brute-forced back to a row key even though the office id is known to the receiver. A secret would
/// add provisioning, protection and backup for no real gain, and rotating it would silently break every
/// key published before the rotation -- breaking the receiver's grouping with no error anywhere.</para>
/// </summary>
public static class SamePersonGroupKey
{
    /// <summary>Marks host scope, which has no office id. Distinct from every real office.</summary>
    private const string HostScope = "host";

    /// <summary>
    /// Lowercase SHA-256 hex over the office and patient. Deterministic and stable, so equality holds
    /// across pushes indefinitely.
    /// </summary>
    public static string Compute(Guid? tenantId, Guid patientId)
    {
        var scope = tenantId.HasValue
            ? tenantId.Value.ToString("D", CultureInfo.InvariantCulture)
            : HostScope;

        var material = string.Create(CultureInfo.InvariantCulture, $"{scope}|{patientId:D}");
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(material));

        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
