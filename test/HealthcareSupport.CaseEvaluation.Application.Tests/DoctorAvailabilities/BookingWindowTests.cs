using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-10-12 -- <see cref="IDoctorAvailabilitiesAppService.GetBookingWindowAsync"/>, which the
/// booking calendar reads instead of fixed 3 / 60 / 90 copies. The point is that it follows the
/// OFFICE's own settings and the CALLER's role: an office that lowers its lead time must see those
/// dates open in the calendar, and an external user must get their type's horizon, not staff's.
/// </summary>
public abstract class BookingWindowTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IDoctorAvailabilitiesAppService _appService;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principalAccessor;

    protected BookingWindowTests()
    {
        _appService = GetRequiredService<IDoctorAvailabilitiesAppService>();
        _systemParameterRepository = GetRequiredService<ISystemParameterRepository>();
        _appointmentTypeRepository = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    [Fact]
    public async Task An_external_booker_gets_the_offices_lead_time_and_their_types_horizon()
    {
        var pqmeTypeId = await ArrangeOfficeAsync();

        var window = await AsAsync(
            IdentityUsersTestData.ApplicantAttorneyRoleName, () => _appService.GetBookingWindowAsync(pqmeTypeId));

        window.LeadTimeDays.ShouldBe(1);
        window.MaxDays.ShouldBe(30);
        window.CeilingDays.ShouldBe(75);
    }

    [Fact]
    public async Task Staff_get_the_internal_horizon_whatever_the_type()
    {
        var pqmeTypeId = await ArrangeOfficeAsync();

        var window = await AsAsync(
            IdentityUsersTestData.HostAdminRoleName, () => _appService.GetBookingWindowAsync(pqmeTypeId));

        window.LeadTimeDays.ShouldBe(1);
        window.MaxDays.ShouldBe(75);
        window.CeilingDays.ShouldBe(75);
    }

    [Fact]
    public async Task No_type_or_an_unknown_type_falls_back_to_the_other_horizon()
    {
        await ArrangeOfficeAsync();

        var none = await AsAsync(
            IdentityUsersTestData.PatientRoleName, () => _appService.GetBookingWindowAsync(null));
        var unknown = await AsAsync(
            IdentityUsersTestData.PatientRoleName, () => _appService.GetBookingWindowAsync(Guid.NewGuid()));

        none.MaxDays.ShouldBe(50);
        unknown.MaxDays.ShouldBe(50);
    }

    /// <summary>Gives TenantA distinctive numbers and a PQME-horizon type; returns the type id.</summary>
    private async Task<Guid> ArrangeOfficeAsync()
    {
        var typeId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var parameters = await _systemParameterRepository.GetCurrentTenantAsync();
                parameters.ShouldNotBeNull();
                parameters.AppointmentLeadTime = 1;
                parameters.AppointmentMaxTimePQME = 30;
                parameters.AppointmentMaxTimeAME = 40;
                parameters.AppointmentMaxTimeOTHER = 50;
                parameters.AppointmentMaxTimeInternal = 75;
                await _systemParameterRepository.UpdateAsync(parameters, autoSave: true);

                await _appointmentTypeRepository.InsertAsync(
                    new AppointmentType(
                        typeId,
                        $"TEST-Window-{typeId:N}",
                        maxTimeCategory: AppointmentMaxTimeCategory.Pqme),
                    autoSave: true);
            }
        });
        return typeId;
    }

    private async Task<BookingWindowDto> AsAsync(string role, Func<Task<BookingWindowDto>> action)
    {
        BookingWindowDto? result = null;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            using (WithCurrentUser.Run(_principalAccessor, Guid.NewGuid(), role))
            {
                result = await action();
            }
        });
        return result!;
    }
}
