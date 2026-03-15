## 1. Faction Runtime Skeleton

- [x] 1.1 Create `FactionScenarioDirector` runtime entry under `UnityProject/Assets/Scripts/Faction/`.
- [x] 1.2 Define V1 faction runtime state (`Assemble`, `Advance`, `Attack`, `Regroup`) and transition hooks.
- [x] 1.3 Add single-hostile-faction initialization path and enforce V1 single-profile activation.

## 2. Fixed Spawn Anchor System

- [x] 2.1 Add spawn anchor data contract (anchor id, transform, faction id, enabled flag).
- [x] 2.2 Implement wave spawn from fixed anchors with deterministic anchor selection order.
- [x] 2.3 Implement invalid-anchor fallback/skip logic without breaking wave processing.
- [x] 2.4 Implement wave spawn budget and alive-cap checks before each spawn batch.

## 3. Objective And Planning Bridge

- [x] 3.1 Add faction objective preset contract for player/core target priority and regroup thresholds.
- [x] 3.2 Implement `FactionAgentBrainBridge` that maps faction directive to GOAP blackboard inputs.
- [x] 3.3 Connect bridge output to existing `GoapP0Controller` planning tick and plan executor lifecycle.
- [x] 3.4 Ensure planning failures trigger deterministic fallback behaviors (`Regroup`/`Hold`/`Idle`).

## 4. Shared Combat/Authority Integration

- [x] 4.1 Route faction-controlled actions through existing `CombatActionPipeline` execution path.
- [x] 4.2 Enforce home/away terrain legality checks for `BreakBlock` and `PlaceBlock` actions.
- [x] 4.3 Emit consistent deny/failure reasons when legality gate blocks terrain actions.

## 5. Assault Loop Behavior

- [x] 5.1 Implement target resolution with V1 priority: reachable player first, else core fallback.
- [x] 5.2 Implement assault stage progression (`Assemble -> Advance -> Attack -> Regroup`) with transition conditions.
- [x] 5.3 Implement regroup trigger on casualty/action-failure thresholds and recovery re-entry to assault.

## 6. Validation

- [ ] 6.1 Validate fixed spawn consistency by replaying multiple wave starts with same anchor setup.
- [ ] 6.2 Validate V1 scope guard by confirming non-primary faction profiles stay inactive.
- [ ] 6.3 Validate shared pipeline behavior for both normal attacks and denied away-terrain actions.
- [ ] 6.4 Validate fallback and recovery loop after repeated plan failures.
- [x] 6.5 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve compile errors.
