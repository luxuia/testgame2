## 1. Unified Hit And Damage Entry

- [x] 1.1 Add a shared `ApplyHit/ApplyDamage` path that all unit-hit calls must use.
- [x] 1.2 Ensure hit validation rejects invalid/dead targets without mutating health.
- [x] 1.3 Emit `OnHit` and `OnDamageApplied` in deterministic order for successful hits.

## 2. Death Lifecycle State Machine

- [x] 2.1 Add lifecycle states (`Alive`, `Dying`, `Dead`, `Despawned`) to unit runtime.
- [x] 2.2 Transition to death lifecycle immediately when health reaches zero and emit `OnDeath` exactly once.
- [x] 2.3 Prevent dead units from re-entering alive behavior via subsequent damage calls.

## 3. Disable Movement And Control On Death

- [x] 3.1 For player units, disable control/movement input when entering death lifecycle.
- [x] 3.2 For crowd agents, disable navigation target advancement and movement execution when entering death lifecycle.
- [x] 3.3 Block attack and skill execution requests for dead units.

## 4. Despawn And Cleanup

- [x] 4.1 Add configurable despawn timing and execute despawn at the configured condition.
- [x] 4.2 Remove dead unit object from scene/runtime and emit `OnDespawn` exactly once.
- [x] 4.3 Invalidate targeting/pathfinding/assignment references that point to despawned units.

## 5. Integration Across Existing Combat Flow

- [x] 5.1 Integrate lifecycle handling into `CombatActionPipeline` for attack and skill damage paths.
- [x] 5.2 Wire player and agent runtime scripts to lifecycle events (hit/death/despawn).
- [x] 5.3 Keep animation trigger flow consistent with lifecycle transitions (hit react, death, despawn-safe state).

## 6. Validation

- [x] 6.1 Verify hit -> damage -> death transition with both player and crowd agent targets.
- [x] 6.2 Verify dead units cannot move, act, or be controlled before despawn.
- [x] 6.3 Verify despawn removes unit and clears references without null-access errors.
- [x] 6.4 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve compile issues.
