## ADDED Requirements

### Requirement: Faction Assault Runtime MUST Support Fixed Spawn Anchors
The system MUST allow hostile faction units to spawn only from predefined scene anchors assigned to that faction.

#### Scenario: Spawn uses configured anchor positions
- **WHEN** a hostile faction wave starts
- **THEN** each spawned unit MUST use a position from configured fixed spawn anchors

#### Scenario: Invalid anchor is skipped safely
- **WHEN** an anchor is unavailable or invalid at spawn time
- **THEN** the runtime MUST skip or fallback to another valid anchor without crashing the wave loop

### Requirement: V1 Scope MUST Be Limited To One Hostile Assault Faction
The first release SHALL support exactly one active hostile faction profile dedicated to attacking the player side.

#### Scenario: Single hostile profile is activated
- **WHEN** the scenario initializes in V1 mode
- **THEN** the runtime MUST activate one configured assault faction profile and keep other faction profiles inactive

#### Scenario: Non-V1 faction content stays out-of-scope
- **WHEN** additional faction profiles are present in content data
- **THEN** V1 runtime MUST ignore them unless the spec is expanded

### Requirement: Hostile Faction MUST Execute A Goal-Driven Assault Loop
The faction runtime MUST evaluate and execute an assault loop with bounded stages: assemble, advance, attack, and regroup.

#### Scenario: Assault stage progression
- **WHEN** faction conditions satisfy stage transition criteria
- **THEN** the runtime MUST move through `Assemble -> Advance -> Attack -> Regroup` in deterministic order

#### Scenario: Regroup after heavy loss or repeated failure
- **WHEN** casualty or action-failure thresholds are exceeded
- **THEN** the runtime MUST switch stage to `Regroup` and request replanning

### Requirement: Faction Objectives MUST Bridge Into Existing GOAP And Combat Pipelines
Faction intent MUST be translated into GOAP and executed through the shared combat action pipeline rather than bypassing it.

#### Scenario: Goal translation drives unit planning
- **WHEN** faction directive is issued to member units
- **THEN** each unit MUST receive objective context that can be evaluated by GOAP runtime inputs

#### Scenario: Shared legality pipeline is preserved
- **WHEN** a faction-controlled unit executes combat or terrain actions
- **THEN** execution MUST pass through the same legality and action pipeline used by manual/managed actions

### Requirement: Target Prioritization MUST Prefer Player Before Core
In V1 assault behavior, hostile units MUST prioritize player targets and fallback to core targets when player targeting is not feasible.

#### Scenario: Player target is reachable
- **WHEN** at least one valid player target is reachable
- **THEN** hostile units MUST choose player-target assault intent before core assault intent

#### Scenario: Fallback to core target
- **WHEN** no reachable player target exists
- **THEN** hostile units MUST switch to core-target assault intent

### Requirement: Spawn Waves MUST Respect Budget And Alive Caps
Faction spawning SHALL enforce wave-level spawn budget and total alive-unit cap to keep runtime bounded.

#### Scenario: Wave budget limits spawned count
- **WHEN** a wave has a configured spawn budget
- **THEN** spawned units in that wave MUST NOT exceed the configured budget

#### Scenario: Alive cap prevents over-spawn
- **WHEN** alive hostile units already reach configured cap
- **THEN** additional spawning MUST be deferred or skipped until capacity is available

### Requirement: Terrain-Related Actions MUST Respect Home/Away Authority
Hostile faction terrain actions MUST obey existing home/away authority checks.

#### Scenario: Away terrain action without authority is denied
- **WHEN** a hostile unit tries `BreakBlock` or `PlaceBlock` in away territory without temporary authority
- **THEN** the action MUST be denied with a legality reason

#### Scenario: Home terrain action follows normal legality
- **WHEN** a hostile unit executes terrain action in home-authorized context
- **THEN** the action MUST be evaluated under normal legality rules and may proceed

### Requirement: Faction Runtime MUST Provide Safe Fallback On Planning Failure
If no feasible assault plan is available, the faction runtime SHALL choose deterministic fallback behavior and continue running.

#### Scenario: Infeasible plan triggers fallback
- **WHEN** unit planning fails within configured budget/depth/retry constraints
- **THEN** runtime MUST assign a fallback behavior such as regroup, hold, or idle

#### Scenario: Recovery attempts continue after fallback
- **WHEN** fallback behavior is active and replan conditions become valid
- **THEN** runtime MUST attempt replanning and return to assault stages