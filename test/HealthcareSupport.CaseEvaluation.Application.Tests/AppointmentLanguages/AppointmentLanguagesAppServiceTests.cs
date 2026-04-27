using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public abstract class AppointmentLanguagesAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentLanguagesAppService _languagesAppService;
    private readonly IAppointmentLanguageRepository _languageRepository;
    private readonly AppointmentLanguageManager _languageManager;

    protected AppointmentLanguagesAppServiceTests()
    {
        _languagesAppService = GetRequiredService<IAppointmentLanguagesAppService>();
        _languageRepository = GetRequiredService<IAppointmentLanguageRepository>();
        _languageManager = GetRequiredService<AppointmentLanguageManager>();
    }

    // ------------------------------------------------------------------------
    // CRUD happy path -- AppointmentLanguage is host-only; no CurrentTenant wrap.
    // ------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_ReturnsSeededLanguage1()
    {
        var result = await _languagesAppService.GetAsync(AppointmentLanguagesTestData.Language1Id);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(AppointmentLanguagesTestData.Language1Id);
        result.Name.ShouldBe(AppointmentLanguagesTestData.Language1Name);
    }

    [Fact]
    public async Task GetListAsync_ReturnsAllSeededLanguages()
    {
        var result = await _languagesAppService.GetListAsync(new GetAppointmentLanguagesInput
        {
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentLanguagesTestData.Language1Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentLanguagesTestData.Language2Id).ShouldBeTrue();
    }

    [Fact]
    public async Task GetListAsync_FiltersByFilterText()
    {
        var result = await _languagesAppService.GetListAsync(new GetAppointmentLanguagesInput
        {
            FilterText = "Spanish",
            MaxResultCount = 100
        });

        result.Items.Any(x => x.Id == AppointmentLanguagesTestData.Language2Id).ShouldBeTrue();
        result.Items.Any(x => x.Id == AppointmentLanguagesTestData.Language1Id).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_PersistsNewLanguage()
    {
        var input = new AppointmentLanguageCreateDto { Name = "TEST-Mandarin" };

        var created = await _languagesAppService.CreateAsync(input);

        created.ShouldNotBeNull();
        created.Name.ShouldBe(input.Name);

        var persisted = await _languageRepository.FindAsync(created.Id);
        persisted.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesMutableFields()
    {
        var update = new AppointmentLanguageUpdateDto { Name = "TEST-MutatedSpanish" };

        var result = await _languagesAppService.UpdateAsync(AppointmentLanguagesTestData.Language2Id, update);
        result.Name.ShouldBe("TEST-MutatedSpanish");

        // Restore seed value.
        var current = await _languageRepository.GetAsync(AppointmentLanguagesTestData.Language2Id);
        current.Name = AppointmentLanguagesTestData.Language2Name;
        await _languageRepository.UpdateAsync(current);
    }

    [Fact]
    public async Task DeleteAsync_RemovesLanguage()
    {
        var created = await _languagesAppService.CreateAsync(new AppointmentLanguageCreateDto
        {
            Name = "TEST-ToDeleteLanguage"
        });

        await _languagesAppService.DeleteAsync(created.Id);

        (await _languageRepository.FindAsync(created.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNameExceedsMax_ThrowsArgumentException()
    {
        // NameMaxLength = 50. A 51-char name triggers Check.Length.
        var oversizedName = new string('a', 51);

        await Should.ThrowAsync<ArgumentException>(() =>
            _languageManager.CreateAsync(oversizedName));
    }
}
