using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Reads and edits an office's packet letterhead -- what the generated packets print as the
/// office's identity. There is no host-wide letterhead.
///
/// <para><see cref="GetAsync"/> / <see cref="UpdateAsync"/> work on the CURRENT office (the
/// in-office branding page). <see cref="GetForOfficeAsync"/> / <see cref="UpdateForOfficeAsync"/>
/// work on one office by id and are HOST-ONLY (2026-10-21): the host branding page, where IT Admin
/// and the host Staff Supervisor already edit each office's display name and logo. A host
/// Supervisor who enters an office lands as that office's Staff Supervisor shadow, which does not
/// hold Branding.Edit, so the in-office editor was out of their reach.</para>
/// </summary>
public interface IOfficeLetterheadAppService : IApplicationService
{
    Task<OfficeLetterheadDto> GetAsync();

    Task<OfficeLetterheadDto> UpdateAsync(UpdateOfficeLetterheadInput input);

    Task<OfficeLetterheadDto> GetForOfficeAsync(Guid officeId);

    Task<OfficeLetterheadDto> UpdateForOfficeAsync(Guid officeId, UpdateOfficeLetterheadInput input);
}
