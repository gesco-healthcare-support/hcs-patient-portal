# Integration Prompts — File Management & Language Management (addendum)

Two host/IT-Admin pages designed **after** the main redesign handoff. The rest of the rework is already handed off and largely implemented, so this is a focused addendum — same codebase, same conventions, same component system. Drop this folder into the repo (e.g. `<repo>/design-admin/`) and run the prompts in Claude Code inside the repo.

**Pages:** File Management (blob storage) · Language Management.

---

## Phase 0 — Kickoff (paste at the start of every session)

```
You are adding two host/IT-Admin pages to this Angular 20 + ABP v10 codebase (angular/ = SPA, src/ = .NET backend). These match the existing redesigned internal pages exactly — same system, not a new one.

Read first:
1. design-admin/<page>.html — the reference prototype for the page (React-in-browser; recreate as Angular standalone components, do NOT copy React in, and do NOT implement the prototype's top "chrome" bar — that's a harness-only state switcher).
2. design-admin/styles/tokens.css — design tokens (already ported in the main handoff as _tokens.scss; reuse). Brand is fixed: primary #055495, accent green #9dc13b, Roboto. No new colors/fonts.
3. design-admin/components/in-shell.jsx — the navy sidebar shell ALL these pages render inside; should already exist as the internal layout from the main handoff. These two pages are new routes in the IT-Admin (host) "System" nav group. If the shell isn't built yet, build it first.

Conventions to reuse (already in the app from the main handoff — match, don't reinvent):
- Scaffold: `.ia-head` (title + actions), `.ia-toolbar`/`.ia-search`, `.ia-wrap` + `.ia-table` (list), `.ia-empty` (empty state), `.cf` + `.cf-rail` (left sub-nav for multi-section pages).
- Cards/modals/forms: `.ra-card`, `.ra-modal` (`--md`/`--lg`), `.ra-grid`+`.ra-field`/`.ra-input`/`.ra-select`, `.ra-switch` (toggle), `.ra-rowbtn`(`.danger`) (row actions), `.af-btn` (`--primary`/`--ghost`/`--sm`).
- Status pill: `.lw-active.on/.off`. Destructive confirm button = `.af-btn--primary` with inline `background: var(--st-rejected-fg)`.
- Icons: `window.Ico(name,size)` set → port as your app's icon component. Icons used here: folder, folderOpen, file, download, upload, edit, trash, globe, doc, chevRight, search, plus, check, x, refresh, grid.

Host scoping: both pages are IT-Admin (host) only — guard routes with the host/IT-Admin permission. Loading + empty states are required (the prototype's "State" chrome switch shows them).

After each page: build clean, no console errors, give me a manual test checklist, and wait for my live sign-off.

Today: [PAGE].
```

---

## 1 · File Management — `File Management - Redesign.html`
- **Nav/route:** System group (host). Files in prototype: `components/in-files.jsx`, `styles/in-files.css`.
- **What it is:** explorer — breadcrumb, folder tree (left), file list (Name · Size · Type · Modified). Toolbar: New folder, Upload, search. Row actions Download / Rename / Delete (confirm modal). Upload modal = drag-drop zone with **per-file progress**. Empty-folder + loading states.

- **Folder hierarchy (important):** the `appointment-documents` container is organized **`{yyyy-MM}` month folder → `{ConfirmationNumber}` appointment folder → the appointment's documents**. So every appointment's documents sit together, separated from other appointments, grouped by month. The explorer renders this tree by reading folder structure; clicking a month shows its appointment folders, clicking an appointment shows its files.

- **Frontend vs backend for the hierarchy — how it actually works:**
  - ABP **BLOB Storing** (and most blob backends: Azure Blob, S3, filesystem provider) has **no real folder entity** — a blob is stored under a single string **name/key**. "Folders" are virtual: they're just **prefixes in the blob name**. So the hierarchy is stored by **naming the blob** `appointment-documents/{yyyy-MM}/{ConfirmationNumber}/{fileName}` when it's saved. There's no separate folder record to create — the path IS the storage. The explorer **derives the tree by splitting blob keys on `/`**.
  - **What the backend must do:** enforce this key convention wherever appointment documents are written (the existing appointment-document upload path). Compute `{yyyy-MM}` from the appointment/created date and `{ConfirmationNumber}` from the appointment. If documents are currently stored flat (e.g. `{ConfirmationNumber}_{file}` as the old mock showed) — **add a migration** to re-key existing blobs into the month/appointment prefix layout.
  - **What needs a list endpoint:** an explorer endpoint that returns, for a given prefix, the immediate child "folders" (distinct next path segments) and files (key, size, content-type, last-modified). Most providers expose prefix+delimiter listing; for providers that don't, list keys under the prefix and group client-side.
  - **One caveat — empty folders:** prefix-based folders only exist while they contain ≥1 blob, so a freshly "created" empty folder (and the empty `temp-uploads` state) won't persist on a pure blob store. Options: (a) accept that folders appear once a file lands in them (simplest), or (b) persist a tiny **folder-marker blob** (e.g. a zero-byte `.keep`) or a small `Folders` table if you need empty folders to stick. Pick (a) unless product needs empty folders.

- **Backend (ABP BLOB Storing):** `IBlobContainer` per tenant. Endpoints: list-by-prefix, download (stream or SAS/presigned URL), upload (multipart with progress, or presigned PUT — the convention key built server-side), create folder (only if you choose the marker/table approach), rename (copy+delete on blob stores), delete. Enforce the 50 MB max shown in the UI server-side.

- **Test:** tree shows month → appointment → files; breadcrumb tracks the path; list shows real metadata; upload (into the selected appointment folder) shows progress then the file appears; rename/delete; download; empty folder + loading states.

## 2 · Language Management — `Language Management - Redesign.html`
- **Nav/route:** System group (host). Files: `components/in-lang.jsx`, `styles/in-lang.css`. Two sections via the `cf-rail`.
- **What it is:** **Languages** — table (Language · Culture code · UI culture · Default · Status) + New-language modal (display name, culture, UI culture, flag code, enabled) + per-row **Set default** (the default row can't be deleted or un-defaulted). **Language Texts** — Resource selector + Target-culture selector + search, then a key / base(English) / override table; the override is an inline input with **save-per-row** and **reset-to-default** (an empty override falls back to base; rows show Customized vs Default).
- **Backend (ABP Language Management module):** `ILanguageAppService` (CRUD + set-default + enable/disable for **Languages**) and `ILanguageTextAppService` for **Language Texts** — `GetListAsync(resourceName, cultureName, baseCultureName, filter)`, `UpdateAsync(resourceName, cultureName, name, value)`, `RestoreToDefaultAsync(resourceName, cultureName, name)`. Resource list = registered localization resources (CaseEvaluation, AbpUi, AbpIdentity…). "Customized" = a stored `LanguageText` override exists; "Default" = none (falls back to the base resource file).
- **Test:** add/edit/disable a language; set default (old default clears; default can't be deleted); texts table loads per resource+culture; edit override → save persists and flips to Customized; reset-to-default clears it and falls back to base; search filters keys; loading + empty.

---

### Per-page sign-off checklist
Renders pixel-close to the prototype · host/IT-Admin-only route guard · real data loads · every action hits a real endpoint (no dead buttons) · loading + empty states present · File Management writes blobs under the `appointment-documents/{yyyy-MM}/{ConfirmationNumber}/` key convention · no console errors · added to the IT-Admin System nav.
