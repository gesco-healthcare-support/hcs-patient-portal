using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorAppointment : Entity
{
    public Guid AppointmentAccessorId { get; protected set; }

    public Guid AppointmentId { get; protected set; }

    private AppointmentAccessorAppointment()
    {
    }

    public AppointmentAccessorAppointment(Guid appointmentAccessorId, Guid appointmentId)
    {
        AppointmentAccessorId = appointmentAccessorId;
        AppointmentId = appointmentId;
    }

    public override object[] GetKeys()
    {
        return new object[] {
            AppointmentAccessorId,
            AppointmentId
        };
    }
}