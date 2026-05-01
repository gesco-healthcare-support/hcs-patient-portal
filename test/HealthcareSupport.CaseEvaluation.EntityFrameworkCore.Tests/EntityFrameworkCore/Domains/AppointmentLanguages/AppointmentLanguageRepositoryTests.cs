using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentLanguageRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentLanguageRepository _languageRepository;

    public AppointmentLanguageRepositoryTests()
    {
        _languageRepository = GetRequiredService<IAppointmentLanguageRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesFilterText()
    {
        // The custom IAppointmentLanguageRepository.GetListAsync(filterText)
        // overload is what AppointmentLanguagesAppService.GetListAsync delegates
        // to. Confirm it scopes the result to matching rows only.
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _languageRepository.GetListAsync(filterText: "Spanish");

            results.Any(x => x.Id == AppointmentLanguagesTestData.Language2Id).ShouldBeTrue();
            results.Any(x => x.Id == AppointmentLanguagesTestData.Language1Id).ShouldBeFalse();
        });
    }
}
