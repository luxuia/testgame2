## 0. Week 1 Execution Order (Suggested)

- [ ] W1-D1: Add GOAP runtime contracts (`Fact`, `Condition`, `Effect`, `ActionDef`, `GoalDef`, `Plan`).
- [ ] W1-D2: Implement `GoalSelector` scoring and goal-switch hysteresis.
- [ ] W1-D3: Implement bounded planner (`maxPlanDepth`, `plannerBudgetPerTick`).
- [ ] W1-D4: Implement `PlanExecutor` with interrupt + cooldown replan behavior.
- [ ] W1-D5: Integrate authority gate for terrain actions and run compile + smoke validation.

## 0.1 Week 1 Exit Criteria

- [ ] Planner outputs bounded plan chains (depth-limited).
- [ ] Emergency triggers can preempt cooldown and force replan.
- [ ] Terrain actions in plans obey home/away legality checks.
- [ ] Runtime config accepts only approved nine keys.

## 1. GOAP Runtime Skeleton

- [ ] 1.1 Add runtime models for facts, conditions, effects, action definitions, goal definitions, and plans.
- [ ] 1.2 Add deterministic condition evaluation and effect application utilities.
- [ ] 1.3 Add lightweight world-state cloning utility for plan simulation.

## 2. Goal Selection

- [ ] 2.1 Implement bounded goal set (`DefendCore`, `ExpandFungus`, `BuildDefense`, `MineResource`, `AttackThreat`, `Recover`).
- [ ] 2.2 Implement utility scoring contract (`base + dynamic`) with deterministic tie-break.
- [ ] 2.3 Add `goalSwitchHysteresis` guard to reduce oscillation.

## 3. Bounded Planner

- [ ] 3.1 Implement action applicability check from preconditions.
- [ ] 3.2 Implement bounded depth search with cost evaluation.
- [ ] 3.3 Enforce per-tick planning budget (`plannerBudgetPerTick`).
- [ ] 3.4 Return fallback plan (`Recover` / `Idle`) when no feasible path exists.

## 4. Plan Execution And Replanning

- [ ] 4.1 Implement plan cursor execution with step completion and failure handling.
- [ ] 4.2 Implement cooldown-based replanning (`replanCooldownSec`).
- [ ] 4.3 Implement emergency preemption (`CoreHpPct <= emergencyOverrideCoreHpPct`).
- [ ] 4.4 Implement stale-plan detection (`planStaleTimeoutSec`).

## 5. Shared Legality And Authority Integration

- [ ] 5.1 Route `BreakBlock`, `PlaceBlock`, `ConvertToFungus` through shared authority gate.
- [ ] 5.2 Ensure managed and manual actions share one legality/execution pipeline.
- [ ] 5.3 Emit deny reasons for authority-gated failures.

## 6. Minimal Config Contract (Nine Keys)

- [ ] 6.1 Implement planner config loader for exactly:
  - `maxPlanDepth`
  - `replanCooldownSec`
  - `goalSwitchHysteresis`
  - `plannerBudgetPerTick`
  - `emergencyOverrideCoreHpPct`
  - `actionFailureRetryLimit`
  - `planStaleTimeoutSec`
  - `homeBiasWeight`
  - `awaySafetyWeight`
- [ ] 6.2 Reject missing required keys.
- [ ] 6.3 Reject extra out-of-scope top-level keys.

## 7. Managed Agent Integration

- [ ] 7.1 Attach GOAP decision loop to deputy/puji managed controllers.
- [ ] 7.2 Add claim/conflict guard for mutually exclusive world actions.
- [ ] 7.3 Add retry and fallback policy on repeated action failure.

## 8. Validation And Regression

- [ ] 8.1 Validate goal-switch stability under noisy world-state updates.
- [ ] 8.2 Validate bounded planning cost under crowd load.
- [ ] 8.3 Validate home/away legality enforcement for terrain plan actions.
- [ ] 8.4 Validate emergency preemption and recovery behavior.
- [ ] 8.5 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve errors.
