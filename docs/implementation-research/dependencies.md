# Dependency graph + wave ordering

**Status:** placeholder. Populated by Phase 3.

Phase 3 consumes the `## Dependencies` section of every `solutions/<slug>.md` brief,
builds a directed graph (nodes = capability slugs, edges = blocks relations),
detects cycles, topologically sorts the graph, and writes the result here:

- Mermaid `graph TD` rendering of the full dependency graph.
- Numbered wave list (Wave N contains every capability whose blocked-by set is
  entirely in waves less than N).
- Separate section for capabilities whose wave position is contingent on an open
  scope question being answered (routed via `blocked-on-scope.md`).

If Phase 3 detects a cycle, it stops and surfaces the offending brief paths so the
briefs can be revised. A cycle indicates a mis-stated dependency in one or more
briefs.

See `README.md` for the full phase flow and `solutions/` for the input briefs.
