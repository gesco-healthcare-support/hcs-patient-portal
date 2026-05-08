using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Per-tenant <c>NotificationTemplate</c> management surface for IT Admin.
///
/// Mirrors OLD's <c>TemplateDomain</c> (<c>P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs</c>)
/// MINUS Add / Delete: Phase 4 unifies OLD's two separate template
/// mechanisms (16 DB-managed + 43 disk-HTML) into a single seeded
/// <c>NotificationTemplates</c> table. Templates are seeded once per
/// tenant; IT Admin edits Subject + Body content but does not Create or
/// Delete -- handlers across the system depend on the stable code set.
///
/// Authorization (Phase 2.5):
///   - <c>GetListAsync</c>, <c>GetAsync</c>, <c>GetByCodeAsync</c>,
///     <c>GetTypeLookupAsync</c>: gated by
///     <c>NotificationTemplates.Default</c>. IT Admin + Staff Supervisor
///     hold this; Clinic Staff does not.
///   - <c>UpdateAsync</c>: gated by
///     <c>NotificationTemplates.Edit</c>. IT Admin + Staff Supervisor.
/// </summary>
public interface INotificationTemplatesAppService : IApplicationService
{
    /// <summary>Paged list with text + type + active-flag filters.</summary>
    Task<PagedResultDto<NotificationTemplateWithNavigationPropertiesDto>> GetListAsync(
        GetNotificationTemplatesInput input);

    /// <summary>Loads a template by id (with nav props).</summary>
    Task<NotificationTemplateWithNavigationPropertiesDto> GetAsync(Guid id);

    /// <summary>
    /// Loads the active template for the given code (case-sensitive). The
    /// notification handler pipeline uses this to resolve a template at
    /// fire time without exposing internal Guid ids. Throws
    /// <c>BusinessException</c> with code
    /// <c>CaseEvaluation:NotificationTemplate.NotFound</c> when no active
    /// row exists.
    /// </summary>
    Task<NotificationTemplateWithNavigationPropertiesDto> GetByCodeAsync(string templateCode);

    /// <summary>
    /// Lookup endpoint for the host-scoped Email / SMS template-type
    /// dropdown in the editor UI. Returns a list-result of
    /// <see cref="NotificationTemplateTypeDto"/> rather than a stripped
    /// <c>LookupDto</c> because the editor wants
    /// <c>IsActive</c> to disable a row.
    /// </summary>
    Task<ListResultDto<NotificationTemplateTypeDto>> GetTypeLookupAsync();

    /// <summary>
    /// Updates Subject + BodyEmail + BodySms + IsActive on the singleton
    /// row identified by <paramref name="id"/>. <c>TemplateCode</c>,
    /// <c>TemplateTypeId</c>, and <c>Description</c> are immutable on this
    /// path (see <see cref="NotificationTemplateUpdateDto"/> for rationale).
    /// </summary>
    Task<NotificationTemplateDto> UpdateAsync(Guid id, NotificationTemplateUpdateDto input);
}
