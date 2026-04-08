using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using System;
using Volo.Abp.Domain.Entities;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorAppointmentType : Entity
{
    public Guid DoctorId { get; protected set; }
    public virtual Doctor Doctor { get; set; }

    public Guid AppointmentTypeId { get; protected set; }
    public virtual AppointmentType AppointmentType { get; set; }

    private DoctorAppointmentType()
    {
    }

    public DoctorAppointmentType(Guid doctorId, Guid appointmentTypeId)
    {
        DoctorId = doctorId;
        AppointmentTypeId = appointmentTypeId;
    }

    public override object[] GetKeys()
    {
        return new object[] {
            DoctorId,
            AppointmentTypeId
        };
    }
}