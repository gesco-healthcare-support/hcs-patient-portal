using System;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// An injury row created, edited or deleted re-sends its appointment with the current <c>data.injuries</c>.
///
/// <para>These edits were already re-sent before 2026-11-23, but only through ABP's
/// <c>PublishEntityUpdatedEventWhenNavigationChanges</c> default raising the appointment's own update event (measured
/// 2026-11-23: with that option off, nothing went). This makes it explicit; with the option on, the two builds are
/// identical and collapse onto one outbox row.</para>
/// </summary>
public class InjuryChangedHandler : AppointmentChildChangedHandler<AppointmentInjuryDetail>, ITransientDependency
{
    public InjuryChangedHandler(AppointmentChangedHandler rePush)
        : base(rePush)
    {
    }

    protected override Guid AppointmentIdOf(AppointmentInjuryDetail child) => child.AppointmentId;
}
