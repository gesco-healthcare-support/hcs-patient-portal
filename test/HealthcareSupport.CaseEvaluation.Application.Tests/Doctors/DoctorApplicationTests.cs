using System;
using System.Linq;
using Shouldly;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public abstract class DoctorsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule> where TStartupModule : IAbpModule
{
    private readonly IDoctorsAppService _doctorsAppService;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly IRepository<DoctorAvailability, Guid> _doctorAvailabilityRepository;
    private readonly IRepository<DoctorPreferredLocation> _doctorPreferredLocationRepository;
    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;

    protected DoctorsAppServiceTests()
    {
        _doctorsAppService = GetRequiredService<IDoctorsAppService>();
        _doctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
        _doctorAvailabilityRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _doctorPreferredLocationRepository = GetRequiredService<IRepository<DoctorPreferredLocation>>();
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // --- One-doctor-per-tenant invariant guards (PARITY-FLAG-NEW-006) ---
    //
    // The IMultiTenant filter is ON in this rig and isolates by tenant, so
    // dependent-bucket tests provision a dedicated tenant and seed only the
    // dependent under test. The seeded TenantA already has Doctor1 + 2 slots
    // + 1 appointment, which serves the "already has doctor" and "appointment
    // blocks delete" cases directly.

    [Fact]
    public async Task CreateAsync_WhenTenantAlreadyHasDoctor_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<BusinessException>(
                async () => await _doctorsAppService.CreateAsync(BuildDoctorInput()));
            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorOnePerTenantViolated);
        }
    }

    [Fact]
    public async Task CreateAsync_WhenTenantHasOnlySoftDeletedDoctor_Succeeds()
    {
        // Soft-delete TenantA's only doctor via the raw repository (the
        // AppService DeleteAsync would itself throw -- TenantA has dependents).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _doctorRepository.DeleteAsync(DoctorsTestData.Doctor1Id, autoSave: true);
            }
        });

        DoctorDto result;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            result = await _doctorsAppService.CreateAsync(BuildDoctorInput());
        }

        result.ShouldNotBeNull();
        result.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteAsync_WithNoDependents_Succeeds()
    {
        var tenantId = await ProvisionTenantAsync();

        Guid doctorId;
        using (_currentTenant.Change(tenantId))
        {
            doctorId = (await _doctorsAppService.CreateAsync(BuildDoctorInput())).Id;
            await _doctorsAppService.DeleteAsync(doctorId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var found = await _doctorRepository.FindAsync(x => x.Id == doctorId);
                found.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task DeleteAsync_WithDoctorAvailability_Throws()
    {
        var tenantId = await ProvisionTenantAsync();

        Guid doctorId;
        using (_currentTenant.Change(tenantId))
        {
            doctorId = (await _doctorsAppService.CreateAsync(BuildDoctorInput())).Id;
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _doctorAvailabilityRepository.InsertAsync(new DoctorAvailability(
                    id: Guid.NewGuid(),
                    locationId: LocationsTestData.Location1Id,
                    appointmentTypeId: null,
                    availableDate: new DateTime(2026, 1, 1),
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(9, 30),
                    bookingStatusId: BookingStatus.Available), autoSave: true);
            }
        });

        BusinessException ex;
        using (_currentTenant.Change(tenantId))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _doctorsAppService.DeleteAsync(doctorId));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents);
        ex.Data["entity"].ShouldBe("DoctorAvailability");
    }

    [Fact]
    public async Task DeleteAsync_WithAppointment_Throws()
    {
        // TenantA has Doctor1 + Appointment1 (and slots). Appointments are
        // probed first, so the surfaced bucket is "Appointment".
        BusinessException ex;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _doctorsAppService.DeleteAsync(DoctorsTestData.Doctor1Id));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents);
        ex.Data["entity"].ShouldBe("Appointment");
    }

    [Fact]
    public async Task DeleteAsync_WithActiveDoctorPreferredLocation_Throws()
    {
        var tenantId = await ProvisionTenantAsync();

        Guid doctorId;
        using (_currentTenant.Change(tenantId))
        {
            doctorId = (await _doctorsAppService.CreateAsync(BuildDoctorInput())).Id;
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _doctorPreferredLocationRepository.InsertAsync(new DoctorPreferredLocation(
                    doctorId: doctorId,
                    locationId: LocationsTestData.Location1Id,
                    tenantId: tenantId,
                    isActive: true), autoSave: true);
            }
        });

        BusinessException ex;
        using (_currentTenant.Change(tenantId))
        {
            ex = await Should.ThrowAsync<BusinessException>(
                async () => await _doctorsAppService.DeleteAsync(doctorId));
        }

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.DoctorCannotDeleteWithDependents);
        ex.Data["entity"].ShouldBe("DoctorPreferredLocation");
    }

    [Fact]
    public async Task DeleteAsync_WithOnlyInactivePreferredLocation_Succeeds()
    {
        var tenantId = await ProvisionTenantAsync();

        Guid doctorId;
        using (_currentTenant.Change(tenantId))
        {
            doctorId = (await _doctorsAppService.CreateAsync(BuildDoctorInput())).Id;
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _doctorPreferredLocationRepository.InsertAsync(new DoctorPreferredLocation(
                    doctorId: doctorId,
                    locationId: LocationsTestData.Location1Id,
                    tenantId: tenantId,
                    isActive: false), autoSave: true);
            }
        });

        using (_currentTenant.Change(tenantId))
        {
            await _doctorsAppService.DeleteAsync(doctorId);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var found = await _doctorRepository.FindAsync(x => x.Id == doctorId);
                found.ShouldBeNull();
            }
        });
    }

    private static DoctorCreateDto BuildDoctorInput()
    {
        return new DoctorCreateDto
        {
            FirstName = "a1b2c3d4e5f6a7b8c9",
            LastName = "d0e1f2a3b4c5d6e7f8",
            Email = "a9b8c7d6@e5f4a3b2.com",
            Gender = default
        };
    }

    private async Task<Guid> ProvisionTenantAsync()
    {
        var id = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var tenant = await _tenantManager.CreateAsync("TEST-doctor-guard-" + Guid.NewGuid().ToString("N"));
                await _tenantRepository.InsertAsync(tenant, autoSave: true);
                id = tenant.Id;
            }
        });
        return id;
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