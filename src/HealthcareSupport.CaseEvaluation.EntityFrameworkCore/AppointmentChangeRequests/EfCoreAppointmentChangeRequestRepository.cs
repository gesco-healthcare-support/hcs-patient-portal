using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

public class EfCoreAppointmentChangeRequestRepository
    : EfCoreRepository<CaseEvaluationDbContext, AppointmentChangeRequest, Guid>, IAppointmentChangeRequestRepository
{
    public EfCoreAppointmentChangeRequestRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}

public class EfCoreAppointmentChangeRequestDocumentRepository
    : EfCoreRepository<CaseEvaluationDbContext, AppointmentChangeRequestDocument, Guid>, IAppointmentChangeRequestDocumentRepository
{
    public EfCoreAppointmentChangeRequestDocumentRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
