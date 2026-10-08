using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.BlobStoring.Minio;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// MinIO storage that scans every file before writing it (B11, decision D2).
/// </summary>
/// <remarks>
/// <para>
/// Set per container in <c>CaseEvaluationDomainModule</c> for the seven containers that hold
/// user-supplied files. The scan sits HERE, in the provider, rather than in each app service, so a
/// future upload path into those containers is scanned without anyone remembering to call a scanner.
/// Generated packets (<c>appointment-packets</c>) keep the plain MinIO provider.
/// </para>
/// <para>
/// The scan runs BEFORE <see cref="MinioBlobProvider.SaveAsync"/>, so a refused file never reaches
/// storage and the exception surfaces from the caller's own <c>SaveAsync</c> call, ahead of any row
/// it would have written after it. It fails CLOSED: a found signature throws
/// <see cref="CaseEvaluationDomainErrorCodes.UploadRefused"/>, and a scanner that cannot answer throws
/// <see cref="CaseEvaluationDomainErrorCodes.UploadScanUnavailable"/>. Neither stores anything.
/// </para>
/// <para>
/// The refusal log names the signature and the container only. It never logs the blob name, because
/// master-document blob names contain the uploader's original file name.
/// </para>
/// </remarks>
[ExposeServices(typeof(IBlobProvider), typeof(ScanningMinioBlobProvider))]
public class ScanningMinioBlobProvider : MinioBlobProvider
{
    private readonly IUploadScanner _scanner;
    private readonly ILogger<ScanningMinioBlobProvider> _logger;

    public ScanningMinioBlobProvider(
        IMinioBlobNameCalculator minioBlobNameCalculator,
        IBlobNormalizeNamingService blobNormalizeNamingService,
        IUploadScanner scanner,
        ILogger<ScanningMinioBlobProvider> logger)
        : base(minioBlobNameCalculator, blobNormalizeNamingService)
    {
        _scanner = scanner;
        _logger = logger;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var seekable = await EnsureSeekableAsync(args);
        var start = seekable.BlobStream.Position;

        UploadScanResult result;
        try
        {
            result = await _scanner.ScanAsync(seekable.BlobStream, args.CancellationToken);
        }
        catch (UploadScanUnavailableException ex)
        {
            _logger.LogWarning(ex,
                "Upload refused because the malware scan could not run, container {ContainerName}",
                args.ContainerName);
            throw new BusinessException(CaseEvaluationDomainErrorCodes.UploadScanUnavailable);
        }

        if (!result.IsClean)
        {
            _logger.LogWarning(
                "Upload refused by the malware scan: signature {Signature}, container {ContainerName}",
                result.Signature, args.ContainerName);
            throw new BusinessException(CaseEvaluationDomainErrorCodes.UploadRefused);
        }

        seekable.BlobStream.Position = start;
        await SaveToStorageAsync(seekable);
    }

    /// <summary>
    /// The write itself, separated so tests can record it without a MinIO server. Production always
    /// calls the MinIO provider's save.
    /// </summary>
    protected virtual Task SaveToStorageAsync(BlobProviderSaveArgs args) => base.SaveAsync(args);

    /// <summary>
    /// A request body cannot seek, and the same bytes have to be read twice: once by the scanner,
    /// once by the store. A non-seekable stream is buffered first. The framework caps an upload at
    /// 12 MB (<c>CaseEvaluationHttpApiHostModule</c>), which bounds the buffer.
    /// </summary>
    private static async Task<BlobProviderSaveArgs> EnsureSeekableAsync(BlobProviderSaveArgs args)
    {
        if (args.BlobStream.CanSeek)
        {
            return args;
        }

        var buffered = new MemoryStream();
        await args.BlobStream.CopyToAsync(buffered, args.CancellationToken);
        buffered.Position = 0;
        return new BlobProviderSaveArgs(
            args.ContainerName,
            args.Configuration,
            args.BlobName,
            buffered,
            args.OverrideExisting,
            args.CancellationToken);
    }
}
