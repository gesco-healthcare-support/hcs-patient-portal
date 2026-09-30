using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

/// <summary>
/// Domain-service tests for <see cref="AppointmentInjuryDetailManager"/>'s argument guards. Written
/// as direct Manager tests rather than left to incidental reach through the app service, because
/// the Check branches below are unreachable from that surface: the DTO's own [Required] and
/// [StringLength] rules reject the same inputs at ABP's validator, so a request that would trip a
/// Check never arrives.
///
/// EVERY DB-TOUCHING CALL RUNS INSIDE WithUnitOfWorkAsync so the repository's DbContext outlives
/// the call -- the same requirement LocationManagerTests records in its own docstring. A guard that
/// throws before any repository access does not need it, but wrapping uniformly keeps one shape.
///
/// THESE FACTS PIN THE GUARANTEE, NOT THE LAYER, AND MUTATION TESTING IS WHAT ESTABLISHED THAT.
/// The length rules are enforced TWICE: here in the Manager, and again in the entity constructor
/// at AppointmentInjuryDetail.cs:66-71. Raising only the Manager's cap left every Fact below GREEN,
/// because the constructor still threw. Raising BOTH caps killed
/// CreateAsync_WithClaimNumberOverTheMaxLength_Throws by name.
///
/// So the Manager's Check.Length calls are REDUNDANT on this path -- deleting them changes no
/// observable behaviour. That is a production observation, logged to the backlog rather than acted
/// on here, and it is recorded in this docstring so nobody later reads a passing Fact as evidence
/// that the Manager's own guard works.
///
/// WHAT IS DELIBERATELY NOT TESTED: `Check.NotNull(appointmentId, nameof(appointmentId))` at
/// CreateAsync and UpdateAsync. appointmentId is a Guid, a value type, so it boxes to a non-null
/// object and that guard CANNOT FIRE for any input. A Fact for it could not fail, which makes it
/// worse than no Fact at all. Logged to the backlog; the empty-Guid case is genuinely guarded one
/// layer up, in the app service, and is covered there.
/// </summary>
public abstract class AppointmentInjuryDetailManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentInjuryDetailManager _manager;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentInjuryDetailManagerTests()
    {
        _manager = GetRequiredService<AppointmentInjuryDetailManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static readonly DateTime InjuryDate = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);

    private const string ValidClaim = "TEST-CLAIM-MGR";
    private const string ValidSummary = "TEST-neck";
    private const string ValidAdj = "TEST-ADJ-MGR";

    private Task<AppointmentInjuryDetail> CreateAsync(
        string claimNumber = ValidClaim,
        string summary = ValidSummary,
        string? adj = ValidAdj)
    {
        return WithUnitOfWorkAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id,
            InjuryDate,
            claimNumber,
            false,
            summary,
            null,
            adj,
            null));
    }

    [Fact]
    public async Task CreateAsync_WithValidArguments_PersistsTheDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var claim = $"TEST-C-{Guid.NewGuid():N}"[..20];

            var created = await CreateAsync(claimNumber: claim);

            created.ShouldNotBeNull();
            created.ClaimNumber.ShouldBe(claim);
            created.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
        }
    }

    // ------------------------------------------------------------------------
    // claimNumber -- Check.NotNullOrWhiteSpace then Check.Length(50)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithWhitespaceClaimNumber_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(claimNumber: "   "));

            // Asserts the argument NAME, not merely that something threw: three separate strings
            // are guarded here, and without the name a caller cannot tell which one was rejected.
            ex.Message.ShouldContain("claimNumber");
        }
    }

    [Fact]
    public async Task CreateAsync_WithClaimNumberOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('C', AppointmentInjuryDetailConsts.ClaimNumberMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(claimNumber: tooLong));

            ex.Message.ShouldContain("claimNumber");
        }
    }

    // ------------------------------------------------------------------------
    // bodyPartsSummary -- Check.NotNullOrWhiteSpace then Check.Length(500)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithWhitespaceBodyPartsSummary_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(summary: "   "));

            ex.Message.ShouldContain("bodyPartsSummary");
        }
    }

    [Fact]
    public async Task CreateAsync_WithBodyPartsSummaryOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('B', AppointmentInjuryDetailConsts.BodyPartsSummaryMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(summary: tooLong));

            ex.Message.ShouldContain("bodyPartsSummary");
        }
    }

    // ------------------------------------------------------------------------
    // wcabAdj -- declared `string? wcabAdj = null` and then guarded with
    // Check.NotNullOrWhiteSpace, so the DEFAULT THE SIGNATURE ADVERTISES THROWS.
    // That is the finding this Fact pins: null is not an accepted value despite the signature.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithNullWcabAdj_Throws_DespiteTheParameterDefaultingToNull()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(adj: null));

            // wcabAdj defaults to null in the signature and is then rejected as null by the body.
            // This Fact exists to keep that contradiction visible rather than letting a reader
            // trust the default the signature advertises.
            ex.Message.ShouldContain("wcabAdj");
        }
    }

    [Fact]
    public async Task CreateAsync_WithWcabAdjOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('A', AppointmentInjuryDetailConsts.WcabAdjMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(adj: tooLong));

            ex.Message.ShouldContain("wcabAdj");
        }
    }
}
