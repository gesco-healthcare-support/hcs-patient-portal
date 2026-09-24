using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Locations;

/// <summary>
/// The two lookups of <see cref="LocationsAppService"/> that <c>LocationsAppServiceTests</c> does
/// not reach: states and appointment types. Each runs with a non-matching row present, so a filter
/// that matched everything would fail. Host context; names are synthetic.
/// </summary>
public abstract class LocationsLookupTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ILocationsAppService _locations;

    protected LocationsLookupTests()
    {
        _locations = GetRequiredService<ILocationsAppService>();
    }

    [Fact]
    public async Task The_state_and_appointment_type_lookups_return_only_what_matches_the_filter()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await WithUnitOfWorkAsync(async () =>
        {
            var states = GetRequiredService<IRepository<State, Guid>>();
            await states.InsertAsync(new State(Guid.NewGuid(), $"Synthetic State {token}"), autoSave: true);
            await states.InsertAsync(new State(Guid.NewGuid(), "Synthetic Unmatched State"), autoSave: true);
            var types = GetRequiredService<IRepository<AppointmentType, Guid>>();
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), $"Synthetic Type {token}"), autoSave: true);
            await types.InsertAsync(new AppointmentType(Guid.NewGuid(), "Synthetic Unmatched Type"), autoSave: true);
        });

        var states = await WithUnitOfWorkAsync(() => _locations.GetStateLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));
        var types = await WithUnitOfWorkAsync(() => _locations.GetAppointmentTypeLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));

        states.TotalCount.ShouldBe(1);
        states.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic State {token}");
        types.TotalCount.ShouldBe(1);
        types.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic Type {token}");
    }
}
