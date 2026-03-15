## Context

当前项目已具备体素改造、菌毯主客场、战斗与托管基础，但行为决策仍偏分散。  
总纲对系统提出了明确约束：低成本、主客场规则一致、托管保底、新手可恢复。  
因此 GOAP 的关键不是“功能广”，而是“优先级正确”。

## Goals / Non-Goals

**Goals:**
- 给出符合总纲的 GOAP 实施优先级（P0/P1/P2）。
- 抽取 RimWorld 可借鉴机制，映射到低成本 GOAP-Lite。
- 明确哪些机制必须先做、哪些机制后做、哪些机制不做。
- 让实施阶段可以按节点推进并可验证。

**Non-Goals:**
- 不实现完整任务/剧情导演系统。
- 不引入高成本行为树编辑器或通用规划语言。
- 不在本轮定义数值平衡细节。

## Decisions

1. 采用“三阶段优先级”而非一次性全量 GOAP  
- P0（必须先做）：黑板事实、目标评分、紧急中断、动作合法性网关、最小回退策略。  
- P1（闭环增强）：限深规划、计划执行监控、reservation、失败重试策略。  
- P2（内容扩展）：preset 体系、角色差异化策略、长链规划优化。  
- 原因：与总纲“小成本适配”一致，先保核心稳定。

2. 主客场权限进入 GOAP 前置条件（P0）  
- `BreakBlock/PlaceBlock/ConvertToFungus` 必须先判权限，再进入动作执行。  
- 原因：总纲核心规则是“菌毯即权柄”；这是第一性约束。

3. 核心防守紧急中断为最高优先级（P0）  
- 当核心血量/威胁超阈值时，直接抢占当前目标并重规划到防守/修复。  
- 借鉴 RimWorld “高优先级 Job 抢占低优先级行为”。

4. 借鉴 RimWorld 的 reservation 机制（P1）  
- 对目标方块、修复点、建造槽位做占用，避免多个单位争抢同一工作位。  
- 原因：这是群体行为稳定性的关键，成本低、收益高。

5. 借鉴 toil-like 小步执行（P1）  
- 将计划步拆成可中断小步骤，允许在危险时快速切换。  
- 原因：适配战斗/建造混合场景中的高频状态变更。

6. 不照搬 RimWorld 的复杂叙事导演系统（明确不做）  
- 只保留“事件触发权重调节”思想，不实现重剧情编排器。  
- 原因：超出团队与总纲成本边界。

## RimWorld Borrowing Map (MVP Boundary)

| Borrowed pattern | Local low-cost implementation |
|---|---|
| Priority filtering + preemption | `GoapP0Controller.Tick` uses score + hysteresis selection, and emergency preemption to force defense/recover intent. |
| Reservation to avoid target conflicts | `IGoapReservationStore` + `InMemoryGoapReservationStore` + action-level reservation key (`ReservationKind + ReservationTargetId`). |
| Toil-like interruptible steps | `PlanExecutor` executes per-step state with `BeginCurrentStep/MarkCurrentStepInterrupted/MarkCurrentStepSucceeded`. |
| Goal/interrupt/reservation telemetry | `GoapTelemetryBuffer` records goal switch, emergency interrupt, reservation conflict for panel/data sink. |

### Explicitly Excluded (MVP)

- Full narrative storyteller/director pipeline.
- High-cost scripted event orchestration with global pacing simulation.
- Generic visual GOAP authoring/editor stack in this milestone.

## Risks / Trade-offs

- [风险] 过早追求长链规划导致性能抖动 -> Mitigation: P0/P1 强制限深与预算上限。
- [风险] 无 reservation 会造成单位打架抢目标 -> Mitigation: P1 前置上线 reservation。
- [风险] 中断过于频繁导致行为抖动 -> Mitigation: 加入切换迟滞与重规划冷却。
- [风险] 只做核心节点可能短期“看起来不智能” -> Mitigation: 先保证稳定，再在 P2 做差异化。

## Migration Plan

1. 落地 P0 骨架并在 `SinglePlayer` 验证三类场景：防守、扩张、建造。  
2. 落地 P1 机制（reservation + 可中断执行 + 失败重试）。  
3. 在稳定后进入 P2，逐步加入角色策略 preset。  
4. 每阶段结束都跑编译与回归，不跨阶段叠加高风险变更。

## Open Questions

- reservation 粒度按“方块”还是“区块槽位”更合适。  
- 紧急中断是否对所有单位一致，还是副官/噗叽分层阈值。  
- P2 是否需要轻量“环境权重导演”来调节目标分布。
