using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

/// <summary>
/// Covers <see cref="IAppointmentBodyPartsAppService"/>, which had no test of any kind before this
/// tranche -- the 29.4% it reported was constructor and top-level lines executed while other tests
/// ran, not assertions about behaviour.
///
/// AppointmentBodyPart is IMultiTenant and hangs off AppointmentInjuryDetail, so every Fact runs
/// inside TenantA where AppointmentInjuryDetailsTestData.Detail1Id is seeded.
///
/// EVERY FACT CREATES ITS OWN ROWS AND FILTERS BY A UNIQUE TOKEN. The rig shares one SQLite
/// connection across the whole collection and never rolls back, so a Fact asserting a total count
/// or a list position would pass or fail according to what ran before it. Nothing below asserts on
/// rows it did not create.
///
/// NO AUTHORIZATION FACTS. AddAlwaysAllowAuthorization() runs in the test module, so every
/// [Authorize] on this service is a no-op and a Fact claiming a non-permitted caller is refused
/// could not fail.
/// </summary>
public abstract class AppointmentBodyPartsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentBodyPartsAppService _bodyPartsAppService;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentBodyPartsAppServiceTests()
    {
        _bodyPartsAppService = GetRequiredService<IAppointmentBodyPartsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string Token(string label) => $"TEST-bodypart-{label}-{Guid.NewGuid():N}";

    private async Task<AppointmentBodyPartDto> CreateAsync(string description)
    {
        return await _bodyPartsAppService.CreateAsync(new AppointmentBodyPartCreateDto
        {
            AppointmentInjuryDetailId = AppointmentInjuryDetailsTestData.Detail1Id,
            BodyPartDescription = description
        });
    }

    [Fact]
    public async Task CreateAsync_PersistsThePartAgainstTheInjuryDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var description = Token("create");

            var created = await CreateAsync(description);

            created.ShouldNotBeNull();
            created.Id.ShouldNotBe(Guid.Empty);
            created.BodyPartDescription.ShouldBe(description);
            created.AppointmentInjuryDetailId.ShouldBe(
                AppointmentInjuryDetailsTestData.Detail1Id,
                "the created part must hang off the injury detail it was created against, or the "
                + "parent link is silently dropped and body parts orphan.");
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsThePartThatWasCreated()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var description = Token("get");
            var created = await CreateAsync(description);

            var fetched = await _bodyPartsAppService.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.BodyPartDescription.ShouldBe(description);
            fetched.AppointmentInjuryDetailId.ShouldBe(AppointmentInjuryDetailsTestData.Detail1Id);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByInjuryDetailId_IncludesOnlyPartsOfThatDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var description = Token("list");
            var created = await CreateAsync(description);

            var result = await _bodyPartsAppService.GetListAsync(new GetAppointmentBodyPartsInput
            {
                AppointmentInjuryDetailId = AppointmentInjuryDetailsTestData.Detail1Id,
                MaxResultCount = 1000
            });

            // Asserts on the row this Fact created, never on TotalCount -- the collection shares
            // one database and accumulates, so a count assertion here would be decided by whatever
            // ran first.
            result.Items.Any(x => x.Id == created.Id).ShouldBeTrue(
                "a part created against this injury detail must appear when the list is filtered "
                + "to it.");
            result.Items.ShouldAllBe(
                x => x.AppointmentInjuryDetailId == AppointmentInjuryDetailsTestData.Detail1Id,
                "the AppointmentInjuryDetailId filter must exclude every other detail's parts; if "
                + "it does not, the WhereIf is not being applied.");
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheDescription()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-before"));
            var after = Token("update-after");

            var updated = await _bodyPartsAppService.UpdateAsync(created.Id, new AppointmentBodyPartUpdateDto
            {
                AppointmentInjuryDetailId = AppointmentInjuryDetailsTestData.Detail1Id,
                BodyPartDescription = after
            });

            updated.BodyPartDescription.ShouldBe(after);

            var refetched = await _bodyPartsAppService.GetAsync(created.Id);
            refetched.BodyPartDescription.ShouldBe(
                after,
                "the update must be persisted, not merely reflected in the returned DTO.");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesThePart()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete"));

            await _bodyPartsAppService.DeleteAsync(created.Id);

            await Should.ThrowAsync<EntityNotFoundException>(
                () => _bodyPartsAppService.GetAsync(created.Id));
        }
    }

    // ------------------------------------------------------------------------
    // The guards. Both are `input.AppointmentInjuryDetailId == Guid.Empty` at the top of the
    // method, ahead of any repository call.
    //
    // The exception TYPE is asserted, not merely that something threw. A bare
    // Should.ThrowAsync<Exception> would be satisfied by an unrelated failure from a layer above
    // the guard -- which is exactly how three of eleven upload tests passed against code that
    // never ran, in #949.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithEmptyInjuryDetailId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                () => _bodyPartsAppService.CreateAsync(new AppointmentBodyPartCreateDto
                {
                    AppointmentInjuryDetailId = Guid.Empty,
                    BodyPartDescription = Token("create-guard")
                }));
        }
    }

    [Fact]
    public async Task UpdateAsync_WithEmptyInjuryDetailId_ThrowsUserFriendlyException()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-guard"));

            await Should.ThrowAsync<UserFriendlyException>(
                () => _bodyPartsAppService.UpdateAsync(created.Id, new AppointmentBodyPartUpdateDto
                {
                    AppointmentInjuryDetailId = Guid.Empty,
                    BodyPartDescription = Token("update-guard-after")
                }));
        }
    }
}
