---
title: Status-conditional button matrix
date: 2026-05-25
status: ready
audience: Adrian (presenter)
---

# Status-conditional button matrix (Staff Supervisor view)

Verified live 2026-05-25 23:55 PT against the 3 demo appointments
on `main` (commit 8f21671).

| Status | Approve | Reject | Save | Upload Docs | Regenerate |
|---|---|---|---|---|---|
| A00001 Approved (AME, 2026-06-02) | hidden | hidden | shown | shown | shown |
| A00002 Rejected (QME, 2026-06-03) | hidden | hidden | shown | shown | shown |
| A00003 Pending (Panel QME, 2026-06-04) | shown | shown | shown | shown | shown |

**Key finding for demo:** Approve/Reject buttons correctly show ONLY
on Pending. Audience-safe: a mis-click on A00001 cannot re-approve;
the button isn't there to click.

**Side note:** Upload Documents + Regenerate buttons are visible
regardless of status. Intentional? Possibly -- allows post-approval
or in-error-reject doc management. Not a demo blocker; minor UX
question worth a future audit.
