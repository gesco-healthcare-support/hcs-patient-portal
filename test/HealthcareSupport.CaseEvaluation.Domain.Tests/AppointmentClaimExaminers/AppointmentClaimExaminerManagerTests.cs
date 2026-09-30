using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

/// <summary>
/// Domain-service tests for <see cref="AppointmentClaimExaminerManager"/>'s length guards.
///
/// Direct Manager tests rather than incidental reach: the DTO carries [StringLength] on the same
/// fields, so ABP's validator rejects an over-long value before the Manager is reached and these
/// branches are unreachable from the app-service surface.
///
/// ALL LENGTH GUARDS HERE PASS minLength 0, so null and empty are ACCEPTED -- only overflow throws.
/// The Facts below therefore assert overflow and never absence; a Fact asserting that null is
/// rejected would fail against correct code.
///
/// NOT TESTED, DELIBERATELY: `Check.NotNull(appointmentId, ...)` -- appointmentId is a Guid, a
/// value type, so it boxes non-null and that guard cannot fire. Logged to the backlog.
/// </summary>
public abstract class AppointmentClaimExaminerManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentClaimExaminerManager _manager;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentClaimExaminerManagerTests()
    {
        _manager = GetRequiredService<AppointmentClaimExaminerManager>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<AppointmentClaimExaminer> CreateAsync(
        string? name = "TEST-name",
        string? email = "examiner@test.local",
        string? phoneNumber = "2135550134",
        string? zip = "90013")
    {
        return WithUnitOfWorkAsync(() => _manager.CreateAsync(
            AppointmentsTestData.Appointment1Id,
            true,
            name,
            null,
            email,
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
            var name = $"TEST-ace-{Guid.NewGuid():N}"[..40];

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
            // The guards pass minLength 0, so absence is legal. This Fact pins that, because a
            // later reader could reasonably assume Check.Length rejects null and "tighten" it.
            var created = await CreateAsync(name: null, email: null, phoneNumber: null, zip: null);

            created.ShouldNotBeNull();
            created.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
        }
    }

    [Fact]
    public async Task CreateAsync_WithNameOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('N', AppointmentClaimExaminerConsts.NameMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(name: tooLong));

            ex.Message.ShouldContain("name");
        }
    }

    [Fact]
    public async Task CreateAsync_WithEmailOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('E', AppointmentClaimExaminerConsts.EmailMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(email: tooLong));

            ex.Message.ShouldContain("email");
        }
    }

    [Fact]
    public async Task CreateAsync_WithZipOverTheMaxLength_Throws()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var tooLong = new string('9', AppointmentClaimExaminerConsts.ZipMaxLength + 1);

            var ex = await Should.ThrowAsync<ArgumentException>(() => CreateAsync(zip: tooLong));

            ex.Message.ShouldContain("zip");
        }
    }
}
