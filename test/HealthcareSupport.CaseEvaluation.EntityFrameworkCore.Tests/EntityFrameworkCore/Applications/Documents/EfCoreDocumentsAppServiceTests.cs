using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Integration coverage for <see cref="DocumentsAppService"/>, the IT Admin master-document catalog
/// (phase 8 tranche 1). Nothing drove this service before: a word search for "Documents" matches
/// <c>AppointmentDocumentsAppServiceTests</c> and friends, but a word-boundary search for
/// <c>IDocumentsAppService</c> returns nothing, so its reported 0% is real rather than an artefact.
///
/// <para>WHAT IS NOT COVERED, AND WHY. <c>CreateAsync</c> and <c>ReplaceFileAsync</c> both call
/// <c>IBlobContainer&lt;MasterDocumentsContainer&gt;.SaveAsync</c> before touching the database, and
/// this rig configures no blob storage at all -- there is no <c>Blob</c> or <c>FileSystem</c>
/// registration in either test module. Those two methods therefore cannot reach their own logic here,
/// and a test that pretended otherwise would be asserting against a substituted container rather than
/// the service. The remaining four public members are all reachable and all carry real rules.</para>
///
/// <para>Authorization is NOT asserted anywhere below. <c>AddAlwaysAllowAuthorization()</c> runs in
/// the test module, so every <c>[Authorize]</c> on this class is a no-op and a test claiming a
/// non-IT-Admin caller is refused could not fail. The RealAuthorization harness is where that
/// belongs.</para>
///
/// <para>Every Fact scopes itself with a unique token because the rig shares one SQLite database
/// across the whole collection and never rolls back. No assertion below counts rows it did not
/// create.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDocumentsAppServiceTests
    : CaseEvaluationApplicationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IDocumentsAppService _documentsAppService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IRepository<DocumentPackage> _documentPackageRepository;
    private readonly IRepository<PackageDetail, Guid> _packageDetailRepository;

    public EfCoreDocumentsAppServiceTests()
    {
        _documentsAppService = GetRequiredService<IDocumentsAppService>();
        _documentRepository = GetRequiredService<IDocumentRepository>();
        _documentPackageRepository = GetRequiredService<IRepository<DocumentPackage>>();
        _packageDetailRepository = GetRequiredService<IRepository<PackageDetail, Guid>>();
    }

    /// <summary>
    /// Inserts a host-scoped Document. Host scope matches the rig's ambient tenant (no resolver runs,
    /// so CurrentTenant.Id is null), which keeps these Facts readable without a tenant wrap.
    /// </summary>
    private Task<Guid> SeedDocumentAsync(string token, string nameSuffix, bool isActive = true)
    {
        var id = Guid.NewGuid();
        return WithUnitOfWorkAsync(async () =>
        {
            await _documentRepository.InsertAsync(
                new Document(
                    id: id,
                    tenantId: null,
                    name: $"TEST-doc-{token}-{nameSuffix}",
                    blobName: $"host/{Guid.NewGuid():N}.pdf",
                    contentType: "application/pdf",
                    isActive: isActive),
                autoSave: true);
            return id;
        });
    }

    /// <summary>
    /// Links a document to a package, seeding a REAL PackageDetail first.
    ///
    /// <para>The first version of this helper passed <c>packageDetailId: Guid.NewGuid()</c> and both
    /// delete Facts failed with <c>SQLite Error 19: FOREIGN KEY constraint failed</c>.
    /// <c>CaseEvaluationSharedModelConfiguration.cs:539</c> declares
    /// <c>HasMany(x =&gt; x.DocumentPackages).WithOne().HasForeignKey(x =&gt; x.PackageDetailId).IsRequired()</c>,
    /// and this rig turns SQLite foreign-key enforcement ON, so an invented parent id cannot be
    /// inserted. Worth recording because I had written that exact warning for others and then
    /// tripped over it myself.</para>
    /// </summary>
    private Task LinkToPackageAsync(Guid documentId, bool isActive) =>
        WithUnitOfWorkAsync(async () =>
        {
            var packageDetailId = Guid.NewGuid();
            await _packageDetailRepository.InsertAsync(
                new PackageDetail(
                    id: packageDetailId,
                    tenantId: null,
                    packageName: $"TEST-pkg-{packageDetailId:N}"[..40],
                    appointmentTypeId: null,
                    isActive: true),
                autoSave: true);

            await _documentPackageRepository.InsertAsync(
                new DocumentPackage(
                    packageDetailId: packageDetailId,
                    documentId: documentId,
                    isActive: isActive),
                autoSave: true);
        });

    // ------------------------------------------------------------------------
    // DeleteAsync -- the only genuine business rule on this service, and the pair below is what
    // makes it discriminating. The guard is `AnyAsync(x => x.DocumentId == id && x.IsActive)`, so a
    // test that only seeded an ACTIVE link would still pass with `&& x.IsActive` deleted. The second
    // Fact exists to kill that mutation specifically.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_WhenAnActiveDocumentPackageStillLinksIt_IsRefused()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var documentId = await SeedDocumentAsync(token, "linked");

        // LOAD-BEARING SETUP: the link is the thing the guard exists to find. Without it this Fact
        // would assert a refusal against an empty table and pass with the whole guard deleted.
        await LinkToPackageAsync(documentId, isActive: true);

        var ex = await Should.ThrowAsync<BusinessException>(
            async () => await _documentsAppService.DeleteAsync(documentId),
            "A document still linked to an active package must not be deletable. IT Admin has to "
            + "unlink it first, or the link rows are orphaned.");

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentInUse);

        // The refusal must also be effective, not merely thrown before a delete that happened anyway.
        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.FindAsync(documentId)).ShouldNotBeNull(
                "The document must still exist after a refused delete.");
        });
    }

    [Fact]
    public async Task DeleteAsync_WhenTheOnlyLinkIsInactive_RemovesTheDocument()
    {
        // The discriminating half. An INACTIVE link must not block the delete, which is what pins
        // the `&& x.IsActive` clause -- delete that clause and this Fact fails while the one above
        // still passes.
        var token = Guid.NewGuid().ToString("N")[..8];
        var documentId = await SeedDocumentAsync(token, "unlinked");
        await LinkToPackageAsync(documentId, isActive: false);

        await _documentsAppService.DeleteAsync(documentId);

        await WithUnitOfWorkAsync(async () =>
        {
            (await _documentRepository.FindAsync(documentId)).ShouldBeNull(
                "An inactive link is a historical row, not a live reference, so it must not block "
                + "deletion.");
        });
    }

    // ------------------------------------------------------------------------
    // GetListAsync -- filtering and sorting. Each Fact uses a token that appears in the seeded names,
    // so FilterText narrows the shared table to this test's own rows and the counts are safe.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FilterText_MatchesTheNameSubstring_AndExcludesEverythingElse()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var wanted = await SeedDocumentAsync(token, "wanted");
        var other = await SeedDocumentAsync(token, "other");

        // Both halves matter. Finding "wanted" proves the filter matches; NOT finding "other" proves
        // it actually narrows rather than returning the table.
        var result = await _documentsAppService.GetListAsync(
            new GetDocumentsInput { FilterText = $"TEST-doc-{token}-wanted", MaxResultCount = 100 });

        result.Items.Select(x => x.Id).ShouldContain(wanted);
        result.Items.Select(x => x.Id).ShouldNotContain(
            other,
            "A name filter that returns non-matching rows is not filtering.");
    }

    [Fact]
    public async Task GetListAsync_WhenIsActiveIsFalse_ReturnsOnlyTheDeactivatedRow()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var active = await SeedDocumentAsync(token, "active", isActive: true);
        var inactive = await SeedDocumentAsync(token, "inactive", isActive: false);

        var result = await _documentsAppService.GetListAsync(
            new GetDocumentsInput { FilterText = $"TEST-doc-{token}", IsActive = false, MaxResultCount = 100 });

        result.Items.Select(x => x.Id).ShouldContain(inactive);
        result.Items.Select(x => x.Id).ShouldNotContain(
            active,
            "IsActive=false must exclude active rows. Null means 'any status' so IT Admin can review "
            + "deactivated templates; false means only the deactivated ones.");
    }

    [Fact]
    public async Task GetListAsync_SortingByNameDescending_ReversesTheDefaultOrder()
    {
        // ApplySortingAndPaging is private static, so the sort is only observable through here. The
        // seeded names are deliberately ordered so ascending and descending differ: -a sorts before
        // -b, and the default sort is Name ascending.
        var token = Guid.NewGuid().ToString("N")[..8];
        await SeedDocumentAsync(token, "a");
        await SeedDocumentAsync(token, "b");

        var ascending = await _documentsAppService.GetListAsync(
            new GetDocumentsInput { FilterText = $"TEST-doc-{token}-", MaxResultCount = 100 });
        var descending = await _documentsAppService.GetListAsync(
            new GetDocumentsInput { FilterText = $"TEST-doc-{token}-", Sorting = "name desc", MaxResultCount = 100 });

        ascending.Items.Count.ShouldBe(2);
        descending.Items.Count.ShouldBe(2);
        descending.Items.Select(x => x.Name).ShouldBe(
            ascending.Items.Select(x => x.Name).Reverse(),
            "\"name desc\" must reverse the default ascending order. If the sort switch stops "
            + "recognising the string it silently falls through to ascending, and the grid quietly "
            + "ignores the user's click.");
    }

    // ------------------------------------------------------------------------
    // UpdateAsync + GetAsync
    // ------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PersistsNameContentTypeAndIsActive()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var documentId = await SeedDocumentAsync(token, "before");

        await _documentsAppService.UpdateAsync(
            documentId,
            new DocumentUpdateDto
            {
                Name = $"TEST-doc-{token}-after",
                ContentType = "application/msword",
                IsActive = false,
            });

        // Read back through the repository rather than trusting the returned DTO: the DTO is mapped
        // from the in-memory entity, so it would show the new values even if nothing was saved.
        await WithUnitOfWorkAsync(async () =>
        {
            var reloaded = await _documentRepository.FindAsync(documentId);
            reloaded.ShouldNotBeNull();
            reloaded!.Name.ShouldBe($"TEST-doc-{token}-after");
            reloaded.ContentType.ShouldBe("application/msword");
            reloaded.IsActive.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task GetAsync_ReturnsTheSeededRow()
    {
        // COVERAGE-ORIENTED, and labelled as such. GetAsync applies no filter and makes no decision;
        // it loads by id and maps. The only mutation that would fail this is deleting the call, which
        // proves the method exists rather than that it works. Kept because the mapping itself is
        // worth one assertion -- BlobName is the column that replaced OLD's DocumentFilePath and a
        // mapping that dropped it would be invisible everywhere else.
        var token = Guid.NewGuid().ToString("N")[..8];
        var documentId = await SeedDocumentAsync(token, "read");

        var dto = await _documentsAppService.GetAsync(documentId);

        dto.Id.ShouldBe(documentId);
        dto.Name.ShouldBe($"TEST-doc-{token}-read");
        dto.BlobName.ShouldNotBeNullOrWhiteSpace("BlobName is the blob-storage reference; a mapping "
            + "that dropped it would leave the SPA unable to download the template.");
    }
}
