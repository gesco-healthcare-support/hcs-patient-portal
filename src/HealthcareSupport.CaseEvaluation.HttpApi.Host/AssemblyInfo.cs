using System.Runtime.CompilerServices;

// BUG-025 (2026-05-21): expose internal helpers in
// CaseEvaluationHttpApiHostModule (MapAppointmentDocumentErrorCodes,
// ConfigureUploadLimits) to the Application.Tests project so they can
// be unit-tested without booting the full host. Scope intentionally
// narrow (one assembly, this codebase only) -- mirrors the pattern
// already used by the Application project's AssemblyInfo.cs.
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")]

// Phase 3 task 9 (2026-09-08): also expose it to EntityFrameworkCore.Tests, so
// ConfigureMultiTenancy can be run against a BOOTED application rather than a
// bare ServiceCollection. Full reasoning is in AuthServer/AssemblyInfo.cs, which
// gained the same line for the same task; the short version is that a bare
// collection cannot exhibit module ordering, so it cannot show that ABP's
// default resolvers were present at the moment our Clear() ran.
//
// Compile-time only, zero runtime effect, and EntityFrameworkCore.Tests is
// already an established grantee in src/ (Domain/Properties/AssemblyInfo.cs:9).
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests")]
