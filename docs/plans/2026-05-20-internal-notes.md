---
status: draft
issue: internal-appointment-notes
owner: AdrianG
created: 2026-05-20
approach: tdd (entity + manager invariants are pure logic) + code
  (controller + Angular panel are orchestration)
sequence: standalone feature; no upstream dependency
depends-on: external-user appointment view component (already shipped)
parity-audit: docs/parity/wave-1-parity/appointment-notes.md
branch: create a new branch off `feat/replicate-old-app`. PR back
  to `feat/replicate-old-app`.
decisions-locked-2026-05-20:
  Q1 (visibility): Internal-staff only. ITAdmin, StaffSupervisor,
    ClinicStaff can see and write notes. External users (Patient,
    AA, DA, CE) NEVER see notes on the appointment view -- the
    Notes tab is hidden from them and the API rejects their reads
    with 403. HIPAA-conservative choice.
  Q2 (rich text): Plain text only. Multi-line textarea. Mirrors
    OLD; no rich-text library; no XSS sanitization risk surface.
  Q3 (edit permissions): Author-only + admin override. The note's
    creator (CreatorId == CurrentUser.Id) can edit own notes;
    StaffSupervisor and ITAdmin can edit any note. ClinicStaff
    cannot edit notes they did not write.
  Q4 (entity name): `AppointmentNote`. Matches NEW convention --
    AppointmentAccessor, AppointmentLanguage, AppointmentApplicantAttorney
    all use the same prefix. Strict parity is preserved on data
    shape, not class name.
  Q5 (IsLatest invariant): On edit, set IsLatest=false on every
    other row in the same edit chain (defensive). Cheap server-
    side and protects against data drift if a row ever escapes
    the standard edit flow.
---

# Internal appointment notes

## Goal

Add a working internal-staff-only notes feature to the appointment
view page. Threaded (reply-to-parent), with edit history preserved
(insert-on-edit pattern, not in-place update). Mirrors OLD's data
model + behavior, fixes OLD's missing front-end wiring + missing
permission gate.

## Why

OLD shipped Notes as half-built: backend CRUD + threading + edit
chain logic existed, but the Angular UI was a `<h1>Note</h1>` stub
and the appointment-detail page button was commented out. The
parity audit at `docs/parity/wave-1-parity/appointment-notes.md`
flags this as a priority-3 gap.

Clinic staff need a working notes surface to record per-appointment
context that doesn't fit existing fields: "patient called to confirm
time", "AA emailed wanting reschedule", "scheduling conflict to
resolve before approval". Today they have nowhere to put this.

OLD's data model is sound (threading + edit chain). The fix is to
ship the entity + AppService + a working Angular panel.

## Non-goals

- No external-user visibility (decision Q1 = internal-only).
- No rich text editor in MVP (decision Q2).
- No bulk operations (delete multiple, etc.) -- one note at a time.
- No notification triggers when a note is added -- OLD didn't have
  any. If clinic staff want to alert each other, the existing
  email/dashboard surfaces stay primary.
- No mention/tagging (@username) -- not in OLD; not in scope.
- No attachments on notes -- documents have their own surface.
- No keyword search across notes -- per-appointment is plenty for
  MVP.

## Data model

The `AppointmentNote` entity:

```csharp
public class AppointmentNote :
    FullAuditedAggregateRoot<Guid>,
    IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid AppointmentId { get; protected set; }

    /// <summary>
    /// Free-text content. Plain text only; no HTML allowed
    /// (server strips on save). Soft 4000-char cap to match
    /// OLD's untyped nvarchar(max) usage in practice.
    /// </summary>
    public virtual string Comments { get; set; } = string.Empty;

    /// <summary>
    /// 2026-05-15 parity audit -- null when this is a top-level
    /// note; set to the parent's Id when this is a reply.
    /// Forms the reply tree.
    /// </summary>
    public virtual Guid? ParentNoteId { get; protected set; }

    /// <summary>
    /// 2026-05-15 parity audit -- null when this is an original
    /// note. When set, references the FIRST row of the edit
    /// chain (NOT the immediately-prior version). Lets the UI
    /// group all revisions of one note together.
    /// </summary>
    public virtual Guid? EditNoteId { get; protected set; }

    /// <summary>
    /// True when this is the latest revision visible in the
    /// default list. When an edit lands, the new row is
    /// IsLatest=true and every other row in the same chain is
    /// flipped to IsLatest=false.
    /// </summary>
    public virtual bool IsLatest { get; protected set; }

    protected AppointmentNote() { }

    public AppointmentNote(
        Guid id,
        Guid appointmentId,
        string comments,
        Guid? parentNoteId = null)
    {
        Id = id;
        AppointmentId = appointmentId;
        Comments = Check.NotNullOrWhiteSpace(comments, nameof(comments), maxLength: 4000);
        ParentNoteId = parentNoteId;
        EditNoteId = null;
        IsLatest = true;
    }

    internal void MarkSupersededByEdit()
    {
        IsLatest = false;
    }

    internal void StampAsEditOf(Guid editChainRootId)
    {
        EditNoteId = editChainRootId;
    }
}
```

