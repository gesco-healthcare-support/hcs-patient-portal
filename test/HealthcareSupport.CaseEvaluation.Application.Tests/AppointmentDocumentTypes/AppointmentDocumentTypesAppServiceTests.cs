using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

/// <summary>
/// Covers <see cref="IAppointmentDocumentTypesAppService"/>, which reported 0.0% before this
/// tranche. Its Manager already sat at 89.1% via EfCoreAppointmentDocumentTypeManagerTests, so the
/// gap was entirely in the service surface.
///
/// THE APP SERVICE HAS NO GUARDS OF ITS OWN -- Create and Update delegate to
/// AppointmentDocumentTypeManager. So the Facts below assert round trips, the AppointmentTypeIds
/// projection, the UsageCount projection, and the two bulk deletes that exist only here.
///
/// DeleteAllAsync IS ONLY EVER CALLED WITH A FILTER THAT MATCHES THIS FACT'S OWN ROWS. It deletes
/// everything matching its input, and the rig shares one SQLite connection with no rollback across
/// the whole collection -- an unfiltered call here would delete other tests' data and the damage
/// would surface as unrelated failures elsewhere. The filter is the containment.
///
/// NO AUTHORIZATION FACTS -- AddAlwaysAllowAuthorization() makes every [Authorize] a no-op.
/// </summary>
public abstract class AppointmentDocumentTypesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentDocumentTypesAppService _service;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentDocumentTypesAppServiceTests()
    {
        _service = GetRequiredService<IAppointmentDocumentTypesAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string Token(string label) => $"TEST-adt-{label}-{Guid.NewGuid():N}";

    private async Task<AppointmentDocumentTypeDto> CreateAsync(string name, bool appliesToAll = false)
    {
        return await _service.CreateAsync(new AppointmentDocumentTypeCreateDto
        {
            Name = name,
            AppointmentTypeIds = appliesToAll
                ? new List<Guid>()
                : new List<Guid> { LocationsTestData.AppointmentType1Id },
            AppliesToAll = appliesToAll,
            IsActive = true
        });
    }

    [Fact]
    public async Task CreateAsync_PersistsTheTypeAndItsAppointmentTypeLinks()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = Token("create");

            var created = await CreateAsync(name);

            created.Id.ShouldNotBe(Guid.Empty);
            created.Name.ShouldBe(name);
            created.IsActive.ShouldBeTrue();
            created.AppointmentTypeIds.ShouldContain(
                LocationsTestData.AppointmentType1Id,
                "the join rows must be projected back onto the DTO; an empty list here means "
                + "MapWithAppointmentTypes is not reading the navigation collection.");
        }
    }

    [Fact]
    public async Task GetAsync_ReturnsTheTypeWithItsAppointmentTypeIds()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = Token("get");
            var created = await CreateAsync(name);

            var fetched = await _service.GetAsync(created.Id);

            fetched.Id.ShouldBe(created.Id);
            fetched.Name.ShouldBe(name);
            fetched.AppointmentTypeIds.ShouldContain(LocationsTestData.AppointmentType1Id);
        }
    }

    [Fact]
    public async Task GetListAsync_FilteredByAppointmentTypeId_IncludesTheLinkedType()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("list"));

            var result = await _service.GetListAsync(new GetAppointmentDocumentTypesInput
            {
                AppointmentTypeId = LocationsTestData.AppointmentType1Id,
                MaxResultCount = 1000
            });

            result.Items.Any(x => x.Id == created.Id).ShouldBeTrue(
                "a type linked to this appointment type must appear when the list is filtered to it.");
        }
    }

    [Fact]
    public async Task GetListAsync_PopulatesUsageCountForATypeNoDocumentUses()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("usage"));

            var result = await _service.GetListAsync(new GetAppointmentDocumentTypesInput
            {
                FilterText = created.Name,
                MaxResultCount = 1000
            });

            var row = result.Items.Single(x => x.Id == created.Id);
            row.UsageCount.ShouldBe(
                0,
                "UsageCount is counted per row against AppointmentDocument; a freshly created type "
                + "is used by nothing, and a null here means the count was never projected.");
        }
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheNameAndTheLinkedAppointmentTypes()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("update-before"));
            var after = Token("update-after");

            var updated = await _service.UpdateAsync(created.Id, new AppointmentDocumentTypeUpdateDto
            {
                Name = after,
                AppointmentTypeIds = new List<Guid>(),
                AppliesToAll = true,
                IsActive = false
            });

            updated.Name.ShouldBe(after);
            updated.AppliesToAll.ShouldBeTrue();
            updated.IsActive.ShouldBeFalse();
            updated.AppointmentTypeIds.ShouldBeEmpty(
                "clearing the id list must REMOVE the join rows. This assertion is only meaningful "
                + "because the fixture created the type WITH a link -- against an empty list it "
                + "would pass with the removal deleted.");

            var refetched = await _service.GetAsync(created.Id);
            refetched.Name.ShouldBe(after, "the update must be persisted, not merely returned.");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheType()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(Token("delete"));

            await _service.DeleteAsync(created.Id);

            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(created.Id));
        }
    }

    [Fact]
    public async Task DeleteByIdsAsync_RemovesEveryIdItWasGiven()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var first = await CreateAsync(Token("byids-1"));
            var second = await CreateAsync(Token("byids-2"));
            var survivor = await CreateAsync(Token("byids-survivor"));

            await _service.DeleteByIdsAsync(new List<Guid> { first.Id, second.Id });

            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(first.Id));
            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(second.Id));

            // The survivor is the half that makes this Fact non-vacuous: without it, a
            // DeleteByIds that deleted EVERYTHING would pass exactly the same way.
            var stillThere = await _service.GetAsync(survivor.Id);
            stillThere.Id.ShouldBe(survivor.Id);
        }
    }

    [Fact]
    public async Task DeleteAllAsync_FilteredByText_RemovesOnlyTheMatchingTypes()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var marker = Guid.NewGuid().ToString("N");
            var doomed = await CreateAsync($"TEST-adt-deleteall-{marker}");
            var survivor = await CreateAsync(Token("deleteall-survivor"));

            // FILTERED DELIBERATELY. DeleteAllAsync removes everything its input matches, and this
            // database is shared across the whole collection -- an unfiltered call would delete
            // other tests' rows and surface as failures with no connection to this Fact.
            await _service.DeleteAllAsync(new GetAppointmentDocumentTypesInput
            {
                FilterText = marker
            });

            await Should.ThrowAsync<EntityNotFoundException>(() => _service.GetAsync(doomed.Id));

            var stillThere = await _service.GetAsync(survivor.Id);
            stillThere.Id.ShouldBe(
                survivor.Id,
                "a type outside the filter must survive; if it does not, DeleteAll is ignoring its "
                + "input and the filter is decorative.");
        }
    }
}
