using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for fully-qualified MinIO object keys. The blob name stored on a document /
/// packet row is only the LOGICAL key; ABP's MinIO provider prepends a scope segment when it
/// saves. The Case Tracker reads objects in place, so the key we publish must match what ABP
/// actually wrote or the fetch 404s.
///
/// <para>Prefix semantics verified two ways: ABP v10 <c>DefaultMinioBlobNameCalculator</c>
/// source, and the live bucket on the portal server (whose root contains exactly
/// <c>host/</c> and <c>tenants/</c>).</para>
/// </summary>
public class ObjectKeyBuilderTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");

    [Fact]
    public void TenantScopedKey_IsPrefixedWithDashedTenantSegment()
    {
        const string blobName = "b8844bba-414c-e238-4a71-3a22841f21af/ada5e3c5-0034-ebde-253c-3a2293631dee/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf";

        var key = ObjectKeyBuilder.BuildFullyQualifiedKey(TenantId, blobName);

        key.ShouldBe("tenants/b8844bba-414c-e238-4a71-3a22841f21af/" + blobName);
    }

    [Fact]
    public void HostScopedKey_IsPrefixedWithHost()
    {
        var key = ObjectKeyBuilder.BuildFullyQualifiedKey(null, "b8844bba414ce2384a713a22841f21af.png");

        key.ShouldBe("host/b8844bba414ce2384a713a22841f21af.png");
    }

    [Fact]
    public void DocumentKey_PreservesTheNoDashSegmentsVerbatim()
    {
        // Uploaded documents use the "N" (no-dash) guid format for their own segments while
        // packets use the dashed form. The builder must not normalise either.
        const string blobName = "b8844bba414ce2384a713a22841f21af/ada5e3c50034ebde253c3a2293631dee/f97796c9365b4ad3a16408f72981cae3";

        var key = ObjectKeyBuilder.BuildFullyQualifiedKey(TenantId, blobName);

        key.ShouldBe("tenants/b8844bba-414c-e238-4a71-3a22841f21af/" + blobName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingBlobName_Throws(string? blobName)
    {
        Should.Throw<ArgumentException>(() => ObjectKeyBuilder.BuildFullyQualifiedKey(TenantId, blobName!));
    }
}
