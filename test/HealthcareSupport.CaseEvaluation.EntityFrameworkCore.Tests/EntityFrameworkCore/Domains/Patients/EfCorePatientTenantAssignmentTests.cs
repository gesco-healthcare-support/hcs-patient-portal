using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// The [Collection] attribute is required, not decorative: without it xUnit gives this class its
/// own collection, which builds a SECOND ABP host that re-runs the seed contributors against the
/// same SQLite database and fails on a foreign key during initialization. Every sibling test in
/// this project carries it for the same reason.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCorePatientTenantAssignmentTests
    : PatientTenantAssignmentTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
