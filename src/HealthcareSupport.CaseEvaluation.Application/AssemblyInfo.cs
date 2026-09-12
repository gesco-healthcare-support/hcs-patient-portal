using System.Runtime.CompilerServices;

// Phase 3 (2026-05-02): expose internal helpers in
// SystemParametersAppService (positive-int validator, CC-email length
// validator, in-place ApplyUpdate field copy) to the unit-test project so
// they can be exercised without standing up the full ABP integration test
// harness. Scope is intentionally narrow (one assembly, this codebase only).
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")]
