using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Guardians for the party-level access check on <c>AppointmentPacketsAppService</c>, added
/// 2026-09-08 alongside the check itself.
///
/// <para><b>WHY THIS FILE EXISTS.</b> Until that change the service had NO party-level access
/// check on any of its four public methods. Measured, not inferred: all four take a
/// caller-supplied <c>appointmentId</c>, and a repo-wide
/// <c>grep -rn "EnsureCanReadAsync" --include=*.cs src/</c> did not list the file at all. Its
/// two gates were <c>[Authorize(CaseEvaluationPermissions.AppointmentPackets.Default)]</c>,
/// which <c>ExternalUserRoleDataSeedContributor</c> grants to all four external roles, and
/// <c>PacketVisibility.IsAllowed(CurrentUser.Roles, kind)</c>, a per-ROLE/per-KIND allow-list
/// that takes no appointment and no caller identity. Neither can express "is this caller a
/// party to THIS appointment".</para>
///
/// <para>This file is the successor to a probe that proved the gap. That probe asserted the
/// absence of a control and failed by design; it is replaced rather than kept, because a test
/// describing a defect that no longer exists is worse than no test.</para>
///
/// <para><b>WHY THE ERROR-CODE ASSERTION IS NOT OPTIONAL.</b> Every test here asserts
/// <c>ex.Code == AppointmentAccessDenied</c>, never merely that a
/// <c>BusinessException</c> was thrown. Three other <c>BusinessException</c> subclasses are
/// reachable in these methods -- <c>EntityNotFoundException</c> (no packet row),
/// <c>UserFriendlyException</c> ("not ready yet", and the storage fallback) -- so a type-only
/// assertion would pass on any of them and report an access-control guarantee it had not
/// tested. This is the same reasoning as <c>MultiOfficeDocumentDownloadAccessTests</c>, and it
/// is the difference between a guardian and a green tick.</para>
///
/// <para><b>WHY THE SEEDED PACKET IS LOAD-BEARING, not setup noise.</b> A negative guarantee
/// cannot be proven against an empty fixture. With no packet row, deleting a guard makes these
/// methods throw <c>EntityNotFoundException</c> or return empty -- so the break would still
/// look like a refusal, or like nothing at all. With a <c>Generated</c> Patient packet present,
/// deleting a guard lets the request reach something it should never have reached, and the
/// break is unambiguous. See the measured break behaviour on each test.</para>
///
/// <para><b>WHY THE SEED IS IDEMPOTENT.</b>
/// <c>CaseEvaluationSharedModelConfiguration.cs:740</c> puts a UNIQUE filtered index on
/// <c>(TenantId, AppointmentId, Kind)</c>, and this harness seeds its two offices ONCE for the
/// whole run (<c>CaseEvaluationMultiOfficeTestBase</c>, process-wide static state). Four tests
/// inserting the same key would violate that index and die inside the helper -- a red test on
/// exactly the right name for entirely the wrong reason.</para>
///
/// <para><b>WHAT THESE TESTS DO NOT PROVE.</b> The harness calls
/// <c>AddAlwaysAllowAuthorization()</c> (<c>CaseEvaluationMultiOfficeTestModule.cs:103</c>), so
/// the <c>[Authorize]</c> permission attribute never refuses here and is NOT under test. What
/// is under test is the party check, which is the only gate that distinguishes one external
/// party from another. Synthetic data only, and no blob is ever written (HIPAA).</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class PacketPartyAccessTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentPacketsAppService _packets;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PacketPartyAccessTests()
    {
        _packets = GetRequiredService<IAppointmentPacketsAppService>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// Break behaviour, MEASURED 2026-09-08 by deleting this method's guard call:
    /// <code>
    /// should throw  Volo.Abp.BusinessException
    /// but did not
    /// </code>
    /// The method returned the packet DTO to a non-party. Only this test failed; the other
    /// three passed in the same run.
    /// </summary>
    [Fact]
    public async Task GetByAppointmentAsync_ByAnExternalUserWhoIsNotAPartyToTheAppointment_IsDenied()
    {
        await AssertNonPartyIsDeniedAsync(
            (packets, appointmentId) => packets.GetByAppointmentAsync(appointmentId),
            "packet metadata");
    }

    /// <summary>
    /// Break behaviour, MEASURED 2026-09-08 by deleting this method's guard call:
    /// <code>
    /// should throw  Volo.Abp.BusinessException
    /// but threw     Minio.Exceptions.InternalClientException
    /// </code>
    /// <para>The call ran past the missing guard to <c>_blobContainer.GetAsync</c>, the LAST
    /// statement in the method. Storage is unreachable in this harness, so reaching it is
    /// itself the proof that every preceding access check passed -- the unreachable server is
    /// the instrument, not the obstacle. Note what catches this break: the exception TYPE. The
    /// <c>Code</c> assertion is never reached in the break case. Only this test failed; the
    /// other three passed in the same run.</para>
    ///
    /// <para><b>Inferred, not measured:</b> that a real caller would receive the file. With
    /// storage reachable the only remaining statements are a null check and a
    /// <c>DownloadResult</c>, so they would -- but this harness cannot demonstrate it, and it
    /// is written as inference so nobody later cites it as a result.</para>
    /// </summary>
    [Fact]
    public async Task DownloadAsync_ByAnExternalUserWhoIsNotAPartyToTheAppointment_IsDenied()
    {
        await AssertNonPartyIsDeniedAsync(
            (packets, appointmentId) => packets.DownloadAsync(appointmentId),
            "the assembled packet file");
    }

    /// <summary>
    /// Break behaviour, MEASURED 2026-09-08 by deleting this method's guard call:
    /// <code>
    /// should throw  Volo.Abp.BusinessException
    /// but did not
    /// </code>
    /// The method returned a list containing the seeded Patient packet to a non-party. Only
    /// this test failed; the other three passed in the same run.
    /// </summary>
    [Fact]
    public async Task GetListByAppointmentAsync_ByAnExternalUserWhoIsNotAPartyToTheAppointment_IsDenied()
    {
        await AssertNonPartyIsDeniedAsync(
            (packets, appointmentId) => packets.GetListByAppointmentAsync(appointmentId),
            "the packet list");
    }

    /// <summary>
    /// The kind is caller-supplied on this surface, so <c>PacketVisibility</c> constrains what
    /// is asked for and never whose it is. <c>PacketKind.Patient</c> is passed deliberately:
    /// the Patient role IS allowed that kind, so the role/kind gate passes and the party check
    /// is the only thing left to refuse the call. Break behaviour, MEASURED 2026-09-08 by
    /// deleting this method's guard call: as <c>DownloadAsync</c> above --
    /// <c>but threw Minio.Exceptions.InternalClientException</c>, so the call reached storage.
    /// Only this test failed; the other three passed in the same run.
    /// </summary>
    [Fact]
    public async Task DownloadByKindAsync_ByAnExternalUserWhoIsNotAPartyToTheAppointment_IsDenied()
    {
        await AssertNonPartyIsDeniedAsync(
            (packets, appointmentId) => packets.DownloadByKindAsync(appointmentId, PacketKind.Patient),
            "the assembled packet file, by kind");
    }

    /// <summary>
    /// Runs <paramref name="call"/> as a caller who holds the Patient role -- so
    /// <c>PacketVisibility</c> admits the Patient kind -- and who differs from the appointment's
    /// booker in exactly one way: they are not a party to it.
    /// </summary>
    private async Task AssertNonPartyIsDeniedAsync(
        Func<IAppointmentPacketsAppService, Guid, Task> call,
        string whatWouldLeak)
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedGeneratedPatientPacketAsync(officeA);

        var strangerUserId = Guid.NewGuid();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, strangerUserId, "Patient"))
            {
                // The custom message is not decoration. These four tests share this helper, so
                // Shouldly renders THIS lambda rather than the concrete call under test -- the
                // test name carries the guarantee, but the message would otherwise be identical
                // across all four. Naming the surface here is what makes a break report which
                // one broke.
                var ex = await Should.ThrowAsync<BusinessException>(
                    () => call(_packets, officeA.AppointmentId),
                    $"a caller who is not a party to this appointment must not receive {whatWouldLeak}");

                ex.Code.ShouldBe(
                    CaseEvaluationDomainErrorCodes.AppointmentAccessDenied,
                    "the read-access guard must be what refuses this, not a missing row, a "
                    + "not-yet-generated status or the storage fallback -- all of those also "
                    + "throw BusinessException, so only the code tells them apart, and a "
                    + $"type-only assertion would pass with the guard deleted, leaking {whatWouldLeak}");
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Ensures office A's shared appointment has a Patient packet in <c>Generated</c> status.
    ///
    /// <para>The status is load-bearing: anything else makes the download surfaces throw "not
    /// ready yet" before reaching the part under test, which would be neither a refusal nor a
    /// storage error. The blob is never written -- these tests assert the request is refused
    /// before storage is reached, and it could not be written anyway, because the harness has no
    /// reachable MinIO and a seeding attempt would die here rather than in the test.</para>
    ///
    /// <para>Insert-if-absent, not insert: see the class docstring on the unique index.</para>
    /// </summary>
    private Task SeedGeneratedPatientPacketAsync(SeededOffice office) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                var alreadySeeded = await db.Set<AppointmentPacket>()
                    .AnyAsync(p => p.AppointmentId == office.AppointmentId
                        && p.Kind == PacketKind.Patient);
                if (alreadySeeded)
                {
                    return;
                }

                db.Set<AppointmentPacket>().Add(new AppointmentPacket(
                    id: Guid.NewGuid(),
                    tenantId: office.OfficeId,
                    appointmentId: office.AppointmentId,
                    kind: PacketKind.Patient,
                    blobName: "synthetic/packet-party-access.pdf",
                    status: PacketGenerationStatus.Generated));
                await db.SaveChangesAsync();
            }
        }, requiresNew: true);
}
