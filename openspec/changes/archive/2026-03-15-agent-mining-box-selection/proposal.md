## Why

当前交互与挖掘流程是单点目标模型：点击一个方块、单目标高亮、单目标挖掘。  
这与“框选一片区域并由多个 agent 协作施工”的目标不匹配，导致多人跟随时目标重叠、人员利用率低、玩家操作效率低。

## What Changes

- 新增 3D 框选挖掘交互：
  - 点击左键：选中单个方块并创建单块挖掘任务。
  - 左键拖拽：按起止方块形成 3D 体积，批量创建挖掘任务。
- 新增多选可视化：
  - 选中方块保留与当前一致的高亮体验。
  - 支持“当前焦点块”显示现有挖掘进度贴图效果。
- 新增挖掘任务管理与分配层（job/slot/assignment）：
  - 为每个目标方块计算周围可站位槽位（walkable slots）。
  - agent 按槽位容量分配，单块最多分配数不超过可站位数量。
  - 分配策略优先“每块至少 1 人”，然后再填充剩余空槽。
- 新增协作挖掘伤害模型：
  - 同一方块上多个有效 agent 的挖掘伤害线性叠加。
- 与现有 crowd 流程集成：
  - agent 自动前往分配槽位并执行挖掘。
  - 方块完成后自动回收任务并触发局部重分配。
- 增加性能保护：
  - 限制单次框选最大方块数。
  - 对大选区执行分帧处理与增量重分配，避免峰值卡顿和 GC 抖动。

## Capabilities

### New Capabilities
- `agent-assisted-mining-jobs`: 3D 框选挖掘、槽位容量约束分配、以及 agent 协作挖掘执行能力。

### Modified Capabilities
- 无

## Impact

- 主要影响模块：
  - `UnityProject/Assets/Scripts/PlayerControls/TargetSelector.cs`
  - `UnityProject/Assets/Scripts/PlayerControls/PlayerController.cs`
  - `UnityProject/Assets/Scripts/Pathfinding/CrowdFlowFieldCoordinator.cs`
  - `UnityProject/Assets/Scripts/Pathfinding/CrowdAgentController.cs`
  - `UnityProject/Assets/Scripts/Rendering/ShaderUtility.cs`
- 新增模块（预期）：
  - 挖掘任务管理（selection/job/slot/assignment）
  - 多选高亮管理（marker pool）
- 运行时影响：
  - 需要在输入、任务调度、分配与挖掘执行之间新增状态同步。
  - 需要控制批量选区下的 CPU/GC 峰值。
