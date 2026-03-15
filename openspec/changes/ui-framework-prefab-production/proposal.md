## Why

`setting-outline-ui-framework` 已定义 UI 架构与行为合同，但若没有对应可复用的 Prefab 资产，框架无法在场景中稳定落地。需要单独定义并推进 UI 预制体制作规范与交付边界，确保后续功能接入不返工。

## What Changes

- 新增 UI 预制体交付能力：明确必须制作的核心 Prefab 列表与层级结构。
- 新增 Prefab 组装规范：命名、目录、挂载脚本、事件绑定、占位节点与资源引用策略。
- 新增 与 `ToaruUnity.UI` 对齐的页面装配要求：页面生命周期、打开关闭行为、层级归属一致。
- 新增 场景集成要求：`SinglePlayer` 场景必须可一键挂载 `UIRoot` 并驱动核心页面显隐。
- 新增 Prefab 验收标准：可实例化、可切换、可绑定占位数据、不会因缺引用报错。

## Capabilities

### New Capabilities
- `ui-prefab-production`: 定义并交付设定总纲对应的 UI 预制体资产（UIRoot/HUD/SidePanel/Modal/CombatOverlay）及其场景集成标准。

### Modified Capabilities
- (none)

## Impact

- 主要影响目录：
  - `UnityProject/Assets/Prefabs/UI/`（新增或整理 UI 预制体资产）
  - `UnityProject/Assets/Scripts/UI/`（预制体挂载脚本与绑定入口）
  - `UnityProject/Assets/Scenes/SinglePlayer.unity`（UIRoot 实例与引用）
- 关联模块：
  - `UnityProject/Assets/ToaruUnity.UI/`（页面切换与生命周期）
  - 输入与状态桥接模块（为预制体留绑定点）
- 非目标：
  - 本变更不要求完成全部玩法逻辑数据接入与最终美术精修。
