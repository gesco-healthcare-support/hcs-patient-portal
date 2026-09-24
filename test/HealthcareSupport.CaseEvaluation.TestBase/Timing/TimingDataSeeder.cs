using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Timing;

/// <summary>
/// Issue #1031 measurement only (a draft PR that never merges). Replaces ABP's
/// <see cref="DataSeeder"/> so each seed contributor's time can be recorded separately.
/// </summary>
/// <remarks>
/// The shared-unit-of-work branch is ABP 10.0.2's <c>DataSeeder.SeedAsync</c> loop copied verbatim,
/// with one Stopwatch per contributor added. The same <see cref="UnitOfWorkAttribute"/> wraps it,
/// so the seeding behaves exactly as before. With no current record (any other rig) or a
/// separate-UoW request, it defers to the base class unchanged.
/// </remarks>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IDataSeeder))]
public class TimingDataSeeder : DataSeeder
{
    public TimingDataSeeder(IOptions<AbpDataSeedOptions> options, IServiceScopeFactory serviceScopeFactory)
        : base(options, serviceScopeFactory)
    {
    }

    [UnitOfWork]
    public override async Task SeedAsync(DataSeedContext context)
    {
        var record = PhaseClock.Current;
        if (record == null || context.Properties.ContainsKey(DataSeederExtensions.SeedInSeparateUow))
        {
            await base.SeedAsync(context);
            return;
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            foreach (var contributorType in Options.Contributors)
            {
                var started = Stopwatch.GetTimestamp();
                var contributor = (IDataSeedContributor)scope.ServiceProvider.GetRequiredService(contributorType);
                await contributor.SeedAsync(context);
                record.AddContributor(contributorType.Name, Stopwatch.GetTimestamp() - started);
            }
        }
    }
}
