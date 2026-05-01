using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Slim DomainService for <see cref="AppointmentDocument"/>. The AppService
/// orchestrates blob upload -> entity insert in a single UoW; this manager
/// just centralizes the validation + entity construction.
/// </summary>
public class AppointmentDocumentManager : DomainService
{
    protected IRepository<AppointmentDocument, Guid> _appointmentDocumentRepository;

    public AppointmentDocumentManager(IRepository<AppointmentDocument, Guid> appointmentDocumentRepository)
    {
        _appointmentDocumentRepository = appointmentDocumentRepository;
    }

    public virtual async Task<AppointmentDocument> CreateAsync(
        Guid? tenantId,
        Guid appointmentId,
        string documentName,
        string fileName,
        string blobName,
        string? contentType,
        long fileSize,
        Guid uploadedByUserId)
    {
        Check.NotNull(appointmentId, nameof(appointmentId));
        if (appointmentId == Guid.Empty)
        {
            throw new UserFriendlyException("AppointmentId is required.");
        }
        if (fileSize <= 0)
        {
            throw new UserFriendlyException("File is empty.");
        }
        if (fileSize > AppointmentDocumentConsts.MaxFileSizeBytes)
        {
            throw new UserFriendlyException($"File exceeds the {AppointmentDocumentConsts.MaxFileSizeBytes / (1024 * 1024)} MB upload cap.");
        }

        var entity = new AppointmentDocument(
            GuidGenerator.Create(),
            tenantId,
            appointmentId,
            documentName,
            fileName,
            blobName,
            contentType,
            fileSize,
            uploadedByUserId);

        return await _appointmentDocumentRepository.InsertAsync(entity);
    }
}
