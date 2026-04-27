using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentTypeRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentTypeRepository _appointmentTypeRepository;

    public AppointmentTypeRepositoryTests()
    {
        _appointmentTypeRepository = GetRequiredService<IAppointmentTypeRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesNameFilter()
    {
        // The custom IAppointmentTypeRepository.GetListAsync(name) overload is
        // what AppointmentTypesAppService.GetListAsync delegates to. Confirm it
        // filters by Name + returns only the matching type row.
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _appointmentTypeRepository.GetListAsync(name: "Orthopedic");

            results.Any(x => x.Id == AppointmentTypesTestData.AppointmentType2Id).ShouldBeTrue();
            results.All(x => x.Name == AppointmentTypesTestData.AppointmentType2Name).ShouldBeTrue();
        });
    }
}
