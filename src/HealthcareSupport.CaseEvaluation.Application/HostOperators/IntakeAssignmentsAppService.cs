using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Phase D (2026-06-25) -- manages <see cref="IntakeOfficeAssignment"/> rows and
/// feeds the SPA office switcher. Every method runs in HOST context
/// (assignments + operators live in the host/management DB). Assigning eagerly
/// provisions the operator's limited shadow Intake user in the office DB
/// (O-D3); unassigning disables it. The actual office boundary the impersonation
/// grant enforces is <see cref="IIntakeAssignmentChecker"/> reading these rows.
/// </summary>
[Authorize]
public class IntakeAssignmentsAppService : CaseEvaluationAppService, IIntakeAssignmentsAppService
{
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignmentRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IIntakeShadowUserProvisioner _shadowProvisioner;

    public IntakeAssignmentsAppService(
        IRepository<IntakeOfficeAssignment, Guid> assignmentRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IdentityUserManager userManager,
        IIntakeShadowUserProvisioner shadowProvisioner)
    {
        _assignmentRepository = assignmentRepository;
        _tenantRepository = tenantRepository;
        _userManager = userManager;
        _shadowProvisioner = shadowProvisioner;
    }

    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Default)]
    public virtual async Task<ListResultDto<IntakeOfficeAssignmentDto>> GetListAsync()
    {
        using (CurrentTenant.Change(null))
        {
            var assignments = await _assignmentRepository.GetListAsync();
            var dtos = new List<IntakeOfficeAssignmentDto>(assignments.Count);
            foreach (var a in assignments)
            {
                var op = await _userManager.FindByIdAsync(a.OperatorUserId.ToString());
                var office = await _tenantRepository.FindAsync(a.OfficeId);
                dtos.Add(new IntakeOfficeAssignmentDto
                {
                    Id = a.Id,
                    OperatorUserId = a.OperatorUserId,
                    OperatorName = op == null ? string.Empty : JoinName(op.Name, op.Surname),
                    OperatorEmail = op?.Email ?? string.Empty,
                    OfficeId = a.OfficeId,
                    OfficeName = office?.Name ?? string.Empty,
                });
            }
            return new ListResultDto<IntakeOfficeAssignmentDto>(
                dtos.OrderBy(d => d.OperatorName).ThenBy(d => d.OfficeName).ToList());
        }
    }

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged + searchable variant of
    /// <see cref="GetListAsync"/>. Removes that method's per-row N+1
    /// (FindByIdAsync + FindAsync per assignment) by batch-loading the host Intake
    /// operators and the office registry once into dictionaries, then joining in
    /// memory. Operator name / email / office filter, sort, and offset paging are
    /// applied in memory (the join spans the host identity + SaaS contexts, so a
    /// single SQL join is not possible). Host context throughout.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Default)]
    public virtual async Task<PagedResultDto<IntakeOfficeAssignmentDto>> GetPagedListAsync(
        GetIntakeAssignmentsInput input)
    {
        Check.NotNull(input, nameof(input));

        using (CurrentTenant.Change(null))
        {
            var assignments = await _assignmentRepository.GetListAsync();

            // Batch-load (one query each) instead of per-row lookups.
            var operatorsById = (await _userManager.GetUsersInRoleAsync(
                    InternalUserRoleDataSeedContributor.IntakeStaffRoleName))
                .Where(u => u.TenantId == null) // host operators only (exclude per-office shadows)
                .ToDictionary(u => u.Id);
            var officeNameById = (await _tenantRepository.GetListAsync())
                .ToDictionary(t => t.Id, t => t.Name);

            var projected = assignments
                .Select(a =>
                {
                    operatorsById.TryGetValue(a.OperatorUserId, out var op);
                    officeNameById.TryGetValue(a.OfficeId, out var officeName);
                    return new IntakeOfficeAssignmentDto
                    {
                        Id = a.Id,
                        OperatorUserId = a.OperatorUserId,
                        OperatorName = op == null ? string.Empty : JoinName(op.Name, op.Surname),
                        OperatorEmail = op?.Email ?? string.Empty,
                        OfficeId = a.OfficeId,
                        OfficeName = officeName ?? string.Empty,
                    };
                })
                .ToList();

            var filter = input.Filter?.Trim();
            var filtered = projected
                .Where(d => string.IsNullOrEmpty(filter)
                    || d.OperatorName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || d.OperatorEmail.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || d.OfficeName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var totalCount = filtered.Count;

            var page = SortIntakeAssignments(filtered, input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            return new PagedResultDto<IntakeOfficeAssignmentDto>(totalCount, page);
        }
    }

    private static List<IntakeOfficeAssignmentDto> SortIntakeAssignments(
        List<IntakeOfficeAssignmentDto> rows, string? sorting)
    {
        var parts = (sorting ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts.Length > 0 ? parts[0].ToLowerInvariant() : "operatorname";
        var descending = parts.Length > 1 && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<IntakeOfficeAssignmentDto> ordered = field switch
        {
            "operatoremail" or "email" => descending
                ? rows.OrderByDescending(r => r.OperatorEmail, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => r.OperatorEmail, StringComparer.OrdinalIgnoreCase),
            "officename" or "office" => descending
                ? rows.OrderByDescending(r => r.OfficeName, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => r.OfficeName, StringComparer.OrdinalIgnoreCase),
            _ => descending
                ? rows.OrderByDescending(r => r.OperatorName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.OfficeName, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => r.OperatorName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.OfficeName, StringComparer.OrdinalIgnoreCase),
        };
        return ordered.ToList();
    }

    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Manage)]
    public virtual async Task AssignAsync(AssignIntakeOfficeDto input)
    {
        Check.NotNull(input, nameof(input));

        using (CurrentTenant.Change(null))
        {
            var op = await _userManager.FindByIdAsync(input.OperatorUserId.ToString());
            if (op == null || op.TenantId != null)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserNotFound)
                    .WithData("UserId", input.OperatorUserId);
            }
            if (!await _userManager.IsInRoleAsync(op, InternalUserRoleDataSeedContributor.IntakeStaffRoleName))
            {
                // Only host Intake operators are assignable to offices.
                throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserInvalidRole)
                    .WithData("AttemptedRole", "(not Intake Staff)")
                    .WithData("AllowedRoles", InternalUserRoleDataSeedContributor.IntakeStaffRoleName);
            }
            if (await _tenantRepository.FindAsync(input.OfficeId) == null)
            {
                throw new EntityNotFoundException(typeof(Tenant), input.OfficeId);
            }

            var already = await _assignmentRepository.AnyAsync(
                x => x.OperatorUserId == input.OperatorUserId && x.OfficeId == input.OfficeId);
            if (!already)
            {
                await _assignmentRepository.InsertAsync(
                    new IntakeOfficeAssignment(GuidGenerator.Create(), input.OperatorUserId, input.OfficeId),
                    autoSave: true);
            }
        }

        // Eager (O-D3): ensure the limited shadow Intake user exists in the office DB.
        await _shadowProvisioner.EnsureShadowUserAsync(input.OfficeId, input.OperatorUserId);
    }

    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Manage)]
    public virtual async Task UnassignAsync(Guid operatorUserId, Guid officeId)
    {
        using (CurrentTenant.Change(null))
        {
            var row = await _assignmentRepository.FirstOrDefaultAsync(
                x => x.OperatorUserId == operatorUserId && x.OfficeId == officeId);
            if (row != null)
            {
                await _assignmentRepository.DeleteAsync(row, autoSave: true);
            }
        }

        // Defense in depth: lock the shadow user (the removed row already blocks the gate).
        await _shadowProvisioner.DisableShadowUserAsync(officeId, operatorUserId);
    }

    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Manage)]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetAssignableOperatorsAsync()
    {
        using (CurrentTenant.Change(null))
        {
            var operators = await _userManager.GetUsersInRoleAsync(
                InternalUserRoleDataSeedContributor.IntakeStaffRoleName);
            var items = operators
                .Where(u => u.TenantId == null) // host operators only (exclude per-office shadows)
                .OrderBy(u => u.UserName)
                .Select(u => new LookupDto<Guid> { Id = u.Id, DisplayName = DisplayFor(u) })
                .ToList();
            return new ListResultDto<LookupDto<Guid>>(items);
        }
    }

    [Authorize(CaseEvaluationPermissions.IntakeAssignments.Manage)]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetOfficeOptionsAsync()
    {
        using (CurrentTenant.Change(null))
        {
            var query = await _tenantRepository.GetQueryableAsync();
            var items = query
                .OrderBy(t => t.Name)
                .Select(t => new LookupDto<Guid> { Id = t.Id, DisplayName = t.Name! })
                .ToList();
            return new ListResultDto<LookupDto<Guid>>(items);
        }
    }

    [Authorize(CaseEvaluationPermissions.IntakeImpersonation.Default)]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetMyOfficesAsync()
    {
        return await GetAssignedOfficesAsync(CurrentUser.Id);
    }

    [Authorize]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetSwitchableOfficesAsync()
    {
        // In-office single-click hop (F Half 2): the caller is the impersonated office
        // user, so resolve the host operator behind the session from the signed
        // impersonation claim. A non-impersonating caller (a real office user, or a
        // host operator who has not switched in) has no such claim -> empty list. The
        // grant's per-office assignment gate is the real boundary; this only shapes
        // the switcher menu.
        var operatorId = Guid.TryParse(
            CurrentUser.FindClaim(AbpClaimTypes.ImpersonatorUserId)?.Value, out var id)
            ? id
            : (Guid?)null;
        return await GetAssignedOfficesAsync(operatorId);
    }

    private async Task<ListResultDto<LookupDto<Guid>>> GetAssignedOfficesAsync(Guid? operatorId)
    {
        if (operatorId == null)
        {
            return new ListResultDto<LookupDto<Guid>>(new List<LookupDto<Guid>>());
        }

        using (CurrentTenant.Change(null))
        {
            var assignments = await _assignmentRepository.GetListAsync(a => a.OperatorUserId == operatorId.Value);
            var items = new List<LookupDto<Guid>>(assignments.Count);
            foreach (var a in assignments)
            {
                var office = await _tenantRepository.FindAsync(a.OfficeId);
                if (office != null)
                {
                    items.Add(new LookupDto<Guid> { Id = office.Id, DisplayName = office.Name! });
                }
            }
            return new ListResultDto<LookupDto<Guid>>(items.OrderBy(i => i.DisplayName).ToList());
        }
    }

    private static string DisplayFor(IdentityUser user)
    {
        var name = JoinName(user.Name, user.Surname);
        return string.IsNullOrWhiteSpace(name) ? user.Email ?? user.UserName : $"{name} ({user.Email})";
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast) return first!.Trim() + " " + last!.Trim();
        if (hasFirst) return first!.Trim();
        if (hasLast) return last!.Trim();
        return string.Empty;
    }
}
