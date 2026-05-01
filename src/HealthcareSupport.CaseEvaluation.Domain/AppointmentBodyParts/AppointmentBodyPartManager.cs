using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public class AppointmentBodyPartManager : DomainService
{
    protected IRepository<AppointmentBodyPart, Guid> _repository;

    public AppointmentBodyPartManager(IRepository<AppointmentBodyPart, Guid> repository)
    {
        _repository = repository;
    }

    public virtual async Task<AppointmentBodyPart> CreateAsync(Guid appointmentInjuryDetailId, string bodyPartDescription)
    {
        Check.NotNull(appointmentInjuryDetailId, nameof(appointmentInjuryDetailId));
        Check.NotNullOrWhiteSpace(bodyPartDescription, nameof(bodyPartDescription));
        Check.Length(bodyPartDescription, nameof(bodyPartDescription), AppointmentBodyPartConsts.BodyPartDescriptionMaxLength);
        var entity = new AppointmentBodyPart(GuidGenerator.Create(), appointmentInjuryDetailId, bodyPartDescription);
        return await _repository.InsertAsync(entity);
    }
}
