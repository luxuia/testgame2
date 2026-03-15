## Context

本设计聚焦“目标规划系统（GOAP-like）”，不包含任务接取、奖励发放、剧情树。  
目标是在现有 Minecraft 体素改造 + 菌毯主客场 + 托管系统上，建立低成本、可验证、可扩展的决策内核。

## Goals / Non-Goals

**Goals**
- 建立统一目标选择：防守、扩张、建造、采集、进攻、撤退等行为统一评分。
- 建立有限深度动作规划：固定动作库 + precondition/effect + 成本搜索。
- 建立统一执行中断机制：动作失败、世界突变、紧急状态触发重规划。
- 与主客场权限网关统一，避免双规则。
- 维持最小配置合同（9 键）并可验证。

**Non-Goals**
- 不实现任务系统（Quest/Task lifecycle）。
- 不实现全量通用 GOAP 编辑器与任意谓词语言。
- 不重写路径搜索、区块管理、渲染系统。

## Architecture

```text
World Snapshot (blackboard facts)
            |
            v
+-----------------------+
|      GoalSelector     |  评分选择目标
+-----------------------+
            |
            v
+-----------------------+
|       GoapPlanner     |  有限动作集合 + 小深度规划
+-----------------------+
            |
            v
+-----------------------+
|      PlanExecutor     |  执行、监控、重规划
+-----------------------+
            |
            v
Shared Action Pipeline + Authority Gate
(manual / AI / managed 全部统一)
```

## Decisions

1. 采用 GOAP-Lite（固定动作 + 限深）  
- 规划深度由 `maxPlanDepth` 限制。  
- 不支持运行时新增动作 schema。

2. 使用数值化黑板事实而非任意对象图  
- 事实统一为 `FactKey -> float`。  
- bool 使用 `0/1` 表示。  
- 理由：便于比较、拷贝、规划模拟，避免序列化复杂度。

3. 条件与效果合同固定  
- `Condition`: `FactKey + CompareOp + Value`  
- `Effect`: `FactKey + EffectOp + Value`
- 理由：保证规划器可预测、可测试。

4. 紧急重规划优先于冷却  
- 核心血量告警、威胁激增、权限切换可立即重规划。  
- 常规场景受 `replanCooldownSec` 限制，防止抖动。

5. 地形动作必须受权限网关约束  
- `BreakBlock`, `PlaceBlock`, `ConvertToFungus` 在 away 场景需满足临时权限。  
- 与 `fungus-territory-combat` 规则一致。

6. 规划最小配置合同（9 键）
- `maxPlanDepth`
- `replanCooldownSec`
- `goalSwitchHysteresis`
- `plannerBudgetPerTick`
- `emergencyOverrideCoreHpPct`
- `actionFailureRetryLimit`
- `planStaleTimeoutSec`
- `homeBiasWeight`
- `awaySafetyWeight`

7. 目标与动作集合固定边界（MVP）
- 目标：`DefendCore`, `ExpandFungus`, `BuildDefense`, `MineResource`, `AttackThreat`, `Recover`。
- 动作：`MoveTo`, `BreakBlock`, `PlaceBlock`, `ConvertToFungus`, `BuildFunctionalBlock`, `RepairCore`, `AttackTarget`, `Retreat`, `GatherResource`, `SummonUnit`, `HoldPosition`, `Idle`。

## Runtime Contracts

### Blackboard State
- `Dictionary<FactKey, float>`
- 必备事实示例：
  - `CoreHpPct`
  - `ThreatLevel`
  - `ResourceNeed`
  - `BuildNeed`
  - `IsInHomeTerritory`
  - `HasPortableFungusCharge`
  - `AgentHpPct`
  - `TargetReachable`

### Action Definition
- `id`
- `type`
- `preconditions[]`
- `effects[]`
- `baseCost`
- `cooldownTag`
- `interruptTags[]`

### Plan
- `steps[]`
- `totalCost`
- `createdAt`

## Planning Rules

1. 目标评分  
- 每个候选目标由基础权重 + 动态权重计算得分。  
- 仅当分差超过 `goalSwitchHysteresis` 才切换目标。

2. 限深规划  
- 在 `plannerBudgetPerTick` 和 `maxPlanDepth` 内寻找最低代价可行路径。  
- 无可行路径时回退到 `Recover` 或 `Idle`。

3. 重规划触发  
- 触发条件：
  - `CoreHpPct <= emergencyOverrideCoreHpPct`
  - 关键动作失败次数超过 `actionFailureRetryLimit`
  - 权限状态切换（home/away）
  - 计划超时（`planStaleTimeoutSec`）
- 常规重规划受 `replanCooldownSec` 限制。

## Risks / Trade-offs

- [风险] 固定动作集限制内容自由度  
  - [缓解] 通过 preset 参数化扩展，不改动作合同。
- [风险] 高频世界变化导致频繁重规划  
  - [缓解] 冷却 + 抖动阈值 + 每 tick 预算。
- [风险] away 场景过于保守  
  - [缓解] 调整 `awaySafetyWeight` 与临时权限策略。

## Migration Plan

1. 先落地纯运行时合同与规划骨架（无场景依赖）。  
2. 接入战斗/地形权限网关，验证 legality 一致性。  
3. 接入托管控制器，验证 managed 执行共用管线。  
4. 在 `SinglePlayer` 里回归核心流程：防守、建造、扩张、采集。  
5. 仅在必要时通过新 OpenSpec 变更扩展全局键。

## Open Questions

- `plannerBudgetPerTick` 是否需要按单位数量动态缩放。  
- `goalSwitchHysteresis` 是否按战斗态与非战斗态分开。  
- `awaySafetyWeight` 是否与失败次数联动。
