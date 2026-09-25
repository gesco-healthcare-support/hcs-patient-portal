using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The on-demand missing-intake report (#944) for the host's Case Tracker failures page.
///
/// <para>Gated like every other Case Tracker admin action since 2026-09-24: the Host-only
/// <see cref="CaseEvaluationPermissions.CaseTrackerIntegration"/> permission, and a refusal for any caller inside
/// an office, because the report reads every office.</para>
/// </summary>
[Authorize]
public class CaseTrackerMissingIntakeAppService : CaseEvaluationAppService, ICaseTrackerMissingIntakeAppService
{
    private readonly CaseTrackerMissingIntakeReporter _reporter;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public CaseTrackerMissingIntakeAppService(
        CaseTrackerMissingIntakeReporter reporter,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _reporter = reporter;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<CaseTrackerMissingIntakeReportDto> GetReportAsync()
    {
        EnsureHostCaller();

        var offices = await _reporter.BuildAsync();

        return new CaseTrackerMissingIntakeReportDto
        {
            GeneratedAt = _clock.Now,
            Offices = offices.Select(ToDto).ToList(),
        };
    }

    /// <summary>
    /// Refuses a caller who is inside an office, before any office is read. The Host-only permission already stops
    /// an office caller at the authorization interceptor; this keeps the refusal if that permission's side is ever
    /// widened.
    /// </summary>
    private void EnsureHostCaller()
    {
        if (_currentTenant.IsAvailable)
        {
            throw new AbpAuthorizationException(
                "Case Tracker delivery is managed from the host, not from inside an office.");
        }
    }

    private static CaseTrackerMissingIntakeOfficeDto ToDto(CaseTrackerMissingIntakeOffice office) => new()
    {
        OfficeId = office.OfficeId,
        OfficeName = office.OfficeName,
        Failed = office.Failed,
        FirstIntakeRowAt = office.FirstIntakeRowAt,
        LikelyLost = ToDtos(office.LikelyLost),
        Settling = ToDtos(office.Settling),
        BeforeIntegration = ToDtos(office.BeforeIntegration),
        BeforeIntegrationCount = office.BeforeIntegrationCount,
    };

    private static List<CaseTrackerMissingIntakeItemDto> ToDtos(IEnumerable<CaseTrackerMissingIntakeItem> items) =>
        items.Select(i => new CaseTrackerMissingIntakeItemDto
        {
            AppointmentId = i.AppointmentId,
            ConfirmationNumber = i.ConfirmationNumber,
            Status = i.Status,
            ApprovedAt = i.ApprovedAt,
        }).ToList();
}
