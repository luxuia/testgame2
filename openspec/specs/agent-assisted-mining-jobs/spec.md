# agent-assisted-mining-jobs Specification

## Purpose
TBD - created by archiving change agent-mining-box-selection. Update Purpose after archive.
## Requirements
### Requirement: Click And Drag Selection For Mining
The system SHALL support both single-click mining selection and drag-based 3D volume mining selection on solid blocks.

#### Scenario: Click selects one block
- **WHEN** the player left-clicks without exceeding the drag threshold and the raycast hits a solid block
- **THEN** the system MUST create a selection containing exactly that one block

#### Scenario: Drag selects a 3D block volume
- **WHEN** the player drags from a valid start block to a valid end block and releases the mouse
- **THEN** the system MUST select all diggable solid blocks within the axis-aligned 3D volume bounded by the two block positions

#### Scenario: Selection is clamped by safety limit
- **WHEN** the computed 3D selection size exceeds `MaxSelectedBlocks`
- **THEN** the system MUST clamp the selected set to the configured maximum and remain responsive

### Requirement: Selected Blocks Are Visually Highlighted
The system SHALL provide clear visual feedback for selected mining blocks using the existing highlight style.

#### Scenario: Selected set is visible
- **WHEN** one or more blocks are selected for mining
- **THEN** every selected block MUST have visible highlight feedback

#### Scenario: Focus block keeps dig progress overlay
- **WHEN** a selected block is currently being actively mined as the focus block
- **THEN** the block MUST continue to use the existing dig progress visual overlay behavior

### Requirement: Agent Assignment Is Slot-Capacity Constrained
The system SHALL assign agents to mining blocks based on free walkable slots around each block, and MUST not over-assign beyond available slot capacity.

#### Scenario: Single block with limited free slots
- **WHEN** one selected block has only 2 valid free adjacent walkable slots
- **THEN** at most 2 agents MUST be assigned to mine that block

#### Scenario: Many blocks prefer broad distribution first
- **WHEN** there are 10 selected blocks and at least 10 available agents
- **THEN** the assignment strategy MUST prioritize giving each block at least one assigned agent before filling extra slots on any block

#### Scenario: Invalid slot triggers reassignment
- **WHEN** an assigned slot becomes invalid (blocked, unreachable, or removed)
- **THEN** the system MUST revoke that assignment and attempt localized reassignment without rebuilding all assignments

### Requirement: Cooperative Mining Uses Linear Damage Stacking
The system SHALL accumulate mining damage for each block as the linear sum of all valid assigned agents mining that block in the current frame.

#### Scenario: Two agents mine one block together
- **WHEN** two valid agents are simultaneously mining the same block
- **THEN** the per-frame block damage MUST equal the sum of both agents' mining contributions

#### Scenario: Block completion finalizes and requeues
- **WHEN** accumulated damage reaches or exceeds the block hardness
- **THEN** the block MUST be removed from the world, removed from active mining jobs, and affected agents MUST be made available for reassignment

### Requirement: Bulk Selection Processing Is Performance-Bounded
The system SHALL process large selections and assignment updates with bounded per-frame work to avoid frame spikes and excessive garbage generation.

#### Scenario: Large selection update stays bounded
- **WHEN** a large 3D selection is submitted
- **THEN** the system MUST process job creation and slot discovery incrementally or in bounded batches instead of unbounded one-frame work

#### Scenario: Runtime avoids avoidable allocations
- **WHEN** recurring update loops execute for selection, assignment, and mining execution
- **THEN** the system MUST avoid avoidable per-frame heap allocations in steady state

