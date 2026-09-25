using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// The master-document library's storage paths in <see cref="DocumentsAppService"/>: creating a
/// document with its file, replacing the file, and the list's sort orders.
/// </summary>
/// <remarks>
/// The master-documents blob container is replaced for THIS class only, in
/// <c>AfterAddApplication</c>. On this rig the real provider points at a host that does not resolve,
/// so the create and replace paths were out of reach. Every document name carries a per-test
/// token, and each list assertion filters by it. All names are synthetic.
/// </remarks>
public abstract class DocumentsStorageTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private IBlobContainer<MasterDocumentsContainer> _blobs = null!;

    private readonly IDocumentsAppService _documents;
    private readonly IRepository<Document, Guid> _repository;
    private readonly ICurrentTenant _currentTenant;

    protected DocumentsStorageTests()
    {
        _documents = GetRequiredService<IDocumentsAppService>();
        _repository = GetRequiredService<IRepository<Document, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _blobs = Substitute.For<IBlobContainer<MasterDocumentsContainer>>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IBlobContainer<MasterDocumentsContainer>), _blobs));
    }

    private async Task<T> InOffice<T>(Guid? officeId, Func<Task<T>> call)
    {
        using (_currentTenant.Change(officeId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task<DocumentDto> CreateAsync(Guid? officeId, string name, string fileName, string? contentType = "application/pdf") =>
        InOffice(officeId, () => _documents.CreateAsync(
            new DocumentCreateDto { Name = name, ContentType = contentType, IsActive = true },
            new MemoryStream("%PDF-1.7 synthetic"u8.ToArray()),
            fileName));

    private string[] SavedBlobNames() =>
        _blobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBlobContainer.SaveAsync))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToArray();

    [Fact]
    public async Task Creating_a_document_stores_its_file_under_the_office_and_saves_the_record()
    {
        var token = Guid.NewGuid().ToString("N")[..8];

        var created = await CreateAsync(TenantsTestData.TenantARef, $"Synthetic Intake Form {token}", "intake.pdf");
        var hostCreated = await CreateAsync(null, $"Synthetic Host Form {token}", "host-form.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        var saved = SavedBlobNames();
        saved[0].ShouldStartWith($"{TenantsTestData.TenantARef:N}/");
        saved[0].ShouldEndWith(".pdf");
        saved[1].ShouldStartWith("host/");
        saved[1].ShouldEndWith(".docx");
        var stored = await InOffice(TenantsTestData.TenantARef, () => _repository.GetAsync(created.Id));
        stored.Name.ShouldBe($"Synthetic Intake Form {token}");
        stored.BlobName.ShouldBe(saved[0]);
        stored.ContentType.ShouldBe("application/pdf");
        hostCreated.Id.ShouldNotBe(created.Id);
    }

    [Fact]
    public async Task Replacing_a_file_stores_a_new_blob_and_keeps_the_type_unless_a_new_one_is_given()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var created = await CreateAsync(TenantsTestData.TenantARef, $"Synthetic Consent {token}", "consent.pdf");

        await InOffice(TenantsTestData.TenantARef, () => _documents.ReplaceFileAsync(
            created.Id, new MemoryStream("%PDF-1.7 v2"u8.ToArray()), "consent-v2.pdf", contentType: null));
        var keptType = await InOffice(TenantsTestData.TenantARef, () => _repository.GetAsync(created.Id));
        await InOffice(TenantsTestData.TenantARef, () => _documents.ReplaceFileAsync(
            created.Id, new MemoryStream("PK-synthetic"u8.ToArray()), "consent-v3.docx", contentType: "application/msword"));
        var newType = await InOffice(TenantsTestData.TenantARef, () => _repository.GetAsync(created.Id));

        var saved = SavedBlobNames();
        saved.Length.ShouldBe(3);
        saved.Distinct().Count().ShouldBe(3, "each replacement is a new blob, never an overwrite");
        keptType.BlobName.ShouldBe(saved[1]);
        keptType.ContentType.ShouldBe("application/pdf");
        newType.BlobName.ShouldBe(saved[2]);
        newType.ContentType.ShouldBe("application/msword");
        await _blobs.Received(3).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_list_sorts_by_name_or_creation_time_in_either_direction()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await CreateAsync(TenantsTestData.TenantARef, $"Synthetic B {token}", "b.pdf");
        await CreateAsync(TenantsTestData.TenantARef, $"Synthetic A {token}", "a.pdf");
        await CreateAsync(TenantsTestData.TenantARef, $"Synthetic C {token}", "c.pdf");

        async Task<string[]> Names(string sorting) =>
            (await InOffice(TenantsTestData.TenantARef, () => _documents.GetListAsync(
                new GetDocumentsInput { FilterText = token, Sorting = sorting, MaxResultCount = 10 })))
            .Items.Select(d => d.Name[..11]).ToArray();

        (await Names("name desc")).ShouldBe(new[] { "Synthetic C", "Synthetic B", "Synthetic A" });
        (await Names("creationtime")).ShouldBe(new[] { "Synthetic B", "Synthetic A", "Synthetic C" });
        (await Names("creationtime desc")).ShouldBe(new[] { "Synthetic C", "Synthetic A", "Synthetic B" });
        (await Names("an unknown order")).ShouldBe(new[] { "Synthetic A", "Synthetic B", "Synthetic C" });
    }
}
