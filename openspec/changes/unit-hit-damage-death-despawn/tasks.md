## 1. Unified Hit And Damage Entry

- [ ] 1.1 Add a shared `ApplyHit/ApplyDamage` path that all unit-hit calls must use.
- [ ] 1.2 Ensure hit validation rejects invalid/dead targets without mutating health.
- [ ] 1.3 Emit `OnHit` and `OnDamageApplied` in deterministic order for successful hits.

## 2. Death Lifecycle State Machine

- [ ] 2.1 Add lifecycle states (`Alive`, `Dying`, `Dead`, `Despawned`) to unit runtime.
- [ ] 2.2 Transition to death lifecycle immediately when health reaches zero and emit `OnDeath` exactly once.
- [ ] 2.3 Prevent dead units from re-entering alive behavior via subsequent damage calls.

## 3. Disable Movement And Control On Death

- [ ] 3.1 For player units, disable control/movement input when entering death lifecycle.
- [ ] 3.2 For crowd agents, disable navigation target advancement and movement execution when entering death lifecycle.
- [ ] 3.3 Block attack and skill execution requests for dead units.

## 4. Despawn And Cleanup

- [ ] 4.1 Add configurable despawn timing and execute despawn at the configured condition.
- [ ] 4.2 Remove dead unit object from scene/runtime and emit `OnDespawn` exactly once.
- [ ] 4.3 Invalidate targeting/pathfinding/assignment references that point to despawned units.

## 5. Integration Across Existing Combat Flow

- [ ] 5.1 Integrate lifecycle handling into `CombatActionPipeline` for attack and skill damage paths.
- [ ] 5.2 Wire player and agent runtime scripts to lifecycle events (hit/death/despawn).
- [ ] 5.3 Keep animation trigger flow consistent with lifecycle transitions (hit react, death, despawn-safe state).

## 6. Validation

- [ ] 6.1 Verify hit -> damage -> death transition with both player and crowd agent targets.
- [ ] 6.2 Verify dead units cannot move, act, or be controlled before despawn.
- [ ] 6.3 Verify despawn removes unit and clears references without null-access errors.
- [ ] 6.4 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve compile issues.
