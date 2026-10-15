using System;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// Which containers are scanned (B11, decision D2), read from the real module configuration.
/// </summary>
/// <remarks>
/// <c>appointment-packets</c> is the NAMED DECOY. It holds generated packets and must keep the plain
/// MinIO provider. Asserting it proves this test reads each container's own setting: a single
/// default applied to every container would pass the seven positive facts and fail this one.
/// </remarks>
public abstract class UploadContainerScanWiringTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AbpBlobStoringOptions _options;
    private readonly IBlobProviderSelector _selector;

    protected UploadContainerScanWiringTests()
    {
        _options = GetRequiredService<IOptions<AbpBlobStoringOptions>>().Value;
        _selector = GetRequiredService<IBlobProviderSelector>();
    }

    public static TheoryData<Type> UserFileContainers => new()
    {
        typeof(AppointmentDocumentsContainer),
        typeof(AnonymousUploadsContainer),
        typeof(DocumentPackagesContainer),
        typeof(MasterDocumentsContainer),
        typeof(JointDeclarationsContainer),
        typeof(UserSignaturesContainer),
        typeof(OfficeLogosContainer),
    };

    [Theory]
    [MemberData(nameof(UserFileContainers))]
    public void Every_container_holding_user_files_is_configured_to_scan(Type container)
    {
        var name = BlobContainerNameAttribute.GetContainerName(container);

        _options.Containers.GetConfiguration(name).ProviderType.ShouldBe(typeof(ScanningMinioBlobProvider));
    }

    [Theory]
    [MemberData(nameof(UserFileContainers))]
    public void Every_container_holding_user_files_resolves_the_scanning_provider(Type container)
    {
        // The setting alone is not enough: ABP picks the provider instance from DI by type, so this
        // proves the scanning provider is registered where the selector looks for it.
        var name = BlobContainerNameAttribute.GetContainerName(container);

        _selector.Get(name).ShouldBeOfType<ScanningMinioBlobProvider>();
    }

    [Fact]
    public void Generated_packets_are_not_scanned()
    {
        var name = BlobContainerNameAttribute.GetContainerName<AppointmentPacketsContainer>();

        _options.Containers.GetConfiguration(name).ProviderType.ShouldBe(typeof(MinioBlobProvider));
        _selector.Get(name).ShouldBeOfType<MinioBlobProvider>();
    }
}
