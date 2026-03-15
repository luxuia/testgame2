## 1. Faction Runtime Skeleton

- [ ] 1.1 Create `FactionScenarioDirector` runtime entry under `UnityProject/Assets/Scripts/Faction/`.
- [ ] 1.2 Define V1 faction runtime state (`Assemble`, `Advance`, `Attack`, `Regroup`) and transition hooks.
- [ ] 1.3 Add single-hostile-faction initialization path and enforce V1 single-profile activation.

## 2. Fixed Spawn Anchor System

- [ ] 2.1 Add spawn anchor data contract (anchor id, transform, faction id, enabled flag).
- [ ] 2.2 Implement wave spawn from fixed anchors with deterministic anchor selection order.
- [ ] 2.3 Implement invalid-anchor fallback/skip logic without breaking wave processing.
- [ ] 2.4 Implement wave spawn budget and alive-cap checks before each spawn batch.

## 3. Objective And Planning Bridge

- [ ] 3.1 Add faction objective preset contract for player/core target priority and regroup thresholds.
- [ ] 3.2 Implement `FactionAgentBrainBridge` that maps faction directive to GOAP blackboard inputs.
- [ ] 3.3 Connect bridge output to existing `GoapP0Controller` planning tick and plan executor lifecycle.
- [ ] 3.4 Ensure planning failures trigger deterministic fallback behaviors (`Regroup`/`Hold`/`Idle`).

## 4. Shared Combat/Authority Integration

- [ ] 4.1 Route faction-controlled actions through existing `CombatActionPipeline` execution path.
- [ ] 4.2 Enforce home/away terrain legality checks for `BreakBlock` and `PlaceBlock` actions.
- [ ] 4.3 Emit consistent deny/failure reasons when legality gate blocks terrain actions.

## 5. Assault Loop Behavior

- [ ] 5.1 Implement target resolution with V1 priority: reachable player first, else core fallback.
- [ ] 5.2 Implement assault stage progression (`Assemble -> Advance -> Attack -> Regroup`) with transition conditions.
- [ ] 5.3 Implement regroup trigger on casualty/action-failure thresholds and recovery re-entry to assault.

## 6. Validation

- [ ] 6.1 Validate fixed spawn consistency by replaying multiple wave starts with same anchor setup.
- [ ] 6.2 Validate V1 scope guard by confirming non-primary faction profiles stay inactive.
- [ ] 6.3 Validate shared pipeline behavior for both normal attacks and denied away-terrain actions.
- [ ] 6.4 Validate fallback and recovery loop after repeated plan failures.
- [ ] 6.5 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve compile errors.
