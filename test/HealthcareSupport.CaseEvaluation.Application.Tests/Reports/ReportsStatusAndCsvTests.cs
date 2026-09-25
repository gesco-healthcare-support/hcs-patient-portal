using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// The status counts and the CSV export of <see cref="ReportsAppService"/>, which
/// <c>ReportsAppServiceTests</c> does not reach, and their filter guards.
/// </summary>
/// <remarks>
/// The CSV test is also a PHI guard. Patient 1 HAS a full SSN on file, so an export that wrote the
/// unmasked value would fail it. Office A, seeded data only; office B's seeded appointment is inside
/// the date window, so the office boundary is what keeps it out.
/// </remarks>
public abstract class ReportsStatusAndCsvTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IReportsAppService _reports;
    private readonly ICurrentTenant _currentTenant;

    protected ReportsStatusAndCsvTests()
    {
        _reports = GetRequiredService<IReportsAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private Task<T> InOfficeA<T>(Func<Task<T>> call) => InOffice(TenantsTestData.TenantARef, call);

    private Task<T> InOfficeB<T>(Func<Task<T>> call) => InOffice(TenantsTestData.TenantBRef, call);

    private async Task<T> InOffice<T>(Guid officeId, Func<Task<T>> call)
    {
        using (_currentTenant.Change(officeId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    /// <summary>
    /// A date window that holds BOTH seeded appointments: office A's pending Appointment 1 and
    /// office B's approved Appointment 2.
    /// </summary>
    /// <remarks>
    /// LOAD-BEARING DECOY: Appointment 2 must sit inside this window. If the window excluded it, the
    /// date filter would drop it first and the "not office B" assertions below would pass with the
    /// office boundary removed. Do not narrow the window below Appointment 2's date.
    /// </remarks>
    private static GetAppointmentReportInput SpanningBothOffices() => new()
    {
        AppointmentDateMin = AppointmentsTestData.Appointment1Date.AddDays(-1),
        AppointmentDateMax = AppointmentsTestData.Appointment2Date.AddDays(1),
        MaxResultCount = 100,
    };

    [Fact]
    public async Task Status_counts_report_the_offices_pending_booking_in_the_date_range()
    {
        var counts = await InOfficeA(() => _reports.GetStatusCountsAsync(SpanningBothOffices()));

        counts.Single(c => c.Status == AppointmentStatusType.Pending).Count.ShouldBe(1);
        counts.ShouldNotContain(c => c.Status == AppointmentStatusType.Approved && c.Count > 0,
            "office B's approved appointment must not be counted in office A");

        // Control: the same window, read as office B, DOES count Appointment 2. So the date filter
        // lets it through, and only the office boundary keeps it out of office A above.
        var officeBCounts = await InOfficeB(() => _reports.GetStatusCountsAsync(SpanningBothOffices()));
        officeBCounts.Single(c => c.Status == AppointmentStatusType.Approved).Count.ShouldBe(1,
            "Appointment 2 must fall inside the window, or the office A assertion proves nothing");
    }

    [Fact]
    public async Task The_csv_export_lists_the_booking_and_never_the_full_ssn()
    {
        var file = await InOfficeA(() => _reports.GetReportCsvAsync(SpanningBothOffices()));

        file.ContentType.ShouldBe("text/csv");
        file.FileName.ShouldBe("appointment-request-report.csv");
        using var reader = new StreamReader(file.Content, Encoding.UTF8);
        var text = await reader.ReadToEndAsync();
        text.ShouldContain(AppointmentsTestData.Appointment1RequestConfirmationNumber);
        text.ShouldNotContain(PatientsTestData.Patient1SocialSecurityNumber);
        text.ShouldNotContain(AppointmentsTestData.Appointment2RequestConfirmationNumber);

        // Control: office B's export of the same window lists Appointment 2, so the office
        // boundary, not the date filter, is what kept it out of office A's file.
        var officeBFile = await InOfficeB(() => _reports.GetReportCsvAsync(SpanningBothOffices()));
        using var officeBReader = new StreamReader(officeBFile.Content, Encoding.UTF8);
        (await officeBReader.ReadToEndAsync()).ShouldContain(AppointmentsTestData.Appointment2RequestConfirmationNumber);
    }

    [Fact]
    public async Task Both_reads_need_a_filter_and_a_valid_date_range()
    {
        var empty = new GetAppointmentReportInput { MaxResultCount = 10 };
        var inverted = new GetAppointmentReportInput
        {
            AppointmentDateMin = AppointmentsTestData.Appointment1Date.AddDays(1),
            AppointmentDateMax = AppointmentsTestData.Appointment1Date.AddDays(-1),
            MaxResultCount = 10,
        };

        foreach (var input in new[] { empty, inverted })
        {
            await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _reports.GetStatusCountsAsync(input)));
            await Should.ThrowAsync<UserFriendlyException>(() => InOfficeA(() => _reports.GetReportCsvAsync(input)));
        }
    }
}
