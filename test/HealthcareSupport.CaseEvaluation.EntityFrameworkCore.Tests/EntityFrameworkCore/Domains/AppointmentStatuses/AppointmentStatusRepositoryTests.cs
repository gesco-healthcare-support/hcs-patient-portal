using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentStatusRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentStatusRepository _statusRepository;

    public AppointmentStatusRepositoryTests()
    {
        _statusRepository = GetRequiredService<IAppointmentStatusRepository>();
    }

    [Fact]
    public async Task GetListAsync_PlainOverload_AppliesFilterText()
    {
        // The custom IAppointmentStatusRepository.GetListAsync(filterText)
        // overload is what AppointmentStatusesAppService.GetListAsync delegates
        // to. Confirm it scopes the result to matching rows only.
        await WithUnitOfWorkAsync(async () =>
        {
            var results = await _statusRepository.GetListAsync(filterText: "Approved");

            results.Any(x => x.Id == AppointmentStatusesTestData.Status2Id).ShouldBeTrue();
            results.Any(x => x.Id == AppointmentStatusesTestData.Status1Id).ShouldBeFalse();
        });
    }
}
