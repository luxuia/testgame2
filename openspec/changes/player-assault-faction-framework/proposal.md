## Why

当前项目已经具备 GOAP-Lite、战斗执行管线、主客场权限网关与群体单位移动能力，但缺少“阵营级编排层”，导致敌方单位无法以固定出生点和统一目标计划形成稳定行为闭环。为了让设定总纲中的“阵营攻防 + 目标驱动”尽快落地，需要先完成一个低成本、可验证的首版阵营框架，并限定首版为“进攻玩家阵营”。

## What Changes

- 新增阵营编排主控：按固定出生点生成敌对阵营单位，维护阵营生命周期、波次与全局指令。
- 新增阵营数据合同：定义阵营、出生点、目标预设、首版攻方参数（进攻玩家/菌核优先级）。
- 新增阵营到行为桥接：将阵营目标映射到现有 GOAP/Combat 执行链路，统一 legality 与失败回退。
- 新增首版进攻循环：集结 -> 推进 -> 攻击玩家或菌核 -> 受挫后 regroup/重试。
- 增加最小验证点：固定出生点一致性、目标切换稳定性、Away 权限限制下的动作拒绝与回退行为。

## Capabilities

### New Capabilities
- `faction-assault-orchestration`: 定义敌对阵营从固定出生点出生、依据目标计划执行进攻玩家行为的首版运行时能力。

### Modified Capabilities
- (none)

## Impact

- 主要影响模块（实现阶段）：
  - `UnityProject/Assets/Scripts/GoalPlanning/`
  - `UnityProject/Assets/Scripts/Combat*.cs`
  - `UnityProject/Assets/Scripts/Pathfinding/CrowdAgentController.cs`
  - 新增 `UnityProject/Assets/Scripts/Faction/`（阵营编排与配置合同）
- OpenSpec 影响：新增 `specs/faction-assault-orchestration/spec.md`。
- 非目标：首版不实现多阵营外交、不实现复杂剧情导演、不改底层寻路与区块系统。
