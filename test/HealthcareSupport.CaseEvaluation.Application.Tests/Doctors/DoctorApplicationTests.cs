using System;
using System.Linq;
using Shouldly;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public abstract class DoctorsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule> where TStartupModule : IAbpModule
{
    private readonly IDoctorsAppService _doctorsAppService;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorsAppServiceTests()
    {
        _doctorsAppService = GetRequiredService<IDoctorsAppService>();
        _doctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync()
    {
        // Act
        var result = await _doctorsAppService.GetListAsync(new GetDoctorsInput());
        // Assert
        result.TotalCount.ShouldBe(2);
        result.Items.Count.ShouldBe(2);
        result.Items.Any(x => x.Doctor.Id == DoctorsTestData.Doctor1Id).ShouldBe(true);
        result.Items.Any(x => x.Doctor.Id == DoctorsTestData.Doctor2Id).ShouldBe(true);
    }

    [Fact]
    public async Task GetAsync()
    {
        // DoctorsAppService.GetAsync does not disable the IMultiTenant filter,
        // so looking up a tenant-scoped Doctor requires running inside that
        // tenant's context. Both seeded doctors live in TenantA (see orchestrator).
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Act
            var result = await _doctorsAppService.GetAsync(DoctorsTestData.Doctor1Id);
            // Assert
            result.ShouldNotBeNull();
            result.Id.ShouldBe(DoctorsTestData.Doctor1Id);
        }
    }

    [Fact]
    public async Task CreateAsync()
    {
        // Arrange
        const string firstName = "c014822702a54810a377d172f55e915329a52881e14c4dbb90";
        const string lastName = "b54dcc63c7d74c90af5b316d936dc9d2ed673f2df76d417390";
        const string email = "27ff91b42eed448e91265@e00ce97ffe31409791156.com";
        var input = new DoctorCreateDto
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Gender = default
        };
        // Act
        var serviceResult = await _doctorsAppService.CreateAsync(input);
        // Assert
        var result = await _doctorRepository.FindAsync(c => c.Id == serviceResult.Id);
        result.ShouldNotBeNull();
        result.FirstName.ShouldBe(firstName);
        result.LastName.ShouldBe(lastName);
        result.Email.ShouldBe(email);
        result.Gender.ShouldBe(default);
    }

    [Fact]
    public async Task UpdateAsync()
    {
        // Arrange
        const string firstName = "7ecabf274da4454ea567089e82eb4445bf17ff277cfd4ef5bf";
        const string lastName = "8ebad7371dd3486b92cb3c8fe35d6aa8ae60dfe41f24489da3";
        const string email = "626ec684c0084734b43ed@cf38013ab1b448b58871f.com";
        var input = new DoctorUpdateDto()
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Gender = default
        };
        // Wrap in TenantA context because Doctor1 is seeded there and
        // DoctorsAppService.UpdateAsync does not disable the IMultiTenant filter.
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // Act
            var serviceResult = await _doctorsAppService.UpdateAsync(DoctorsTestData.Doctor1Id, input);
            // Assert
            var result = await _doctorRepository.FindAsync(c => c.Id == serviceResult.Id);
            result.ShouldNotBeNull();
            result.FirstName.ShouldBe(firstName);
            result.LastName.ShouldBe(lastName);
            result.Email.ShouldBe(email);
            result.Gender.ShouldBe(default);
        }
    }

    [Fact]
    public async Task DeleteAsync()
    {
        // Act
        await _doctorsAppService.DeleteAsync(DoctorsTestData.Doctor1Id);
        // Assert
        var result = await _doctorRepository.FindAsync(c => c.Id == DoctorsTestData.Doctor1Id);
        result.ShouldBeNull();
    }
}