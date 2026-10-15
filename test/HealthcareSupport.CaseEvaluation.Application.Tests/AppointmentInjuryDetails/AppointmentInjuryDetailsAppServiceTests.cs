using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

/// <summary>
/// Covers <see cref="IAppointmentInjuryDetailsAppService"/>. The only prior test in this area was
/// AppointmentInjuryDetailWcabAdjUnitTests, which asserts a domain rule rather than the service;
/// the 32.3% reported was incidental execution.
///
/// This entity is the parent of AppointmentBodyPart, which is why its fixture was added to TestBase
/// rather than built inline -- see AppointmentInjuryDetailsTestData.
///
/// EVERY CONSTRUCTION PASSES WcabAdj EVEN THOUGH THE DTO ALLOWS IT TO BE ABSENT. The domain
/// constructor runs Check.NotNullOrWhiteSpace on it despite declaring it optional with a null
/// default, so omitting it throws from a layer below the one under test. Recorded at the fixture
/// and logged to the backlog.
///
/// Facts create their own rows and filter by AppointmentId or a token; nothing asserts a
/// TotalCount, because the rig shares one SQLite connection and never rolls back.
///
/// NO AUTHORIZATION FACTS -- AddAlwaysAllowAuthorization() makes every [Authorize] a no-op, and
/// this class deliberately carries a MIX of [Authorize] and [Authorize(...Default)] which the test
/// rig cannot distinguish.
/// </summary>
public abstract class AppointmentInjuryDetailsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentInjuryDetailsAppService _service;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentInjuryDetailsAppServiceTests()
    {
        _service = GetRequiredService<IAppointmentInjuryDetailsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    private static string Token(string label) => $"TEST-aid-{label}-{Guid.NewGuid():N}"[..40];

    private async Task<AppointmentInjuryDetailDto> CreateAsync(string claimNumber)
    {
        return await _service.CreateAsync(new AppointmentInjuryDetailCreateDto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            DateOfInjury = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            ClaimNumber = claimNumber,
            IsCumulativeInjury = false,
            BodyPartsSummary = "TEST-neck",
            WcabAdj = "TEST-ADJ-NEW"
        });
    }

    [Fact]
    public async Task CreateAsync_PersistsAgainstTheAppointment()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var claim = Token("create");

            var created = await CreateAsync(claim);

            created.Id.ShouldNotBe(Guid.Empty);
            created.ClaimNumber.ShouldBe(claim);
            created.AppointmentId.ShouldBe(
                AppointmentsTestData.Appointment1Id,
                "the injury detail must hang off the appointment it was created against.");
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsTheDetailThatWasCreated()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var claim = Token("get");
            var created = await CreateAsync(claim);

            var fetched = await _service.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.ClaimNumber.ShouldBe(claim);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByAppointmentId_ReturnsOnlyThatAppointmentsDetails()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("list"));

            var result = await _service.GetListAsync(new GetAppointmentInjuryDetailsInput
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                MaxResultCount = 1000
            });

            result.Items.Any(x => x.AppointmentInjuryDetail.Id == created.Id).ShouldBeTrue();
            result.Items.ShouldAllBe(
                x => x.AppointmentInjuryDetail.AppointmentId == AppointmentsTestData.Appointment1Id,
                "the AppointmentId filter must exclude every other appointment's injury details.");
        }
    }

    [Fact]
    public async Task GetByAppointmentIdAsync_ReturnsTheDetailsForThatAppointment()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("byappt"));

            var result = await _service.GetByAppointmentIdAsync(AppointmentsTestData.Appointment1Id);

            result.Any(x => x.AppointmentInjuryDetail.Id == created.Id).ShouldBeTrue(
                "a detail created against this appointment must be returned by the by-appointment "
                + "read the Claim Information modal uses.");
            result.ShouldAllBe(
                x => x.AppointmentInjuryDetail.AppointmentId == AppointmentsTestData.Appointment1Id);
        }
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_HydratesTheWcabOfficeJoin()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Uses the SEEDED Detail1, which is the row that carries a WcabOfficeId. Detail2 leaves
            // it null deliberately, so this Fact would be vacuous against that one.
            //
            // BOTH scopes are needed: Detail1 lives in TenantA, while WcabOffice is IMultiTenant
            // and seeded as a HOST row, so inside TenantA alone the office is filtered out and the
            // join hydrates as null -- the Fact would fail against a perfectly good join.
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var result = await _service.GetWithNavigationPropertiesAsync(
                    AppointmentInjuryDetailsTestData.Detail1Id);

                result.AppointmentInjuryDetail.Id.ShouldBe(AppointmentInjuryDetailsTestData.Detail1Id);
                result.WcabOffice.ShouldNotBeNull(
                    "Detail1 was seeded WITH a WcabOfficeId, so the navigation property must "
                    + "hydrate; null here means the join is not being loaded.");
                result.WcabOffice!.Id.ShouldBe(WcabOfficesTestData.Office1Id);
            }
        }
    }

    [Fact]
    public async Task GetWcabOfficeLookupAsync_FilteredByName_ReturnsTheSeededOffice()
    {
        // No tenant wrap: WcabOffice is IMultiTenant and seeded as a host row, so a query from
        // inside a tenant returns nothing and the Fact would fail for a reason unrelated to the
        // lookup it names.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _service.GetWcabOfficeLookupAsync(new LookupRequestDto
            {
                Filter = WcabOfficesTestData.Office1Name,
                MaxResultCount = 1000
            });

            result.Items.ShouldContain(
                x => x.Id == WcabOfficesTestData.Office1Id,
                "this lookup carries only plain [Authorize] so any authenticated booker can reach "
                + "it; the seeded office must come back.");
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheStoredClaimNumber()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("upd-before"));
            var after = Token("upd-after");

            var updated = await _service.UpdateAsync(created.Id, new AppointmentInjuryDetailUpdateDto
            {
                AppointmentId = created.AppointmentId,
                DateOfInjury = created.DateOfInjury,
                ClaimNumber = after,
                IsCumulativeInjury = created.IsCumulativeInjury,
                // Literals rather than echoing the fetched DTO: the read DTO exposes these as
                // nullable while the update DTO requires them, so echoing is a null-reference
                // assignment the compiler is right to reject.
                BodyPartsSummary = "TEST-neck",
                WcabAdj = "TEST-ADJ-NEW",
                ConcurrencyStamp = created.ConcurrencyStamp
            });

            updated.ClaimNumber.ShouldBe(after);

            var refetched = await _service.GetAsync(created.Id);
            refetched.ClaimNumber.ShouldBe(
                after,
                "the update must be persisted, not merely reflected in the returned DTO.");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete"));

            await _service.DeleteAsync(created.Id);

            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(created.Id));
        }
    }

    [Fact]
    public async Task CreateAsync_WithEmptyAppointmentId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                () => _service.CreateAsync(new AppointmentInjuryDetailCreateDto
                {
                    AppointmentId = Guid.Empty,
                    DateOfInjury = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
                    ClaimNumber = Token("create-guard"),
                    IsCumulativeInjury = false,
                    BodyPartsSummary = "TEST-neck",
                    WcabAdj = "TEST-ADJ-GUARD"
                }));
        }
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyAppointmentId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("upd-guard"));

            await Should.ThrowAsync<UserFriendlyException>(
                () => _service.UpdateAsync(created.Id, new AppointmentInjuryDetailUpdateDto
                {
                    AppointmentId = Guid.Empty,
                    DateOfInjury = created.DateOfInjury,
                    ClaimNumber = Token("upd-guard-claim"),
                    IsCumulativeInjury = created.IsCumulativeInjury,
                    BodyPartsSummary = "TEST-neck",
                    WcabAdj = "TEST-ADJ-GUARD",
                    ConcurrencyStamp = created.ConcurrencyStamp
                }));
        }
    }
}
