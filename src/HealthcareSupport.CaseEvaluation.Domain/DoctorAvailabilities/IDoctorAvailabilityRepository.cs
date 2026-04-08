using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public interface IDoctorAvailabilityRepository : IRepository<DoctorAvailability, Guid>
{
    Task<DoctorAvailabilityWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DoctorAvailabilityWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, Guid? locationId = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DoctorAvailability>> GetListAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, Guid? locationId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default);
}