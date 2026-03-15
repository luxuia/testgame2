## ADDED Requirements

### Requirement: UI Prefab Set MUST Include Core Deliverables
The project MUST provide a core UI prefab set including `UIRoot`, `HUDRoot`, `SidePanel`, `ModalTemplate`, and `CombatOverlay`.

#### Scenario: Core prefab inventory check
- **WHEN** prefab delivery is reviewed
- **THEN** all five required prefabs MUST exist under the designated UI prefab directory

#### Scenario: Missing required prefab fails delivery
- **WHEN** any required core prefab is absent
- **THEN** the prefab delivery MUST be marked incomplete

### Requirement: UIRoot Prefab MUST Define Four Stable Layers
`UIRoot` MUST define and expose four stable child layers for `HUD`, `SidePanel`, `Modal`, and `CombatOverlay` placement.

#### Scenario: Layer hierarchy integrity
- **WHEN** `UIRoot` is instantiated
- **THEN** the four required layer nodes MUST be present with deterministic ordering

#### Scenario: Overlay priority validation
- **WHEN** modal and combat overlay are active simultaneously
- **THEN** their render/input priority MUST follow configured layer ordering without ambiguity

### Requirement: Core View Prefabs MUST Be Compatible With UI Manager Lifecycle
All core view prefabs MUST be openable/closable through the configured UI manager lifecycle without scene reload.

#### Scenario: Open and close core views
- **WHEN** the runtime requests open or close operations for core views
- **THEN** each view MUST transition through manager lifecycle states successfully

#### Scenario: Reopen from pooled or cached state
- **WHEN** a previously closed core view is reopened
- **THEN** it MUST restore to an operable state without duplicate root objects

### Requirement: Prefab Assembly MUST Avoid Missing References
Delivered prefabs MUST not contain missing script or missing object references in required serialized fields.

#### Scenario: Missing reference scan
- **WHEN** prefabs are loaded in editor validation
- **THEN** required serialized references on core prefabs MUST resolve successfully

#### Scenario: Runtime instantiate validation
- **WHEN** core prefabs are instantiated in runtime play check
- **THEN** no missing-reference runtime errors MUST be emitted for required bindings

### Requirement: SinglePlayer Scene MUST Support One-Step UI Root Integration
`SinglePlayer` scene MUST support one-step integration of the delivered `UIRoot` prefab through an explicit bootstrap reference.

#### Scenario: Scene bootstrap loads UI root
- **WHEN** `SinglePlayer` scene enters play mode
- **THEN** one UI root instance MUST be created and linked to the scene bootstrap

#### Scenario: Duplicate UI root is prevented
- **WHEN** a second UI root initialization is attempted
- **THEN** the runtime MUST prevent duplicate active UI root instances

### Requirement: Side Panel Prefab MUST Support Collapsible Tab Shell
`SidePanel` prefab MUST provide a collapsible shell and tab container for six configured categories: build, slime, adjutant, resource, quest, and diplomacy.

#### Scenario: Toggle side panel shell
- **WHEN** side panel toggle command is triggered
- **THEN** the panel MUST switch between collapsed and expanded state

#### Scenario: Tab scaffold completeness
- **WHEN** side panel prefab is inspected
- **THEN** tab entries for all six categories MUST be available as predefined slots

### Requirement: Modal Template Prefab MUST Provide Shared Structural Slots
`ModalTemplate` prefab MUST expose shared slots for title, content root, close action, and optional pagination area.

#### Scenario: Secondary page mounts into modal content slot
- **WHEN** a secondary functional page is opened
- **THEN** it MUST be mountable into the modal content slot without changing modal shell structure

#### Scenario: Shared close action behavior
- **WHEN** close is triggered from modal template
- **THEN** the template MUST invoke standardized close handling for all mounted pages
