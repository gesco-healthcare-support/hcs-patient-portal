using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public abstract class AppointmentTypesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentTypesAppService _appointmentTypesAppService;
    private readonly IAppointmentTypeRepository _appointmentTypeRepository;
    private readonly AppointmentTypeManager _appointmentTypeManager;

    protected AppointmentTypesAppServiceTests()
    {
        _appointmentTypesAppService = GetRequiredService<IAppointmentTypesAppService>();
        _appointmentTypeRepository = GetRequiredService<IAppointmentTypeRepository>();
        _appointmentTypeManager = GetRequiredService<AppointmentTypeManager>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- AppointmentType is host-only; no CurrentTenant wrap.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededAppointmentType1()
    {
        var result = await _appointmentTypesAppService.GetAsync(AppointmentTypesTestData.AppointmentType1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(AppointmentTypesTestData.AppointmentType1Id);
        result.Name.ShouldBe(AppointmentTypesTestData.AppointmentType1Name);
        // AppointmentType1 was seeded with no Description (null) to exercise
        // the optional-field branch alongside AppointmentType2.
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededAppointmentTypes()
    {
        var result = await _appointmentTypesAppService.GetListAsync(new GetAppointmentTypesInput
        {
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentTypesTestData.AppointmentType1Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentTypesTestData.AppointmentType2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FiltersByName_ReturnsMatchingType()
    {
        var result = await _appointmentTypesAppService.GetListAsync(new GetAppointmentTypesInput
        {
            Name = "Orthopedic",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentTypesTestData.AppointmentType2Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentTypesTestData.AppointmentType1Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewAppointmentType_WithDescription()
    {
        var input = new AppointmentTypeCreateDto
        {
            Name = "TEST-Neurological",
            Description = "TEST-Neurological-Description"
        };

        var created = await _appointmentTypesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);
        created.Description.ShouldBe(input.Description);

        var persisted = await _appointmentTypeRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewAppointmentType_WithNullDescription()
    {
        // Optional-field path: Description is nullable; a null value must
        // bypass the `Check.Length` guard in the manager (Check.Length passes
        // a null string through without throwing).
        var input = new AppointmentTypeCreateDto
        {
            Name = "TEST-NoDescriptionType",
            Description = null
        };

        var created = await _appointmentTypesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Description.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        var existing = await _appointmentTypeRepository.GetAsync(AppointmentTypesTestData.AppointmentType2Id);
        var update = new AppointmentTypeUpdateDto
        {
            Name = "TEST-MutatedOrthopedic",
            Description = existing.Description
        };

        var result = await _appointmentTypesAppService.UpdateAsync(AppointmentTypesTestData.AppointmentType2Id, update);
        result.Name.ShouldBe("TEST-MutatedOrthopedic");

        // Restore seed value.
        var current = await _appointmentTypeRepository.GetAsync(AppointmentTypesTestData.AppointmentType2Id);
        current.Name = AppointmentTypesTestData.AppointmentType2Name;
        await _appointmentTypeRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAppointmentType()
    {
        var created = await _appointmentTypesAppService.CreateAsync(new AppointmentTypeCreateDto
        {
            Name = "TEST-ToDeleteType"
        });

        await _appointmentTypesAppService.DeleteAsync(created.Id);

        (await _appointmentTypeRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNameExceedsMax_ThrowsArgumentException()
    {
        // NameMaxLength = 100. A 101-char name triggers Check.Length(name, max=100).
        var oversizedName = new string('a', 101);

        await Should.ThrowAsync<ArgumentException>(() =>
            _appointmentTypeManager.CreateAsync(oversizedName));
    }

    // ------------------------------------------------------------------------
    // Permission-gap encoding (GAP: AppointmentTypesAppService class-level
    // [Authorize] is generic; feature-specific Default permission exists in
    // CaseEvaluationPermissions.AppointmentTypes but is NOT enforced on Read
    // methods. Create/Edit/Delete are correctly permission-gated).
    // ------------------------------------------------------------------------

    [Fact(Skip = "IP1 (2026-06-03): the read guard is now in place -- "
              + "AppointmentTypesAppService carries [Authorize(...AppointmentTypes.Default)] at "
              + "class level, so GetAsync/GetListAsync require the Default permission in "
              + "production. A LIVE permission-denied assertion is not possible in-process "
              + "because the test base registers AddAlwaysAllowAuthorization "
              + "(CaseEvaluationTestBaseModule), which makes every [Authorize] succeed "
              + "regardless of granted permissions. Enforcement is verified by the attribute "
              + "plus the Staff-Supervisor role-grant seed query.")]
    public Task GetAsync_WhenCallerLacksDefaultPermission_ShouldThrow()
    {
        // The guard exists (class-level [Authorize(...AppointmentTypes.Default)]). Under
        // AddAlwaysAllowAuthorization the authorization interceptor always succeeds, so the
        // caller-lacks-permission path cannot be exercised in-process; kept as documentation
        // of the intended production behavior.
        return Task.CompletedTask;
    }
}
