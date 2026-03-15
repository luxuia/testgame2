## ADDED Requirements

### Requirement: UI Runtime MUST Provide Four Fixed Presentation Layers
The UI runtime MUST provide a single root with exactly four logical layers: `HUD`, `SidePanel`, `Modal`, and `CombatOverlay`, and MUST enforce deterministic draw/input priority between these layers.

#### Scenario: Layer stack is initialized
- **WHEN** the gameplay scene UI root is created
- **THEN** the runtime MUST initialize `HUD`, `SidePanel`, `Modal`, and `CombatOverlay` layers in a deterministic order

#### Scenario: Modal blocks lower interaction
- **WHEN** a modal view is active
- **THEN** input to `HUD` and `SidePanel` interactive elements MUST be blocked unless explicitly whitelisted

### Requirement: UI Input MUST Be Routed Through Unified Commands
UI input handling MUST route mouse/keyboard or gamepad input through a unified command router instead of page-local direct input logic.

#### Scenario: Build and combat share right-click command contract
- **WHEN** the player triggers right-click input
- **THEN** the command router MUST resolve behavior from current context and dispatch exactly one command path

#### Scenario: Context change updates command resolution
- **WHEN** control context changes between camera control and unit control
- **THEN** `WASD` commands MUST be remapped through the same router without requiring per-page rebind logic

### Requirement: HUD MUST Expose Core Persistent Modules
The runtime MUST provide a persistent HUD view including four core module groups: player status, 9-slot hotbar, mana/capacity bar, and right-bottom fixed action buttons.

#### Scenario: HUD default visibility
- **WHEN** gameplay enters normal exploration state
- **THEN** the four HUD module groups MUST be visible without opening additional panels

#### Scenario: Right-bottom button alert replacement
- **WHEN** invasion alert state becomes active
- **THEN** one fixed action button MUST switch to alert presentation and alert action binding

### Requirement: Side Panel MUST Support Collapsible Multi-Tab Navigation
The runtime MUST provide one collapsible side panel with tabbed pages for build blocks, slime encyclopedia, adjutant management, resource summary, quests, and diplomacy.

#### Scenario: Side panel opens and closes from unified trigger
- **WHEN** the player triggers side panel toggle via configured input
- **THEN** the panel MUST transition between collapsed and expanded states without scene reload

#### Scenario: Locked tab remains hidden in guided mode
- **WHEN** guided mode is active and a tab is not unlocked
- **THEN** the tab MUST NOT be visible in the tab list

### Requirement: Secondary Functional Views MUST Reuse Modal Template
Secondary views such as sin skill tree, slime detail, and adjutant growth MUST be rendered using a shared modal template contract for frame, close action, and paging behavior.

#### Scenario: Secondary view opens with shared shell
- **WHEN** any registered secondary functional view is opened
- **THEN** the runtime MUST instantiate it under the shared modal template contract

#### Scenario: Shared close behavior remains consistent
- **WHEN** the close action is triggered from any secondary modal
- **THEN** the runtime MUST apply the same close transition and focus restore policy

### Requirement: Combat Overlay MUST Be Event-Driven
Combat-specific overlay UI MUST appear and disappear according to combat lifecycle events and MUST NOT remain permanently visible outside combat context.

#### Scenario: Combat enter shows overlay
- **WHEN** combat state transitions from non-combat to combat
- **THEN** the combat overlay MUST become visible and bind current combat data

#### Scenario: Combat exit hides overlay
- **WHEN** combat state transitions from combat to non-combat
- **THEN** the combat overlay MUST be hidden and release transient bindings

### Requirement: UI Data MUST Flow Through Aggregated UI State Contracts
Views MUST consume aggregated UI state contracts and MUST NOT directly query multiple gameplay systems per-frame.

#### Scenario: HUD refresh from state snapshot
- **WHEN** core HUD data changes (status, hotbar, mana, alert)
- **THEN** the HUD MUST refresh from a single aggregated state snapshot update

#### Scenario: Domain refactor does not force view coupling change
- **WHEN** internal domain providers are replaced behind the state adapter
- **THEN** view contracts MUST remain stable without page-level direct dependency rewrites

### Requirement: Framework MUST Support Incremental Delivery Gates
The framework MUST define staged readiness gates so teams can ship skeleton-first and progressively attach functional data.

#### Scenario: Skeleton stage completion
- **WHEN** stage-1 gate is evaluated
- **THEN** layer structure, navigation routing, and placeholder pages MUST be operational

#### Scenario: Functional stage completion
- **WHEN** stage-2 gate is evaluated
- **THEN** core HUD and side panel pages MUST bind live data with validated interaction flows
