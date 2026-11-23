using System;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// A claim examiner row created, edited or deleted re-sends its appointment with the current
/// <c>data.claimExaminers</c>. Made explicit 2026-11-23; see <see cref="InjuryChangedHandler"/> for why. The staff
/// parties path also calls <see cref="AppointmentChangedHandler"/> itself; all three builds collapse onto one row.
/// </summary>
public class ClaimExaminerChangedHandler
    : AppointmentChildChangedHandler<AppointmentClaimExaminer>, ITransientDependency
{
    public ClaimExaminerChangedHandler(AppointmentChangedHandler rePush)
        : base(rePush)
    {
    }

    protected override Guid AppointmentIdOf(AppointmentClaimExaminer child) => child.AppointmentId;
}
