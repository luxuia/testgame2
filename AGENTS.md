# Agent Instructions (scope: repository root and subdirectories unless overridden)

## Scope and layout
- This AGENTS.md applies to: `./` and below.
- Prefer the closest nested AGENTS.md when instructions conflict.
- Key directories:
  - `UnityProject/` - Unity game project (runtime/editor code + assets)
  - `openspec/` - spec-driven change artifacts
  - `doc/` - product/world documentation
  - `.github/` - CI workflows, templates, prompts, and skill assets

## Modules / subprojects
| Module | Type | Path | What it owns | How to run | Tests | Docs | AGENTS |
|---|---|---|---|---|---|---|---|
| UnityProject | unity-game | `UnityProject/` | Gameplay/runtime code, scenes, assets, Unity settings | Open with Unity Editor (`ProjectSettings/ProjectVersion.txt`) and Play | `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`; Unity Test Runner/CI | `UnityProject/README.md`, root milestone docs | `UnityProject/AGENTS.md` |
| openspec | spec-artifacts | `openspec/` | Change proposals/design/tasks/spec deltas/archive | Use `openspec` CLI if available, otherwise edit artifacts directly | Structure + checklist validation | `openspec/config.yaml` | `openspec/AGENTS.md` |
| doc | docs | `doc/` | Product/world-level narrative docs | Edit Markdown directly | Heading/structure checks | `doc/*.md` | `doc/AGENTS.md` |
| github | ci-meta | `.github/` | Workflows, issue/PR templates, repository automation | GitHub Actions execution | Workflow validation via PR runs | `.github/workflows/*.yml` | `.github/AGENTS.md` |

## Cross-domain workflows
- Spec -> implementation:
  - Draft and track intent in `openspec/changes/...` when using spec-driven workflow.
  - Implement code in `UnityProject/`.
  - Sync status and decisions in root docs: `Prompt.md`, `Plan.md`, `Implement.md`, `Documentation.md`.
- Gameplay/pathfinding changes:
  - Keep `Plan.md` as single source of truth for milestone boundaries.
  - Validate with `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`.
  - If behavior/perf changed, update `Documentation.md` decisions and known issues.
- CI alignment:
  - Unity checks are defined in `.github/workflows/unity-tests.yml` and consumed by PR workflows.
  - If file paths or test roots move, update path filters in `.github/workflows/pr-tests.yml`.

## Verification (preferred order)
- Run quiet and narrow first; use verbose/debug only when a failure needs diagnosis.
- Repo-level quick checks:
  - `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`
  - `rg -n "^#|^##|^###" Prompt.md Plan.md Implement.md Documentation.md`
- Unity runtime checks:
  - Open `UnityProject/Assets/Scenes/SinglePlayer.unity`, enter Play Mode, verify click-to-move behavior.

## Docs usage
- Do not open/read `doc/` by default unless the task requires it or the user asks.

## Global conventions
- Keep edits scoped; do not mix unrelated refactors into milestone work.
- Prefer updating the owning module first, then update cross-module docs.
- Never commit generated Unity folders (`Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`).

## Do not
- Do not place Unity-specific implementation details in root docs when module docs can own them.
- Do not modify archived OpenSpec changes unless explicitly requested.
- Do not infer successful runtime behavior from compile-only checks.

## Links to module instructions
- `UnityProject/AGENTS.md`
- `openspec/AGENTS.md`
- `doc/AGENTS.md`
- `.github/AGENTS.md`
