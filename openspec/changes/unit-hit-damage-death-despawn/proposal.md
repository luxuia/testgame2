## Why

当前项目已有基础攻击与动画触发，但“命中后扣血、死亡后禁用移动/控制、最终删除单位”的战斗闭环尚未完整落地。  
这会导致战斗反馈不完整、状态机不一致（角色看起来死亡但仍可移动/被控制），必须优先补齐。

## What Changes

- 新增单位受击结算链路：命中判定成功后统一进入伤害计算与扣血流程。
- 新增死亡状态切换：血量归零后进入 `Dead` 状态，并立即禁用移动与玩家/AI控制输入。
- 新增删除（Despawn）流程：单位死亡后按配置延时或即时执行删除，清理目标引用与调度占用。
- 新增最小事件钩子：`OnHit`、`OnDamageApplied`、`OnDeath`、`OnDespawn`，供动画、UI与后续掉落逻辑复用。
- 对玩家与Crowd Agent统一接入相同生命周期语义，避免多套规则并存。

## Capabilities

### New Capabilities
- `unit-hit-damage-death-lifecycle`: 定义从攻击命中到扣血、死亡状态切换、禁用控制、删除回收的完整单位生命周期行为。

### Modified Capabilities
- 无（`openspec/specs/` 目前无既有能力定义需要增量修改）。

## Impact

- 主要影响代码路径（实现阶段）：
  - `UnityProject/Assets/Scripts/CombatFramework.cs`
  - `UnityProject/Assets/Scripts/Entities/PlayerEntity.cs`
  - `UnityProject/Assets/Scripts/Pathfinding/CrowdAgentController.cs`
  - `UnityProject/Assets/Scripts/PlayerControls/PlayerController.cs`
  - `UnityProject/Assets/Scripts/Pathfinding/CrowdFlowFieldCoordinator.cs`（目标失效与回收）
- 预期新增：
  - 统一的死亡状态/生命周期控制器（可内嵌于现有实体脚本或独立组件）
  - 单位删除后的引用清理机制（路径、目标、分配关系）
