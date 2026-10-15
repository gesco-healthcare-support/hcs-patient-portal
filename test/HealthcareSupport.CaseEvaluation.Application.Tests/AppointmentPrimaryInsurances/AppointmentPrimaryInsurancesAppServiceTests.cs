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

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

/// <summary>
/// Covers <see cref="IAppointmentPrimaryInsurancesAppService"/>, which had no test of any kind
/// before this tranche -- the 34.4% reported was incidental execution, not assertions.
///
/// Same shape as AppointmentClaimExaminersAppServiceTests deliberately: the two services are
/// near-identical claim-party surfaces over different entities, and keeping the Facts parallel
/// makes a divergence between them visible rather than buried.
///
/// The entity is IMultiTenant and hangs off Appointment1 in TenantA. Every Fact creates its own
/// rows; nothing asserts a TotalCount, because the rig shares one SQLite connection and never
/// rolls back.
///
/// NO AUTHORIZATION FACTS -- AddAlwaysAllowAuthorization() makes every [Authorize] a no-op.
/// </summary>
public abstract class AppointmentPrimaryInsurancesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentPrimaryInsurancesAppService _service;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentPrimaryInsurancesAppServiceTests()
    {
        _service = GetRequiredService<IAppointmentPrimaryInsurancesAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // CAPPED AT 40 because Name is StringLength(NameMaxLength) = 50 and the untruncated token ran
    // to 54 on the longer labels, which ABP's validator rejected before the service was reached.
    private static string Token(string label) => $"api-{label}-{Guid.NewGuid():N}"[..40];

    // The repo's [PhoneNumber] requires EXACTLY ten digits once punctuation is stripped. This one
    // must also fit PhoneNumberMaxLength = 12, which ten bare digits does.
    private const string TenDigitPhone = "2135550134";

    private async Task<AppointmentPrimaryInsuranceDto> CreateAsync(string name)
    {
        return await _service.CreateAsync(new AppointmentPrimaryInsuranceCreateDto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            Name = name,
            PhoneNumber = TenDigitPhone,
            City = "TEST-City",
            StateId = LocationsTestData.State1Id,
            IsActive = true
        });
    }

    [Fact]
    public async Task CreateAsync_PersistsAgainstTheAppointment()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = Token("create");

            var created = await CreateAsync(name);

            created.Id.ShouldNotBe(Guid.Empty);
            created.Name.ShouldBe(name);
            created.AppointmentId.ShouldBe(
                AppointmentsTestData.Appointment1Id,
                "the row must hang off the appointment it was created against.");
            created.IsActive.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsTheRowThatWasCreated()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = Token("get");
            var created = await CreateAsync(name);

            var fetched = await _service.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.Name.ShouldBe(name);
            fetched.StateId.ShouldBe(LocationsTestData.State1Id);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByAppointmentId_ReturnsOnlyThatAppointmentsRows()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("list"));

            var result = await _service.GetListAsync(new GetAppointmentPrimaryInsurancesInput
            {
                AppointmentId = AppointmentsTestData.Appointment1Id,
                MaxResultCount = 1000
            });

            result.Items.Any(x => x.Id == created.Id).ShouldBeTrue();
            result.Items.ShouldAllBe(
                x => x.AppointmentId == AppointmentsTestData.Appointment1Id,
                "the AppointmentId filter must exclude every other appointment's rows.");
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheStoredName()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-before"));
            var after = Token("update-after");

            var updated = await _service.UpdateAsync(created.Id, new AppointmentPrimaryInsuranceUpdateDto
            {
                AppointmentId = created.AppointmentId,
                Name = after,
                PhoneNumber = created.PhoneNumber,
                City = created.City,
                StateId = created.StateId,
                IsActive = created.IsActive,
                ConcurrencyStamp = created.ConcurrencyStamp
            });

            updated.Name.ShouldBe(after);

            var refetched = await _service.GetAsync(created.Id);
            refetched.Name.ShouldBe(
                after,
                "the update must be persisted, not merely reflected in the returned DTO.");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheRow()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete"));

            await _service.DeleteAsync(created.Id);

            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(created.Id));
        }
    }

    [Fact]
    public async Task GetStateLookupAsync_FilteredByName_ReturnsTheSeededState()
    {
        // No tenant wrap: State is IMultiTenant and seeded as a host row, so a query from inside a
        // tenant returns nothing and the Fact would fail for a reason unrelated to the lookup.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _service.GetStateLookupAsync(new LookupRequestDto
            {
                Filter = StatesTestData.State1Name,
                MaxResultCount = 1000
            });

            result.Items.ShouldContain(x => x.Id == StatesTestData.State1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WithEmptyAppointmentId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                () => _service.CreateAsync(new AppointmentPrimaryInsuranceCreateDto
                {
                    AppointmentId = Guid.Empty,
                    Name = Token("create-guard"),
                    IsActive = true
                }));
        }
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyAppointmentId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-guard"));

            await Should.ThrowAsync<UserFriendlyException>(
                () => _service.UpdateAsync(created.Id, new AppointmentPrimaryInsuranceUpdateDto
                {
                    AppointmentId = Guid.Empty,
                    Name = Token("update-guard-after"),
                    IsActive = true,
                    ConcurrencyStamp = created.ConcurrencyStamp
                }));
        }
    }
}
