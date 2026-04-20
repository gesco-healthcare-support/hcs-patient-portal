using Shouldly;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.TestData;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Doctors;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class DoctorRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IDoctorRepository _doctorRepository;

    public DoctorRepositoryTests()
    {
        _doctorRepository = GetRequiredService<IDoctorRepository>();
    }

    [Fact]
    public async Task GetListAsync()
    {
        // Arrange
        await WithUnitOfWorkAsync(async () =>
        {
            // Act
            var result = await _doctorRepository.GetListAsync(
                firstName: DoctorsTestData.Doctor1FirstName,
                lastName: DoctorsTestData.Doctor1LastName,
                email: DoctorsTestData.Doctor1Email);
            // Assert
            result.Count.ShouldBe(1);
            result.FirstOrDefault().ShouldNotBe(null);
            result.First().Id.ShouldBe(DoctorsTestData.Doctor1Id);
        });
    }

    [Fact]
    public async Task GetCountAsync()
    {
        // Arrange
        await WithUnitOfWorkAsync(async () =>
        {
            // Act
            var result = await _doctorRepository.GetCountAsync(
                firstName: DoctorsTestData.Doctor2FirstName,
                lastName: DoctorsTestData.Doctor2LastName,
                email: DoctorsTestData.Doctor2Email);
            // Assert
            result.ShouldBe(1);
        });
    }
}