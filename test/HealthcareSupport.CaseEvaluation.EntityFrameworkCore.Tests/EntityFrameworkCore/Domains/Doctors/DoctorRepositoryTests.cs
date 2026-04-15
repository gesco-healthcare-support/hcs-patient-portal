using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Domains.Doctors;

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
            var result = await _doctorRepository.GetListAsync(firstName: "551551e068be423cb150129a2fb3fd1f0c6bc2ecc74145619f", lastName: "221de0f2b24843429fbb2b7101ced2cbcca103583b4d4cd89c", email: "7c7fa4aa54e94b09adf79@07d1fd7ead804f659d7d5.com");
            // Assert
            result.Count.ShouldBe(1);
            result.FirstOrDefault().ShouldNotBe(null);
            result.First().Id.ShouldBe(Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"));
        });
    }

    [Fact]
    public async Task GetCountAsync()
    {
        // Arrange
        await WithUnitOfWorkAsync(async () =>
        {
            // Act
            var result = await _doctorRepository.GetCountAsync(firstName: "b032f90ee6b14bec8ce85eb2c239d6779b0a5be0ee7a4dc2be", lastName: "1967da12b041453b9280d4befe7d582fe8e72d7b5a13447291", email: "eb5b574cbd18458f84700@a4260fb508044a75afd13.com");
            // Assert
            result.ShouldBe(1);
        });
    }
}