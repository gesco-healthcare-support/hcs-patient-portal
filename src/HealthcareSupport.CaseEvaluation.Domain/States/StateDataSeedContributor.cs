using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.States;

/// <summary>
/// Seeds the 50 US states. Host-scoped (no IMultiTenant); idempotent via per-row
/// upsert-by-ID so future state additions do not require wiping existing rows.
/// California GUID matches <see cref="CaseEvaluationSeedIds.States.California"/>
/// because it is referenced by WcabOffice (Southern CA offices) and Location (demo clinics).
/// </summary>
public class StateDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<State, Guid> _stateRepository;

    public StateDataSeedContributor(IRepository<State, Guid> stateRepository)
    {
        _stateRepository = stateRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // Host-only: skip the per-tenant pass.
        if (context?.TenantId != null)
        {
            return;
        }

        foreach (var (name, id) in Seeds)
        {
            var existing = await _stateRepository.FindAsync(id);
            if (existing != null)
            {
                continue;
            }

            await _stateRepository.InsertAsync(new State(id, name), autoSave: false);
        }
    }

    private static readonly (string Name, Guid Id)[] Seeds =
    {
        ("Alabama",        new Guid("a0a00001-0000-4000-9000-000000000001")),
        ("Alaska",         new Guid("a0a00001-0000-4000-9000-000000000002")),
        ("Arizona",        new Guid("a0a00001-0000-4000-9000-000000000003")),
        ("Arkansas",       new Guid("a0a00001-0000-4000-9000-000000000004")),
        ("California",     CaseEvaluationSeedIds.States.California),
        ("Colorado",       new Guid("a0a00001-0000-4000-9000-000000000006")),
        ("Connecticut",    new Guid("a0a00001-0000-4000-9000-000000000007")),
        ("Delaware",       new Guid("a0a00001-0000-4000-9000-000000000008")),
        ("Florida",        new Guid("a0a00001-0000-4000-9000-000000000009")),
        ("Georgia",        new Guid("a0a00001-0000-4000-9000-00000000000a")),
        ("Hawaii",         new Guid("a0a00001-0000-4000-9000-00000000000b")),
        ("Idaho",          new Guid("a0a00001-0000-4000-9000-00000000000c")),
        ("Illinois",       new Guid("a0a00001-0000-4000-9000-00000000000d")),
        ("Indiana",        new Guid("a0a00001-0000-4000-9000-00000000000e")),
        ("Iowa",           new Guid("a0a00001-0000-4000-9000-00000000000f")),
        ("Kansas",         new Guid("a0a00001-0000-4000-9000-000000000010")),
        ("Kentucky",       new Guid("a0a00001-0000-4000-9000-000000000011")),
        ("Louisiana",      new Guid("a0a00001-0000-4000-9000-000000000012")),
        ("Maine",          new Guid("a0a00001-0000-4000-9000-000000000013")),
        ("Maryland",       new Guid("a0a00001-0000-4000-9000-000000000014")),
        ("Massachusetts",  new Guid("a0a00001-0000-4000-9000-000000000015")),
        ("Michigan",       new Guid("a0a00001-0000-4000-9000-000000000016")),
        ("Minnesota",      new Guid("a0a00001-0000-4000-9000-000000000017")),
        ("Mississippi",    new Guid("a0a00001-0000-4000-9000-000000000018")),
        ("Missouri",       new Guid("a0a00001-0000-4000-9000-000000000019")),
        ("Montana",        new Guid("a0a00001-0000-4000-9000-00000000001a")),
        ("Nebraska",       new Guid("a0a00001-0000-4000-9000-00000000001b")),
        ("Nevada",         new Guid("a0a00001-0000-4000-9000-00000000001c")),
        ("New Hampshire",  new Guid("a0a00001-0000-4000-9000-00000000001d")),
        ("New Jersey",     new Guid("a0a00001-0000-4000-9000-00000000001e")),
        ("New Mexico",     new Guid("a0a00001-0000-4000-9000-00000000001f")),
        ("New York",       new Guid("a0a00001-0000-4000-9000-000000000020")),
        ("North Carolina", new Guid("a0a00001-0000-4000-9000-000000000021")),
        ("North Dakota",   new Guid("a0a00001-0000-4000-9000-000000000022")),
        ("Ohio",           new Guid("a0a00001-0000-4000-9000-000000000023")),
        ("Oklahoma",       new Guid("a0a00001-0000-4000-9000-000000000024")),
        ("Oregon",         new Guid("a0a00001-0000-4000-9000-000000000025")),
        ("Pennsylvania",   new Guid("a0a00001-0000-4000-9000-000000000026")),
        ("Rhode Island",   new Guid("a0a00001-0000-4000-9000-000000000027")),
        ("South Carolina", new Guid("a0a00001-0000-4000-9000-000000000028")),
        ("South Dakota",   new Guid("a0a00001-0000-4000-9000-000000000029")),
        ("Tennessee",      new Guid("a0a00001-0000-4000-9000-00000000002a")),
        ("Texas",          new Guid("a0a00001-0000-4000-9000-00000000002b")),
        ("Utah",           new Guid("a0a00001-0000-4000-9000-00000000002c")),
        ("Vermont",        new Guid("a0a00001-0000-4000-9000-00000000002d")),
        ("Virginia",       new Guid("a0a00001-0000-4000-9000-00000000002e")),
        ("Washington",     new Guid("a0a00001-0000-4000-9000-00000000002f")),
        ("West Virginia",  new Guid("a0a00001-0000-4000-9000-000000000030")),
        ("Wisconsin",      new Guid("a0a00001-0000-4000-9000-000000000031")),
        ("Wyoming",        new Guid("a0a00001-0000-4000-9000-000000000032")),
    };
}
