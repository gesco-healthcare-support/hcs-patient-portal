using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// AppService for the per-(appointment, kind) generated packets.
///
/// <para>Phase 1D.9 (2026-05-08): extended from the single-Patient
/// surface to a per-kind list + per-kind download surface. The legacy
/// <see cref="GetByAppointmentAsync"/> + <see cref="DownloadAsync"/>
/// methods stay so old Angular callers compile without proxy regen,
/// but new UI consumers should call the per-kind methods.</para>
/// </summary>
public interface IAppointmentPacketsAppService
{
    /// <summary>
    /// Phase 1A.1 backward-compat: returns the Patient packet only.
    /// New UI should call <see cref="GetListByAppointmentAsync"/>.
    /// </summary>
    Task<AppointmentPacketDto?> GetByAppointmentAsync(Guid appointmentId);

    /// <summary>
    /// Phase 1A.1 backward-compat: streams the Patient packet only.
    /// New UI should call <see cref="DownloadByKindAsync"/>.
    /// </summary>
    Task<DownloadResult> DownloadAsync(Guid appointmentId);

    /// <summary>
    /// Returns every generated packet for the appointment, one row per
    /// kind. Empty list when the generation job has not run yet (e.g.
    /// appointment not yet Approved).
    /// </summary>
    Task<List<AppointmentPacketDto>> GetListByAppointmentAsync(Guid appointmentId);

    /// <summary>
    /// Streams the packet blob of the requested kind for download.
    /// Throws when the (appointment, kind) row does not exist or is not
    /// yet Generated. Phase 1 ships DOCX (content-type
    /// <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c>);
    /// Phase 2 will swap to <c>application/pdf</c> after conversion is wired.
    /// </summary>
    Task<DownloadResult> DownloadByKindAsync(Guid appointmentId, PacketKind kind);
}
