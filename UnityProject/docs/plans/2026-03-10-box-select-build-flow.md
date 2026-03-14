# 框选、采集、建造流程移植实现计划

> **For Claude:** 参考 Project Porcupine 的 BuildModeController、Job、MouseController 流程，移植到 MinecraftClone 3D 体素项目。

**Goal:** 在 MinecraftClone 中实现建造模式下的框选、采集（分解）、建造流程，支持 Job 队列与 Worker 角色执行。

**Architecture:** BuildModeController 处理模式切换与框选；DragParams 表示 3D AABB 选区；Job/JobManager 管理任务队列；Worker 实体执行 Job（移动→工作→完成）。

**Tech Stack:** Unity, C#, MinecraftClone 现有 World/Block/Chunk 体系

---

## 流程图依赖（来自 Project Porcupine）

1. 用户点击模式按钮 → SetMode_* → MouseMode BUILD
2. 用户拖拽 → HandleDragFinished(DragParams)
3. 遍历选区 → DoBuild(tile) / SetDeconstructJob / Mine
4. Job 入队 JobManager
5. Character.GetJob → JobState → HaulState/MoveState → DoWork
6. OnJobCompleted → PlaceFurniture / Deconstruct

---

## 任务列表

### Task 1: DragParams 与 3D 选区

**Files:**
- Create: `Assets/Scripts/BuildMode/DragParams.cs`

**内容:** 3D AABB 选区结构体，含 MinX/Y/Z, MaxX/Y/Z，支持从两个 Vector3Int 构造。

---

### Task 2: BuildMode 枚举与 BuildModeController

**Files:**
- Create: `Assets/Scripts/BuildMode/BuildMode.cs`
- Create: `Assets/Scripts/BuildMode/BuildModeController.cs`

**内容:** BuildMode (CONSTRUCT, DECONSTRUCT, MINE)，BuildModeController 管理当前模式、框选逻辑、DoBuild 对每个方块的调度。

---

### Task 3: Job 系统

**Files:**
- Create: `Assets/Scripts/Jobs/BlockJob.cs`
- Create: `Assets/Scripts/Jobs/JobManager.cs`

**内容:** BlockJob 含 Position, JobType (Build/Deconstruct), BlockType；JobManager 队列与 GetJob。

---

### Task 4: Worker 角色

**Files:**
- Create: `Assets/Scripts/Entities/WorkerEntity.cs`
- Modify: `Assets/Scripts/Entities/EntityManager.cs`（注册 Worker）

**内容:** Worker 继承 Entity，每帧检查 JobManager.GetJob，有 Job 时移动至目标并执行 DoWork。

---

### Task 5: BuildModeInput 与 BlockInteraction 集成

**Files:**
- Create: `Assets/Scripts/PlayerControls/BuildModeInput.cs`
- Modify: `Assets/Scripts/PlayerControls/BlockInteraction.cs`

**内容:** BuildModeInput 处理拖拽、调用 BuildModeController.HandleDragFinished；BlockInteraction 在非 BuildMode 时保持原有单格操作。

---

### Task 6: 建造模式 UI

**Files:**
- Create: `Assets/Scripts/UI/BuildModePanel.cs`（可选，简单按钮）
- Modify: `Assets/Scenes/SinglePlayer.unity`（挂载 UI）

**内容:** 建造/分解/采集按钮，切换 BuildModeController 模式。

---

## 实施顺序

1. DragParams
2. BuildMode + BuildModeController
3. BlockJob + JobManager
4. WorkerEntity
5. BuildModeInput + BlockInteraction 集成
6. UI（可选）
