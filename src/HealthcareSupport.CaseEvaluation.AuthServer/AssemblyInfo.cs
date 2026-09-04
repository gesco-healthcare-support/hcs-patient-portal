using System.Runtime.CompilerServices;

// Phase 3 item APP-OWN-01 (2026-09-04): expose the internal
// CaseEvaluationAuthServerModule.ConfigureMultiTenancy helper to the
// Application.Tests project so the assembled AbpTenantResolveOptions can be
// asserted without booting the AuthServer. Scope intentionally narrow (one
// assembly, this codebase only) -- mirrors HttpApi.Host/AssemblyInfo.cs, added
// for BUG-025, and the Application project's own AssemblyInfo.cs.
//
// WHY THE AUTHSERVER AND NOT JUST THE API. Both processes clear the resolver
// chain and register the same two contributors, but the AuthServer is where the
// token that CurrentUserTenantResolveContributor later reads is minted. Pinning
// only the API side would leave a framework upgrade free to re-add a default
// __tenant resolver on the login path with nothing in the repository failing.
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.Application.Tests")]
