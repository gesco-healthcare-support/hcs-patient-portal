using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public abstract class AppointmentEmployerDetailsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentEmployerDetailsAppService _detailsAppService;
    private readonly IAppointmentEmployerDetailRepository _detailRepository;
    private readonly AppointmentEmployerDetailManager _detailManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentEmployerDetailsAppServiceTests()
    {
        _detailsAppService = GetRequiredService<IAppointmentEmployerDetailsAppService>();
        _detailRepository = GetRequiredService<IAppointmentEmployerDetailRepository>();
        _detailManager = GetRequiredService<AppointmentEmployerDetailManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- Detail1 lives in TenantA; wrap fetches in TenantA.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _detailsAppService.GetAsync(AppointmentEmployerDetailsTestData.Detail1Id);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(AppointmentEmployerDetailsTestData.Detail1Id);
            result.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
            result.StateId.ShouldBe(LocationsTestData.State1Id);
            result.EmployerName.ShouldBe(AppointmentEmployerDetailsTestData.Detail1EmployerName);
            result.Occupation.ShouldBe(AppointmentEmployerDetailsTestData.Detail1Occupation);
            result.PhoneNumber.ShouldBe(AppointmentEmployerDetailsTestData.Detail1PhoneNumber);
            result.Street.ShouldBe(AppointmentEmployerDetailsTestData.Detail1Street);
            result.City.ShouldBe(AppointmentEmployerDetailsTestData.Detail1City);
            result.ZipCode.ShouldBe(AppointmentEmployerDetailsTestData.Detail1ZipCode);
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsOnlyDetail1()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _detailsAppService.GetListAsync(new GetAppointmentEmployerDetailsInput());

            result.Items.Any(x => x.AppointmentEmployerDetail.Id == AppointmentEmployerDetailsTestData.Detail1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentEmployerDetail.Id == AppointmentEmployerDetailsTestData.Detail2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CreateAsync_PersistsNewDetail()
    {
        var input = new AppointmentEmployerDetailCreateDto
        {
            EmployerName = "TEST-NewEmployer",
            Occupation = "TEST-NewOccupation",
            PhoneNumber = "5551234567",
            Street = "TEST-Street",
            City = "TEST-City",
            ZipCode = "90210",
            AppointmentId = AppointmentsTestData.Appointment1Id,
            StateId = LocationsTestData.State1Id
        };

        var created = await _detailsAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.EmployerName.ShouldBe(input.EmployerName);
        created.Occupation.ShouldBe(input.Occupation);
        created.PhoneNumber.ShouldBe(input.PhoneNumber);
        created.AppointmentId.ShouldBe(input.AppointmentId);
        created.StateId.ShouldBe(input.StateId);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var persisted = await _detailRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var existing = await _detailRepository.GetAsync(AppointmentEmployerDetailsTestData.Detail1Id);
            var update = new AppointmentEmployerDetailUpdateDto
            {
                EmployerName = "TEST-UpdatedEmployer",
                Occupation = existing.Occupation,
                PhoneNumber = existing.PhoneNumber,
                Street = existing.Street,
                City = existing.City,
                ZipCode = existing.ZipCode,
                AppointmentId = existing.AppointmentId,
                StateId = existing.StateId,
                ConcurrencyStamp = existing.ConcurrencyStamp
            };

            var result = await _detailsAppService.UpdateAsync(AppointmentEmployerDetailsTestData.Detail1Id, update);
            result.EmployerName.ShouldBe("TEST-UpdatedEmployer");

            // Restore seed value.
            var current = await _detailRepository.GetAsync(AppointmentEmployerDetailsTestData.Detail1Id);
            current.EmployerName = AppointmentEmployerDetailsTestData.Detail1EmployerName;
            await _detailRepository.UpdateAsync(current);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesDetail()
    {
        var created = await _detailsAppService.CreateAsync(new AppointmentEmployerDetailCreateDto
        {
            EmployerName = "TEST-ToDelete",
            Occupation = "TEST-Occ",
            AppointmentId = AppointmentsTestData.Appointment1Id
        });

        await _detailsAppService.DeleteAsync(created.Id);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            (await _detailRepository.FindAsync(created.Id)).ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------------
    // Cross-tenant isolation.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsDetailsFromBothTenants()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _detailsAppService.GetListAsync(new GetAppointmentEmployerDetailsInput());

            result.Items.Any(x => x.AppointmentEmployerDetail.Id == AppointmentEmployerDetailsTestData.Detail1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentEmployerDetail.Id == AppointmentEmployerDetailsTestData.Detail2Id).ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------------
    // Validation -- the manager guards on EmployerName + Occupation.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WhenAppointmentIdIsEmpty_ThrowsUserFriendlyException()
    {
        // AppService Create has an explicit Guid.Empty check that surfaces a
        // user-friendly message before the manager runs. Locks in that entry
        // point rather than the manager-level guard.
        var input = new AppointmentEmployerDetailCreateDto
        {
            EmployerName = "TEST-X",
            Occupation = "TEST-Y",
            AppointmentId = Guid.Empty
        };

        await Should.ThrowAsync<UserFriendlyException>(() => _detailsAppService.CreateAsync(input));
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenEmployerNameIsWhiteSpace_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _detailManager.CreateAsync(
            appointmentId: AppointmentsTestData.Appointment1Id,
            stateId: null,
            employerName: "   ",
            occupation: "TEST-Occ"));
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenOccupationIsWhiteSpace_ThrowsArgumentException()
    {
        await Should.ThrowAsync<ArgumentException>(() => _detailManager.CreateAsync(
            appointmentId: AppointmentsTestData.Appointment1Id,
            stateId: null,
            employerName: "TEST-Emp",
            occupation: "   "));
    }

    // ------------------------------------------------------------------------
    // Nav-prop + lookup
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsDetailWithPopulatedState()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _detailsAppService.GetWithNavigationPropertiesAsync(AppointmentEmployerDetailsTestData.Detail1Id);

            result.ShouldNotBeNull();
            result.AppointmentEmployerDetail.Id.ShouldBe(AppointmentEmployerDetailsTestData.Detail1Id);
            result.State.ShouldNotBeNull();
            result.State!.Id.ShouldBe(LocationsTestData.State1Id);
        }
    }

    [Fact]
    public async Task GetStateLookupAsync_ReturnsSeededStates()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _detailsAppService.GetStateLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 100
            });

            result.Items.Any(x => x.Id == LocationsTestData.State1Id).ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------------
    // Permission-gap encoding (GAP: AppointmentEmployerDetailsAppService
    // CreateAsync and UpdateAsync use generic [Authorize] instead of the
    // feature-specific Create/Edit permissions. Only DeleteAsync is enforced).
    // ------------------------------------------------------------------------

    [Fact(Skip = "GAP: AppointmentEmployerDetailsAppService Create/Update use generic "
              + "[Authorize]; feature-specific Create/Edit permissions exist in "
              + "CaseEvaluationPermissions.AppointmentEmployerDetails but are NOT "
              + "enforced. Only DeleteAsync uses the specific permission. When the "
              + "AppService gets [Authorize(...Create)] / [Authorize(...Edit)] this "
              + "test flips live. Tracked: src/.../Domain/AppointmentEmployerDetails/CLAUDE.md "
              + "Known Gotchas #2.")]
    public Task CreateAsync_WhenCallerLacksCreatePermission_ShouldThrow()
    {
        // Target behaviour: caller without CaseEvaluation.AppointmentEmployerDetails.Create
        // should get AbpAuthorizationException when calling CreateAsync. Today the
        // method only requires generic [Authorize] so any authenticated user creates.
        return Task.CompletedTask;
    }
}
