## ADDED Requirements

### Requirement: Combat MUST Respect Fungus Territory Authority
The system MUST classify combat space as fungus-covered home territory or non-fungus away territory, and SHALL gate terrain-edit actions by that authority.

#### Scenario: Home territory allows free terrain edits
- **WHEN** a combat action is executed on blocks inside fungus-covered territory
- **THEN** the system MUST allow normal block break/place actions without extra unlock cost

#### Scenario: Away territory denies unrestricted edits
- **WHEN** a combat action is executed on blocks outside fungus-covered territory
- **THEN** the system MUST deny unrestricted terrain edits unless temporary fungus edit permission is active

### Requirement: Home/Away Combat Rules MUST Differ
The system SHALL apply fixed combat modifiers in home territory and no default territory bonus in away territory.

#### Scenario: Home territory applies fixed combat bonuses
- **WHEN** a unit deals or receives combat damage inside fungus-covered territory
- **THEN** the system MUST apply configured home damage and defense bonus coefficients

#### Scenario: Away territory has no default territory bonus
- **WHEN** a unit deals or receives combat damage outside fungus-covered territory
- **THEN** the system MUST evaluate damage without home-territory bonus coefficients

### Requirement: Away Territory Temporary Edit Permission MUST Be Limited
The system SHALL support temporary away-territory editing through portable fungus charges with explicit usage limits.

#### Scenario: Temporary edit is unlocked by charge consumption
- **WHEN** the player consumes one portable fungus charge in away territory
- **THEN** the system MUST unlock terrain editing only within the configured temporary radius

#### Scenario: No charges means no temporary edit unlock
- **WHEN** the player attempts to unlock temporary edit authority with zero remaining charges
- **THEN** the system MUST reject the unlock request and keep away-territory edit restrictions active

### Requirement: MVP Combat Config Surface MUST Stay Minimal
The MVP combat system MUST expose exactly nine global configuration keys and SHALL keep all other combat rules as fixed conventions or internal constants.

#### Scenario: Config contract remains bounded
- **WHEN** the combat system initializes runtime config
- **THEN** it MUST load only these keys: `homeDamageBonusPct`, `homeDefenseBonusPct`, `awayEditCharges`, `awayEditRadius`, `dodgeInvulnSec`, `autoTakeoverCoreHpPct`, `autoRepairIntervalSec`, `bossTerrainBreakIntervalSec`, `adaptiveFailCountThreshold`

#### Scenario: Extra runtime key is rejected in MVP scope
- **WHEN** an additional combat runtime key outside the approved nine-key contract is introduced
- **THEN** the change MUST be treated as out-of-scope for this MVP and require a new spec update

### Requirement: Skill Execution MUST Use A Unified Model
The system SHALL execute all combat and terrain-relevant skills through one shared skill model with fixed phases and bounded effect types.

#### Scenario: Skill phase order is consistent
- **WHEN** any unit executes a skill
- **THEN** the skill MUST run in the order `Windup -> Execute -> Recovery` without skipping phase validation

#### Scenario: Terrain effects obey authority gate
- **WHEN** a skill includes `BreakBlock` or `PlaceBlock` effects
- **THEN** the terrain effect MUST be accepted or rejected by the same home/away authority gate used by direct terrain actions

#### Scenario: Skill effect set stays within MVP boundary
- **WHEN** a new MVP skill is authored
- **THEN** its effects MUST be composed only from `Damage`, `Heal`, `BuffDebuff`, `BreakBlock`, `PlaceBlock`, and `Summon`

### Requirement: Combat AI MUST Follow A Three-Layer Architecture
The system SHALL organize combat decision and execution into global orchestration, unit decision, and action execution layers.

#### Scenario: Global layer can override priorities in emergencies
- **WHEN** core health crosses the auto-takeover threshold
- **THEN** the global AI layer MUST raise defense and repair priorities for subordinate unit brains

#### Scenario: Unit layer uses bounded state set
- **WHEN** a unit brain evaluates its next behavior
- **THEN** it MUST choose from the bounded state set `Idle`, `Move`, `Attack`, `TerrainOp`, `Retreat`, and `Repair`

#### Scenario: Execution layer is shared with manual actions
- **WHEN** AI commits to a combat or terrain action
- **THEN** the action MUST execute through the same shared action/skill pipeline as manual player-triggered actions

### Requirement: Post-MVP Content Growth MUST Prefer Presets Over New Global Keys
The system SHALL expand combat content by adding preset assets before introducing additional top-level global runtime keys.

#### Scenario: Content expansion uses preset assets
- **WHEN** a new unit loadout or encounter pattern is introduced after MVP
- **THEN** the change MUST be represented through `SkillPreset`, `AIPreset`, or `EncounterPreset` assets without increasing the nine-key global contract

#### Scenario: New global key requires spec-level approval
- **WHEN** a proposal introduces an additional top-level global combat config key
- **THEN** the change MUST require a new spec update before implementation

### Requirement: Auto-Takeover Safety MUST Protect New Players
The system SHALL provide a takeover safety mechanism that triggers defense-oriented automation when core health falls below a configured threshold.

#### Scenario: Core low-health triggers takeover
- **WHEN** core health drops below `autoTakeoverCoreHpPct`
- **THEN** the system MUST switch AI priorities to defense, including return-to-base and repair-first behavior

#### Scenario: Repeated failures trigger adaptive relief
- **WHEN** consecutive failures reach `adaptiveFailCountThreshold`
- **THEN** the system MUST lower relevant encounter pressure using predefined adaptive difficulty rules

### Requirement: Boss Combat MUST Include Terrain Interaction
The system SHALL provide a low-cost boss terrain interaction loop to keep terrain tactics relevant during boss fights.

#### Scenario: Boss periodically breaks player cover
- **WHEN** a boss encounter is active
- **THEN** the boss MUST execute periodic terrain-break actions at the configured interval

#### Scenario: Boss enters terrain-pressure phase at low health
- **WHEN** boss health reaches the defined low-health phase threshold
- **THEN** the system MUST increase boss terrain-pressure intensity (for example, faster break cadence or attack tempo)
