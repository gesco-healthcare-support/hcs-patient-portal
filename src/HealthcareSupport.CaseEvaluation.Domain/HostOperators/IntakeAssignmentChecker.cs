using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Reads <see cref="IntakeOfficeAssignment"/> in HOST context (assignments live
/// in the host/management DB). Forces host scope via <c>CurrentTenant.Change(null)</c>
/// so the lookup is correct regardless of the caller's ambient tenant -- the
/// impersonation grant runs at the token endpoint where the operator is host
/// context, but forcing it is defense in depth.
/// </summary>
public class IntakeAssignmentChecker : DomainService, IIntakeAssignmentChecker
{
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignmentRepository;

    public IntakeAssignmentChecker(IRepository<IntakeOfficeAssignment, Guid> assignmentRepository)
    {
        _assignmentRepository = assignmentRepository;
    }

    public async Task<bool> IsAssignedAsync(Guid operatorUserId, Guid officeId)
    {
        using (CurrentTenant.Change(null))
        {
            return await _assignmentRepository.AnyAsync(
                a => a.OperatorUserId == operatorUserId && a.OfficeId == officeId);
        }
    }
}
