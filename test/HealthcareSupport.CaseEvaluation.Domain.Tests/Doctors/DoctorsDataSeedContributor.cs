using System;
using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
using HealthcareSupport.CaseEvaluation.Doctors;

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class DoctorsDataSeedContributor : IDataSeedContributor, ISingletonDependency
{
    private bool IsSeeded = false;
    private readonly IDoctorRepository _doctorRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public DoctorsDataSeedContributor(IDoctorRepository doctorRepository, IUnitOfWorkManager unitOfWorkManager)
    {
        _doctorRepository = doctorRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (IsSeeded)
        {
            return;
        }

        await _doctorRepository.InsertAsync(new Doctor(id: Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67"), firstName: "551551e068be423cb150129a2fb3fd1f0c6bc2ecc74145619f", lastName: "221de0f2b24843429fbb2b7101ced2cbcca103583b4d4cd89c", email: "7c7fa4aa54e94b09adf79@07d1fd7ead804f659d7d5.com", gender: default, identityUserId: null));
        await _doctorRepository.InsertAsync(new Doctor(id: Guid.Parse("b6d53903-5956-47fe-a12d-02982664ed4f"), firstName: "b032f90ee6b14bec8ce85eb2c239d6779b0a5be0ee7a4dc2be", lastName: "1967da12b041453b9280d4befe7d582fe8e72d7b5a13447291", email: "eb5b574cbd18458f84700@a4260fb508044a75afd13.com", gender: default, identityUserId: null));
        await _unitOfWorkManager!.Current!.SaveChangesAsync();
        IsSeeded = true;
    }
}