using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Phase 3 task 6 (path 3.4b) -- guardian for the per-ROLE packet KIND filter as the
/// application actually applies it.
///
/// <para><b>WHY THIS IS A SEPARATE FILE FROM <c>PacketPartyAccessTests</c>.</b> After
/// 2026-09-08 the packet path has TWO independent guards and they are easy to conflate:</para>
/// <list type="number">
///   <item><b>Party guard</b> -- "is this caller a party to THIS appointment".
///         Appointment-scoped. Pinned by <c>PacketPartyAccessTests</c>.</item>
///   <item><b>Role/kind allow-list</b> -- "may this ROLE see this KIND".
///         <c>PacketVisibility</c> takes no appointment and no caller identity, so it cannot
///         answer question 1. This file pins question 2.</item>
/// </list>
/// <para>A caller who passes one has proved nothing about the other. Keeping them in separate
/// files with separate docstrings is deliberate: a single file asserting both invites a future
/// reader to assume either is covered because the other is.</para>
///
/// <para><b>WHAT WAS MEASURED, AND WHY THIS FILE EXISTS AT ALL.</b> On 2026-09-08 the filter
/// was deliberately deleted and the suites re-run:</para>
/// <code>
/// delete AppointmentPacketsAppService.cs:131  -- `.Where(x => allowedKinds.Contains(x.Kind))`
/// leave  AppointmentPacketsAppService.cs:122  -- the AllowedKinds(...) call, so it still looks used
///   Application.Tests   1127 passed, 0 failed
///   Domain.Tests         693 passed, 0 failed
///   MultiOffice          105 passed, 0 failed
/// </code>
/// <para><b>Zero of 1,925 tests failed.</b> The allow-list was asserted twelve ways in
/// <c>PacketVisibilityUnitTests</c> -- one layer BELOW the boundary that relies on it -- and
/// nowhere at the boundary itself. That is the generalisable shape worth remembering: <b>a
/// guarantee tested one layer below where it is actually relied upon.</b> It is not a test that
/// cannot fail; it is a test of the wrong subject. Deleting the filter while leaving the helper
/// call in place is a particularly nasty break because the file still READS as though the
/// allow-list is applied.</para>
///
/// <para><b>AND THIS FILE WAS THEN SEEN TO FAIL, same break, 2026-09-08:</b></para>
/// <code>
/// Failed: 4, Passed: 1
///   FAILED  ForAPatientParty_ReturnsThePatientKindOnly
///   FAILED  ForAnAttorneyOrClaimExaminerParty (Applicant Attorney / Defense Attorney / Claim Examiner)
///   PASSED  ForAnInternalCaller_..._Control     &lt;- see that test; this is why it says Control
///
///   should be [AttorneyClaimExaminer]
///   but was   [PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer]
/// </code>
/// <para>The four guardians name the guarantee rather than a downstream symptom: a non-patient
/// party was handed the Patient AND Doctor packets. Source restored byte-identical afterwards.</para>
///
/// <para>The phase plan's prescribed break for 3.4 -- "make <c>AllowedKinds</c> return all three
/// kinds for Doctor" -- was also measured, and it fails exactly one test:
/// <c>PacketVisibilityUnitTests.AllowedKinds_Doctor_ReturnsNone</c>, the unit test asserting the
/// very line changed. It proves the unit test tests the unit. It is additionally the wrong
/// DIRECTION (phase 3 catches a guard's REMOVAL; that break ADDS a permissive branch) and rests
/// on a stale premise: IR1 (2026-06-03) retired "Doctor" as an internal persona, so there is no
/// Doctor arm to break. Recorded here because that break is still written in the record.</para>
///
/// <para><b>HOW PARTY ACCESS IS HELD CONSTANT -- read before changing the caller.</b> Every test
/// below calls as <c>officeA.BookerUserId</c>. The seeder sets
/// <c>Patient.IdentityUserId = bookerUserId</c> (<c>MultiOfficeSeeder.cs:147</c>) and the
/// appointment's <c>PatientId</c> to that patient, so the party guard admits this caller through
/// the patient-identity pathway -- a match on USER ID, which is independent of any role claim.
/// That is what makes varying the role claim a clean experiment: guard 1 is satisfied identically
/// in every case, so the ONLY variable is the role, and therefore the only thing these tests can
/// be measuring is guard 2. A caller who is a patient while holding an attorney role is
/// synthetic; it is chosen precisely because it isolates the variable under test.</para>
///
/// <para>Synthetic data only, and no blob is ever written (HIPAA).</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class PacketKindVisibilityTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly IAppointmentPacketsAppService _packets;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly ICurrentPrincipalAccessor _principalAccessor;
    private readonly ICurrentTenant _currentTenant;

    public PacketKindVisibilityTests()
    {
        _packets = GetRequiredService<IAppointmentPacketsAppService>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    /// <summary>
    /// GUARDIAN. Break `AppointmentPacketsAppService.cs:131` and this fails naming the extra
    /// kinds a Patient received.
    /// </summary>
    [Fact]
    public async Task GetListByAppointmentAsync_ForAPatientParty_ReturnsThePatientKindOnly()
    {
        var kinds = await ListKindsAsAsync("Patient");

        kinds.ShouldBe(
            new[] { PacketKind.Patient },
            "a Patient may see only the Patient packet. All three kinds exist on this "
            + "appointment, so any extra kind here is the service failing to apply "
            + "PacketVisibility.AllowedKinds -- not a fixture artefact.");
    }

    /// <summary>
    /// GUARDIAN. The three external non-patient roles share one allow-list entry, so all three
    /// are asserted rather than one standing in for the others.
    /// </summary>
    [Theory]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    public async Task GetListByAppointmentAsync_ForAnAttorneyOrClaimExaminerParty_ReturnsTheAttyCeKindOnly(
        string role)
    {
        var kinds = await ListKindsAsAsync(role);

        kinds.ShouldBe(
            new[] { PacketKind.AttorneyClaimExaminer },
            $"{role} may see only the Attorney/Claim-Examiner packet. Seeing the Patient or "
            + "Doctor kind means the service is not filtering by the caller's allowed kinds.");
    }

    /// <summary>
    /// <b>CONTROL, NOT A GUARDIAN. Do not count this as another guarantee.</b> An internal caller
    /// is allowed all three kinds, so this assertion cannot detect the break the other tests exist
    /// for. That is not a prediction: in the 2026-09-08 break run it was <b>the one test of five
    /// that PASSED</b> while the four guardians failed.
    ///
    /// <para>It earns its place by pinning the FIXTURE rather than the filter. Without it a reader
    /// cannot tell whether the guardians above pass because filtering works or because only one
    /// packet kind was ever seeded -- and the second of those would make them vacuous. If this
    /// test ever fails, suspect the seed helper before the production code.</para>
    /// </summary>
    [Fact]
    public async Task GetListByAppointmentAsync_ForAnInternalCaller_ReturnsAllThreeKinds_Control()
    {
        var kinds = await ListKindsAsAsync("Intake Staff");

        kinds.ShouldBe(
            new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer },
            "this is the fixture check: all three kinds must be present and visible to an "
            + "internal caller, otherwise the restrictive assertions above prove nothing");
    }

    /// <summary>
    /// Lists office A's packets as a caller holding <paramref name="role"/>, who is a party via
    /// the patient-identity pathway (see the class docstring), and returns the kinds ordered so
    /// the assertions do not depend on query order.
    /// </summary>
    private async Task<PacketKind[]> ListKindsAsAsync(string role)
    {
        var (officeA, _) = await GetSeededOfficesAsync();
        await SeedAllThreeKindsAsync(officeA);

        PacketKind[] kinds = Array.Empty<PacketKind>();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            using (WithCurrentUser.Run(_principalAccessor, officeA.BookerUserId, role))
            {
                var packets = await _packets.GetListByAppointmentAsync(officeA.AppointmentId);
                kinds = packets.Select(p => p.Kind).OrderBy(k => k).ToArray();
            }
        }, requiresNew: true);

        return kinds;
    }

    /// <summary>
    /// Ensures office A's shared appointment has a <c>Generated</c> packet of ALL THREE kinds.
    ///
    /// <para><b>THE FULL SET IS LOAD-BEARING, NOT SETUP NOISE. Do not "simplify" it.</b> A
    /// restrictive guarantee cannot be proven against a fixture that lacks the thing being
    /// excluded: with only a Patient packet seeded, a Patient caller sees exactly one row whether
    /// the filter is applied or deleted, and both guardians above pass with the production code
    /// broken. This is the same defect as phase 3 task 1's empty <c>ServiceCollection</c>, which
    /// asserted only what the code ADDED and was structurally blind to what it REMOVED.</para>
    ///
    /// <para>Insert-if-absent, for two independent reasons:
    /// <c>CaseEvaluationSharedModelConfiguration.cs:740</c> puts a UNIQUE filtered index on
    /// <c>(TenantId, AppointmentId, Kind)</c>, and this harness seeds its two offices ONCE PER
    /// RUN in process-wide static state (<c>CaseEvaluationMultiOfficeTestBase.cs:32-33</c>), so
    /// rows survive across every test in the collection. A plain insert would throw inside this
    /// helper on the second test -- a red test on exactly the right name for entirely the wrong
    /// reason.</para>
    ///
    /// <para>No blob is written: these tests read metadata only, and the harness has no reachable
    /// MinIO, so a seeding attempt would die here rather than in the test.</para>
    /// </summary>
    private Task SeedAllThreeKindsAsync(SeededOffice office) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office.OfficeId))
            {
                var db = await _dbContextProvider.GetDbContextAsync();
                foreach (var kind in new[]
                         {
                             PacketKind.Patient,
                             PacketKind.Doctor,
                             PacketKind.AttorneyClaimExaminer,
                         })
                {
                    var present = await db.Set<AppointmentPacket>()
                        .AnyAsync(p => p.AppointmentId == office.AppointmentId && p.Kind == kind);
                    if (present)
                    {
                        continue;
                    }

                    db.Set<AppointmentPacket>().Add(new AppointmentPacket(
                        id: Guid.NewGuid(),
                        tenantId: office.OfficeId,
                        appointmentId: office.AppointmentId,
                        kind: kind,
                        blobName: $"synthetic/packet-kind-visibility-{kind}.pdf",
                        status: PacketGenerationStatus.Generated));
                }

                await db.SaveChangesAsync();
            }
        }, requiresNew: true);
}
