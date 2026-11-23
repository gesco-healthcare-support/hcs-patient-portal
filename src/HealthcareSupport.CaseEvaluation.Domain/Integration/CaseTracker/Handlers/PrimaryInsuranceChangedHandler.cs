using System;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// An insurance row created, edited or deleted re-sends its appointment with the current
/// <c>data.primaryInsurances</c>. Made explicit 2026-11-23; see <see cref="InjuryChangedHandler"/> for why.
/// </summary>
public class PrimaryInsuranceChangedHandler
    : AppointmentChildChangedHandler<AppointmentPrimaryInsurance>, ITransientDependency
{
    public PrimaryInsuranceChangedHandler(AppointmentChangedHandler rePush)
        : base(rePush)
    {
    }

    protected override Guid AppointmentIdOf(AppointmentPrimaryInsurance child) => child.AppointmentId;
}
