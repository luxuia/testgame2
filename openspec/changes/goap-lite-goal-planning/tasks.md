## 0. Week 1 Execution Order (Suggested)

- [x] W1-D1: Add GOAP runtime contracts (`Fact`, `Condition`, `Effect`, `ActionDef`, `GoalDef`, `Plan`).
- [x] W1-D2: Implement `GoalSelector` scoring and goal-switch hysteresis.
- [x] W1-D3: Implement bounded planner (`maxPlanDepth`, `plannerBudgetPerTick`).
- [x] W1-D4: Implement `PlanExecutor` with interrupt + cooldown replan behavior.
- [ ] W1-D5: Integrate authority gate for terrain actions and run compile + smoke validation.

## 0.1 Week 1 Exit Criteria

- [x] Planner outputs bounded plan chains (depth-limited).
- [x] Emergency triggers can preempt cooldown and force replan.
- [x] Terrain actions in plans obey home/away legality checks.
- [x] Runtime config accepts only approved nine keys.

## 1. GOAP Runtime Skeleton

- [x] 1.1 Add runtime models for facts, conditions, effects, action definitions, goal definitions, and plans.
- [x] 1.2 Add deterministic condition evaluation and effect application utilities.
- [x] 1.3 Add lightweight world-state cloning utility for plan simulation.

## 2. Goal Selection

- [x] 2.1 Implement bounded goal set (`DefendCore`, `ExpandFungus`, `BuildDefense`, `MineResource`, `AttackThreat`, `Recover`).
- [x] 2.2 Implement utility scoring contract (`base + dynamic`) with deterministic tie-break.
- [x] 2.3 Add `goalSwitchHysteresis` guard to reduce oscillation.

## 3. Bounded Planner

- [x] 3.1 Implement action applicability check from preconditions.
- [x] 3.2 Implement bounded depth search with cost evaluation.
- [x] 3.3 Enforce per-tick planning budget (`plannerBudgetPerTick`).
- [x] 3.4 Return fallback plan (`Recover` / `Idle`) when no feasible path exists.

## 4. Plan Execution And Replanning

- [x] 4.1 Implement plan cursor execution with step completion and failure handling.
- [x] 4.2 Implement cooldown-based replanning (`replanCooldownSec`).
- [x] 4.3 Implement emergency preemption (`CoreHpPct <= emergencyOverrideCoreHpPct`).
- [x] 4.4 Implement stale-plan detection (`planStaleTimeoutSec`).

## 5. Shared Legality And Authority Integration

- [x] 5.1 Route `BreakBlock`, `PlaceBlock`, `ConvertToFungus` through shared authority gate.
- [x] 5.2 Ensure managed and manual actions share one legality/execution pipeline.
- [x] 5.3 Emit deny reasons for authority-gated failures.

## 6. Minimal Config Contract (Nine Keys)

- [x] 6.1 Implement planner config loader for exactly:
  - `maxPlanDepth`
  - `replanCooldownSec`
  - `goalSwitchHysteresis`
  - `plannerBudgetPerTick`
  - `emergencyOverrideCoreHpPct`
  - `actionFailureRetryLimit`
  - `planStaleTimeoutSec`
  - `homeBiasWeight`
  - `awaySafetyWeight`
- [x] 6.2 Reject missing required keys.
- [x] 6.3 Reject extra out-of-scope top-level keys.

## 7. Managed Agent Integration

- [x] 7.1 Attach GOAP decision loop to deputy/puji managed controllers.
- [x] 7.2 Add claim/conflict guard for mutually exclusive world actions.
- [x] 7.3 Add retry and fallback policy on repeated action failure.

## 8. Validation And Regression

- [ ] 8.1 Validate goal-switch stability under noisy world-state updates.
- [ ] 8.2 Validate bounded planning cost under crowd load.
- [ ] 8.3 Validate home/away legality enforcement for terrain plan actions.
- [ ] 8.4 Validate emergency preemption and recovery behavior.
- [x] 8.5 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve errors.

## 9. GOAP + Input Chain Structural Refactor (No Behavior Change)

### 9.1 GOAP File-Level Decomposition (G-series)

- [x] G1 Create `GoalPlanning` layered folders and move existing types without logic changes:
  - `Core/`
  - `Planning/`
  - `Execution/`
  - `Runtime/`
  - `Telemetry/`
  - `Presets/`
  - `Adapters/`
- [x] G2 Move core contracts and deterministic utilities (`Fact/Condition/Effect/Plan/ConfigContract`) from monolithic runtime file into `GoalPlanning/Core`.
- [x] G3 Move `GoalSelector` and `GoapPlanner` into `GoalPlanning/Planning` while preserving current selection and search behavior.
- [x] G4 Move `PlanExecutor`, reservation store, and fallback factory into `GoalPlanning/Execution` while preserving current retry/cooldown behavior.
- [x] G5 Move role presets and telemetry classes into `GoalPlanning/Presets` and `GoalPlanning/Telemetry`.
- [x] G6 Keep a thin `GoapP0Controller` facade in `GoalPlanning/Runtime` and update callers (including faction bridge) to new namespaces/paths.

### 9.2 Input Chain Unification (I-series)

- [x] I1 Add command-level input model (`PlayerCommand`) and central command router (`PlayerCommandRouter`) for gameplay and UI intents.
- [x] I2 Consolidate raw input sampling into a single entry point, replacing multi-point direct polling in player/entity selector/UI router classes.
- [x] I3 Refactor `TargetSelector` to consume normalized commands and focus on selection state + visualization only.
- [x] I4 Refactor `PlayerController` to consume normalized commands and keep movement/mining orchestration behavior unchanged.
- [x] I5 Remove remaining duplicate direct `Input.GetKeyDown`/`Input.GetMouseButton*` paths from gameplay chain after router parity is validated.

### 9.3 Structural Refactor Acceptance

- [ ] 9.3.1 Gameplay parity check passes for left-click move, right-click mining, and right-drag multi-select mining.
- [ ] 9.3.2 GOAP runtime parity check passes for target selection, replanning cadence, failure fallback, and reservation behavior.
- [x] 9.3.3 Build check passes after each completed G/I milestone: `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`.
- [x] 9.3.4 Monolithic GOAP runtime file no longer contains mixed concerns (core + planner + executor + telemetry + presets in one file).
