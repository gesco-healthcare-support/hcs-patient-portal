using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

/// <summary>
/// Domain-service tests for <see cref="AppointmentPrimaryInsuranceManager"/>'s length guards.
/// Deliberately parallel to AppointmentClaimExaminerManagerTests -- the two Managers are
/// near-identical claim-party surfaces, and keeping the Facts in step makes a divergence between
/// them visible instead of buried.
///
/// All length guards pass minLength 0, so null and empty are ACCEPTED and only overflow throws.
///
/// NOT TESTED, DELIBERATELY: `Check.NotNull(appointmentId, ...)` -- a Guid boxes non-null, so that
/// guard cannot fire for any input. Logged to the backlog.
/// </summary>
public abstract class AppointmentPrimaryInsuranceManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentPrimaryInsuranceManager _manager;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentPrimaryInsuranceManagerTests()
    {
        _manager = GetRequiredService<AppointmentPrimaryInsuranceManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<AppointmentPrimaryInsurance> CreateAsync(
        string? name = "TEST-insurer",
        string? phoneNumber = "2135550134",
        string? zip = "90013")
    {
        return WithUnitOfWorkAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id,
            true,
            name,
            null,
            phoneNumber,
            null,
            null,
            null,
            zip,
            null));
    }

    [Fact]
    public async Task CreateAsync_WithValidArguments_PersistsAgainstTheAppointment()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var name = $"TEST-api-{Guid.NewGuid():N}"[..40];

            var created = await CreateAsync(name: name);

            created.ShouldNotBeNull();
            created.Name.ShouldBe(name);
            created.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WithNullOptionalStrings_IsAccepted()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var created = await CreateAsync(name: null, phoneNumber: null, zip: null);

            created.ShouldNotBeNull();
            created.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WithNameOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('N', AppointmentPrimaryInsuranceConsts.NameMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(name: tooLong));

            ex.Message.ShouldContain("name");
        }
    }

    [Fact]
    public async Task CreateAsync_WithPhoneNumberOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            // PhoneNumberMaxLength is 12 here, the tightest of the claim-party columns.
            var tooLong = new string('9', AppointmentPrimaryInsuranceConsts.PhoneNumberMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(phoneNumber: tooLong));

            ex.Message.ShouldContain("phoneNumber");
        }
    }

    [Fact]
    public async Task CreateAsync_WithZipOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('9', AppointmentPrimaryInsuranceConsts.ZipMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(zip: tooLong));

            ex.Message.ShouldContain("zip");
        }
    }
}
