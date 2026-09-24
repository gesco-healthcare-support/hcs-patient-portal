using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// EF Core run of <c>PatientTenantAssignmentTests</c>. It carries no <c>[Collection]</c>: each test
/// builds its own application and its own database, so it runs in parallel with the other classes
/// (#1034). The foreign-key failure during initialization that this comment used to warn about
/// matches seeds overwriting the static test tenant ids, which are now fixed
/// (<c>TenantsTestData</c>).
/// </summary>
public class EfCorePatientTenantAssignmentTests
    : PatientTenantAssignmentTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
