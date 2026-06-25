using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- the security-critical intake assignment gate. The
/// custom impersonation grant calls <see cref="IIntakeAssignmentChecker"/> before
/// letting a host Intake operator enter an office; a hole here lets an operator
/// reach an unassigned office's PHI. These pin the deny-by-default behavior and
/// per-office isolation. The full impersonation denial is exercised in the live
/// docker gate (the grant runs in the OpenIddict pipeline, not the test harness);
/// these guard the logic the grant depends on.
/// </summary>
public abstract class IntakeAssignmentGateTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IIntakeAssignmentChecker _gate;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignmentRepository;
    private readonly ICurrentTenant _currentTenant;

    protected IntakeAssignmentGateTests()
    {
        _gate = GetRequiredService<IIntakeAssignmentChecker>();
        _assignmentRepository = GetRequiredService<IRepository<IntakeOfficeAssignment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task IsAssignedAsync_NoAssignment_ReturnsFalse()
    {
        // Deny-by-default: an operator with no assignment row may enter no office.
        await WithUnitOfWorkAsync(async () =>
        {
            var result = await _gate.IsAssignedAsync(Guid.NewGuid(), TenantsTestData.TenantARef);
            result.ShouldBeFalse();
        });
    }

    [Fact]
    public async Task IsAssignedAsync_GrantsOnlyTheAssignedOffice()
    {
        var operatorId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                await _assignmentRepository.InsertAsync(
                    new IntakeOfficeAssignment(Guid.NewGuid(), operatorId, TenantsTestData.TenantARef),
                    autoSave: true);
            }
        });

        await WithUnitOfWorkAsync(async () =>
        {
            // Assigned office -> allowed.
            (await _gate.IsAssignedAsync(operatorId, TenantsTestData.TenantARef)).ShouldBeTrue();
            // A DIFFERENT office never assigned -> denied (no cross-office leak).
            (await _gate.IsAssignedAsync(operatorId, TenantsTestData.TenantBRef)).ShouldBeFalse();
        });
    }
}
