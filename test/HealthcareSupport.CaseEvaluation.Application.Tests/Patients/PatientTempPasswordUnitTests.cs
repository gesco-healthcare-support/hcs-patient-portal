using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// NEW-SEC-04 / SEC-05 / Q-12 (IP6, 2026-06-05): booking-minted patient logins
/// must NOT receive the shared seeded admin default password. The booking path
/// (GetOrCreatePatientForAppointmentBookingAsync) previously used
/// CaseEvaluationConsts.AdminPasswordDefaultValue, so anyone who knew the seed
/// admin default could authenticate as any booking-created patient.
///
/// PatientsAppService.GenerateTempPassword is internal-for-testability (mirrors
/// InternalUsersAppService.GenerateParityPassword); these pure tests verify the
/// non-shared + policy-compliant contract host-side. The DB-backed booking
/// integration assertion remains CI-only (blocked on the runtime-create harness,
/// see PatientsAppServiceTests.GetOrCreatePatient_DoesNotUseHardcodedAdminPassword).
///
/// INTERIM: removed alongside the helper when the record-only rework (IP6 T4)
/// stops minting an account at booking entirely.
/// </summary>
public class PatientTempPasswordUnitTests
{
    private const int Iterations = 1000;

    [Fact]
    public void GenerateTempPassword_IsNeverTheSharedAdminDefault()
    {
        for (var i = 0; i < Iterations; i++)
        {
            PatientsAppService.GenerateTempPassword()
                .ShouldNotBe(CaseEvaluationConsts.AdminPasswordDefaultValue);
        }
    }

    [Fact]
    public void GenerateTempPassword_SatisfiesPasswordPolicy()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var password = PatientsAppService.GenerateTempPassword();

            password.Length.ShouldBeGreaterThanOrEqualTo(8);
            password.Any(char.IsUpper).ShouldBeTrue();
            password.Any(char.IsLower).ShouldBeTrue();
            password.Any(char.IsDigit).ShouldBeTrue();
            password.Any(c => !char.IsLetterOrDigit(c)).ShouldBeTrue();
        }
    }

    [Fact]
    public void GenerateTempPassword_ProducesDistinctValues()
    {
        var generated = Enumerable.Range(0, 100)
            .Select(_ => PatientsAppService.GenerateTempPassword())
            .ToList();

        generated.Distinct().Count().ShouldBe(generated.Count);
    }
}
