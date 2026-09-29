using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Authorization;

public class EfCoreRolePermissionSurfaceSnapshotTests
    : RolePermissionSurfaceSnapshotTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
