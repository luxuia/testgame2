## Why

《设定总纲》强调“自由改造 + 地下城经营 + RPG 战斗 + 托管保底”的闭环，但当前实现缺少统一的目标规划中枢。  
需要先明确 GOAP 的优先落地节点，避免 1-5 人团队在早期进入“功能很多、核心不稳”的开发节奏。

## What Changes

- 明确 GOAP 系统的三阶段优先级路线（P0/P1/P2），先稳定核心决策闭环，再扩展内容层。
- 固化与总纲强绑定的优先节点：
  - 菌毯主客场权限进入规划前置条件；
  - 核心防守与托管接管为最高优先级中断；
  - 建造/挖掘/防御动作统一走共享合法性管线。
- 引入 RimWorld 可借鉴机制并限定边界：
  - 借鉴 `ThinkTree/JobGiver` 风格的优先级筛选与回退；
  - 借鉴 reservation（资源/目标占用）避免多单位冲突；
  - 借鉴小步可中断执行（toil-like）提升恢复性；
  - 明确不照搬全量复杂故事事件系统与高成本内容调度器。
- 输出可执行实施清单，作为 GOAP 实装前的统一对齐文档。

## Capabilities

### New Capabilities
- `goap-priority-core-loop`: 定义按总纲约束排序的 GOAP 核心节点、RimWorld 借鉴机制、以及分阶段落地要求。

### Modified Capabilities
- 无

## Impact

- 主要影响：
  - `UnityProject/Assets/Scripts/GoalPlanning/`
  - `UnityProject/Assets/Scripts/Pathfinding/`
  - `UnityProject/Assets/Scripts/Entities/`
  - `UnityProject/Assets/Scripts/Combat*/`, `FungalCarpet*`
- 与既有战斗/权限能力的联动：
  - 主客场权限网关会成为 GOAP 动作合法性的前置。
  - 托管保底阈值会成为 GOAP 紧急重规划触发源。
- 非目标：
  - 不引入全量通用 GOAP 编辑器；
  - 不实现高成本剧情/事件导演系统；
  - 不新增与总纲无关的 UI 大改。
