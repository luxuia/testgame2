# Agent Instructions (scope: `.github/` and below)

## Scope and layout
- This AGENTS.md applies to: `.github/` and below.
- Key directories:
  - `workflows/` - GitHub Actions workflows
  - `ISSUE_TEMPLATE/` - issue templates
  - `instructions/` - repository guidance
  - `prompts/` and `skills/` - automation prompt/skill assets

## Conventions
- Keep workflow permissions at least privilege (prefer job-level narrowing).
- Preserve reusable workflow interfaces (`workflow_call` inputs/outputs) when editing.
- If source paths change, update path filters in dependent workflows (notably `workflows/pr-tests.yml`).
- Keep comments concise and focused on intent/risk for future maintainers.

## Verification
- Validate YAML syntax and indentation carefully before commit.
- For logic changes, verify trigger conditions (`on`, `paths`, `if`, `needs`) still match intent.
- For Unity test pipeline updates, cross-check:
  - `workflows/unity-tests.yml`
  - `workflows/pr-tests.yml`

## Common pitfalls
- Breaking reusable workflow outputs consumed by downstream jobs.
- Over-broad path filters causing unnecessary CI load.
- Missing `permissions` blocks leading to unexpected token scope.

## Do not
- Do not hardcode secrets or tokens.
- Do not silently broaden permissions without explicit need.
- Do not change branch protections/status context names unintentionally.
