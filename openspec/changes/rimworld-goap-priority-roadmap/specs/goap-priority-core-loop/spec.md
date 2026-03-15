## ADDED Requirements

### Requirement: GOAP Implementation MUST Follow Priority Phases
The system MUST implement GOAP in ordered phases `P0 -> P1 -> P2` and SHALL complete P0 capabilities before enabling P1/P2-only features.

#### Scenario: P0 baseline gate
- **WHEN** GOAP runtime is enabled for the first milestone
- **THEN** it MUST include P0 capabilities: blackboard facts, goal scoring, emergency interrupt, authority legality gate, and fallback behavior

#### Scenario: P1 does not bypass P0
- **WHEN** a P1 capability (for example reservation) is introduced
- **THEN** P0 capabilities MUST already be available and active

### Requirement: Territory Authority MUST Be A Planning Precondition
Planning and execution for terrain-related actions MUST pass through home/away authority checks before execution.

#### Scenario: Home allows normal terrain step
- **WHEN** planned step is `BreakBlock` or `PlaceBlock` inside home (fungus-covered) territory
- **THEN** the step MUST be considered legal under normal action checks

#### Scenario: Away blocks unrestricted terrain step
- **WHEN** planned step is `BreakBlock` or `PlaceBlock` in away territory without temporary permission
- **THEN** the step MUST be rejected before execution and emit a denial reason

### Requirement: Core Defense Emergency MUST Preempt Current Plans
Core defense urgency MUST preempt lower-priority plans and trigger immediate replanning.

#### Scenario: Emergency override
- **WHEN** core health or threat reaches configured emergency threshold
- **THEN** runtime MUST interrupt current non-defense plan and switch to defense/recovery intent

#### Scenario: Emergency takes precedence over cooldown
- **WHEN** emergency override condition is active
- **THEN** replanning MUST occur immediately even if normal replanning cooldown has not elapsed

### Requirement: Reservation Mechanism MUST Prevent Multi-Agent Target Conflicts
The system SHALL reserve actionable targets (block/slot/repair point) to avoid conflicting assignment among agents.

#### Scenario: Reserved target cannot be double-claimed
- **WHEN** one agent claims a target with active reservation
- **THEN** another agent MUST NOT receive the same target until reservation is released or expired

#### Scenario: Reservation release on failure
- **WHEN** assigned action fails or is interrupted permanently
- **THEN** reservation MUST be released to allow reassignment

### Requirement: Plan Execution MUST Be Small-Step And Interruptible
Planned actions SHALL execute as interruptible small steps to support rapid context switching.

#### Scenario: Step-level interruption
- **WHEN** high-priority interrupt signal arrives during step execution
- **THEN** runtime MUST stop current step safely and request replanning

#### Scenario: Continue when stable
- **WHEN** no interrupt condition is present
- **THEN** runtime MUST continue remaining steps in order

### Requirement: RimWorld-Inspired Borrowing MUST Stay Within Low-Cost Boundary
The system MUST borrow lightweight mechanisms from RimWorld-like planning and MUST NOT require full narrative director complexity for MVP.

#### Scenario: Borrowed mechanisms present
- **WHEN** MVP planning behavior is evaluated
- **THEN** it MUST include at least priority goal filtering, reservation, and interruptible step execution

#### Scenario: Heavy director excluded
- **WHEN** MVP scope is reviewed
- **THEN** full-scale narrative event director and complex scripted storyteller components MUST be out of scope

### Requirement: Priority Node Roadmap MUST Be Implementation-Ready
The roadmap SHALL define concrete node groups that can be implemented and validated in sequence.

#### Scenario: Node groups are explicit
- **WHEN** engineering planning consumes this capability
- **THEN** roadmap MUST expose node groups for P0, P1, and P2 with clear entry criteria

#### Scenario: Validation follows node order
- **WHEN** milestone validation is run
- **THEN** checks MUST verify P0 first, then P1, then P2 in dependency order
