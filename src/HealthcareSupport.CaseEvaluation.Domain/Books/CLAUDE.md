---
feature: Books
status: vestigial
last-verified: 2026-04-24
product-doc: none -- excluded from product intent (vestigial ABP scaffold)
---

# Books

Vestigial ABP scaffold entity. Not a real business feature.

Retained only because `BookAppServiceTests` exercises ABP application-service plumbing
(one of the few features with test coverage at all). Do NOT extend this feature.
Remove it together with `BookStoreDataSeederContributor` and `BookAppServiceTests` when
scaffold cleanup is scheduled.

`Books/CreateUpdateBookDto.cs` uses the banned combined create+update DTO pattern --
see Application.Contracts/CLAUDE.md for the full rule.

## Related

- Root architecture: [CLAUDE.md](/CLAUDE.md)
