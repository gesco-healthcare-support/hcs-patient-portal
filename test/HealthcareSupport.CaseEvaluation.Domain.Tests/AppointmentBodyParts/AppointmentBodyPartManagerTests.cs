using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

/// <summary>
/// Domain-service tests for <see cref="AppointmentBodyPartManager"/>'s argument guards.
///
/// Direct Manager tests rather than incidental reach: the DTO's [Required] and [StringLength] on
/// BodyPartDescription reject the same inputs at ABP's validator, so a request that would trip a
/// Check never reaches the Manager through the app service.
///
/// DB-touching calls run inside WithUnitOfWorkAsync so the repository's DbContext outlives them.
///
/// THESE FACTS PIN THE GUARANTEE, NOT THE LAYER. The description rules are enforced TWICE: here in
/// the Manager, and again in the entity constructor at AppointmentBodyPart.cs:30-31. Raising only
/// the Manager's cap left these Facts GREEN because the constructor still threw; raising BOTH
/// killed CreateAsync_WithDescriptionOverTheMaxLength_Throws by name. The Manager's Check.Length is
/// therefore REDUNDANT on this path -- a production observation logged to the backlog, recorded
/// here so a passing Fact is not misread as evidence that the Manager's own guard fires.
///
/// NOT TESTED, DELIBERATELY: `Check.NotNull(appointmentInjuryDetailId, ...)`. That parameter is a
/// Guid, a value type, so it boxes to a non-null object and the guard cannot fire for any input.
/// A Fact for it could not fail. Logged to the backlog.
/// </summary>
public abstract class AppointmentBodyPartManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentBodyPartManager _manager;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentBodyPartManagerTests()
    {
        _manager = GetRequiredService<AppointmentBodyPartManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<AppointmentBodyPart> CreateAsync(string description)
    {
        return WithUnitOfWorkAsync(() => _manager.CreateAsync(
            AppointmentInjuryDetailsTestData.Detail1Id,
            description));
    }

    [Fact]
    public async Task CreateAsync_WithAValidDescription_PersistsAgainstTheInjuryDetail()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var description = $"TEST-bp-{Guid.NewGuid():N}";

            var created = await CreateAsync(description);

            created.ShouldNotBeNull();
            created.BodyPartDescription.ShouldBe(description);
            created.AppointmentInjuryDetailId.ShouldBe(AppointmentInjuryDetailsTestData.Detail1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WithWhitespaceDescription_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync("   "));

            // Asserts the argument NAME rather than merely that something threw.
            ex.Message.ShouldContain("bodyPartDescription");
        }
    }

    [Fact]
    public async Task CreateAsync_WithDescriptionOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('D', AppointmentBodyPartConsts.BodyPartDescriptionMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(tooLong));

            ex.Message.ShouldContain("bodyPartDescription");
        }
    }
}
