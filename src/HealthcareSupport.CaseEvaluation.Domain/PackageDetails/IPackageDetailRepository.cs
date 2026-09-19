using System;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

public interface IPackageDetailRepository : IRepository<PackageDetail, Guid>
{
}
