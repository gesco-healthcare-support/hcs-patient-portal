using System;
using HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// An employer row created, edited or deleted re-sends its appointment with the current <c>data.employer</c>
/// (2026-11-23). A delete sends <c>"employer": null</c>, which the Case Tracker reads as "clear it".
/// </summary>
public class EmployerChangedHandler : AppointmentChildChangedHandler<AppointmentEmployerDetail>, ITransientDependency
{
    public EmployerChangedHandler(AppointmentChangedHandler rePush)
        : base(rePush)
    {
    }

    protected override Guid AppointmentIdOf(AppointmentEmployerDetail child) => child.AppointmentId;
}
