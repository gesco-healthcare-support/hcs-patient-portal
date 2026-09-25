using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.States;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

/// <summary>
/// The parts of <see cref="WcabOfficesAppService"/> that <c>WcabOfficesAppServiceTests</c> does not
/// reach: the state lookup, the Excel export and the download token that guards it, and the two
/// bulk deletes.
/// </summary>
/// <remarks>
/// Every office row carries a per-test token, and every removal or refusal runs with a row it must
/// NOT touch present: a delete that ignored its ids or its filter would take that row too, and an
/// export check that ignored the token would accept the wrong one. All names are synthetic.
/// </remarks>
public abstract class WcabOfficesExportAndBulkTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IWcabOfficesAppService _offices;
    private readonly IRepository<WcabOffice, Guid> _repository;

    protected WcabOfficesExportAndBulkTests()
    {
        _offices = GetRequiredService<IWcabOfficesAppService>();
        _repository = GetRequiredService<IRepository<WcabOffice, Guid>>();
    }

    private Task<List<Guid>> InsertOfficesAsync(params string[] names) =>
        WithUnitOfWorkAsync(async () =>
        {
            var ids = new List<Guid>();
            foreach (var name in names)
            {
                var office = await _repository.InsertAsync(
                    new WcabOffice(Guid.NewGuid(), null, name, name[..Math.Min(name.Length, 10)], isActive: true, city: "Synthetic City"),
                    autoSave: true);
                ids.Add(office.Id);
            }

            return ids;
        });

    private Task<List<Guid>> RemainingAsync(IEnumerable<Guid> ids) =>
        WithUnitOfWorkAsync(async () => (await _repository.GetListAsync(o => ids.Contains(o.Id))).Select(o => o.Id).ToList());

    [Fact]
    public async Task The_state_lookup_returns_only_what_matches_the_filter()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await WithUnitOfWorkAsync(async () =>
        {
            var states = GetRequiredService<IRepository<State, Guid>>();
            await states.InsertAsync(new State(Guid.NewGuid(), $"Synthetic State {token}"), autoSave: true);
            await states.InsertAsync(new State(Guid.NewGuid(), "Synthetic Unmatched State"), autoSave: true);
        });

        var result = await WithUnitOfWorkAsync(() => _offices.GetStateLookupAsync(new LookupRequestDto { Filter = token, MaxResultCount = 10 }));

        result.TotalCount.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().DisplayName.ShouldBe($"Synthetic State {token}");
    }

    [Fact]
    public async Task An_issued_download_token_exports_the_filtered_offices_as_a_workbook()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        await InsertOfficesAsync($"Synthetic WCAB {token}");

        var issued = await WithUnitOfWorkAsync(() => _offices.GetDownloadTokenAsync());
        var file = await WithUnitOfWorkAsync(() => _offices.GetListAsExcelFileAsync(
            new WcabOfficeExcelDownloadDto { DownloadToken = issued.Token, FilterText = token }));

        issued.Token.ShouldNotBeNullOrWhiteSpace();
        file.FileName.ShouldBe("WcabOffices.xlsx");
        file.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var content = new MemoryStream();
        await file.GetStream().CopyToAsync(content);
        content.ToArray()[..2].ShouldBe(new byte[] { 0x50, 0x4B }, "an .xlsx workbook is a zip archive");
    }

    [Fact]
    public async Task A_download_token_that_was_not_issued_is_refused_even_while_another_is_valid()
    {
        // LOAD-BEARING: a real token exists, so a check that accepted any token would pass the fake.
        await WithUnitOfWorkAsync(() => _offices.GetDownloadTokenAsync());

        await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => _offices.GetListAsExcelFileAsync(
            new WcabOfficeExcelDownloadDto { DownloadToken = Guid.NewGuid().ToString("N") })));
    }

    [Fact]
    public async Task Deleting_by_ids_removes_only_those_offices()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await InsertOfficesAsync($"Synthetic Delete A {token}", $"Synthetic Delete B {token}", $"Synthetic Keep {token}");

        await WithUnitOfWorkAsync(() => _offices.DeleteByIdsAsync(new List<Guid> { ids[0], ids[1] }));

        (await RemainingAsync(ids)).ShouldBe(new[] { ids[2] });
    }

    [Fact]
    public async Task Deleting_all_that_match_a_filter_leaves_the_rest()
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await InsertOfficesAsync($"Synthetic Purge {token}", $"Synthetic Other {Guid.NewGuid():N}"[..30]);

        await WithUnitOfWorkAsync(() => _offices.DeleteAllAsync(new GetWcabOfficesInput { FilterText = token }));

        (await RemainingAsync(ids)).ShouldBe(new[] { ids[1] });
    }
}
