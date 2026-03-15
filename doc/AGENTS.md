# Agent Instructions (scope: `doc/` and below)

## Scope and layout
- This AGENTS.md applies to: `doc/` and below.
- Current primary document:
  - `设定总纲.md`

## Commands
- Structure check:
  - `rg -n "^#|^##|^###" doc/*.md`
- Content search:
  - `rg -n "<keyword>" doc/`

## Conventions
- Preserve terminology consistency with existing Chinese narrative and naming.
- Prefer additive edits; avoid rewriting stable sections unless requested.
- When docs describe implemented behavior, keep alignment with `UnityProject/` and root milestone docs.

## Common pitfalls
- Mixing speculative ideas into canonical setting docs without labels.
- Renaming established terms without a migration note.

## Do not
- Do not introduce implementation-only details that belong in `UnityProject/` docs.
- Do not move or rename setting files without explicit request.
