using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.PhiAccess;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-10-14 -- the internal appointments list's "Export CSV" builds the file in the browser
/// from rows it already holds, so it never reached the server and the PHI access log saw
/// nothing, while the report exports were logged. The browser now records the export here
/// first and builds the file only if that succeeds: the recorder fails closed, so an export
/// that cannot be recorded does not happen.
///
/// <para>A sibling service rather than a method on <c>AppointmentsAppService</c>, which is
/// already over two thousand lines (the same choice <c>AppointmentApprovalAppService</c>
/// made).</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class AppointmentListExportAppService : CaseEvaluationAppService, IAppointmentListExportAppService
{
    private readonly AppointmentReadAccessGuard _readAccessGuard;

    public AppointmentListExportAppService(AppointmentReadAccessGuard readAccessGuard)
    {
        _readAccessGuard = readAccessGuard;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.Default)]
    public virtual async Task RecordCsvExportAsync(RecordAppointmentListExportInput input)
    {
        var appointmentIds = input.AppointmentIds.Distinct().ToList();

        // Every appointment, before anything is recorded: an export naming one the caller may not
        // read is refused whole, and leaves no rows.
        foreach (var appointmentId in appointmentIds)
        {
            await _readAccessGuard.EnsureCanReadAsync(appointmentId);
        }

        await PhiAccessRecorder.RecordManyAsync(
            PhiAccessAction.AppointmentListCsvExported,
            appointmentIds.Select(id => new PhiAccessTarget(null, id)).ToList());
    }
}
