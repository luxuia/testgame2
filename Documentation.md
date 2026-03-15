# Documentation.md

## 1) 当前里程碑状态

- M1 单角色点击链路稳定与低分配：`done`
- M2 群体协调器 Update 开销受控：`done`
- M3 算法观测能力落地：`done`
- M4 文档闭环：`in_progress`

最后更新时间：2026-03-15

## 2) 已做出的决策及原因

- 决策：单角色默认关闭 FlowField（`PreferFlowField=false`）。
- 原因：单角色频繁点击时，FlowField 构建开销和分配大于收益。

- 决策：FlowField 引入失败冷却缓存。
- 原因：目标不可达时避免每帧重复“最近可行走点搜索”。

- 决策：`CrowdFlowFieldCoordinator` 的 agent 自动收集改为按需 + 低频刷新。
- 原因：避免 `GetComponentsInChildren` 每帧全量扫描导致 `Update` 峰值。

- 决策：保留可开关的寻路统计日志。
- 原因：便于快速定位“算法耗时 vs 节点量 vs 缓存命中”问题。

## 3) 运行和演示命令

- 编译验证：
- `dotnet build UnityProject/Assembly-CSharp.csproj -nologo`

- 文档结构快速检查：
- `rg -n "^#|^##|^###" Prompt.md Plan.md Implement.md Documentation.md`

- 关键实现定位：
- `rg -n "LogPathfindingStats|\\[Pathfinding\\]\\[AStar\\]|\\[Pathfinding\\]\\[FlowField\\]" UnityProject/Assets/Scripts/PlayerControls/PathfindingMovementController.cs`
- `rg -n "AutoRefreshInterval|IncludeInactiveAgents|OnTransformChildrenChanged" UnityProject/Assets/Scripts/Pathfinding/CrowdFlowFieldCoordinator.cs`

- 运行演示（Unity Editor）：
- 打开 `UnityProject`，进入 Play Mode。
- 点击目标方块，观察角色移动与 Console 中 `[Pathfinding]` 日志。
- 使用 Profiler 检查 `TargetSelector.Update` 和 `CrowdFlowFieldCoordinator.Update` 的耗时/GC。

## 4) 已知问题 / 后续跟进

- A* 当前仍使用临时 `List/Dictionary/HashSet`，大地图高频重算仍可能产生 GC 峰值。
- 建议后续：引入对象池或复用容器，继续降低点击尖峰。

- `FlowFieldMaxNodes`、`cacheLifetime`、`searchRadius` 目前主要靠手调。
- 建议后续：按地图规模/目标距离做自适应预算策略。

- 目前 crowd 逻辑仍为主线程逐 agent 驱动。
- 建议后续：如角色数继续上升，评估 Job 化或分帧调度策略。

## 5) 本轮交付清单

- 新增 `Prompt.md`
- 新增 `Plan.md`
- 新增 `Implement.md`
- 新增 `Documentation.md`
