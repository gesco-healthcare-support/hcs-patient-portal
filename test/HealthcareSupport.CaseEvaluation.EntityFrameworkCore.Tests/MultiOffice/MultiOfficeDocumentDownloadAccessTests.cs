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
/// <para><b>WHAT DELETING THE GUARD ACTUALLY DOES, measured rather than predicted.</b>
/// Removing <c>EnsureCanReadAsync</c> from <c>DownloadAsync</c> and re-running this test
/// on 2026-09-04: it does not throw a different exception -- <b>it does not throw at
/// all.</b> The call returns a <c>DownloadResult</c> to a caller who is not a party to
/// the appointment. So the assertion that catches the break is
/// <c>Should.ThrowAsync&lt;BusinessException&gt;</c> itself.</para>
///
/// <para>(An earlier draft of this comment predicted the storage fallback would throw
/// <c>UserFriendlyException("Document file is missing from storage.")</c> instead. It does
/// not, and the prediction is corrected here rather than left standing -- a comment
/// asserting behaviour the code does not have is the defect this phase keeps finding.)</para>
///
/// <para><b>THE ERROR-CODE ASSERTION IS STILL DELIBERATE, for a different reason than
/// first written.</b> <c>UserFriendlyException</c> derives from <c>BusinessException</c>,
/// and this method's storage fallback throws one. If a future change made storage fail
/// before the guard, a type-only assertion would report the storage fallback while
/// appearing to prove access control. The code check keeps those apart. It is defence
/// against a case that did NOT occur in this measurement, kept on its own merits rather
/// than because it is what caught the break.</para>
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
    /// written: this test asserts the request is refused BEFORE storage is reached, and
    /// seeding a real blob would make the storage fallback indistinguishable from success.
    /// Synthetic values throughout (HIPAA).
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
