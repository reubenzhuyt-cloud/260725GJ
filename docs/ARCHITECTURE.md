# 项目架构

## 范围与主要位置

本项目基于 Unity 2022.3.62f3c1 LTS 与 URP 14，是一款二维酒店经营游戏。主要代码与资源位于 `Assets/Scripts`、`Assets/Data`、`Assets/Scenes` 与 `Assets/Resources`。

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
```

## 程序集与依赖

- `Core.Events`：无任何依赖的事件通道程序集，提供通用 `GameEvent` 与 `GameEvent<T>`。
- `Hotel.Runtime`：无外部 asmdef 引用，但并非与引擎无关：`RunModel.cs` 使用 `UnityEngine` 与 `[SerializeField]`，且 asmdef 的 `noEngineReferences` 为 `false`。
- `Hotel.Authoring`：依赖 `Hotel.Runtime`，定义阶段循环与资源定义等 ScriptableObject。
- `Hotel.Audio`：依赖 `Core.Events`，承载场景内音频管理器与音效事件。
- 其余游戏脚本（Data、Managers、UI、Services、Presentation、Camera）位于 `Assembly-CSharp`，消费 `Hotel.Runtime` 与事件通道。

> 说明：编辑器回归测试套件（`Hotel.Runtime.Tests`）曾在验证期间达到 105/105 通过，验证完成后已按用户要求移除，仓库中不再保留测试代码。

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

### 存档与事件状态还原

`RunSaveCodec`（`Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs`）负责运行时状态与 `RunSaveData` DTO 的双向深拷贝转换。`CreateSnapshot` 对事件历史（`EventHistory`）、Buff、租客、房间、资源等切片逐条克隆（`OccupantIds`、`TargetTenantIds` 等列表重建为新实例）；`RestoreSnapshot` 校验 `SchemaVersion` 与 `RunId`，按主键重建字典，并对恢复的 `EventHistory` 按 `EventId` 去重（保留更有意义的一条：已结算 > 天数更大 > 出现次数更多），对 Buff 的 `LastTickDay` 越界值钳制到当前天，保证事件历史与 Buff 状态在存/读档后保持一致。

## 玩法编排

`GamePhaseManager`（`Assets/Scripts/Hotel/Managers/GamePhaseManager.cs`）驱动阶段循环（Day → Dusk → Night → Dawn）。`Dawn` 与 `Dusk` 为隐藏阶段：若无预生成事件且无待处理评审则直接跳过；进入 `Dawn` 时推进 `currentDay`。`CanAdvancePhase` 在评审、入住分配与事件处理完成前阻止推进。

`EventManager`（`Assets/Scripts/Hotel/Managers/EventManager.cs`）在 `Day` 阶段开始时为当天各阶段按概率预生成事件；进入阶段后填充事件队列，若存在待评审或待分配租客则挂起阶段门，待评审批次完成或分配变化后释放门，再逐条派发事件弹窗，并借由 `EventProcessedEvent` 驱动下一条；队列清空后置 `IsPhaseComplete` 并通过 `EventQueueEmptyEvent` 通知 UI。

`TenantReviewCoordinator`（`Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs`）按到访调度批量展示候选人，支持连续招募/拒绝；招募通过 `AddTenantChange` 与 `ResolveCandidateChange` 提交内核，并注册到 `TenantAssignmentCoordinator`。`TenantAssignmentCoordinator`（`Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`）维护房间与租客的分配（`AssignRoomChange`，内核另提供 `AssignJobChange`），`HasUnassignedTenants` 同时阻塞阶段推进与阶段门释放。

`SettlementBridge`（`Assets/Scripts/Hotel/Managers/SettlementBridge.cs`，`DefaultExecutionOrder(-100)`）持有运行时 `GameRunState` 与 `StateReducer` 实例，从资源定义（`ResourceDefinition`，含 `Assets/Data/Resources/Currency.asset` 货币）初始化缺失资源，对旧档执行 `medicine`→`currency` 迁移（`MigrateLegacyMedicineToCurrency`），监听阶段进入并在跨夜时执行食物结算，派发 `ResourceAdjustedEvent` / `FoodShortageEvent`。进入 `Dawn` 阶段时调用 `EventEffectManager.TickBuffs` 结算 Buff。

## 事件结算与 Buff

`EventEffectManager`（`Assets/Scripts/Hotel/Managers/EventEffectManager.cs`）是事件效果结算的唯一入口。`TrySettle` 在提交前执行两层守卫：`EventAffordability.CanAfford` 汇总所选选项的负资源效果（`ComputeResourceCosts`）并与当前资源存量比较，`TenantAbilityResolver.HasAllRequiredTags` 经候选配置校验在住租客是否满足 `requiredTags`；任一不满足即返回 `Rejected`，`EventManager` 重开弹窗供重新选择。守卫通过后由 `EventEffectExecutor.BuildChanges` 生成侵蚀/资源/Buff 变更，与 `ResolveEventHistoryChange` 组成单个 `AuthorizedChangeSet` 原子提交；提交失败时 `EventManager` 保留载荷并限次重试（`MaxSettleRetries`），重入的结算请求在未决期间被忽略，阶段变更时再次尝试补结算。

`EventEffectExecutor`（`Assets/Scripts/Hotel/Services/EventEffectExecutor.cs`）解析效果目标。`SameRoomOtherTenants` 取事件主角所在房间的 `OccupantIds`，排除主角自身、跳过不存在的租客并去重后返回其余同住者；主角无房间或状态缺失时返回空。Buff 由 `ApplyBuff` 效果创建（`AddBuffChange`）：以 `eventId|optionId|effectIndex|ownerTenantId|day` 生成唯一 `BuffId`，`TickTiming` 固定为 `Dawn`，`RemainingTicks` 取 `durationTicks`（非正数视为持续生效 -1），创建时把解析到的目标租客冻结到 `TargetTenantIds`。

`SettlementBridge` 在进入 `Dawn` 阶段时调用 `EventEffectManager.TickBuffs`：`ResolveBuffTargets` 优先使用创建时冻结的 `TargetTenantIds` 快照——非空快照存在时逐一校验冻结目标是否仍在住，过滤掉已离店或失效的租客；若全部冻结目标均失效，则返回空目标列表，不重新解析目标，也不会把新入住或当前在住租客纳入目标；仅当未捕获任何冻结快照（`TargetTenantIds` 为空）时才按 `Target` 动态解析目标。解析后生成侵蚀/资源增减变更：对每个有效目标施加侵蚀；租客目标型 Buff 在无有效目标时不继续扣减资源（仅资源型 Buff 仍会按 `ResourceDeltaPerTick` 结算）；`RemainingTicks <= 1` 时到期移除（`RemoveBuffChange`），否则递减并记录 `LastTickDay`（`UpdateBuffTicksChange`）；整批经单个 `AuthorizedChangeSet` 提交，失败时顺延至下一黎明重试。

## UI 职责

- `PhaseUI` 显示当前日与阶段名称。
- `NextPhaseButton`/`NextPhasePanel`：长按 1 秒触发 `GamePhaseManager.AdvancePhase`；面板在阶段处理中隐藏、事件队列清空后显示。
- `EventUI` 渲染事件弹窗（确认/选项两种模式），处理完毕后经 `EventProcessedEvent` 通知 `EventManager`。
- `TenantReviewPanel` 展示评审卡片（头像、能力、描述与招募/拒绝按钮）。
- `TenantAssignmentPanel`/`TenantAvatarListItem` 展示待分配租客；`TenantAvatarDragTrigger`/`TenantDragOverlay`/`AnchorDropTarget` 提供拖放分配交互。

## 双人房容量与同住

房间容量由场景挂载的 `RoomAvatarProperty`（`Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs`）按 `roomId` 配置：`allowDoubleOccupancy` 为 true 时容量 2，否则 1；组件以静态 `Registry` 自注册，`TenantAssignmentCoordinator.TryGetRoomCapacity` 优先查询该 Registry，缺失或失效时回退为容量 1（单人间）并告警。运行时容量体现为 `RoomRunState.OccupantIds`（`List<string>`，见 `RunModel.cs`）的长度上限：`CanAssign` 以 `OccupantIds.Count < capacity` 判定，`AssignRoomChange`/`RemoveTenantChange` 在 `StateReducer` 中维护 `OccupantIds`。

每个房间仅挂一个世界锚点（`RoomTenantAvatarSlot.positionAnchor`），其下两个按 `occupantIndex`（0/1）索引的头像视图 `RoomTenantAvatarSlot` 由 `RoomAvatarSlotLayoutController`（`DefaultExecutionOrder(21)`）在 LateUpdate 中水平居中排列：按入住数 `Clamp(assignedCount, 1, capacity)` 决定可见视图数，间距固定，空槽位以透明色隐藏（而非 `SetActive(false)`）以保留拖放命中面；未挂布局控制器时各槽位自行把锚点投影到 Canvas 并保持固定屏幕尺寸。

## 事件通信

事件通信以 ScriptableObject 通道为主：`Core.Events` 提供通用 `GameEvent`（无参）与 `GameEvent<T>`（带载荷），监听方在 `OnEnable`/`OnDisable` 注册与注销。主要通道包括：阶段（`PhaseEnteredEvent`）、事件（`GamePopupEvent`、`EventProcessedEvent`、`EventQueueEmptyEvent`）、资源（`ResourceAdjustedEvent`、`FoodShortageEvent`）、评审（`TenantReviewQueueActiveEvent`）与音频（`SoundEffectEvent`）。分配与评审完成另通过 C# 事件（`AssignmentChanged`、`ReviewBatchCompleted`）传播。

## 音频

`Hotel.Audio` 提供场景内单例 `AudioManager`（`Assets/Scripts/Hotel/Audio/AudioManager.cs`），不跨场景持久化；在自身子物体上维护两条声道：`BGM Audio`（循环背景乐）与 `SFX Audio`（一次性音效，`PlayOneShot`）。

`SoundEffectEvent : GameEvent<AudioClip>`（`Assets/Scripts/Hotel/Audio/SoundEffectEvent.cs`）通过 `Assets/Data/Events/PlaySoundEffectEvent.asset` 通道连接：任意代码 `Raise(clip)` → `AudioManager.PlaySoundEffect` 播放。默认 BGM 为空时通过 `Resources.LoadAll<AudioClip>("BGM")` 回退加载。当前预期 BGM 资源位置（`Resources` 目录下的 `BGM` 文件夹）中没有任何 BGM 剪辑，因此该回退加载目前不会返回任何剪辑。

## 场景与组合根

`MainScene` 作为组合根：挂载 `GamePhaseManager`、`EventManager`、`SettlementBridge`、`TenantReviewCoordinator`、`TenantAssignmentCoordinator`、`AudioManager` 等管理器，并配置全部 SO 事件通道资源与 UI 面板。构建设置中仅包含 `MainScene`；编辑器回归测试套件已按用户要求移除，不再包含测试代码。

## 阶段类型边界

项目存在两个独立的阶段枚举：`GamePhase`（`Assets/Scripts/Hotel/Data/EventConfig.cs`，顺序为 `Day, Dawn, Night, Dusk`，用于管理器与事件数据）与 `HotelPhase`（`Assets/Scripts/Hotel/Runtime/State/RunModel.cs`，顺序为 `Dawn, Day, Dusk, Night`，用于运行时状态与 Authoring）。两者的枚举顺序不同，架构消费者必须将它们视为不同类型，禁止按序数强转或混用，只通过显式映射（如 `TenantReviewCoordinator.ToHotelPhase`）转换。

## 编辑与数据资源

`Hotel.Authoring` 定义阶段循环 `DayCycleDefinition` 与资源定义 `ResourceDefinition`。`Assets/Data` 布局如下：

```text
Assets/Data
├─ Events      事件通道 SO 资源（含 PlaySoundEffectEvent）
├─ Configs     EventConfig 事件配置
├─ Candidates  租客候选人 TenantReviewCandidateSO
└─ Resources   资源定义（Food、Currency、Medicine 等）
```

其余表现资源（背景、事件图、UI 图、音频等）位于 `Assets/Resources` 下的分类文件夹。
