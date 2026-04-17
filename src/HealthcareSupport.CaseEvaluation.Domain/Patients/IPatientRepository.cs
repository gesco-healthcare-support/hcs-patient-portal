using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Patients;

public interface IPatientRepository : IRepository<Patient, Guid>
{
    Task<PatientWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<PatientWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<Patient>> GetListAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? middleName = null, string? email = null, Gender? genderId = null, DateTime? dateOfBirthMin = null, DateTime? dateOfBirthMax = null, string? phoneNumber = null, string? socialSecurityNumber = null, string? address = null, string? city = null, string? zipCode = null, string? refferedBy = null, string? cellPhoneNumber = null, string? street = null, string? interpreterVendorName = null, string? apptNumber = null, Guid? stateId = null, Guid? appointmentLanguageId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default);
}