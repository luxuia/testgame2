## ADDED Requirements

### Requirement: The System MUST Use Goal-Oriented Planning Instead Of Quest Lifecycle Logic
The runtime SHALL choose behavior by goal utility and bounded action planning, and MUST NOT require quest/task lifecycle states for core decision flow.

#### Scenario: Goal utility drives current intent
- **WHEN** the runtime evaluates world facts for a controlled unit or group
- **THEN** it MUST select current intent from bounded goals based on utility scores

#### Scenario: No quest lifecycle dependency
- **WHEN** planning/execution runs for a behavior tick
- **THEN** it MUST execute without relying on quest states like offer/accept/complete

### Requirement: Planning Depth MUST Stay Bounded
The planner MUST enforce finite search depth and finite per-tick planning budget.

#### Scenario: Depth limit is enforced
- **WHEN** the planner expands candidate action chains
- **THEN** resulting plan depth MUST NOT exceed `maxPlanDepth`

#### Scenario: Tick budget is enforced
- **WHEN** planner work for the current update reaches `plannerBudgetPerTick`
- **THEN** the planner MUST stop further expansion and return best available result

### Requirement: Replanning MUST Support Emergency Preemption With Cooldown Control
The runtime SHALL support immediate replanning for emergencies while preventing constant replanning under normal conditions.

#### Scenario: Emergency state forces immediate replan
- **WHEN** emergency condition is met (for example `CoreHpPct <= emergencyOverrideCoreHpPct`)
- **THEN** the runtime MUST interrupt current plan and trigger immediate replanning

#### Scenario: Non-emergency replanning is cooldown-limited
- **WHEN** no emergency condition is active and last replan happened within `replanCooldownSec`
- **THEN** the runtime MUST defer replanning for this update window

### Requirement: Terrain-Related Plan Actions MUST Respect Fungus Territory Authority
Planned terrain actions SHALL pass through the same home/away authority gate as manual and combat actions.

#### Scenario: Home territory allows normal planned terrain action
- **WHEN** a plan step executes `BreakBlock` or `PlaceBlock` in fungus-covered home territory
- **THEN** the authority gate MUST evaluate it as home-territory action under normal legality rules

#### Scenario: Away territory blocks unrestricted planned terrain action
- **WHEN** a plan step executes `BreakBlock` or `PlaceBlock` in away territory without temporary edit permission
- **THEN** the authority gate MUST reject that step with a deny reason

### Requirement: GOAP Runtime Config Surface MUST Stay Minimal
The MVP GOAP runtime MUST expose exactly nine top-level global config keys.

#### Scenario: Approved nine-key contract
- **WHEN** GOAP runtime config is loaded
- **THEN** it MUST load only:
  - `maxPlanDepth`
  - `replanCooldownSec`
  - `goalSwitchHysteresis`
  - `plannerBudgetPerTick`
  - `emergencyOverrideCoreHpPct`
  - `actionFailureRetryLimit`
  - `planStaleTimeoutSec`
  - `homeBiasWeight`
  - `awaySafetyWeight`

#### Scenario: Extra key is out-of-scope
- **WHEN** a top-level GOAP runtime key outside the approved nine is introduced
- **THEN** the change MUST be treated as out-of-scope for MVP and require spec update

### Requirement: Managed And Manual Execution MUST Share Legality Pipeline
Managed controllers (deputy/puji) and manual control MUST use one action legality and execution pipeline.

#### Scenario: Managed execution uses shared legality
- **WHEN** managed controller executes a planned step
- **THEN** that step MUST pass the same legality checks as manual execution

#### Scenario: Failure handling parity
- **WHEN** managed and manual execution fail for the same legality reason
- **THEN** both paths MUST emit equivalent failure reason categories for telemetry and debugging

### Requirement: Goal Switching MUST Be Stability-Protected
The runtime SHALL use switching hysteresis to avoid high-frequency goal oscillation.

#### Scenario: Small score delta does not switch goal
- **WHEN** alternative goal score does not exceed current goal score by `goalSwitchHysteresis`
- **THEN** the runtime MUST keep current goal

#### Scenario: Significant score delta switches goal
- **WHEN** alternative goal score exceeds hysteresis threshold
- **THEN** the runtime MUST switch current goal and request replanning

### Requirement: Planning MUST Provide Safe Fallback On Infeasible States
If no feasible plan is found, runtime SHALL choose bounded fallback behavior.

#### Scenario: Infeasible planning returns fallback
- **WHEN** no valid action chain satisfies current goal within depth/budget limits
- **THEN** runtime MUST return a fallback behavior such as `Recover` or `Idle`

#### Scenario: Repeated failure escalates fallback priority
- **WHEN** repeated action failures exceed `actionFailureRetryLimit`
- **THEN** runtime MUST increase fallback/safety behavior priority until replanning conditions improve
