## 1. Selection And Visualization

- [x] 1.1 Extend `TargetSelector` to support click-to-select-one and drag-to-select-3d-volume block collection.
- [x] 1.2 Add configurable drag threshold and `MaxSelectedBlocks` guard with graceful clamping behavior.
- [x] 1.3 Implement multi-block highlight rendering via pooled markers while keeping existing focus-block shader highlight.

## 2. Mining Job Domain

- [x] 2.1 Create mining job data structures for selected blocks, per-block progress, and lifecycle states.
- [x] 2.2 Implement per-block free-slot discovery around target blocks using walkable-node rules consistent with pathfinding.
- [x] 2.3 Add incremental refresh logic when selection changes or blocks become invalid/removed.

## 3. Agent Assignment

- [x] 3.1 Implement two-pass assignment (first pass one-agent-per-block, second pass fill remaining free slots).
- [x] 3.2 Enforce slot-capacity limits so no block receives more agents than available free slots.
- [x] 3.3 Add stable assignment retention and localized reassignment on completion/invalid slot/agent state changes.

## 4. Agent Execution And Digging

- [x] 4.1 Integrate assignments with movement targets so agents navigate to slots and enter near-slot execution mode.
- [x] 4.2 Implement cooperative digging with linear damage stacking per block from all valid participating agents.
- [x] 4.3 Remove completed block jobs, update world blocks, and trigger reassignment without full rebuild.

## 5. Performance And Robustness

- [x] 5.1 Add frame-budgeted processing for large selections (task creation, slot scan, reassignment).
- [x] 5.2 Minimize runtime allocations in selection/job/assignment loops to reduce GC spikes.
- [x] 5.3 Add profiling samples and debug counters for selection size, active jobs, assigned agents, and update costs.

## 6. Validation

- [x] 6.1 Verify click mining parity with existing single-block behavior and visuals.
- [x] 6.2 Verify drag selection for 3D volumes across varying heights and chunk boundaries.
- [x] 6.3 Verify assignment scenarios: 10 blocks with many agents distributes broadly; 1 block with 2 free slots assigns only 2 agents.
