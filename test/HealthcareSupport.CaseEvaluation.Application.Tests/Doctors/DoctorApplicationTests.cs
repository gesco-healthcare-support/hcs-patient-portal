using System;
using System.Linq;
using Shouldly;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public abstract class DoctorsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule> where TStartupModule : IAbpModule
{
    private readonly IDoctorsAppService _doctorsAppService;
    private readonly IRepository<Doctor, Guid> _doctorRepository;

    public DoctorsAppServiceTests()
    {
        _doctorsAppService = GetRequiredService<IDoctorsAppService>();
        _doctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
    }

    [Fact]
    public async Task GetListAsync()
    {
        // Act
        var result = await _doctorsAppService.GetListAsync(new GetDoctorsInput());
        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.Any(x => x.Doctor.Id == Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67")).ShouldBe(true);
        result.Items.Any(x => x.Doctor.Id == Guid.Parse("b6d53903-5956-47fe-a12d-02982664ed4f")).ShouldBe(true);
    }

    [Fact]
    public async Task GetAsync()
    {
        // Act
        var result = await _doctorsAppService.GetAsync(Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"));
        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"));
    }

    [Fact]
    public async Task CreateAsync()
    {
        // Arrange
        var input = new DoctorCreateDto
        {
            FirstName = "c014822702a54810a377d172f55e915329a52881e14c4dbb90",
            LastName = "b54dcc63c7d74c90af5b316d936dc9d2ed673f2df76d417390",
            Email = "27ff91b42eed448e91265@e00ce97ffe31409791156.com",
            Gender = default
        };
        // Act
        var serviceResult = await _doctorsAppService.CreateAsync(input);
        // Assert
        var result = await _doctorRepository.FindAsync(c => c.Id == serviceResult.Id);
        result.ShouldNotBe(null);
        result.FirstName.ShouldBe("c014822702a54810a377d172f55e915329a52881e14c4dbb90");
        result.LastName.ShouldBe("b54dcc63c7d74c90af5b316d936dc9d2ed673f2df76d417390");
        result.Email.ShouldBe("27ff91b42eed448e91265@e00ce97ffe31409791156.com");
        result.Gender.ShouldBe(default);
    }

    [Fact]
    public async Task UpdateAsync()
    {
        // Arrange
        var input = new DoctorUpdateDto()
        {
            FirstName = "7ecabf274da4454ea567089e82eb4445bf17ff277cfd4ef5bf",
            LastName = "8ebad7371dd3486b92cb3c8fe35d6aa8ae60dfe41f24489da3",
            Email = "626ec684c0084734b43ed@cf38013ab1b448b58871f.com",
            Gender = default
        };
        // Act
        var serviceResult = await _doctorsAppService.UpdateAsync(Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"), input);
        // Assert
        var result = await _doctorRepository.FindAsync(c => c.Id == serviceResult.Id);
        result.ShouldNotBe(null);
        result.FirstName.ShouldBe("7ecabf274da4454ea567089e82eb4445bf17ff277cfd4ef5bf");
        result.LastName.ShouldBe("8ebad7371dd3486b92cb3c8fe35d6aa8ae60dfe41f24489da3");
        result.Email.ShouldBe("626ec684c0084734b43ed@cf38013ab1b448b58871f.com");
        result.Gender.ShouldBe(default);
    }

    [Fact]
    public async Task DeleteAsync()
    {
        // Act
        await _doctorsAppService.DeleteAsync(Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"));
        // Assert
        var result = await _doctorRepository.FindAsync(c => c.Id == Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"));
        result.ShouldBeNull();
    }
}