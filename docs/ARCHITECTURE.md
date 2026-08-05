# 项目架构

## 范围与主要位置

本项目基于 Unity 2022.3.62f3c1 LTS 与 URP 14，是一款二维酒店经营游戏。主要代码与资源位于 `Assets/Scripts`、`Assets/Data`、`Assets/Scenes`、`Assets/Resources` 与 `Assets/Tests`。

## 项目与模块

```text
Assets/Scripts
├─ Core/Events              Core.Events（事件通道）
├─ Hotel
│  ├─ Runtime               Hotel.Runtime（纯 C# 内核）
│  │  ├─ State
│  │  ├─ Kernel/Changes
│  │  └─ Kernel/Reduction
│  ├─ Authoring             Hotel.Authoring
│  │  ├─ DayCycle
│  │  └─ Resources
│  ├─ Audio                 Hotel.Audio
│  ├─ Data
│  ├─ Managers
│  ├─ UI
│  ├─ Services
│  ├─ Presentation/Avatars
│  └─ Camera
Assets/Data
│  ├─ Events
│  ├─ Configs
│  ├─ Candidates
│  └─ Resources
Assets/Resources
Assets/Scenes
Assets/Tests/Hotel.Runtime.Tests
```

## 程序集与依赖

- `Core.Events`：无任何依赖的事件通道程序集，提供通用 `GameEvent` 与 `GameEvent<T>`。
- `Hotel.Runtime`：无外部 asmdef 引用，但并非与引擎无关：`RunModel.cs` 使用 `UnityEngine` 与 `[SerializeField]`，且 asmdef 的 `noEngineReferences` 为 `false`。
- `Hotel.Authoring`：依赖 `Hotel.Runtime`，定义阶段循环与资源定义等 ScriptableObject。
- `Hotel.Audio`：依赖 `Core.Events`，承载场景内音频管理器与音效事件。
- `Hotel.Runtime.Tests`：依赖 `Hotel.Runtime` 与 `Hotel.Authoring`，仅在编辑器执行。
- 其余游戏脚本（Data、Managers、UI、Services、Presentation、Camera）位于 `Assembly-CSharp`，消费 `Hotel.Runtime` 与事件通道。

## 状态内核

`GameRunState`（`Assets/Scripts/Hotel/Runtime/State/RunModel.cs`）按切片保存运行状态：`Phase` 阶段状态、`Day`/`Seed`、`Decisions` 决策、`EventHistory` 事件历史、`AuditLog` 审计日志、`Tenants`/`Rooms`/`Resources` 字典、`Summary` 结算摘要以及 `ResolvedReviewCandidateIds`/`ReviewHistory` 评审记录。

所有状态修改通过 `RunChange` 与 `AuthorizedChangeSet` 表达：`AuthorizedChangeSet` 携带 `RunId`、期望 `StateVersion`、授权者与命令标识，仅能通过 `Coordinator`（阶段协调者）或 `Domain`（领域协调者）工厂创建。`StateReducer.TryCommit` 先校验 `RunId` 与版本号，再按规则校验整组变更（唯一性、租客/房间/资源引用存在性、招募与评审记录一致性等），全部通过后原子应用并递增 `StateVersion`。

```text
输入变更
  → AuthorizedChangeSet（RunId + 期望版本 + 授权者）
  → StateReducer（版本校验 + 规则校验 + 原子应用）
  → GameRunState 切片 + StateVersion++
```

`VisitorArrivalScheduler`（`Assets/Scripts/Hotel/Runtime/State/VisitorArrivalScheduler.cs`）以种子确定性生成访客到访调度 `VisitorArrival`（天、`HotelPhase`、人数）与每位候选人的初始侵蚀值，评审流程据此分批处理。

## 玩法编排

`GamePhaseManager`（`Assets/Scripts/Hotel/Managers/GamePhaseManager.cs`）驱动阶段循环（Day → Dusk → Night → Dawn）。`Dawn` 与 `Dusk` 为隐藏阶段：若无预生成事件且无待处理评审则直接跳过；进入 `Dawn` 时推进 `currentDay`。`CanAdvancePhase` 在评审、入住分配与事件处理完成前阻止推进。

`EventManager`（`Assets/Scripts/Hotel/Managers/EventManager.cs`）在 `Day` 阶段开始时为当天各阶段按概率预生成事件；进入阶段后填充事件队列，若存在待评审或待分配租客则挂起阶段门，待评审批次完成或分配变化后释放门，再逐条派发事件弹窗，并借由 `EventProcessedEvent` 驱动下一条；队列清空后置 `IsPhaseComplete` 并通过 `EventQueueEmptyEvent` 通知 UI。

