using System;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

public interface IAppointmentChangeRequestRepository : IRepository<AppointmentChangeRequest, Guid>
{
}

public interface IAppointmentChangeRequestDocumentRepository : IRepository<AppointmentChangeRequestDocument, Guid>
{
}
