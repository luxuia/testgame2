## Why

你要的是“类似 RimWorld 的目标规划（GOAP-like）”，不是任务/Quest 系统。  
当前项目已有战斗、菌毯主客场权限、托管与地形改造能力，但缺少一个统一的目标规划中枢，导致：

- 行为逻辑分散在局部控制器，难以形成稳定的“先做什么、再做什么”决策闭环；
- 托管、战斗、建造、采集之间优先级切换缺少统一规则；
- 规则扩展容易变成硬编码分支堆叠，超出小团队维护成本。

需要引入一个 **GOAP-Lite 目标规划系统**：  
固定目标集合、固定动作集合、有限深度规划、统一执行管线，不做全量通用 GOAP。

## What Changes

- 新增 GOAP-Lite 三层能力：
  - `GoalSelector`：按世界事实打分并选择当前目标；
  - `Planner`：在有限动作集上进行小深度规划；
  - `Executor`：执行计划、处理中断、触发重规划。
- 新增世界事实/条件/效果的最小运行时合同（黑板模型）。
- 新增规划器 9 键最小配置合同，禁止额外顶层全局键。
- 新增与主客场权限统一网关的规划动作约束：
  - 地形相关动作（破坏/放置/菌毯扩张）必须经过 home/away 权限判定。
- 新增紧急重规划规则（核心血量、威胁激增、权限状态变更、动作连续失败）。

## Capabilities

### New Capabilities
- `goal-oriented-action-planning`: 定义 GOAP-Lite 目标选择、有限深度动作规划、计划执行中断与重规划、以及与菌毯权限系统的统一约束。

### Modified Capabilities
- 无（与 `fungus-territory-combat` 通过接口契约集成，不改其能力边界）。

## Impact

- 预期影响模块（实现阶段）：
  - `UnityProject/Assets/Scripts/Pathfinding/`（托管与单位行为切换）
  - `UnityProject/Assets/Scripts/Entities/`（单位行为驱动入口）
  - `UnityProject/Assets/Scripts/` 下新增或扩展 `GoalPlanning/`, `AI/`, `Combat/` 相关模块
  - `UnityProject/Assets/Scripts/Configurations/`（规划配置合同）
- 非目标：
  - 不实现任务接取/奖励/剧情树系统；
  - 不实现全量通用 GOAP 编辑器；
  - 不重写寻路、区块、渲染底层。
