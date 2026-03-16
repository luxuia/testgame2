# Memory

## 2026-03-16

### 23:19

#### 今日工作内容
- 完成建筑系统运行时框架，新增 BuildingDefinition/BuildingInstanceRuntime/BuildingRuntimeService 等核心类型并落到 Assets/Scripts/Building。
- 实现建筑功能聚合快照（BuildingFunctionalSnapshot），支持核心权限、孵化、提纯、分解、防御与副官自动化能力汇总。
- 接入世界生命周期与方块变更事件：World 启动时确保 BuildingRuntimeService 实例化，Chunk.SetBlock 后通知建筑系统重校验。
- 扩展 FungalCarpetSystem 查询接口，新增按方块/世界坐标读取菌毯状态与 Completed 判定，供建筑地基权限校验使用。
- 完成一次编译验证（dotnet build UnityProject/Assembly-CSharp.csproj -nologo），结果 0 errors。

#### 后续注意点
- 将建筑放置流程接入玩家输入/快捷栏或建造 UI，打通 TryPlaceBuilding 的游戏内触发路径。
- 把建筑功能加成接入实际系统数值（孵化速度、提纯效率、炮台强度、副官托管效率），避免仅停留在快照层。
- 在 SinglePlayer 场景做 PlayMode 验证，重点检查建筑状态随方块破坏与菌毯变化的实时更新。

### 23:20

#### 今日工作内容
- 完成 OpenSpec 变更 ui-framework-prefab-production 的 proposal/design/spec/tasks，并将 tasks 勾选为 19/19 完成
- 在 SinglePlayer 场景新增 UIBootstrap，运行时实例化单个 UIRoot，并默认打开 HUDRoot、SidePanel、CombatOverlay
- 交付并装配 5 个核心 UI 预制体：UIRoot、HUDRoot、SidePanel、ModalTemplate、CombatOverlay，统一放在 Assets/Prefabs/UI
- 将 UI 资源加载改为单一来源直连 Prefab，移除 Resources/UI 重复副本，降低后续维护成本
- 补充 UI 运行时容错与绑定脚本（UIManagerBindingBridge、UIManager 回填逻辑、各 View 槽位自动解析）并完成编译验证

#### 后续注意点
- Play 模式仍出现既有错误日志（Unknown script missing 与 NullReferenceException），需单独定位旧场景对象来源
- 当前页面为骨架占位，后续需接入真实业务数据（状态/资源/警报）与二级内容页
- 保持 UI 预制体只在 Assets/Prefabs/UI 单点维护，避免再次建立镜像目录

### 23:20

#### 今日工作内容
- 实现菌毯基础规则：人物或agent经过地面触发感染，状态包含 Infecting、Damaged、Completed，并接入世界初始化与方块变更通知。
- 将 FungalCarpetSystem 拆分为独立 MonoBehaviour 文件并补齐编译接入，符合 Unity 脚本文件规则。
- 完成菌毯渲染链路：新增全局 shader 参数与状态图采样，方块材质叠加独立菌毯贴图，随后增强三态视觉差异与边缘爬行动画。
- 修复菌毯可见性问题：补充自动感染兜底（PlayerTransform 或 Camera）、线性状态图采样、叠加遮罩与亮度控制，确保场景可见且不过曝。
- 实现玩家出生点筛选：优先选择周围平坦且非水域的可站立位置，包含头顶空间检查与候选评分。
- 修复出生点扫描空引用：在 EnablePlayer 协程中等待世界初始化与 RWAccessor 就绪，并增加区块可访问校验。

#### 后续注意点
- 当前项目启用了 LOAD_ASSET_BUNDLE_FROM_FILE，shader与材质改动需确认运行时资源来源一致，避免出现改了代码但画面不变。
- 若菌毯亮度仍偏高，可继续下调 FungalCarpetSystem.OverlayStrength 或 Core.hlsl 中 edgeMask 与 completedGlow 系数。
- 出生点算法目前半径为48格并按3x3平坦度评分；若地图更复杂可增加重试预算或分层半径搜索。

### 23:21

#### 今日工作内容
- 完成阵营框架第一版代码：新增 FactionScenarioDirector/FactionAgentBrainBridge/FactionContracts，并打通 GOAP 与 CombatActionPipeline。
- 在 SinglePlayer 场景配置了敌对阵营样例数据：Faction Director、4 个固定锚点、Core/Regroup 点、ObjectivePreset 资源与两组波次。
- 完成固定出生点一致性验证（两次重放坐标一致），并更新 OpenSpec 任务进度到 19/22（已勾选 6.1）。
- 修复编译阻塞：补齐 BuildingRuntimeService.cs.meta 与 CombatFeedbackView.cs.meta，恢复 Assembly-CSharp 可编译。
- 修复 FluidInteractor 空引用链路：为 UpdateState/CheckHead/CheckBody 增加 world/camera/block 判空与流体映射初始化兜底。

#### 后续注意点
- 当前 PlayMode 仍有大量既有 NullReferenceException（PlayerEntity/SectionRendering/WorkScheduler 等），会干扰阵营追击行为验证。
- 现有 Director 逻辑仍是单活跃 profile（V1 guard），若要多阵营同时出兵需改为多 profile 并行调度。
- 敌人仍存在部分高空刷出样本（y=120），需继续排查区块加载与出生贴地时序。

### 23:26

#### 今日工作内容
- 将场景交互改为左键点击/滑动选中待挖掘方块，右键点击/滑动选中待建造方块，并在 TargetSelector 增加动作类型区分（Mine/Build）。
- 扩展 AgentMiningJobManager 支持建造任务：按槽位分配 agent，累计建造进度完成后落块，同时保留挖掘任务流程。
- 新增建造方块来源联动：AgentMiningJobManager 优先读取 BlockInteraction 当前手持方块，失败时回退到配置的内部名/ID。
- 实现拖动过程实时高亮预览：滑动采样时即时显示预览选区与目标标记，鼠标松开后才提交正式任务，避免拖动中频繁重分配。
- 完成 OpenSpec 变更 agent-mining-box-selection 归档并同步 spec，生成主规范 openspec/specs/agent-assisted-mining-jobs/spec.md。
- 对上述改动进行了多次编译验证（dotnet build UnityProject/Assembly-CSharp.csproj -nologo），结果均为 0 errors。

#### 后续注意点
- 在 Unity PlayMode 验证左右键滑动选区的边界行为（跨高度、快速滑动漏采样、Esc 取消）与视觉一致性。
- 确认建造落块时不会与玩家/agent 包围盒冲突；如需约束，补充与旧 PlaceBlock 相同的碰撞校验。
- 当前 AgentMiningJobManager 已兼容 Mine/Build 两类任务，后续若拆分独立管理器需同步场景引用与调试面板字段。
- 新同步出的主规范 Purpose 仍为 TBD，后续在 specs 文档中补全业务目的说明。