`TenantReviewCoordinator`（`Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs`）按到访调度批量展示候选人，支持连续招募/拒绝；招募通过 `AddTenantChange` 与 `ResolveCandidateChange` 提交内核，并注册到 `TenantAssignmentCoordinator`。`TenantAssignmentCoordinator`（`Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`）维护房间与租客的分配（`AssignRoomChange`，内核另提供 `AssignJobChange`），`HasUnassignedTenants` 同时阻塞阶段推进与阶段门释放。

`SettlementBridge`（`Assets/Scripts/Hotel/Managers/SettlementBridge.cs`，`DefaultExecutionOrder(-100)`）持有运行时 `GameRunState` 与 `StateReducer` 实例，从资源定义初始化资源，监听阶段进入并在跨夜时执行食物结算，派发 `ResourceAdjustedEvent` / `FoodShortageEvent`。

## UI 职责

- `PhaseUI` 显示当前日与阶段名称。
- `NextPhaseButton`/`NextPhasePanel`：长按 1 秒触发 `GamePhaseManager.AdvancePhase`；面板在阶段处理中隐藏、事件队列清空后显示。
- `EventUI` 渲染事件弹窗（确认/选项两种模式），处理完毕后经 `EventProcessedEvent` 通知 `EventManager`。
- `TenantReviewPanel` 展示评审卡片（头像、能力、描述与招募/拒绝按钮）。
- `TenantAssignmentPanel`/`TenantAvatarListItem` 展示待分配租客；`TenantAvatarDragTrigger`/`TenantDragOverlay`/`AnchorDropTarget` 提供拖放分配交互。

## 事件通信

事件通信以 ScriptableObject 通道为主：`Core.Events` 提供通用 `GameEvent`（无参）与 `GameEvent<T>`（带载荷），监听方在 `OnEnable`/`OnDisable` 注册与注销。主要通道包括：阶段（`PhaseEnteredEvent`）、事件（`GamePopupEvent`、`EventProcessedEvent`、`EventQueueEmptyEvent`）、资源（`ResourceAdjustedEvent`、`FoodShortageEvent`）、评审（`TenantReviewQueueActiveEvent`）与音频（`SoundEffectEvent`）。分配与评审完成另通过 C# 事件（`AssignmentChanged`、`ReviewBatchCompleted`）传播。

## 音频

`Hotel.Audio` 提供场景内单例 `AudioManager`（`Assets/Scripts/Hotel/Audio/AudioManager.cs`），不跨场景持久化；在自身子物体上维护两条声道：`BGM Audio`（循环背景乐）与 `SFX Audio`（一次性音效，`PlayOneShot`）。

`SoundEffectEvent : GameEvent<AudioClip>`（`Assets/Scripts/Hotel/Audio/SoundEffectEvent.cs`）通过 `Assets/Data/Events/PlaySoundEffectEvent.asset` 通道连接：任意代码 `Raise(clip)` → `AudioManager.PlaySoundEffect` 播放。默认 BGM 为空时通过 `Resources.LoadAll<AudioClip>("BGM")` 回退加载。当前预期 BGM 资源位置（`Resources` 目录下的 `BGM` 文件夹）中没有任何 BGM 剪辑，因此该回退加载目前不会返回任何剪辑。

## 场景与组合根

`MainScene` 作为组合根：挂载 `GamePhaseManager`、`EventManager`、`SettlementBridge`、`TenantReviewCoordinator`、`TenantAssignmentCoordinator`、`AudioManager` 等管理器，并配置全部 SO 事件通道资源与 UI 面板。构建设置中仅包含 `MainScene`；运行时测试仅在编辑器环境执行。

## 阶段类型边界

项目存在两个独立的阶段枚举：`GamePhase`（`Assets/Scripts/Hotel/Data/EventConfig.cs`，顺序为 `Day, Dawn, Night, Dusk`，用于管理器与事件数据）与 `HotelPhase`（`Assets/Scripts/Hotel/Runtime/State/RunModel.cs`，顺序为 `Dawn, Day, Dusk, Night`，用于运行时状态与 Authoring）。两者的枚举顺序不同，架构消费者必须将它们视为不同类型，禁止按序数强转或混用，只通过显式映射（如 `TenantReviewCoordinator.ToHotelPhase`）转换。

## 编辑与数据资源

`Hotel.Authoring` 定义阶段循环 `DayCycleDefinition` 与资源定义 `ResourceDefinition`。`Assets/Data` 布局如下：

```text
Assets/Data
├─ Events      事件通道 SO 资源（含 PlaySoundEffectEvent）
├─ Configs     EventConfig 事件配置
├─ Candidates  租客候选人 TenantReviewCandidateSO
└─ Resources   资源定义（Food、Medicine 等）
```

其余表现资源（背景、事件图、UI 图、音频等）位于 `Assets/Resources` 下的分类文件夹。
