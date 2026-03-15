# Plan.md

## 0) 规则声明

- 本文件是实施阶段唯一事实来源（single source of truth）。
- 所有实现、验证、收口都按里程碑推进，不跨里程碑顺手改功能。

## 1) 目标架构设计

- 输入层：
- `TargetSelector` 负责目标采样与站位选择，只做轻量逻辑与事件分发。

- 单角色寻路层：
- `PathfindingMovementController` 作为策略层，按配置选择 `FlowField` 或 `A*`。
- 单角色默认 A*；多人同目标场景由协调器驱动 FlowField。

- 群体寻路层：
- `CrowdFlowFieldCoordinator` 负责按目标分组、预热场流、按帧发放 next node。
- 自动收集 agent 必须采用低频/按需刷新，避免每帧全量遍历层级。

- 算法层：
- `AStarPathfinding` 负责精准路径。
- `FlowFieldPathfinding` 负责共享势场、缓存与失效。

- 观测层：
- 每次寻路打印 `算法类型 + 耗时(ms) + 遍历节点量 + 缓存命中`。

## 2) 里程碑（小步可验收）

### M1: 单角色点击链路稳定与低分配
- 边界：
- 仅修改 `TargetSelector` 与 `PathfindingMovementController` 的点击/设目标逻辑。
- 验收标准：
- 重复点击同目标不会重复触发重寻路。
- `TargetSelector` 不出现明显可避免的临时分配。
- 验证：
- 命令：`dotnet build UnityProject/Assembly-CSharp.csproj -nologo`
- 运行：进入 Play Mode 连续点击同一方块，角色不抖动、不回弹。

### M2: 群体协调器 Update 开销受控
- 边界：
- 仅修改 `CrowdFlowFieldCoordinator` 的 agent 刷新与 Tick 流程。
- 验收标准：
- `SpawnCount=1` 时 `Update` 不应因全量搜子节点出现显著开销峰值。
- 无 agent 时应尽早返回，不触发无效场流准备。
- 验证：
- 命令：`dotnet build UnityProject/Assembly-CSharp.csproj -nologo`
- 运行：Profiler 查看 `CrowdFlowFieldCoordinator.Update`，确认峰值下降。

### M3: 算法观测能力落地
- 边界：
- 仅修改 `AStarPathfinding` / `FlowFieldPathfinding` 的统计输出接口与调用方日志。
- 验收标准：
- 每次寻路日志包含耗时和节点量；可通过开关关闭。
- 验证：
- 命令：`dotnet build UnityProject/Assembly-CSharp.csproj -nologo`
- 运行：点击触发寻路，Console 出现 `[Pathfinding][AStar]` 或 `[Pathfinding][FlowField]`。

### M4: 文档闭环
- 边界：
- 仅更新四个规范文件与状态记录。
- 验收标准：
- 里程碑状态、决策、验证命令、已知问题都可追溯。
- 验证：
- 命令：`rg -n \"^#|^##|^###\" Prompt.md Plan.md Implement.md Documentation.md`

## 3) 停止并修复规则

- 触发即停：
- 出现编译错误（`dotnet build` 非 0 error）。
- 角色无法移动、无法攻击、点击后明显回退/卡死。
- 新改动引入每帧 GC 明显上升（Profiler 可复现）。

- 修复优先级：
- 先恢复正确性（可走、可打、不卡死），再恢复性能，再恢复日志/文档。

## 4) 决策备注（防止来回摇摆）

- 决策 A：单角色默认 `PreferFlowField=false`。
- 原因：FlowField 适合“多人同目标”共享，不适合每次点击都重建。

- 决策 B：FlowField 保留缓存与失败冷却。
- 原因：避免不可达目标每帧重复搜索造成尖峰。

- 决策 C：Coordinator 的自动收集采用“按需 + 低频”。
- 原因：避免 `GetComponentsInChildren` 每帧扫描造成无谓开销。
