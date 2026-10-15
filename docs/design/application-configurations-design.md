---
feature: application-configurations
date: 2026-05-04
phase: see it-admin-system-parameters-design.md
status: cross-reference
old-source: see it-admin-system-parameters-design.md
new-feature-path: see it-admin-system-parameters-design.md
shell: internal-user-authenticated (side-nav + top-bar)
screenshots: see it-admin-system-parameters-design.md
---

# Design: Application Configurations

## This feature is documented in `it-admin-system-parameters-design.md`

"Application Configurations" and "System Parameters" refer to the same feature.

In OLD, the entity is `SystemParameters` (table `SystemParameters`, one row per tenant).
In NEW, the entity is also `SystemParameter` with the same single-row-per-tenant structure.

The OLD app exposes this at `/system-parameters` (IT Admin only).

**All design details, field inventory, API contract, role visibility, and verification
checklist are in `it-admin-system-parameters-design.md`.**

No additional content in this file. If a future session adds a design.md for a distinct
"Application Configurations" page that differs from System Parameters, replace this stub.
