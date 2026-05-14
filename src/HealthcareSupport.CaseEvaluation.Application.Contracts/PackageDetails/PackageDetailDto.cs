using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Read model for a per-AppointmentType package template -- mirrors OLD's
/// <c>spm.PackageDetails</c> row. <c>AppointmentTypeId</c> is nullable per
/// OLD schema (AppointmentTypeId == null means "applies to any type"; in
/// practice IT Admin always sets it). The "one active package per
/// AppointmentTypeId" rule is enforced at the AppService, not the schema --
/// see <c>P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\PackageDetailDomain.cs</c>:48-53.
/// </summary>
public class PackageDetailDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string PackageName { get; set; } = null!;
    public Guid? AppointmentTypeId { get; set; }
    public bool IsActive { get; set; }
}
