using HealthcareSupport.CaseEvaluation.DoctorPreferredLocations;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace HealthcareSupport.CaseEvaluation;

[Mapper]
public partial class DoctorPreferredLocationToDoctorPreferredLocationDtoMapper : MapperBase<DoctorPreferredLocation, DoctorPreferredLocationDto>
{
    public override partial DoctorPreferredLocationDto Map(DoctorPreferredLocation source);

    public override partial void Map(DoctorPreferredLocation source, DoctorPreferredLocationDto destination);
}
