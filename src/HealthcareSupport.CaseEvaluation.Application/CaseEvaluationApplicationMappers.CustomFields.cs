using HealthcareSupport.CaseEvaluation.CustomFields;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace HealthcareSupport.CaseEvaluation;

// Phase 6 (2026-05-03) -- mapper for the IT-Admin-defined CustomField
// catalog. Partial-class file split per the two-session split rule so this
// track does not collide with concurrent edits in the main mappers file.

[Mapper]
public partial class CustomFieldToCustomFieldDtoMapper : MapperBase<CustomField, CustomFieldDto>
{
    public override partial CustomFieldDto Map(CustomField source);

    public override partial void Map(CustomField source, CustomFieldDto destination);
}
