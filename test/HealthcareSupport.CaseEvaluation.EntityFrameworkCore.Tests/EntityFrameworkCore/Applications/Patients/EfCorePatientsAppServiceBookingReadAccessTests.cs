using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class EfCorePatientsAppServiceBookingReadAccessTests
    : PatientsAppServiceBookingReadAccessTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
