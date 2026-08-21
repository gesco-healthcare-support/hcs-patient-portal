using Volo.Abp.Auditing;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;

/// <summary>
/// M:N mapping row recording which Locations a Doctor accepts appointments
/// at. Mirrors OLD's <c>spm.DoctorPreferredLocations</c> (composite key on
/// <c>DoctorId</c> + <c>LocationId</c>) per
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorPreferredLocationDomain.cs</c>.
/// Phase 7b (2026-05-03).
///
/// OLD pattern: row presence + <c>StatusId</c> toggle. NEW retains the
/// same shape with ABP-conventional <c>IsActive</c> + ABP <c>ISoftDelete</c>.
/// IT Admin / Staff Supervisor toggles a Location on or off via a single
/// <c>ToggleAsync</c> entry point that upserts the row and flips
/// <c>IsActive</c>; the entity is never hard-deleted in the normal flow
/// so historical state is preserved for audit reports.
///
/// Tenant scoping: the entity is <c>IMultiTenant</c> because Doctor
/// itself is tenant-scoped in NEW. Single-doctor-per-deploy in Phase 1
/// means at most one TenantId per row.
/// </summary>
[Audited]
public class DoctorPreferredLocation : FullAuditedEntity, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid DoctorId { get; protected set; }

    public virtual Guid LocationId { get; protected set; }

    public virtual bool IsActive { get; set; }

    protected DoctorPreferredLocation()
    {
    }

    public DoctorPreferredLocation(Guid doctorId, Guid locationId, Guid? tenantId, bool isActive = true)
    {
        DoctorId = doctorId;
        LocationId = locationId;
        TenantId = tenantId;
        IsActive = isActive;
    }

    public override object[] GetKeys() => new object[] { DoctorId, LocationId };
}
