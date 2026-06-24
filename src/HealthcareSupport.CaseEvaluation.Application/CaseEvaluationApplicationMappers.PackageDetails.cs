using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace HealthcareSupport.CaseEvaluation;

// Phase 5 (2026-05-03) -- mapper definitions for the IT Admin master
// Document catalog and PackageDetail / DocumentPackage CRUD surfaces. Kept
// in a partial-class file split per the two-session split memory so this
// track does not collide with concurrent edits in the main mappers file.
// All mappers use the assembly default RequiredMappingStrategy.Target
// declared at the top of CaseEvaluationApplicationMappers.cs.

[Mapper]
public partial class DocumentToDocumentDtoMapper : MapperBase<Document, DocumentDto>
{
    public override partial DocumentDto Map(Document source);

    public override partial void Map(Document source, DocumentDto destination);
}

[Mapper]
public partial class PackageDetailToPackageDetailDtoMapper : MapperBase<PackageDetail, PackageDetailDto>
{
    public override partial PackageDetailDto Map(PackageDetail source);

    public override partial void Map(PackageDetail source, PackageDetailDto destination);
}

[Mapper]
public partial class DocumentPackageToDocumentPackageDtoMapper : MapperBase<DocumentPackage, DocumentPackageDto>
{
    public override partial DocumentPackageDto Map(DocumentPackage source);

    public override partial void Map(DocumentPackage source, DocumentPackageDto destination);
}
