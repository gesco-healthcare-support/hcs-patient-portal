using System.Runtime.CompilerServices;

// Exposes IdentityModelPiiLogging.Apply(bool) to the Application.Tests project.
//
// The public Apply(IHostEnvironment, IConfiguration) overload is reachable without this and
// carries the environment table. The internal bool overload is the primitive underneath it, and
// the case worth pinning is the one the old shape could not express: a process whose switches are
// already ON being turned back OFF. Driving that through the public overload would only prove the
// decision again, not the write.
//
// Scope intentionally narrow (one assembly, this codebase only), compile-time only, zero runtime
// effect -- the same pattern and the same grantee as HttpApi.Host/AssemblyInfo.cs and
// Application/AssemblyInfo.cs.
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")]
