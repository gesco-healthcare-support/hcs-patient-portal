using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.Saas;
using HealthcareSupport.CaseEvaluation.States;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="LocationDataSeedContributor"/>: the synthetic TEST office gets exactly one clinic,
/// once; any other office starts with none. A real practice adds its own locations on the
/// Locations page, so a seeded clinic there would be a fake address in a real office.
///
/// <para>The second case IS the decoy: an office that is not the TEST office. A seeder that ignored
/// which office it was seeding would give it a clinic too.</para>
/// </summary>
public class LocationSeedTests : SeedContributorTestBase
{
    private readonly LocationDataSeedContributor _seeder;
    private readonly IRepository<Location, Guid> _locations;
    private readonly IRepository<State, Guid> _states;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypes;

    public LocationSeedTests()
    {
        _seeder = GetRequiredService<LocationDataSeedContributor>();
        _locations = GetRequiredService<IRepository<Location, Guid>>();
        _states = GetRequiredService<IRepository<State, Guid>>();
        _appointmentTypes = GetRequiredService<IRepository<AppointmentType, Guid>>();
    }

    [Fact]
    public async Task TheTestOffice_GetsExactlyOneClinic_AndASecondRunAddsNone()
    {
        var practiceId = await CreatePracticeWithCatalogAsync(OfficeSeedData.TestOffice.TenantName);

        await SeedAsync(_seeder, new DataSeedContext(practiceId));
        await SeedAsync(_seeder, new DataSeedContext(practiceId));

        var clinic = (await InScopeAsync(practiceId, () => _locations.GetListAsync())).ShouldHaveSingleItem();
        clinic.Id.ShouldBe(CaseEvaluationSeedIds.Locations.TestClinic);
        clinic.Name.ShouldBe("TEST Clinic");
        clinic.TenantId.ShouldBe(practiceId);
        clinic.StateId.ShouldBe(CaseEvaluationSeedIds.States.California);
    }

    [Fact]
    public async Task AnyOtherOffice_GetsNoClinic()
    {
        var practiceId = await CreatePracticeWithCatalogAsync("TEST-practice-" + Guid.NewGuid().ToString("N")[..10]);

        await SeedAsync(_seeder, new DataSeedContext(practiceId));

        (await InScopeAsync(practiceId, () => _locations.GetListAsync())).ShouldBeEmpty();
    }

    [Fact]
    public async Task HostPass_WritesNothing()
    {
        var before = await InScopeAsync<long>(null, () => _locations.GetCountAsync());

        await SeedAsync(_seeder, new DataSeedContext(null));

        (await InScopeAsync<long>(null, () => _locations.GetCountAsync())).ShouldBe(before);
    }

    /// <summary>
    /// A practice holding the state and appointment type the seeded clinic references. Both are
    /// foreign keys, and in production the reference-data seeders put them in the office first.
    /// </summary>
    private async Task<Guid> CreatePracticeWithCatalogAsync(string name)
    {
        var practiceId = await CreatePracticeAsync(name);
        await InScopeAsync(practiceId, async () =>
        {
            await _states.InsertAsync(new State(CaseEvaluationSeedIds.States.California, "TEST-California", true), autoSave: true);
            await _appointmentTypes.InsertAsync(new AppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ame, "TEST-AME"), autoSave: true);
            return true;
        });
        return practiceId;
    }
}
