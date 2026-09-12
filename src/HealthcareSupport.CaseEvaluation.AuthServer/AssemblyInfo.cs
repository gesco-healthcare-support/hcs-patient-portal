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

// Phase 3 task 9 (2026-09-08): also expose it to EntityFrameworkCore.Tests.
//
// WHY A SECOND GRANTEE. The line above lets Application.Tests run the helper
// against a bare ServiceCollection, which is what TenantResolverChainTests
// does. That proves Clear() removes whatever is present; it cannot prove ABP's
// defaults were present to be removed, because a bare collection has no module
// ordering. Answering that needs the options read out of a BOOTED application,
// and the only bootable graph lives in EntityFrameworkCore.Tests -- the
// Application.Tests integration classes are abstract and are concretised there,
// so the project reference runs that way and cannot be reversed.
//
// Measured 2026-09-08: a booted graph without our ConfigureMultiTenancy carries
// exactly one framework default, CurrentUserTenantResolveContributor, and
// neither test module seeds it. So the remaining question -- does our Clear()
// run after the framework's registration and win -- is answerable, and this
// grant is what makes it reachable.
//
// Compile-time only, zero runtime effect, and EntityFrameworkCore.Tests is
// already an established grantee in src/: see
// Domain/Properties/AssemblyInfo.cs:9. No new ProjectReference is needed --
// Application.Tests already references this assembly, and EntityFrameworkCore.Tests
// references Application.Tests, so it flows transitively.
[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests")]
