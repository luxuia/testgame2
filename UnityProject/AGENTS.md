# Agent Instructions (scope: `UnityProject/` and below)

## Scope and layout
- This AGENTS.md applies to: `UnityProject/` and below.
- Key directories:
  - `Assets/Scripts/` - gameplay/runtime/editor C# code
  - `Assets/Scenes/` - playable scenes (`SinglePlayer.unity`)
  - `Assets/Minecraft Default PBR Resources/` - configs, Lua scripts, worldgen resources
  - `Assets/ToaruUnity.UI/` - UI package + runtime tests
  - `Packages/` - package dependencies
  - `ProjectSettings/` - canonical Unity version and project-wide settings

## Commands
- Unity version:
  - Use version from `ProjectSettings/ProjectVersion.txt` (currently `2022.3.14f1c1`).
- Build/compile check (from `UnityProject/`):
  - `dotnet build Assembly-CSharp.csproj -nologo`
- Runtime verification:
  - Open `Assets/Scenes/SinglePlayer.unity` and run Play Mode checks.
- Test entry points:
  - Unity Test Runner (`EditMode`/`PlayMode`) in Editor.
  - CI reusable workflow: `.github/workflows/unity-tests.yml`.

## Feature map
| Feature | Owner | Key paths | Entrypoints | Tests | Docs |
|---|---|---|---|---|---|
| Click target selection | Unity runtime | `Assets/Scripts/PlayerControls/TargetSelector.cs` | `TargetSelector` | Manual Play Mode | `Prompt.md`, `Plan.md` |
| Movement/path strategy | Unity runtime | `Assets/Scripts/PlayerControls/PathfindingMovementController.cs`, `Assets/Scripts/Pathfinding/` | `PathfindingMovementController`, `AStarPathfinding`, `FlowFieldPathfinding` | Manual Play Mode | `Documentation.md` |
| Crowd coordination | Unity runtime | `Assets/Scripts/Pathfinding/CrowdFlowFieldCoordinator.cs` | `CrowdFlowFieldCoordinator` | Profiler + runtime checks | `Documentation.md` |
| World simulation | Unity runtime | `Assets/Scripts/World*.cs`, `Assets/Scripts/Chunk*.cs` | `WorldSinglePlayer`, `World`, `ChunkManager` | Manual Play Mode | `UnityProject/README.md` |
| Lua integration | Unity runtime | `Assets/Scripts/Lua/`, `Assets/Minecraft Default PBR Resources/Lua Scripts/` | `LuaManager`, `main.lua` | Runtime behavior checks | `UnityProject/README.md`, `General/README.md` |
| UI runtime package | Unity package | `Assets/ToaruUnity.UI/` | UI prefabs/components | `Assets/ToaruUnity.UI/Tests/Runtime/ActionCenterTest.cs` | `Assets/ToaruUnity.UI/README.md` |

## Conventions
- Keep `.meta` files in sync with every asset create/move/rename/delete operation.
- Treat `Library/`, `Temp/`, `Logs/`, `obj/`, and `UserSettings/` as generated output.
- For pathfinding changes, preserve constraints recorded in root `Prompt.md`/`Plan.md`.
- When milestone work is completed, update root `Documentation.md` status and decisions.

## Common pitfalls
- `UnityProject/README.md` still mentions an older Unity version; use `ProjectVersion.txt` as source of truth.
- Asset loading supports both AssetDatabase and AssetBundle mode; validate behavior in the configured mode.
- Performance-sensitive code is concentrated in target selection and pathfinding loops; avoid hidden per-frame allocations.

## Do not
- Do not bulk-regenerate or commit unrelated `.csproj`/solution churn unless required by the task.
- Do not change project-wide package versions without explicit request.
- Do not rely on compile success alone for movement/attack/path correctness.
