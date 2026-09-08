using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Security;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Phase 3 task 5, path 3.3 (PHI egress) -- the FIRST test of
/// <c>AppointmentDocumentsAppService.DownloadAsync</c>. Measured 2026-09-04:
/// <c>git grep -n "DownloadAsync" -- 'test/'</c> returned NOTHING across the whole test
/// tree, on the method that streams a stored document out of the system.
///
/// <para>WHY THE PERMISSION ATTRIBUTE IS NOT THE BOUNDARY. <c>DownloadAsync</c> carries
/// <c>[Authorize(CaseEvaluationPermissions.AppointmentDocuments.Default)]</c>, and
/// <c>ExternalUserRoleDataSeedContributor.BookingBaselineGrants()</c> grants exactly that
/// permission to all four external booking roles. So the attribute admits every external
/// party by design, and <c>_readAccessGuard.EnsureCanReadAsync(entity.AppointmentId)</c>
/// is the ENTIRE boundary between one external party and another party's documents
/// (issue #114, 2026-05-13).</para>
///
/// <para>The guard works. What was missing is anything that would catch its removal.</para>
///
/// <para><b>WHAT DELETING THE GUARD ACTUALLY DOES, measured 2026-09-04.</b> The call runs
/// on past the missing guard to <c>_blobContainer.GetAsync</c>, and this test fails with:</para>
/// <code>
/// should throw  Volo.Abp.BusinessException
/// but threw     Minio.Exceptions.InternalClientException
/// </code>
/// <para>Blob storage here is MinIO, configured by the main module, with no test double and
/// no reachable server (<c>No such host is known. (minio:9000)</c>). <b>So what catches the
/// break is the exception TYPE, not the error code</b> -- the <c>Code</c> assertion below is
/// never reached in the break case.</para>
///
/// <para><b>WHAT THAT DOES AND DOES NOT SHOW.</b> It shows the request passed every access
/// check, because the blob fetch is downstream of all of them. It does NOT show what a real
/// caller receives: with storage reachable, the remaining statements are a null check and a
/// <c>DownloadResult</c>, so a non-party would get the file. <b>That last step is read from
/// the code, not measured</b>, and is written here as inference so nobody later cites it as
/// a result.</para>
///
/// <para>(TWO corrections, both left visible. The first draft predicted the storage fallback
/// would throw <c>UserFriendlyException("Document file is missing from storage.")</c>. The
/// second recorded that the break "does not throw at all" and returns a result -- also
/// wrong, and wrong in a more dangerous way, because it read as a completed measurement. A
/// comment asserting behaviour the code does not have is the exact defect this phase keeps
/// finding, and it is no less a defect for being in the comment that says so.)</para>
///
/// <para><b>WHY THE ERROR-CODE ASSERTION STAYS ANYWAY.</b> Not because it catches this
/// break -- it does not. <c>UserFriendlyException</c> derives from <c>BusinessException</c>,
/// and this method's storage fallback throws one, so if a future change made storage fail
/// BEFORE the guard, a type-only assertion would report the storage fallback while appearing
/// to prove access control. It is defence against a reordering that has not happened, kept
/// on that merit alone.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeDocumentDownloadAccessTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentDocumentsAppService _documents;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public MultiOfficeDocumentDownloadAccessTests()
    {
        _documents = GetRequiredService<IAppointmentDocumentsAppService>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task DownloadAsync_ByAnExternalUserWhoIsNotAPartyToTheAppointment_IsDenied()
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        var documentId = Guid.NewGuid();
        await SeedDocumentAsync(officeA, documentId);

        // An external caller holding the SAME permission every external role is seeded
        // with, who is simply not a party to this appointment. That is the case the guard
        // exists for, and the only thing standing between them and the file.
        var strangerUserId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, strangerUserId, "Patient"))
            {
                var ex = await Should.ThrowAsync<BusinessException>(
                    () => _documents.DownloadAsync(documentId));

                ex.Code.ShouldBe(
                    CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
                    "the read-access guard must be what refuses this, not the storage "
                    + "fallback -- both throw BusinessException, so only the code tells "
                    + "them apart, and a type-only assertion would pass with the guard "
                    + "deleted");
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Attaches a document row to the office's seeded appointment. The blob is never
    /// written, for two reasons: this test asserts the request is refused BEFORE storage is
    /// reached, and it could not be written anyway -- the harness has no reachable MinIO, so
    /// a seeding attempt dies in the helper rather than in the test. Synthetic values
    /// throughout (HIPAA).
    /// </summary>
    private Task SeedDocumentAsync(SeededOffice office, Guid documentId) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                db.Set<AppointmentDocument>().Add(new AppointmentDocument(
                    id: documentId,
                    tenantId: office.OfficeId,
                    appointmentId: office.AppointmentId,
                    documentName: "Synthetic doc",
                    fileName: "synthetic.pdf",
                    blobName: "blob/synthetic.pdf",
                    contentType: "application/pdf",
                    fileSize: 1024,
                    uploadedByUserId: office.BookerUserId));
                await db.SaveChangesAsync();
            }
        }, requiresNew: true);
}
