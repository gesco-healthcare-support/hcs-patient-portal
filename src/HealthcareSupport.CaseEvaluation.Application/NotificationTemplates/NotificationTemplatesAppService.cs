using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Per-tenant <c>NotificationTemplate</c> management AppService for
/// IT Admin. Phase 4 (2026-05-03) ships only the read + update surface;
/// Create / Delete are intentionally not exposed because:
///
/// <list type="bullet">
///   <item>Templates are seeded with the canonical 59-code set
///         (<c>NotificationTemplateConsts.Codes.All</c>); creating new
///         codes ad-hoc would not have notification-handler wiring.</item>
///   <item>Deleting a template would silently break every handler that
///         resolves it via <c>FindByCodeAsync</c>. IT Admin disables a
///         template by toggling <c>IsActive</c>.</item>
/// </list>
///
/// Authorization (Phase 2.5):
///   - Class-level <c>[Authorize(NotificationTemplates.Default)]</c> gates
///     the read endpoints.
///   - <c>UpdateAsync</c> overrides with
///     <c>[Authorize(NotificationTemplates.Edit)]</c>.
///
/// Strict-parity notes:
///   - Mirrors OLD <c>TemplateDomain.Get(...)</c> + <c>Update(...)</c> from
///     <c>P:\PatientPortalOld\PatientAppointment.Domain\TemplateManagementModule\TemplateDomain.cs</c>.
///   - OLD's <c>Add</c> and <c>Delete</c> are not ported (see above).
///   - OLD's <c>spm.spTemplates</c> stored-proc-based paged list is
///     replaced by ABP's standard <c>PagedAndSortedResultRequestDto</c>
///     pattern (acceptable framework deviation; same visible behavior).
///   - Optimistic concurrency via <c>ConcurrencyStamp</c> is additive
///     safety -- OLD lacked it. Treated as OLD-bug-fix exception.
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.NotificationTemplates.Default)]
public class NotificationTemplatesAppService : ApplicationService, INotificationTemplatesAppService
{
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _typeRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IEmailBodySanitizer _emailBodySanitizer;

    public NotificationTemplatesAppService(
        INotificationTemplateRepository templateRepository,
        INotificationTemplateTypeRepository typeRepository,
        INotificationDispatcher notificationDispatcher,
        IEmailBodySanitizer emailBodySanitizer)
    {
        _templateRepository = templateRepository;
        _typeRepository = typeRepository;
        _notificationDispatcher = notificationDispatcher;
        _emailBodySanitizer = emailBodySanitizer;
    }

    public virtual async Task<PagedResultDto<NotificationTemplateWithNavigationPropertiesDto>> GetListAsync(
        GetNotificationTemplatesInput input)
    {
        Check.NotNull(input, nameof(input));

        var totalCount = await _templateRepository.GetCountAsync(
            filterText: input.FilterText,
            templateTypeId: input.TemplateTypeId,
            isActive: input.IsActive);

        var items = await _templateRepository.GetListWithNavigationPropertiesAsync(
            filterText: input.FilterText,
            templateTypeId: input.TemplateTypeId,
            isActive: input.IsActive,
            sorting: input.Sorting,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount);

        return new PagedResultDto<NotificationTemplateWithNavigationPropertiesDto>(
            totalCount,
            items.Select(MapToWithNavDto).ToList());
    }

