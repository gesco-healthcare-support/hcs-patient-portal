using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

/// <summary>
/// Seeds the canonical interpreter languages observed in OLD's PROD `AppointmentLanguages`
/// table -- the set of languages Southern California IME intake actually encounters.
/// Host-scoped (no IMultiTenant); idempotent via per-row upsert-by-ID.
/// </summary>
public class AppointmentLanguageDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<AppointmentLanguage, Guid> _repository;

    public AppointmentLanguageDataSeedContributor(IRepository<AppointmentLanguage, Guid> repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        foreach (var (id, name) in Seeds)
        {
            var existing = await _repository.FindAsync(id);
            if (existing != null)
            {
                continue;
            }

            await _repository.InsertAsync(new AppointmentLanguage(id, name), autoSave: false);
        }
    }

    private static readonly (Guid Id, string Name)[] Seeds =
    {
        (CaseEvaluationSeedIds.AppointmentLanguages.English,
            "English"),
        (new Guid("a0a00003-0000-4000-9000-000000000002"), "Spanish"),
        (new Guid("a0a00003-0000-4000-9000-000000000003"), "Vietnamese"),
        (new Guid("a0a00003-0000-4000-9000-000000000004"), "Korean"),
        (new Guid("a0a00003-0000-4000-9000-000000000005"), "Chinese Mandarin"),
        (new Guid("a0a00003-0000-4000-9000-000000000006"), "Chinese Cantonese"),
        (new Guid("a0a00003-0000-4000-9000-000000000007"), "Tagalog"),
        (new Guid("a0a00003-0000-4000-9000-000000000008"), "Russian"),
        (new Guid("a0a00003-0000-4000-9000-000000000009"), "Armenian"),
        (new Guid("a0a00003-0000-4000-9000-00000000000a"), "Portuguese"),
        (new Guid("a0a00003-0000-4000-9000-00000000000b"), "Japanese"),
        (new Guid("a0a00003-0000-4000-9000-00000000000c"), "Hmong"),
    };
}
