## 1. P0 Core Nodes (Must First)

- [x] 1.1 Implement blackboard fact snapshot contract for planning input.
- [x] 1.2 Implement bounded goal scoring and current-goal selection.
- [x] 1.3 Implement emergency preemption trigger for core-defense override.
- [x] 1.4 Route terrain planning actions through home/away authority legality gate.
- [x] 1.5 Implement deterministic fallback behavior when no feasible plan exists.

## 2. P1 Stability Nodes (After P0)

- [x] 2.1 Implement reservation for actionable targets (block/slot/repair point).
- [x] 2.2 Implement reservation release and timeout rules for failed/aborted actions.
- [x] 2.3 Implement small-step interruptible execution model for plan steps.
- [x] 2.4 Add replanning cooldown + switch hysteresis to avoid oscillation.
- [x] 2.5 Add failure retry policy and safe re-assignment path.

## 3. P2 Expansion Nodes (After P1)

- [x] 3.1 Add role-specific planner presets (player clone / deputy / puji).
- [x] 3.2 Add lightweight environment weighting (threat/resource/build pressure) tuning layer.
- [x] 3.3 Add longer-chain optimization only under fixed budget constraints.
- [x] 3.4 Add telemetry panel for goal switches, interrupts, and reservation conflicts.

## 4. RimWorld Borrowing Boundary

- [x] 4.1 Document borrowed patterns: priority filtering, reservation, interruptible step execution.
- [x] 4.2 Document excluded patterns: full narrative storyteller/director complexity.
- [x] 4.3 Add code-level comments or docs linking each borrowed pattern to local low-cost implementation.

## 5. Validation

- [ ] 5.1 Validate P0 node checklist in `SinglePlayer` for defense/build/expand loops.
- [ ] 5.2 Validate P1 conflict-free multi-agent behavior under shared targets.
- [ ] 5.3 Validate emergency preemption and recovery under high threat/core low HP.
- [x] 5.4 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve errors.