Notes:
- `FullAuditedAggregateRoot<Guid>` brings `CreatorId`,
  `CreationTime`, `LastModifierId`, `LastModificationTime`,
  `IsDeleted`, `DeleterId`, `DeletionTime`. These cover OLD's
  `CreatedById/Date`, `ModifiedById/Date`, and `StatusId`
  (replaced by ABP's `ISoftDelete`).
- `IMultiTenant` brings the auto tenant filter -- no manual
  WHERE clauses needed.
- The `Check.NotNullOrWhiteSpace` + 4000-char cap on comments
  enforces the invariant from OLD (`[Required]` attribute) plus
  a reasonable upper bound. OLD used nvarchar(max); we keep that
  in the column but enforce at the entity level.

## Edit-chain semantics (lifted from OLD's NoteDomain.cs:52-77)

When editing note X:
1. Load X (the note being edited).
2. Compute the chain root: if X.EditNoteId is null, X IS the
   root, so chain root = X.Id. Otherwise chain root = X.EditNoteId.
3. Insert a NEW row Y with: Y.Comments = new comments,
   Y.AppointmentId = X.AppointmentId, Y.ParentNoteId = X.ParentNoteId,
   Y.EditNoteId = chain root, Y.IsLatest = true.
4. Flip every existing row Z where Z.EditNoteId == chain root
   OR Z.Id == chain root: set Z.IsLatest = false, Z.IsDeleted = true.
   (Soft-delete + IsLatest=false in one pass. The chain root's
   own Comments stays intact for audit, but it's hidden from the
   default list filter.)
5. Return Y.

This matches OLD's "preserve the first-created-date when editing
an already-edited note" behavior because we anchor on the chain
root, not on the immediately-prior version. Decision Q5 is what
makes step 4 chain-wide instead of single-row.

Delete: ABP soft-delete via `await _repository.DeleteAsync(id)`.
Sets `IsDeleted=true`. The default query filter excludes deleted
rows.

## Threading semantics

A reply to note X:
- Set ParentNoteId = X.Id on the new note.
- New note's IsLatest=true; EditNoteId=null.
- A reply IS the top of its own potential edit chain.
- Cannot reply to a reply more than 1 level deep (MVP -- flatten
  the tree to 2 levels). Decision: if `ParentNoteId` is set on
  the input AND that parent already has a non-null ParentNoteId,
  reject with `AppointmentNoteRepliesMustBeOneLevel` error code.

UI shows notes grouped by chain root, with replies nested one
level under their parent. Edits collapse to the latest visible
row.

## Files touched

### 1. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`

Add four new const strings:

```csharp
/// <summary>
/// 2026-05-20 -- raised when AppointmentNoteManager.UpdateAsync
/// is called by a user who is not the note's CreatorId AND is
/// not StaffSupervisor / ITAdmin.
/// </summary>
public const string AppointmentNoteEditPermissionDenied =
    "CaseEvaluation:AppointmentNote.EditPermissionDenied";

/// <summary>
/// 2026-05-20 -- raised when a reply is attempted on a note
/// that is already a reply (one-level-deep tree invariant).
/// </summary>
public const string AppointmentNoteRepliesMustBeOneLevel =
    "CaseEvaluation:AppointmentNote.RepliesMustBeOneLevel";

/// <summary>
/// 2026-05-20 -- raised when an external user attempts any
/// notes operation (List / Create / Update / Delete). The
/// notes feature is internal-staff only by decision Q1.
/// </summary>
public const string AppointmentNoteExternalUserDenied =
    "CaseEvaluation:AppointmentNote.ExternalUserDenied";

/// <summary>
/// 2026-05-20 -- raised when the note's Comments string is
/// null, empty, whitespace-only, or exceeds 4000 characters.
/// </summary>
public const string AppointmentNoteCommentsInvalid =
    "CaseEvaluation:AppointmentNote.CommentsInvalid";
```

### 2. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

Add the localization values:

```jsonc
"AppointmentNote:EditPermissionDenied":
  "You can only edit notes you created. Contact a Staff Supervisor if this note needs correction.",
"AppointmentNote:RepliesMustBeOneLevel":
  "Replies cannot themselves have replies. Add another reply to the original note instead.",
"AppointmentNote:ExternalUserDenied":
  "Notes are visible to clinic staff only.",
"AppointmentNote:CommentsInvalid":
  "Note must contain between 1 and 4000 characters of text.",
"Menu:AppointmentNotes": "Notes",
"AppointmentNote:Tab": "Notes",
"AppointmentNote:Empty": "No notes on this appointment yet.",
"AppointmentNote:AddPlaceholder": "Add a note...",
"AppointmentNote:ReplyPlaceholder": "Reply...",
"AppointmentNote:EditButton": "Edit",
"AppointmentNote:DeleteButton": "Delete",
"AppointmentNote:DeleteConfirm": "Delete this note? This cannot be undone."
```

### 3. `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`

Map the four new error codes to HTTP 400 (or 403 for the
permission-denied codes). Look at the existing block in this
file for the pattern. Add:

```csharp
options.MapAbpExceptions<BusinessException>(builder =>
{
    builder.Map(CaseEvaluationDomainErrorCodes.AppointmentNoteEditPermissionDenied)
           .WithStatusCode(403);
    builder.Map(CaseEvaluationDomainErrorCodes.AppointmentNoteExternalUserDenied)
           .WithStatusCode(403);
    builder.Map(CaseEvaluationDomainErrorCodes.AppointmentNoteRepliesMustBeOneLevel)
           .WithStatusCode(400);
    builder.Map(CaseEvaluationDomainErrorCodes.AppointmentNoteCommentsInvalid)
           .WithStatusCode(400);
});
```

(Confirm the exact builder pattern by reading the file -- the
exact API may differ from this sketch; the intent is what
matters.)

### 4. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`

Add the new permission constants:

```csharp
public static class AppointmentNotes
{
    public const string Default = GroupName + ".AppointmentNotes";
    public const string Create = Default + ".Create";
    public const string Edit = Default + ".Edit";
    public const string Delete = Default + ".Delete";
}
```

### 5. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs`

Register the parent + 3 children under an existing or new
permission group. Pattern matches the existing `Appointments`
registration.

### 6. `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentNotes/AppointmentNote.cs`

The entity (full code in the data model section above).

### 7. `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentNotes/IAppointmentNoteRepository.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentNotes;

public interface IAppointmentNoteRepository : IRepository<AppointmentNote, Guid>
{
    /// <summary>
    /// 2026-05-20 -- list the visible notes for one appointment.
    /// Filters: IsLatest=true (latest revision only) and
    /// IMultiTenant + ISoftDelete are applied by ABP. Orders by
    /// CreationTime ascending so the UI can render top-to-bottom.
    /// </summary>
    Task<List<AppointmentNote>> GetListByAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 2026-05-20 -- list every row that shares an edit-chain
    /// root id (the chain root itself + every edit on it). Used
    /// by the manager to flip IsLatest=false chain-wide when a
    /// new edit lands. Bypasses the default-soft-delete filter
    /// because chain rows may already be IsDeleted=true from
    /// prior edits.
    /// </summary>
    Task<List<AppointmentNote>> GetEditChainAsync(
        Guid chainRootId,
        CancellationToken cancellationToken = default);
}
```

### 8. `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentNotes/AppointmentNoteManager.cs`

Domain service that wraps the edit-chain invariant + author /
admin permission gate. Reads `ICurrentUser` to identify the
caller. Reads `IPermissionChecker` to test for admin override.

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using HealthcareSupport.CaseEvaluation.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.AppointmentNotes;

public class AppointmentNoteManager : DomainService
{
    private readonly IAppointmentNoteRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IIdentityUserRepository _userRepository;

    public AppointmentNoteManager(
        IAppointmentNoteRepository repository,
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IIdentityUserRepository userRepository)
    {
        _repository = repository;
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _userRepository = userRepository;
    }

    public virtual async Task<AppointmentNote> CreateAsync(
        Guid appointmentId,
        string comments,
        Guid? parentNoteId = null)
    {
        EnsureInternalUser();
        EnsureCommentsValid(comments);

        if (parentNoteId.HasValue)
        {
            var parent = await _repository.GetAsync(parentNoteId.Value);
            if (parent.AppointmentId != appointmentId)
            {
                throw new UserFriendlyException(L["AppointmentNote:ParentMustMatchAppointment"]);
            }
            if (parent.ParentNoteId.HasValue)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.AppointmentNoteRepliesMustBeOneLevel);
            }
        }

        var note = new AppointmentNote(
            GuidGenerator.Create(),
            appointmentId,
            comments,
            parentNoteId);
        return await _repository.InsertAsync(note, autoSave: true);
    }

    public virtual async Task<AppointmentNote> UpdateAsync(
        Guid noteId,
        string newComments)
    {
        EnsureInternalUser();
        EnsureCommentsValid(newComments);

        var existing = await _repository.GetAsync(noteId);
        await EnsureCanEditAsync(existing);

        var chainRoot = existing.EditNoteId ?? existing.Id;

        // Insert the new revision FIRST so we have something
        // to point at. Then flip every chain row to not-latest.
        var newRevision = new AppointmentNote(
            GuidGenerator.Create(),
            existing.AppointmentId,
            newComments,
            existing.ParentNoteId);
        newRevision.StampAsEditOf(chainRoot);

        await _repository.InsertAsync(newRevision, autoSave: true);

        // Chain-wide flip (decision Q5): EVERY existing row in
        // the chain (root + prior edits) goes to IsLatest=false
        // + IsDeleted=true, including the row the user clicked
        // "edit" on. The new row is the sole IsLatest=true.
        var chainRows = await _repository.GetEditChainAsync(chainRoot);
        foreach (var row in chainRows)
        {
            row.MarkSupersededByEdit();
            await _repository.UpdateAsync(row);
        }
        await _repository.DeleteManyAsync(chainRows.Select(r => r.Id).ToList());

        return newRevision;
    }

    public virtual async Task DeleteAsync(Guid noteId)
    {
        EnsureInternalUser();
        var existing = await _repository.GetAsync(noteId);
        await EnsureCanEditAsync(existing);
        await _repository.DeleteAsync(existing);
    }

    private void EnsureInternalUser()
    {
        // Internal users in this codebase are identified by the
        // ABSENCE of the IsExternalUser extension prop set to
        // true. Reading via ICurrentUser's extension props.
        var isExternal = _currentUser.FindClaim("IsExternalUser")?.Value;
        if (string.Equals(isExternal, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentNoteExternalUserDenied);
        }
    }

    private static void EnsureCommentsValid(string comments)
    {
        if (string.IsNullOrWhiteSpace(comments) || comments.Length > 4000)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentNoteCommentsInvalid);
        }
    }

    private async Task EnsureCanEditAsync(AppointmentNote note)
    {
        if (note.CreatorId == _currentUser.Id)
        {
            return;  // own note
        }
        // Admin override: StaffSupervisor or ITAdmin can edit
        // anyone's note. Check via .Edit permission.
        if (await _permissionChecker.IsGrantedAsync(
                CaseEvaluationPermissions.AppointmentNotes.Edit))
        {
            // (.Edit is granted to all internal users, so the
            // distinguishing check is via role membership.)
            var user = await _userRepository.GetAsync(_currentUser.Id!.Value);
            // TODO confirm role-name constants -- the project's
            // role names are seeded in CaseEvaluationDataSeederContributor.
            // For MVP: StaffSupervisor + ITAdmin can edit any.
            // Replace this string check with a permission constant
            // (e.g. AppointmentNotes.EditAny) if a separate
            // permission is preferred.
            var roleNames = await _userRepository.GetRoleNamesAsync(_currentUser.Id!.Value);
            if (roleNames.Any(r =>
                string.Equals(r, "ITAdmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, "StaffSupervisor", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }
        }
        throw new BusinessException(
            CaseEvaluationDomainErrorCodes.AppointmentNoteEditPermissionDenied);
    }
}
```

Note on role check approach: this plan uses role-name strings
because the project's existing role gates also string-match the
seeded names. A cleaner design would introduce a separate
`AppointmentNotes.EditAny` permission and grant it only to those
roles -- if Adrian prefers that, swap the role-name strings for a
single `_permissionChecker.IsGrantedAsync(AppointmentNotes.EditAny)`
call and add the permission to the provider.

### 9. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentNotes/`

DTOs:

```csharp
// AppointmentNoteDto.cs
public class AppointmentNoteDto : FullAuditedEntityDto<Guid>
{
    public Guid AppointmentId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public Guid? ParentNoteId { get; set; }
    public Guid? EditNoteId { get; set; }
    public bool IsLatest { get; set; }

    /// <summary>
    /// 2026-05-20 -- populated by the AppService from
    /// IIdentityUserRepository so the UI can show "by Patrick"
    /// without an extra round-trip. Null when the creator's
    /// IdentityUser row no longer exists.
    /// </summary>
    public string? CreatorDisplayName { get; set; }

    /// <summary>
    /// 2026-05-20 -- true when the current caller can edit /
    /// delete this note. Computed server-side from CreatorId +
    /// the caller's role. Saves the UI a round-trip on
    /// permission decisions.
    /// </summary>
    public bool CanEdit { get; set; }
}

// AppointmentNoteCreateDto.cs
public class AppointmentNoteCreateDto
{
    public Guid AppointmentId { get; set; }
    public string Comments { get; set; } = string.Empty;
    public Guid? ParentNoteId { get; set; }
}

// AppointmentNoteUpdateDto.cs
public class AppointmentNoteUpdateDto : IHasConcurrencyStamp
{
    public string Comments { get; set; } = string.Empty;
    public string? ConcurrencyStamp { get; set; }
}

// GetAppointmentNotesInput.cs
public class GetAppointmentNotesInput
{
    public Guid AppointmentId { get; set; }
}
```

### 10. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentNotes/IAppointmentNotesAppService.cs`

```csharp
public interface IAppointmentNotesAppService : IApplicationService
{
    /// <summary>
    /// 2026-05-20 -- returns IsLatest=true rows for the
    /// appointment in CreationTime ascending order. Replies are
    /// returned alongside their parents; the UI groups them.
    /// External users get 403 (BusinessException with
    /// AppointmentNoteExternalUserDenied).
    /// </summary>
    Task<List<AppointmentNoteDto>> GetListByAppointmentAsync(
        GetAppointmentNotesInput input);

    Task<AppointmentNoteDto> CreateAsync(AppointmentNoteCreateDto input);
    Task<AppointmentNoteDto> UpdateAsync(Guid id, AppointmentNoteUpdateDto input);
    Task DeleteAsync(Guid id);
}
```

### 11. `src/HealthcareSupport.CaseEvaluation.Application/AppointmentNotes/AppointmentNotesAppService.cs`

```csharp
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.AppointmentNotes.Default)]
public class AppointmentNotesAppService :
    CaseEvaluationAppService,
    IAppointmentNotesAppService
{
    private readonly IAppointmentNoteRepository _repository;
    private readonly AppointmentNoteManager _manager;
    private readonly IIdentityUserRepository _userRepository;

    public AppointmentNotesAppService(
        IAppointmentNoteRepository repository,
        AppointmentNoteManager manager,
        IIdentityUserRepository userRepository)
    {
        _repository = repository;
        _manager = manager;
        _userRepository = userRepository;
    }

    public virtual async Task<List<AppointmentNoteDto>> GetListByAppointmentAsync(
        GetAppointmentNotesInput input)
    {
        EnsureInternalUser();
        var rows = await _repository.GetListByAppointmentAsync(input.AppointmentId);
        return await BuildDtosAsync(rows);
    }

    [Authorize(CaseEvaluationPermissions.AppointmentNotes.Create)]
    public virtual async Task<AppointmentNoteDto> CreateAsync(AppointmentNoteCreateDto input)
    {
        var note = await _manager.CreateAsync(
            input.AppointmentId, input.Comments, input.ParentNoteId);
        var dtos = await BuildDtosAsync(new List<AppointmentNote> { note });
        return dtos.Single();
    }

    [Authorize(CaseEvaluationPermissions.AppointmentNotes.Edit)]
    public virtual async Task<AppointmentNoteDto> UpdateAsync(
        Guid id,
        AppointmentNoteUpdateDto input)
    {
        var newRevision = await _manager.UpdateAsync(id, input.Comments);
        var dtos = await BuildDtosAsync(new List<AppointmentNote> { newRevision });
        return dtos.Single();
    }

    [Authorize(CaseEvaluationPermissions.AppointmentNotes.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _manager.DeleteAsync(id);
    }

    private async Task<List<AppointmentNoteDto>> BuildDtosAsync(
        List<AppointmentNote> rows)
    {
        var creatorIds = rows.Where(r => r.CreatorId.HasValue)
                             .Select(r => r.CreatorId!.Value)
                             .Distinct()
                             .ToList();
        var users = await _userRepository.GetListAsync(
            includeDetails: false, filter: null);
        var displayMap = users
            .Where(u => creatorIds.Contains(u.Id))
            .ToDictionary(u => u.Id,
                u => $"{u.Name} {u.Surname}".Trim());

        var result = new List<AppointmentNoteDto>();
        foreach (var row in rows)
        {
            var dto = ObjectMapper.Map<AppointmentNote, AppointmentNoteDto>(row);
            if (row.CreatorId.HasValue
                && displayMap.TryGetValue(row.CreatorId.Value, out var name))
            {
                dto.CreatorDisplayName = name;
            }
            dto.CanEdit = await CanEditAsync(row);
            result.Add(dto);
        }
        return result;
    }

    private async Task<bool> CanEditAsync(AppointmentNote note)
    {
        if (note.CreatorId == CurrentUser.Id) return true;
        if (CurrentUser.Id is null) return false;
        var roleNames = await _userRepository.GetRoleNamesAsync(CurrentUser.Id.Value);
        return roleNames.Any(r =>
            string.Equals(r, "ITAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "StaffSupervisor", StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureInternalUser()
    {
        var isExternal = CurrentUser.FindClaim("IsExternalUser")?.Value;
        if (string.Equals(isExternal, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.AppointmentNoteExternalUserDenied);
        }
    }
}
```

### 12. `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs`

Add a new mapper partial class:

```csharp
[Mapper]
public partial class AppointmentNoteToAppointmentNoteDtoMapper :
    MapperBase<AppointmentNote, AppointmentNoteDto>
{
    [MapperIgnoreTarget(nameof(AppointmentNoteDto.CreatorDisplayName))]
    [MapperIgnoreTarget(nameof(AppointmentNoteDto.CanEdit))]
    public override partial AppointmentNoteDto Map(AppointmentNote source);

    [MapperIgnoreTarget(nameof(AppointmentNoteDto.CreatorDisplayName))]
    [MapperIgnoreTarget(nameof(AppointmentNoteDto.CanEdit))]
    public override partial void Map(AppointmentNote source, AppointmentNoteDto destination);
}
```

### 13. `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/AppointmentNotes/EfCoreAppointmentNoteRepository.cs`

```csharp
public class EfCoreAppointmentNoteRepository :
    EfCoreRepository<CaseEvaluationDbContext, AppointmentNote, Guid>,
    IAppointmentNoteRepository
{
    public EfCoreAppointmentNoteRepository(
        IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider) { }

    public virtual async Task<List<AppointmentNote>> GetListByAppointmentAsync(
        Guid appointmentId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(x => x.AppointmentId == appointmentId)
            .Where(x => x.IsLatest)
            .OrderBy(x => x.CreationTime)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<AppointmentNote>> GetEditChainAsync(
        Guid chainRootId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        // Bypass the IsDeleted query filter -- chain rows may be
        // soft-deleted from prior edits. Manager needs to read
        // them all to enforce the chain-wide IsLatest invariant.
        return await dbSet
            .IgnoreQueryFilters()
            .Where(x => x.Id == chainRootId || x.EditNoteId == chainRootId)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
```

### 14. `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

Add the entity configuration block. Note: this entity is
tenant-scoped, so the config block lives OUTSIDE the
`IsHostDatabase()` guard (mirror Appointment / DoctorAvailability):

```csharp
builder.Entity<AppointmentNote>(b =>
{
    b.ToTable(
        CaseEvaluationConsts.DbTablePrefix + "AppointmentNotes",
        CaseEvaluationConsts.DbSchema);
    b.ConfigureByConvention();
    b.Property(x => x.TenantId).HasColumnName(nameof(AppointmentNote.TenantId));
    b.Property(x => x.AppointmentId)
        .HasColumnName(nameof(AppointmentNote.AppointmentId)).IsRequired();
    b.Property(x => x.Comments)
        .HasColumnName(nameof(AppointmentNote.Comments))
        .IsRequired()
        .HasMaxLength(4000);
    b.Property(x => x.ParentNoteId).HasColumnName(nameof(AppointmentNote.ParentNoteId));
    b.Property(x => x.EditNoteId).HasColumnName(nameof(AppointmentNote.EditNoteId));
    b.Property(x => x.IsLatest)
        .HasColumnName(nameof(AppointmentNote.IsLatest))
        .IsRequired()
        .HasDefaultValue(true);
    b.HasOne<Appointment>()
        .WithMany()
        .HasForeignKey(x => x.AppointmentId)
        .IsRequired()
        .OnDelete(DeleteBehavior.NoAction);
    b.HasIndex(x => new { x.AppointmentId, x.IsLatest });
    b.HasIndex(x => x.ParentNoteId);
    b.HasIndex(x => x.EditNoteId);
});
```

Repeat the same block in `CaseEvaluationTenantDbContext.cs`
(tenant context needs its own configuration).

### 15. EF Core migration: `Add_AppointmentNotes`

Generate:

```bash
DOTNET_ENVIRONMENT=Development \
  dotnet ef migrations add Add_AppointmentNotes \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host \
  --context CaseEvaluationDbContext
```

The auto-generated migration should be reviewed (no edits expected
for a clean add-table migration). Confirm the index names and the
NOT NULL constraint on Comments.

### 16. `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentNotes/AppointmentNoteController.cs`

Manual controller per ABP convention (the AppService has
`[RemoteService(IsEnabled = false)]`):

```csharp
[Route("api/app/appointment-notes")]
public class AppointmentNoteController :
    AbpController,
    IAppointmentNotesAppService
{
    private readonly IAppointmentNotesAppService _service;
    public AppointmentNoteController(IAppointmentNotesAppService service)
    {
        _service = service;
    }

    [HttpGet]
    [Route("by-appointment")]
    public virtual Task<List<AppointmentNoteDto>> GetListByAppointmentAsync(
        [FromQuery] GetAppointmentNotesInput input)
        => _service.GetListByAppointmentAsync(input);

    [HttpPost]
    public virtual Task<AppointmentNoteDto> CreateAsync(AppointmentNoteCreateDto input)
        => _service.CreateAsync(input);

    [HttpPut("{id}")]
    public virtual Task<AppointmentNoteDto> UpdateAsync(
        Guid id, AppointmentNoteUpdateDto input)
        => _service.UpdateAsync(id, input);

    [HttpDelete("{id}")]
    public virtual Task DeleteAsync(Guid id) => _service.DeleteAsync(id);
}
```

### 17. Angular proxy regeneration

After the backend compiles + the proxy refresh, the Angular app
gains `angular/src/app/proxy/appointment-notes/`.

```bash
cd angular
yarn nswag refresh
```

### 18. Angular: `angular/src/app/appointments/notes/` (NEW folder)

Add a notes panel that mounts inside the appointment view page
(`appointment-view.component.ts`). New files:

- `appointment-notes.component.ts` + `.html` + `.scss` (standalone)
- `appointment-notes.service.ts` (thin wrapper over the proxy)

The panel reads the appointment ID from its parent route, calls
`getListByAppointment`, renders the threaded list with reply +
edit + delete buttons (gated by `dto.canEdit`). Plain `<textarea>`
for input. Empty state: "No notes on this appointment yet."

Gating: the parent appointment view must hide the notes panel
when the current user is external. Use ABP's
`PermissionService.getGrantedPolicy('CaseEvaluation.AppointmentNotes')`
or check the `isExternalUser` claim on `ConfigStateService`.

### 19. Angular: `appointment-view.component.ts` integration

Insert a section above the existing layout's footer:

```html
@if (canSeeNotes) {
  <app-appointment-notes [appointmentId]="appointmentId" />
}
```

Where `canSeeNotes` is true iff the current user is internal
(has `CaseEvaluation.AppointmentNotes` permission OR is in
ITAdmin / StaffSupervisor / ClinicStaff role).

## Test plan

### `test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentNotes/AppointmentNoteManagerTests.cs`

TDD: 12 facts on the manager + entity invariants.

| # | Test | Acceptance |
|---|------|------------|
| 1 | `CreateAsync_PlainNote_Persists` | Note inserted with IsLatest=true, ParentNoteId=null, EditNoteId=null. |
| 2 | `CreateAsync_EmptyComments_Throws` | `BusinessException` with `AppointmentNoteCommentsInvalid`. |
| 3 | `CreateAsync_4001CharComments_Throws` | Same. |
| 4 | `CreateAsync_AsReply_LinksParent` | New note has ParentNoteId = parent's Id. |
| 5 | `CreateAsync_ReplyToReply_Throws` | `AppointmentNoteRepliesMustBeOneLevel`. |
| 6 | `UpdateAsync_AsAuthor_CreatesNewRowFlipsOld` | New row IsLatest=true; old row IsLatest=false + IsDeleted=true. New row's EditNoteId = original's Id. |
| 7 | `UpdateAsync_AsNonAuthorNonAdmin_Throws` | `AppointmentNoteEditPermissionDenied`. |
| 8 | `UpdateAsync_AsAdmin_Succeeds` | StaffSupervisor edits another user's note; succeeds. |
| 9 | `UpdateAsync_EditOfEdit_AnchorsOnChainRoot` | Edit rev2 of rev1 of original. rev3 EditNoteId = original.Id (NOT rev1.Id). rev1 + original both flipped to IsLatest=false + IsDeleted. |
| 10 | `UpdateAsync_MultipleStrayLatestInChain_AllFlipped` | Seed a chain with two stray IsLatest=true rows. After edit, exactly one row in chain has IsLatest=true. (Invariant from decision Q5.) |
| 11 | `DeleteAsync_AsAuthor_SoftDeletes` | `IsDeleted=true`; default list query excludes. |
| 12 | `CreateAsync_AsExternalUser_Throws` | `AppointmentNoteExternalUserDenied`. |

### `test/HealthcareSupport.CaseEvaluation.Application.Tests/AppointmentNotes/AppointmentNotesAppServiceTests.cs`

3 facts on the AppService boundary:

| # | Test | Acceptance |
|---|------|------------|
| 13 | `GetListByAppointmentAsync_ReturnsOnlyLatest` | Seed original + edit + reply. Returns 2 rows (latest edit + reply); excludes the soft-deleted original. |
| 14 | `GetListByAppointmentAsync_PopulatesCanEdit` | Author's notes: CanEdit=true. Other staff's notes: CanEdit=false (unless caller is admin). |
| 15 | `GetListByAppointmentAsync_AsExternalUser_Throws` | 403 BusinessException at the boundary, not at the manager. |

### Manual UI verification

After backend + Angular ship:

1. Log in as Clinic Staff. Navigate to an appointment view.
2. Notes panel visible. Type "Patient called to confirm" + send.
   New row appears.
3. Click Edit. Change text to "Patient called to confirm pickup".
   Row updates in place visually. SQL probe: 2 rows in
   `AppointmentNotes` table; old row IsLatest=0 + IsDeleted=1.
4. Click Reply on the same note. Add "Follow up tomorrow". New
   row appears nested under parent.
5. Try to reply to the reply -- button disabled (or 400 from API
   if you bypass). Pin the disabled state in a test.
6. Log out, log back in as another Clinic Staff. View the same
   appointment. Notes visible. Edit / Delete buttons on the
   other user's notes are hidden (CanEdit=false). Edit / Delete
   on own notes work.
7. Log in as Staff Supervisor. Notes visible. Can edit / delete
   any note (CanEdit=true on all rows).
8. Log out, log in as Patient (external). View the same
   appointment. Notes panel NOT rendered. Direct curl to
   `/api/app/appointment-notes/by-appointment?appointmentId=...`
   returns 403.

## Risk and rollback

**Blast radius:**

- One new entity, one new migration, one new AppService, one
  new controller, one new Angular component, one new menu /
  panel insertion on the existing appointment view page.
- ABP's soft-delete + tenant filter cover the data-access
  guarantees automatically.
- No existing tests should fail.

**Rollback:**

- Revert the commit. Run
  `dotnet ef database update <previous_migration>` against the
  affected DB. The `Down()` drops the table.
- The appointment view page falls back to its pre-rework layout
  without the Notes panel.

**Risk: ICurrentUser claim check for IsExternalUser may not be
populated for all auth flows.** Mitigated by the integration
test #15 above. If the claim is missing entirely, the guard
errs on the side of "internal" -- not ideal for HIPAA. Confirm
the claim is populated by reading the existing external-user
auth flow.

**Risk: role-name string-matching is fragile.** A future rename
of the StaffSupervisor or ITAdmin role would silently break the
admin-override path. Mitigated by:
- Adding an integration test that asserts the seeded role names
  match the strings used in this code.
- Optionally swapping for a dedicated `AppointmentNotes.EditAny`
  permission and granting it to those roles in the seeder.

**Risk: chain-wide IsLatest flip on edit holds locks on many
rows in long edit chains.** Mitigated by: edit chains are
expected to be tiny (most notes get edited 0 times; some get
edited 1-2 times; almost none get edited 5+ times). A row-lock
on 5 rows is fine.

**Risk: empty-state UX when no notes exist on an appointment.**
The Angular template renders the "No notes on this appointment
yet." string + a focused composer.

## Verification

End-to-end:

1. Fresh DB; migrate; seed users via DbMigrator.
2. Open an appointment view as Clinic Staff. Notes panel renders.
3. Run the 15 unit + integration tests above.
4. SQL probe after a few create + edit operations:
   ```sql
   SELECT Id, ParentNoteId, EditNoteId, IsLatest, IsDeleted,
          CreatorId, CreationTime, Comments
   FROM AppEntity.AppointmentNotes
   WHERE AppointmentId = '<id>'
   ORDER BY CreationTime;
   ```
   Expected: every chain has exactly one IsLatest=true +
   IsDeleted=false row.

## How to apply

- Create a new branch off `feat/replicate-old-app`.
- Land all changes in a single PR back to `feat/replicate-old-app`.
- Parallel session coordination: this plan does NOT touch the
  Slot Generation Rework series or the Doctor invariant. Can
  ship in parallel.