    public virtual async Task<NotificationTemplateWithNavigationPropertiesDto> GetAsync(Guid id)
    {
        var entity = await _templateRepository.GetWithNavigationPropertiesAsync(id);
        if (entity == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound);
        }
        return MapToWithNavDto(entity);
    }

    public virtual async Task<NotificationTemplateWithNavigationPropertiesDto> GetByCodeAsync(string templateCode)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));
        var entity = await _templateRepository.FindWithNavigationPropertiesByCodeAsync(templateCode);
        if (entity == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
                .WithData("templateCode", templateCode);
        }
        return MapToWithNavDto(entity);
    }

    public virtual async Task<ListResultDto<NotificationTemplateTypeDto>> GetTypeLookupAsync()
    {
        // Host-scoped lookup -- same dataset for every tenant. Two seeded
        // rows (Email + SMS) per Phase 1.3.
        var queryable = await _typeRepository.GetQueryableAsync();
        var items = queryable.OrderBy(x => x.Name).ToList();
        return new ListResultDto<NotificationTemplateTypeDto>(
            items.Select(MapToTypeDto).ToList());
    }

    [Authorize(CaseEvaluationPermissions.NotificationTemplates.Edit)]
    public virtual async Task<NotificationTemplateDto> UpdateAsync(Guid id, NotificationTemplateUpdateDto input)
    {
        Check.NotNull(input, nameof(input));
        ValidateBodies(input);
        ValidateSubjectLength(input);

        // #6 (2026-06-19): sanitize the HTML email body before persist. The
        // renderer returns stored bodies verbatim, so the stored value must
        // already be XSS-safe. BodySms is left untouched -- it is sent as plain
        // text, never HTML, so HTML sanitization does not apply.
        input.BodyEmail = _emailBodySanitizer.Sanitize(input.BodyEmail);

        var entity = await _templateRepository.GetAsync(id);

        // Optimistic concurrency: round-trip the stamp from the read DTO.
        // Tests / first-call paths may omit it -- only enforce when set.
        if (!string.IsNullOrEmpty(input.ConcurrencyStamp))
        {
            entity.ConcurrencyStamp = input.ConcurrencyStamp;
        }

        ApplyUpdate(input, entity);

        await _templateRepository.UpdateAsync(entity, autoSave: true);
        return MapToDto(entity);
    }

    /// <summary>
    /// B-B1 (2026-06-16): preview the live render by emailing this template to
    /// the current user with synthetic sample variables. Dispatches through the
    /// real <see cref="INotificationDispatcher"/> so the preview is exactly what
    /// recipients receive. Email-only (phone omitted) so a test never fires an
    /// SMS. Fails fast on an inactive template -- nothing live to test.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.NotificationTemplates.Edit)]
    public virtual async Task SendTestAsync(Guid id)
    {
        var entity = await _templateRepository.GetAsync(id);

        if (!entity.IsActive)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
                .WithData("templateCode", entity.TemplateCode);
        }

        var email = CurrentUser.Email;
        Check.NotNullOrWhiteSpace(email, "CurrentUser.Email");

        var recipients = new[] { new NotificationRecipient(email!, isRegistered: true) };
        var variables = NotificationTemplateVariableCatalog.BuildSampleVariables(entity.TemplateCode);

        await _notificationDispatcher.DispatchAsync(
            entity.TemplateCode,
            recipients,
            variables,
            contextTag: $"SendTest/{entity.TemplateCode}/{CurrentUser.Id}");
    }

    /// <summary>
    /// B-B2 (2026-06-16): the <c>##Var##</c> tokens valid for
    /// <paramref name="templateCode"/>, for the editor's variable-chip palette.
    /// Pure lookup (no DB hit) -- validates the code against the seeded set and
    /// returns the catalog tokens humanized for display.
    /// </summary>
    public virtual Task<ListResultDto<NotificationTemplateVariableDto>> GetVariablesAsync(string templateCode)
    {
        Check.NotNullOrWhiteSpace(templateCode, nameof(templateCode));

        if (!NotificationTemplateConsts.Codes.All.Contains(templateCode))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)
                .WithData("templateCode", templateCode);
        }

        var items = NotificationTemplateVariableCatalog.GetVariablesForCode(templateCode)
            .Select(token => new NotificationTemplateVariableDto
            {
                Token = token,
                Label = NotificationTemplateVariableCatalog.Humanize(token),
            })
            .ToList();

        return Task.FromResult(new ListResultDto<NotificationTemplateVariableDto>(items));
    }

    /// <summary>
    /// Hand-rolled field copy (Phase 3 pattern). The entity's protected
    /// constructor (singleton-per-tenant invariant -- only the data seed
    /// contributor instantiates rows) blocks Mapperly's
    /// <c>RequiredMappingStrategy.Target</c>, so we copy fields explicitly.
    /// Only the four user-editable fields are written:
    /// <c>Subject</c>, <c>BodyEmail</c>, <c>BodySms</c>, <c>IsActive</c>.
    /// <c>TemplateCode</c>, <c>TemplateTypeId</c>, and <c>Description</c>
    /// are preserved (immutable on this update path).
    /// </summary>
    internal static void ApplyUpdate(NotificationTemplateUpdateDto source, NotificationTemplate destination)
    {
        destination.Subject = source.Subject;
        destination.BodyEmail = source.BodyEmail;
        destination.BodySms = source.BodySms;
        destination.IsActive = source.IsActive;
    }

    /// <summary>
    /// Mirrors OLD's [Required] on Templates.BodyEmail / BodySms. Re-applied
    /// at the AppService layer because DataAnnotations on the DTO are only
    /// validated by ASP.NET Core in the controller pipeline, not when the
    /// AppService is invoked directly (e.g., from tests or in-process
    /// callers). Internal so unit tests can verify without ABP infra.
    /// </summary>
    internal static void ValidateBodies(NotificationTemplateUpdateDto input)
    {
        Check.NotNull(input.BodyEmail, nameof(input.BodyEmail));
        Check.NotNull(input.BodySms, nameof(input.BodySms));
    }

    /// <summary>
    /// Enforces the 200-character cap on <c>Subject</c>. Internal so unit
    /// tests can verify without ABP infra.
    /// </summary>
    internal static void ValidateSubjectLength(NotificationTemplateUpdateDto input)
    {
        if (input.Subject == null)
        {
            return;
        }
        Check.Length(input.Subject, nameof(input.Subject), NotificationTemplateConsts.SubjectMaxLength);
    }

    private NotificationTemplateDto MapToDto(NotificationTemplate entity)
    {
        var dto = ObjectMapper.Map<NotificationTemplate, NotificationTemplateDto>(entity);
        dto.IsCustomized = NotificationTemplateVariableCatalog.IsCustomized(
            entity.TemplateCode, entity.Subject, entity.BodyEmail, entity.BodySms);
        return dto;
    }

    private NotificationTemplateWithNavigationPropertiesDto MapToWithNavDto(
        NotificationTemplateWithNavigationProperties entity) =>
        new()
        {
            NotificationTemplate = MapToDto(entity.NotificationTemplate),
            NotificationTemplateType = entity.NotificationTemplateType is null
                ? null
                : MapToTypeDto(entity.NotificationTemplateType),
        };

    private NotificationTemplateTypeDto MapToTypeDto(NotificationTemplateType entity) =>
        ObjectMapper.Map<NotificationTemplateType, NotificationTemplateTypeDto>(entity);
}
