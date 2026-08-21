using System.Runtime.CompilerServices;

// BUG-025 (2026-05-21): expose internal helpers in
// CaseEvaluationHttpApiHostModule (MapAppointmentDocumentErrorCodes,
// ConfigureUploadLimits) to the Application.Tests project so they can
// be unit-tested without booting the full host. Scope intentionally
// narrow (one assembly, this codebase only) -- mirrors the pattern
// already used by the Application project's AssemblyInfo.cs.
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")]
