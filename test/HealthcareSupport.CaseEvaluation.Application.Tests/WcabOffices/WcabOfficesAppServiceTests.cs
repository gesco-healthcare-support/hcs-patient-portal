using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public abstract class WcabOfficesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IWcabOfficesAppService _officesAppService;
    private readonly IWcabOfficeRepository _officeRepository;
    private readonly WcabOfficeManager _officeManager;

    protected WcabOfficesAppServiceTests()
    {
        _officesAppService = GetRequiredService<IWcabOfficesAppService>();
        _officeRepository = GetRequiredService<IWcabOfficeRepository>();
        _officeManager = GetRequiredService<WcabOfficeManager>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- WcabOffice is host-only; no CurrentTenant wrap.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededOffice1_WithAllSevenFields()
    {
        var result = await _officesAppService.GetAsync(WcabOfficesTestData.Office1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(WcabOfficesTestData.Office1Id);
        result.Name.ShouldBe(WcabOfficesTestData.Office1Name);
        result.Abbreviation.ShouldBe(WcabOfficesTestData.Office1Abbreviation);
        result.Address.ShouldBe(WcabOfficesTestData.Office1Address);
        result.City.ShouldBe(WcabOfficesTestData.Office1City);
        result.ZipCode.ShouldBe(WcabOfficesTestData.Office1ZipCode);
        result.IsActive.ShouldBe(WcabOfficesTestData.Office1IsActive);
        result.StateId.ShouldBe(StatesTestData.State1Id);
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededOffices()
    {
        var result = await _officesAppService.GetListAsync(new GetWcabOfficesInput
        {
            MaxResultCount = 100
        });

        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office1Id).ShouldBeTrue();
        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FiltersByName_ReturnsMatchingOffice()
    {
        var result = await _officesAppService.GetListAsync(new GetWcabOfficesInput
        {
            Name = "Fresno",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office2Id).ShouldBeTrue();
        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office1Id).ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_FiltersByAbbreviation_ReturnsMatchingOffice()
    {
        var result = await _officesAppService.GetListAsync(new GetWcabOfficesInput
        {
            Abbreviation = "LAO",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office1Id).ShouldBeTrue();
        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office2Id).ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_FiltersByIsActiveTrue_ReturnsActiveOnly()
    {
        // Office2 is seeded with IsActive=false so this filter has a meaningful
        // exclusion case (test would be vacuous if both seeded rows were active).
        var result = await _officesAppService.GetListAsync(new GetWcabOfficesInput
        {
            IsActive = true,
            MaxResultCount = 100
        });

        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office1Id).ShouldBeTrue();
        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office2Id).ShouldBeFalse();
    }

    [Fact]
    public async Task GetListAsync_FiltersByStateId_ReturnsOfficesInState()
    {
        var result = await _officesAppService.GetListAsync(new GetWcabOfficesInput
        {
            StateId = StatesTestData.State1Id,
            MaxResultCount = 100
        });

        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office1Id).ShouldBeTrue();
        // Office2 has StateId=null so it must NOT match the State1 filter.
        result.Items.Any(x => x.WcabOffice.Id == WcabOfficesTestData.Office2Id).ShouldBeFalse();
    }

    // ------------------------------------------------------------------------
    // Nav-prop hydration -- both branches of the nullable StateId FK.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsOffice1_WithPopulatedState()
    {
        var result = await _officesAppService.GetWithNavigationPropertiesAsync(WcabOfficesTestData.Office1Id);

        result.ShouldNotBeNull();
        result.WcabOffice.Id.ShouldBe(WcabOfficesTestData.Office1Id);
        result.State.ShouldNotBeNull();
        result.State!.Id.ShouldBe(StatesTestData.State1Id);
    }

    [Fact]
    public async Task GetWithNavigationPropertiesAsync_ReturnsOffice2_WithNullState()
    {
        // Office2 has StateId=null. ABP Suite's auto-generated
        // GetWithNavigationPropertiesAsync historically used FirstOrDefault
        // even when the FK is required (Suite issue #9635); pinning the null
        // branch protects against regressions in the codegen.
        var result = await _officesAppService.GetWithNavigationPropertiesAsync(WcabOfficesTestData.Office2Id);

        result.ShouldNotBeNull();
        result.WcabOffice.Id.ShouldBe(WcabOfficesTestData.Office2Id);
        result.State.ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Mutation paths.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_PersistsNewOffice_WithAllFields()
    {
        var input = new WcabOfficeCreateDto
        {
            Name = "TEST-CreatedScratchOffice",
            Abbreviation = "TEST-CSO",
            Address = "TEST-456 Synthetic Ave",
            City = "TEST-Sacramento",
            ZipCode = "95814",
            IsActive = true,
            StateId = StatesTestData.State1Id
        };

        var created = await _officesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);
        created.Abbreviation.ShouldBe(input.Abbreviation);
        created.Address.ShouldBe(input.Address);
        created.City.ShouldBe(input.City);
        created.ZipCode.ShouldBe(input.ZipCode);
        created.IsActive.ShouldBe(input.IsActive);
        created.StateId.ShouldBe(input.StateId);

        var persisted = await _officeRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields_HappyConcurrencyStamp()
    {
        // Concurrency-stamp HAPPY path only. Mismatch behaviour is enforced
        // inside ABP's AbpEfCoreDbContext.SaveChangesAsync interceptor (see
        // ABP Concurrency Check docs); testing it would re-test the framework.
        var existing = await _officeRepository.GetAsync(WcabOfficesTestData.Office1Id);
        var update = new WcabOfficeUpdateDto
        {
            Name = "TEST-MutatedLosAngelesWcab",
            Abbreviation = existing.Abbreviation,
            Address = existing.Address,
            City = existing.City,
            ZipCode = existing.ZipCode,
            IsActive = existing.IsActive,
            StateId = existing.StateId,
            ConcurrencyStamp = existing.ConcurrencyStamp
        };

        var result = await _officesAppService.UpdateAsync(WcabOfficesTestData.Office1Id, update);
        result.Name.ShouldBe("TEST-MutatedLosAngelesWcab");

        // Restore seed value.
        var current = await _officeRepository.GetAsync(WcabOfficesTestData.Office1Id);
        current.Name = WcabOfficesTestData.Office1Name;
        await _officeRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOffice()
    {
        var created = await _officesAppService.CreateAsync(new WcabOfficeCreateDto
        {
            Name = "TEST-ToDeleteOffice",
            Abbreviation = "TEST-DEL",
            IsActive = true
        });

        await _officesAppService.DeleteAsync(created.Id);

        (await _officeRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    // ------------------------------------------------------------------------
    // Manager-level validation.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Manager_CreateAsync_WhenNameExceedsMax_ThrowsArgumentException()
    {
        // NameMaxLength = 50. A 51-char name triggers Check.Length, which
        // throws ArgumentException (verified directly in Volo.Abp.Core/Check.cs).
        var oversizedName = new string('a', 51);

        await Should.ThrowAsync<ArgumentException>(() => _officeManager.CreateAsync(
            stateId: null,
            name: oversizedName,
            abbreviation: "TEST-OK",
            isActive: true));
    }
}
