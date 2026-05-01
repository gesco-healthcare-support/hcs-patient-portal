using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

/// <summary>
/// Seeds the active Southern California WCAB district offices most commonly referenced
/// during workers'-comp IME scheduling. All rows reference California's State seed by ID.
/// Host-scoped (no IMultiTenant); idempotent via per-row upsert-by-ID.
/// Source: California DIR WCAB District Office directory
/// (https://www.dir.ca.gov/wcab/wcab_locations.html, accessed 2026-04-24).
/// </summary>
public class WcabOfficeDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<WcabOffice, Guid> _repository;

    public WcabOfficeDataSeedContributor(IRepository<WcabOffice, Guid> repository)
    {
        _repository = repository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context?.TenantId != null)
        {
            return;
        }

        foreach (var seed in Seeds)
        {
            var existing = await _repository.FindAsync(seed.Id);
            if (existing != null)
            {
                continue;
            }

            await _repository.InsertAsync(
                new WcabOffice(
                    id: seed.Id,
                    stateId: CaseEvaluationSeedIds.States.California,
                    name: seed.Name,
                    abbreviation: seed.Abbreviation,
                    isActive: true,
                    address: null,
                    city: seed.City,
                    zipCode: null),
                autoSave: false);
        }
    }

    private static readonly WcabSeed[] Seeds =
    {
        new(CaseEvaluationSeedIds.WcabOffices.Anaheim,       "WCAB Anaheim",        "ANA", "Anaheim"),
        new(CaseEvaluationSeedIds.WcabOffices.Bakersfield,   "WCAB Bakersfield",    "BAK", "Bakersfield"),
        new(CaseEvaluationSeedIds.WcabOffices.Glendale,      "WCAB Glendale",       "GLE", "Glendale"),
        new(CaseEvaluationSeedIds.WcabOffices.Irvine,        "WCAB Irvine",         "IRV", "Irvine"),
        new(CaseEvaluationSeedIds.WcabOffices.Riverside,     "WCAB Riverside",      "RIV", "Riverside"),
        new(CaseEvaluationSeedIds.WcabOffices.SanBernardino, "WCAB San Bernardino", "SBN", "San Bernardino"),
        new(CaseEvaluationSeedIds.WcabOffices.VanNuys,       "WCAB Van Nuys",       "VNY", "Van Nuys"),
    };

    private readonly record struct WcabSeed(Guid Id, string Name, string Abbreviation, string City);
}
