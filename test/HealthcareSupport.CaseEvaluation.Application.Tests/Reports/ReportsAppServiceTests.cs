using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01: end-to-end behavior of the Appointment Request Report query over the
/// seeded data. The report reuses the shared appointment query but must redact
/// PHI on every row (SSN to last 4, DOB to birth year) and enforce the legacy
/// "enter a search value" guard. Seeded Appointment1 (TenantA, Pending) is
/// FK'd to Patient1, whose synthetic DOB is 1990-01-01.
/// </summary>
public abstract class ReportsAppServiceTests<TStartupModule> : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportsAppService _reportsAppService;
    private readonly ICurrentTenant _currentTenant;

    protected ReportsAppServiceTests()
    {
        _reportsAppService = GetRequiredService<IReportsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetListAsync_redacts_ssn_and_dob_on_each_row()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var result = await _reportsAppService.GetListAsync(new GetAppointmentReportInput
            {
                FilterText = AppointmentsTestData.Appointment1RequestConfirmationNumber,
            });

            var row = result.Items.ShouldHaveSingleItem();
            row.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);

            // SSN masked to last 4 -- the full value never leaves the service.
            row.SocialSecurityNumber.ShouldStartWith("***-**-");
            row.SocialSecurityNumber.ShouldNotBe(PatientsTestData.Patient1SocialSecurityNumber);
            row.SocialSecurityNumber!.ShouldEndWith(PatientsTestData.Patient1SocialSecurityNumber[^4..]);

            // DOB masked to the birth year only (synthetic DOB is 1990-01-01).
            row.DateOfBirth.ShouldBe("1990");

            // Name is shown in full -- the internal worklist needs it.
            row.PatientName!.ShouldContain(PatientsTestData.Patient1LastName);
        }
    }

    [Fact]
    public async Task GetListAsync_filters_by_status()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var pending = await _reportsAppService.GetListAsync(new GetAppointmentReportInput
            {
                AppointmentStatus = AppointmentStatusType.Pending,
            });
            pending.Items.ShouldContain(r => r.AppointmentId == AppointmentsTestData.Appointment1Id);

            var approved = await _reportsAppService.GetListAsync(new GetAppointmentReportInput
            {
                AppointmentStatus = AppointmentStatusType.Approved,
            });
            approved.Items.ShouldNotContain(r => r.AppointmentId == AppointmentsTestData.Appointment1Id);
        }
    }

    [Fact]
    public async Task GetListAsync_throws_when_no_filter_supplied()
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            await Should.ThrowAsync<UserFriendlyException>(
                async () => await _reportsAppService.GetListAsync(new GetAppointmentReportInput()));
        }
    }
}
