using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PhiAccess;

/// <summary>
/// When the access log cannot be written, the read fails: the caller gets an error, not the data,
/// and a download never opens the file. Proven with the log's repository replaced by one that
/// throws, for one record read and one file download.
/// </summary>
public abstract class PhiAccessLogFailClosedTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid StaffUserId = new("7f4a2b10-0000-4000-9000-000000000011");
    private static readonly Guid OfficeA = TenantsTestData.TenantARef;

    private IRepository<PhiAccessLogEntry, Guid> _failingLog = null!;
    private IBlobContainer<AppointmentDocumentsContainer> _documentBlobs = null!;

    private readonly IPatientsAppService _patients;
    private readonly IAppointmentDocumentsAppService _documents;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected PhiAccessLogFailClosedTests()
    {
        _patients = GetRequiredService<IPatientsAppService>();
        _documents = GetRequiredService<IAppointmentDocumentsAppService>();
        _documentRepository = GetRequiredService<IRepository<AppointmentDocument, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _failingLog = Substitute.For<IRepository<PhiAccessLogEntry, Guid>>();
        _failingLog.InsertManyAsync(Arg.Any<IEnumerable<PhiAccessLogEntry>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("synthetic access-log outage"));
        _documentBlobs = Substitute.For<IBlobContainer<AppointmentDocumentsContainer>>();
        services.Replace(ServiceDescriptor.Transient<IRepository<PhiAccessLogEntry, Guid>>(_ => _failingLog));
        services.Replace(ServiceDescriptor.Singleton<IBlobContainer<AppointmentDocumentsContainer>>(_documentBlobs));
    }

    private async Task<T> AsStaff<T>(Func<Task<T>> call)
    {
        using (_currentTenant.Change(OfficeA))
        using (WithCurrentUser.Run(_principal, StaffUserId, "Staff Supervisor"))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    [Fact]
    public async Task An_ssn_is_not_revealed_when_the_reveal_cannot_be_recorded()
    {
        SsnRevealDto? reveal = null;

        var error = await Should.ThrowAsync<InvalidOperationException>(async () =>
            reveal = await AsStaff(() => _patients.GetFullSsnAsync(PatientsTestData.Patient1Id)));

        error.Message.ShouldBe("synthetic access-log outage");
        reveal.ShouldBeNull();
        await _failingLog.Received(1).InsertManyAsync(
            Arg.Is<IEnumerable<PhiAccessLogEntry>>(rows => rows != null),
            Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_document_is_never_opened_when_the_download_cannot_be_recorded()
    {
        var document = await AsStaff(() => _documentRepository.InsertAsync(
            new AppointmentDocument(Guid.NewGuid(), OfficeA, AppointmentsTestData.Appointment1Id, "Synthetic intake",
                "synthetic-intake.pdf", "office-a/synthetic", "application/pdf", 10, StaffUserId),
            autoSave: true));

        await Should.ThrowAsync<InvalidOperationException>(() => AsStaff(() => _documents.DownloadAsync(document.Id)));

        await _documentBlobs.DidNotReceive().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
