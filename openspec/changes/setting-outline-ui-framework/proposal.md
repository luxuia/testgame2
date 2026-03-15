## Why

`doc/设定总纲.md` 已经明确了 MC 像素风极简 UI、统一操作逻辑与建造/战斗/经营一体化的目标，但当前项目缺少一套可扩展的 UI 运行时框架来承载这些规则。需要先建立低成本、可复用、可分阶段落地的 UI 骨架，避免后续功能 UI 各自为政。

## What Changes

- 新增设定驱动的 UI 框架能力：定义 UI 分层、页面生命周期、输入路由与状态桥接合同。
- 新增页面分组与最小首版清单：常驻 HUD、侧边栏分页、统一弹窗模板、战斗浮层。
- 新增输入与状态统一约束：一套按键映射覆盖建造/经营/战斗，不新增割裂模式切换。
- 新增 UI 与领域系统桥接要求：对接菌毯权限、托管状态、战斗警报与目标选择状态。
- 新增分阶段交付边界：先框架与占位页，再逐步接入具体玩法数据。

## Capabilities

### New Capabilities
- `setting-driven-ui-framework`: 基于设定总纲定义 UI 分层架构、页面合同、输入路由与状态桥接规则，用于支撑 MC 风格经营+建造+战斗一体化界面。

### Modified Capabilities
- (none)

## Impact

- 主要影响目录：
  - `UnityProject/Assets/Scripts/UI/`（新增 UI 框架层、路由、状态桥接）
  - `UnityProject/Assets/ToaruUnity.UI/`（复用并约束 View 生命周期和切换策略）
  - `UnityProject/Assets/Scenes/SinglePlayer.unity`（挂载 UI Root 与入口）
- 相关系统对接：
  - `FungalCarpetSystem`（菌毯权柄显示与权限提示）
  - `CombatFramework` / `GoalPlanning`（战斗警报、目标态势、快捷栏高亮）
  - 输入系统（左键/右键/WASD/Tab/E/Esc 统一路由）
- 非目标：
  - 本变更不包含完整美术素材生产与全部玩法面板细节实现。
