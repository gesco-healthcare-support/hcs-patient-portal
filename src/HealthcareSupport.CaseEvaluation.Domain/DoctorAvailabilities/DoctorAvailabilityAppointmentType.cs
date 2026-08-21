using System;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// 2026-05-15 -- M2M join between <see cref="DoctorAvailability"/> and
/// <see cref="AppointmentType"/>. Composite primary key on
/// (DoctorAvailabilityId, AppointmentTypeId); TenantId is mirrored from
/// the parent slot at insert time so ABP's IMultiTenant filter scopes
/// correctly. Empty set on a slot means "any AppointmentType accepted" --
/// the loose-or-strict-mode parity rule from OLD's slot generation.
/// </summary>
public class DoctorAvailabilityAppointmentType : Entity, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid DoctorAvailabilityId { get; protected set; }

    public Guid AppointmentTypeId { get; protected set; }

    public virtual DoctorAvailability DoctorAvailability { get; protected set; } = null!;

    public virtual AppointmentType AppointmentType { get; protected set; } = null!;

    protected DoctorAvailabilityAppointmentType()
    {
    }

    public DoctorAvailabilityAppointmentType(
        Guid doctorAvailabilityId,
        Guid appointmentTypeId,
        Guid? tenantId)
    {
        DoctorAvailabilityId = doctorAvailabilityId;
        AppointmentTypeId = appointmentTypeId;
        TenantId = tenantId;
    }

    public override object[] GetKeys()
    {
        return new object[] { DoctorAvailabilityId, AppointmentTypeId };
    }
}
