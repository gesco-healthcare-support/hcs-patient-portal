using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// F3 (2026-05-29) -- PackageDocumentQueueHandler now seeds package-document
/// rows on <see cref="AppointmentSubmittedEto"/> (request time, was
/// AppointmentApprovedEto). This covers the idempotency guard: a re-delivered
/// submission event must NOT double-insert when the appointment already has
/// queued rows (a queued row carries a VerificationCode; ad-hoc uploads do not).
/// </summary>
public class PackageDocumentQueueHandlerTests
{
    [Fact]
    public async Task HandleEventAsync_WhenPackageRowsAlreadyExist_SkipsAndDoesNotQueue()
    {
        var appointmentId = Guid.NewGuid();

        var packageDetailRepository = Substitute.For<IPackageDetailRepository>();
        var documentRepository = Substitute.For<IRepository<Document, Guid>>();
        var appointmentDocumentRepository = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        var documentTypeRepository = Substitute.For<IAppointmentDocumentTypeRepository>();
        var manager = Substitute.For<AppointmentDocumentManager>(
            Substitute.For<IRepository<AppointmentDocument, Guid>>());
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        // The appointment already has a queued package row (has a VerificationCode).
        var existing = AppointmentDocument.CreateQueued(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: appointmentId,
            documentName: "Existing Doc",
            verificationCode: Guid.NewGuid());
        appointmentDocumentRepository.GetQueryableAsync()
            .Returns(new List<AppointmentDocument> { existing }.AsQueryable());

        var handler = new PackageDocumentQueueHandler(
            packageDetailRepository,
            documentRepository,
            appointmentDocumentRepository,
            documentTypeRepository,
            manager,
            currentTenant,
            NullLogger<PackageDocumentQueueHandler>.Instance);

        await handler.HandleEventAsync(new AppointmentSubmittedEto
        {
            AppointmentId = appointmentId,
            TenantId = null,
            AppointmentTypeId = Guid.NewGuid(),
        });

        // Guard short-circuits before any package resolution / row creation.
        await manager.DidNotReceive().CreateQueuedAsync(
            Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<Guid?>());
    }
}
