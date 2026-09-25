using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

/// <summary>
/// Domain-service tests for <see cref="ClaimExaminerManager"/>'s length guards.
///
/// Direct Manager tests rather than incidental reach. Two of these branches are genuinely
/// unreachable from the app service for DIFFERENT reasons, which is the argument for testing here:
/// Email carries [EmailAddress] on the DTO so an over-long junk string is rejected for its shape
/// before its length, and PhoneNumber carries the repo's [PhoneNumber] ten-digit rule, so an
/// over-long value never survives the validator either. The Manager's own Check.Length is the last
/// line of defence for any caller that is not the HTTP surface.
///
/// All guards pass minLength 0, so null and empty are ACCEPTED and only overflow throws.
/// </summary>
public abstract class ClaimExaminerManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ClaimExaminerManager _manager;
    private readonly ICurrentTenant _currentTenant;

    protected ClaimExaminerManagerTests()
    {
        _manager = GetRequiredService<ClaimExaminerManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<ClaimExaminer> CreateAsync(
        string? firstName = "TEST-First",
        string? email = "examiner@test.local",
        string? zipCode = "90013")
    {
        return WithUnitOfWorkAsync(() => _manager.CreateAsync(
            null,
            null,
            "2135550134",
            null,
            null,
            null,
            zipCode,
            email,
            firstName,
            "TEST-Last"));
    }

    [Fact]
    public async Task CreateAsync_WithValidArguments_PersistsTheExaminer()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var first = $"TEST-{Guid.NewGuid():N}"[..20];

            var created = await CreateAsync(firstName: first);

            created.ShouldNotBeNull();
            created.FirstName.ShouldBe(first);
            created.LastName.ShouldBe("TEST-Last");
        }
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalStrings_IsAccepted()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // minLength 0 on every guard means absence is legal. Pinned so a later reader does not
            // "tighten" Check.Length into a required-field check.
            var created = await CreateAsync(firstName: null, email: null, zipCode: null);

            created.ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task CreateAsync_WithFirstNameOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('F', ClaimExaminerConsts.FirstNameMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(firstName: tooLong));

            ex.Message.ShouldContain("firstName");
        }
    }

    [Fact]
    public async Task CreateAsync_WithEmailOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('E', ClaimExaminerConsts.EmailMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(email: tooLong));

            ex.Message.ShouldContain("email");
        }
    }

    [Fact]
    public async Task CreateAsync_WithZipCodeOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('9', ClaimExaminerConsts.ZipCodeMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(zipCode: tooLong));

            ex.Message.ShouldContain("zipCode");
        }
    }
}
