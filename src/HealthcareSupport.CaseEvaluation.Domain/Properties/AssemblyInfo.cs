using System.Runtime.CompilerServices;
[assembly: InternalsVisibleToAttribute("HealthcareSupport.CaseEvaluation.Domain.Tests")]
[assembly: InternalsVisibleToAttribute("HealthcareSupport.CaseEvaluation.TestBase")]
// Phase 4c (2026-08-05): the consent-round integration tests need ChangeRequestConsentManager
// .ComputeTokenHash to seed a token with a KNOWN raw value and a PAST expiry, which is the only
// way to drive the expiry branch without substituting the ambient IClock for every test class
// sharing the multi-office module. Re-implementing SHA256 hex in the test would assert the
// test's own crypto rather than the manager's.
[assembly: InternalsVisibleToAttribute("HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests")]
