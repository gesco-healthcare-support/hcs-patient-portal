using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace HealthcareSupport.CaseEvaluation;

// Phase 4 (2026-05-03) -- per-feature partial-class mapper file.
// Lives separately from CaseEvaluationApplicationMappers.cs per the
// Session B vs Session A shared-file split rule (memory:
// project_two-session-split.md). Mapperly supports `partial class`
// declarations spread across files; ABP's source generator picks them
// all up.
//
// The Update DTO -> entity write mapping is intentionally NOT a Mapperly
// mapper because NotificationTemplate's protected constructor (singleton
// invariant -- only the data seed contributor instantiates rows) blocks
// RequiredMappingStrategy.Target source generation. The hand-rolled copy
// in NotificationTemplatesAppService.ApplyUpdate covers the four
// editable fields and is unit-tested via InternalsVisibleTo.

[Mapper]
public partial class NotificationTemplateToNotificationTemplateDtoMapper
    : MapperBase<NotificationTemplate, NotificationTemplateDto>
{
    public override partial NotificationTemplateDto Map(NotificationTemplate source);
    public override partial void Map(NotificationTemplate source, NotificationTemplateDto destination);
}

[Mapper]
public partial class NotificationTemplateTypeToNotificationTemplateTypeDtoMapper
    : MapperBase<NotificationTemplateType, NotificationTemplateTypeDto>
{
    public override partial NotificationTemplateTypeDto Map(NotificationTemplateType source);
    public override partial void Map(NotificationTemplateType source, NotificationTemplateTypeDto destination);
}
