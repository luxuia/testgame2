## ADDED Requirements

### Requirement: Hit Resolution MUST Apply Damage Through A Unified Entry
The system MUST process successful unit hits through a unified damage entry path, and SHALL update target health in the same flow.

#### Scenario: Successful hit applies target damage
- **WHEN** an attack hit is confirmed against a valid alive target unit
- **THEN** the system MUST execute unified damage application and reduce the target's health accordingly

#### Scenario: Invalid target does not apply damage
- **WHEN** an attack request points to a missing, already-dead, or invalid target
- **THEN** the system MUST reject damage application and keep health state unchanged

### Requirement: Zero Health MUST Transition Unit To Death State
The system SHALL transition a unit into death lifecycle states when health reaches zero or below.

#### Scenario: Health reaches zero
- **WHEN** damage application reduces a unit's health to zero or lower
- **THEN** the unit MUST transition from `Alive` to death lifecycle (`Dying` then `Dead`) and emit `OnDeath`

#### Scenario: Further damage does not revive or reset dead unit
- **WHEN** additional hit processing occurs after a unit is already dead
- **THEN** the system MUST keep the unit in dead lifecycle state and MUST NOT restore movement/control availability

### Requirement: Death State MUST Disable Movement And Control
The system SHALL disable movement and control behavior immediately after entering death lifecycle.

#### Scenario: Player death disables control
- **WHEN** a player-controlled unit enters death lifecycle
- **THEN** movement input and control actions MUST be disabled immediately

#### Scenario: Agent death disables navigation update
- **WHEN** an AI/crowd agent enters death lifecycle
- **THEN** navigation target advancement and movement execution MUST be disabled immediately

### Requirement: Dead Units MUST Be Despawned And Cleaned Up
The system SHALL despawn dead units and clear runtime references after death handling.

#### Scenario: Unit despawns after death delay
- **WHEN** a unit remains in dead lifecycle until configured despawn timing condition is met
- **THEN** the system MUST remove the unit object from scene/runtime and emit `OnDespawn`

#### Scenario: Despawn clears target references
- **WHEN** a dead unit is despawned
- **THEN** pathfinding/targeting/assignment references to that unit MUST be invalidated or removed

### Requirement: Combat Lifecycle MUST Expose Minimal Observable Events
The system SHALL expose consistent lifecycle events for combat integration points.

#### Scenario: Hit and damage events are observable
- **WHEN** a successful hit is processed
- **THEN** `OnHit` and `OnDamageApplied` MUST be raised in deterministic order

#### Scenario: Death and despawn events are observable
- **WHEN** a unit transitions into death and then despawn phases
- **THEN** `OnDeath` and `OnDespawn` MUST be raised exactly once per lifecycle
