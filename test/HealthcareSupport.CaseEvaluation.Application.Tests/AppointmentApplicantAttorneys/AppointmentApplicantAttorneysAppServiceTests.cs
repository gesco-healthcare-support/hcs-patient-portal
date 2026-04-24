using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public abstract class AppointmentApplicantAttorneysAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentApplicantAttorneysAppService _joinsAppService;
    private readonly IAppointmentApplicantAttorneyRepository _joinRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;

    protected AppointmentApplicantAttorneysAppServiceTests()
    {
        _joinsAppService = GetRequiredService<IAppointmentApplicantAttorneysAppService>();
        _joinRepository = GetRequiredService<IAppointmentApplicantAttorneyRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- Join1 lives in TenantA; wrap fetches in TenantA.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededJoin()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetAsync(AppointmentApplicantAttorneysTestData.Join1Id);

            result.ShouldNotBeNull();
            result.Id.ShouldBe(AppointmentApplicantAttorneysTestData.Join1Id);
            result.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
            result.ApplicantAttorneyId.ShouldBe(ApplicantAttorneysTestData.Attorney1Id);
            result.IdentityUserId.ShouldBe(IdentityUsersTestData.ApplicantAttorney1UserId);
        }
    }

    [Fact]
    public async Task GetListAsync_FromTenantAContext_ReturnsOnlyJoin1()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetListAsync(new GetAppointmentApplicantAttorneysInput());

            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task CreateAsync_PersistsNewJoin()
    {
        // Create in host context -> TenantId = null; use IDataFilter.Disable to
        // fetch it back without a tenant wrap in the assertion.
        var input = new AppointmentApplicantAttorneyCreateDto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
            IdentityUserId = IdentityUsersTestData.Patient1UserId
        };

        var created = await _joinsAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.AppointmentId.ShouldBe(input.AppointmentId);
        created.ApplicantAttorneyId.ShouldBe(input.ApplicantAttorneyId);
        created.IdentityUserId.ShouldBe(input.IdentityUserId);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var persisted = await _joinRepository.FindAsync(created.Id);
            persisted.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        // Exercises the concurrency-stamp path in AppointmentApplicantAttorneyManager.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var existing = await _joinRepository.GetAsync(AppointmentApplicantAttorneysTestData.Join1Id);
            var update = new AppointmentApplicantAttorneyUpdateDto
            {
                AppointmentId = existing.AppointmentId,
                ApplicantAttorneyId = existing.ApplicantAttorneyId,
                IdentityUserId = IdentityUsersTestData.Patient1UserId, // Flip the IdentityUser to prove the write landed.
                ConcurrencyStamp = existing.ConcurrencyStamp
            };

            var result = await _joinsAppService.UpdateAsync(AppointmentApplicantAttorneysTestData.Join1Id, update);
            result.IdentityUserId.ShouldBe(IdentityUsersTestData.Patient1UserId);

            // Restore seed value so downstream tests keep their baseline.
            var current = await _joinRepository.GetAsync(AppointmentApplicantAttorneysTestData.Join1Id);
            current.IdentityUserId = IdentityUsersTestData.ApplicantAttorney1UserId;
            await _joinRepository.UpdateAsync(current);
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesJoin()
    {
        var created = await _joinsAppService.CreateAsync(new AppointmentApplicantAttorneyCreateDto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            ApplicantAttorneyId = ApplicantAttorneysTestData.Attorney1Id,
            IdentityUserId = IdentityUsersTestData.Patient1UserId
        });

        await _joinsAppService.DeleteAsync(created.Id);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            (await _joinRepository.FindAsync(created.Id)).ShouldBeNull();
        }
    }

    // ------------------------------------------------------------------------
    // Cross-tenant isolation (IMultiTenant).
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetListAsync_FromTenantBContext_ReturnsOnlyJoin2()
    {
        using (_currentTenant.Change(TenantsTestData.TenantBRef))
        {
            var result = await _joinsAppService.GetListAsync(new GetAppointmentApplicantAttorneysInput());

            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join2Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join1Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetListAsync_FromHostContextWithFilterDisabled_ReturnsJoinsFromBothTenants()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var result = await _joinsAppService.GetListAsync(new GetAppointmentApplicantAttorneysInput());

            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join1Id).ShouldBeTrue();
            result.Items.Any(x => x.AppointmentApplicantAttorney.Id == AppointmentApplicantAttorneysTestData.Join2Id).ShouldBeTrue();
        }
    }

    // ------------------------------------------------------------------------
    // Lookup endpoints
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAppointmentLookupAsync_ReturnsAppointmentsInTenant()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetAppointmentLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 100
            });

            result.Items.Any(x => x.Id == AppointmentsTestData.Appointment1Id).ShouldBeTrue();
            // Appointment2 is TenantB -- must NOT leak into the TenantA lookup.
            result.Items.Any(x => x.Id == AppointmentsTestData.Appointment2Id).ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetApplicantAttorneyLookupAsync_ReturnsAttorneysInTenant()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _joinsAppService.GetApplicantAttorneyLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 100
            });

            result.Items.Any(x => x.Id == ApplicantAttorneysTestData.Attorney1Id).ShouldBeTrue();
            result.Items.Any(x => x.Id == ApplicantAttorneysTestData.Attorney2Id).ShouldBeFalse();
        }
    }
}
