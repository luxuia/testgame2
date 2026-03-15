## 1. Combat Domain Baseline

- [x] 1.1 Define combat runtime models for entities, actions, and combat context (`Home` / `Away`).
- [x] 1.2 Build a unified action execution pipeline (input -> validate -> execute -> result event) for attack, skill, dodge, break-block, and place-block actions.
- [x] 1.3 Add fixed base damage evaluation flow that can apply territory coefficients without per-action special cases.

## 2. Territory Authority And Edit Permissions

- [ ] 2.1 Implement fungus-territory classification and query APIs used by combat and terrain actions.
- [ ] 2.2 Enforce home-territory free edit authority and away-territory default edit restrictions.
- [ ] 2.3 Implement away-territory temporary edit unlock via portable fungus charges with radius-limited effect.

## 3. Unified Skill Model

- [x] 3.1 Implement shared skill data contract: `Trigger + Targeting + Phases + Effects + Cooldown/Cost`.
- [x] 3.2 Implement fixed phase execution order: `Windup -> Execute -> Recovery`.
- [x] 3.3 Implement MVP effect handlers for `Damage`, `Heal`, `BuffDebuff`, `BreakBlock`, `PlaceBlock`, and `Summon`.
- [x] 3.4 Ensure `BreakBlock` and `PlaceBlock` skill effects pass through the same home/away authority gate as direct terrain actions.

## 4. Combat AI Three-Layer Architecture

- [x] 4.1 Implement `BattleDirector` for global priority control (offense, defense, repair, takeover override).
- [x] 4.2 Implement `RoleBrain` with bounded states: `Idle`, `Move`, `Attack`, `TerrainOp`, `Retreat`, `Repair`.
- [x] 4.3 Implement lightweight score-based state selection with anti-jitter guards (minimum stay time and switch cooldown).
- [x] 4.4 Route AI-selected actions through the same action/skill executor used by manual actions.

## 5. Minimal Config Contract (Nine Keys)

- [ ] 5.1 Add a combat config contract that loads only the nine approved MVP keys.
- [ ] 5.2 Add startup validation that rejects missing required keys and flags extra out-of-scope runtime keys.
- [ ] 5.3 Wire all combat/authority/takeover/boss tuning points to the approved nine-key contract.

## 6. Auto-Takeover And Adaptive Safety

- [ ] 6.1 Implement core-health threshold monitor based on `autoTakeoverCoreHpPct`.
- [ ] 6.2 Implement takeover behavior priorities: return-to-base, repair-first, and defense reinforcement.
- [ ] 6.3 Implement adaptive failure counter and relief trigger at `adaptiveFailCountThreshold`.

## 7. Boss Terrain Interaction MVP

- [ ] 7.1 Implement periodic boss terrain-break loop controlled by `bossTerrainBreakIntervalSec`.
- [ ] 7.2 Implement low-health boss phase that increases terrain pressure intensity.
- [ ] 7.3 Ensure boss terrain interaction remains compatible with home/away authority rules.

## 8. Preset-Based Post-MVP Config Path

- [ ] 8.1 Define `SkillPreset`, `AIPreset`, and `EncounterPreset` asset schemas without adding new top-level global keys.
- [ ] 8.2 Add a minimal preset loader and bind at least player, one officer, and one boss to preset assets.
- [ ] 8.3 Document preset extension rules and the OpenSpec gate required before adding new top-level global keys.

## 9. Validation And Regression

- [ ] 9.1 Verify home vs away behavior: edit authority, combat modifiers, and temporary unlock boundaries.
- [ ] 9.2 Verify skill phase order and terrain-effect authority gating.
- [ ] 9.3 Verify AI state switching stability and takeover override behavior under pressure.
- [ ] 9.4 Verify boss terrain interaction cadence and low-health phase transition.
- [ ] 9.5 Run `dotnet build UnityProject/Assembly-CSharp.csproj -nologo` and resolve compile issues.
