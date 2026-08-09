# 玩家日志（Player Log）系统 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **本计划特殊约定：** 与既有计划一致，本计划**不包含任何 git 提交步骤**（用户未要求提交，所有任务以「评审门」收尾）；`Assets/Scenes/MainScene.unity` 的任何修改仅由 **unitymaster** 子代理执行，且只做「新增组件 + 序列化引用接线」，禁止任何 UI 布局/美术/样式改动；**默认不进入 Play 模式**，Play 模式人工验证仅在本计划 Task 11 且用户明确要求时执行，其余任务一律以「Unity 编译验证（Console 0 错误）＋ EditMode 测试通过」为完成标准。

**Goal:** 依据已批准规格《玩家日志（Player Log）系统设计（2026-08-09）》实现纯 C# 记录/查询内核（`PlayerLogManager` + `PlayerLogs` 持久化）、各玩法系统在自身事务提交成功后的记录钩子、只读 UI 控制器/视图与 MainScene 最小接线——全部内容为玩家可见中文、摘要卡、单条混合时间线、分类标签，不触碰 `StateReducer`/`StateVersion`。

**Architecture:** `Hotel.Runtime` 新增纯 C# 静态服务 `PlayerLogManager`（`Assets/Scripts/Hotel/Runtime/State/` 下，无 MonoBehaviour、无场景实例）：记录方以 `PlayerLogWriteDto` 结构体调用 `Record(state, dto)`，读取方经 `Query(state)` 返回的 `IPlayerLogQuery` 接口读取（内部 `PlayerLogQuery` 对所有返回列表/条目做防御性克隆，UI 无法改写日志）。`GameRunState.PlayerLogs`（`List<PlayerLogEntry>`）随 `RunSaveCodec` 逐条克隆保存/还原，`SchemaVersion` 保持 1，Sequence 基于 `Count + 1` 跨存档延续。各协调器/管理器（Assembly-CSharp）只在各自 `CommitResult.Succeeded` 分支内调用 `Record`，事件结算经 `EventManager.TrySettleProcessedEvent` 单一成功汇点落「事件/特殊故事」卡并以 `TrySettle` 的 out 参数带回「效果汇总」卡；UI 控制器只经查询接口构建只读卡片视图。

**Tech Stack:** Unity 2022.3.62f3c1 LTS、C#、`Hotel.Runtime` 纯 C# 程序集（`noEngineReferences: false`，可用 `UnityEngine.Debug`）、UnityEngine.TestRunner（NUnit EditMode 测试，`Assets/Tests/Hotel.Runtime.Tests`，引用 `Hotel.Runtime` 与 `Hotel.Authoring`）、UGUI/TMP 既有 UI 模式（SO 事件通道 `Register/Unregister` + 序列化引用，仅只读数据绑定接缝，无新建布局）。

## Global Constraints

- **LogManager 接口边界（仅 DTO/查询接口）**：`PlayerLogManager` 对外只暴露 `PlayerLogWriteDto`（写入契约，readonly struct）与 `IPlayerLogQuery`（只读查询契约）；记录方只能以 DTO 调用 `PlayerLogManager.Record(state, dto)`，读取方只能使用 `PlayerLogManager.Query(state)` 返回的 `IPlayerLogQuery`。内部 `PlayerLogQuery` 对所有返回的列表与条目做防御性克隆，任何改写（清空列表、改条目字段）都不影响日志本体。
- **提交成功前不落玩法日志**：任何记录方只在 `CommitResult.Succeeded` 分支内调用 `Record`；提交失败不产生任何日志条目（含 `EventEffectManager.TrySettle` 的降级 resolve-only 路径——它不产出效果汇总）；不设待补记队列、不缓存、不重试。
- **PlayerLogs 永久存档/还原**：`PlayerLogs` 随 `GameRunState` 由 `RunSaveData`/`RunSaveCodec` 逐条克隆保存/还原（旧存档缺失该字段 → 空列表且不报错）；`SchemaVersion` 保持 1；新一局（新 `GameRunState`）从空日志开始；`Sequence` 自 1 起、跨存档加载延续（= `state.PlayerLogs.Count + 1`）。
- **不触碰 StateReducer/StateVersion**：日志**不**经 `RunChange`/`AuthorizedChangeSet`/`StateReducer` 提交，不参与任何校验、不递增 `StateVersion`、不影响重放与确定性；`PlayerLogs` 新增字段不进入任何 `RunChange` 校验。
- **仅混合时间线 + 分类标签 + 摘要卡**：事件/选择、特殊故事、效果汇总、Buff tick/到期、招募/拒绝、分配/移动、资源/食物与短缺、阶段推进全部汇入同一条时间线（`Sequence` 升序 = 追加顺序 = 玩法发生顺序）；每条记录带 `PlayerLogCategory` 标签；每条记录为「标题 + 摘要文本」摘要卡，不含内部变更明细。
- **UI 只读**：UI 仅经 `IPlayerLogQuery` 查询，从不写入；UI 展示形态（时间线按 Sequence 倒序、按 Day 分组、分类筛选、摘要卡 = 标题 + 摘要 + 分类标签 + 日/阶段）由 `PlayerLogPanelController` 提供只读数据 + Console 接缝，可视化面板布局（预制体、美术、样式）另行批准，本计划不做。
- **默认不进入 Play 模式**：除 Task 11（最终验证）外，本计划不以 Play 模式为验证手段；Task 11 的 Play 模式人工验证步骤**仅在用户明确要求时执行**；其余任务完成标准 = Unity 编译 0 错误 + EditMode 测试全过。
- **新增代码不加注释**：本计划所有新增/修改的代码不添加任何代码注释（与既有文件中的既有注释共存的新增代码同样不加）。
- **场景操作仅限 unitymaster**：`MainScene.unity` 只在 Task 10 由 unitymaster 修改，且仅限「`GameManager`（GameObject fileID 1918893930）新增 `PlayerLogPanelController` 组件 + 序列化引用接线」，禁止任何 UI 布局、美术、样式改动。
- **不做 git 提交**：所有任务以「评审门」步骤收尾，不执行任何 `git add`/`git commit`/`git push`（用户未要求提交）。
- **既有系统与既有 UI 不改动**：`EventManager`/`EventEffectManager`/`SettlementBridge`/`TenantReviewCoordinator`/`TenantAssignmentCoordinator` 仅新增记录调用点（不改既有行为、不改事件通道）；`PhaseUI`/`EventUI`/`NextPhasePanel`/`TenantReviewPanel`/`TenantAssignmentPanel`/`UIManager` 不改动。
- **程序集归属（无 Assembly-CSharp 类型泄漏）**：`PlayerLogCategory`/`PlayerLogEntry`/`PlayerLogWriteDto`/`IPlayerLogQuery`/`PlayerLogManager`/`PlayerLogQuery` 全部位于 `Hotel.Runtime`（`Assets/Scripts/Hotel/Runtime/State/`）；`EffectTarget` 保持在 `RunModel.cs` 原位置不迁移；Assembly-CSharp 层（协调器/管理器/UI 控制器）只消费这些类型，不反向定义或泄漏新运行时类型。
- **测试程序集现状**：`Assets/Tests/Hotel.Runtime.Tests/` 当前**不存在**（仓库根残留 `Hotel.Runtime.Tests.csproj` 为旧产物）；Task 1 先检查、若缺失则以 Unity 操作创建；若已存在（如其它计划已建且可运行），跳过创建直接复用。

---

## File Structure

| 文件 | 操作 | 职责 |
| --- | --- | --- |
| `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef` | 创建（若缺失） | EditMode 测试程序集（Editor-only，Test Assemblies，引用 Hotel.Runtime/Hotel.Authoring） |
| `Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs` | 创建 | 冒烟测试：证明测试程序集可编译可运行 |
| `Assets/Scripts/Hotel/Runtime/State/RunModel.cs` | 修改 | 新增 `PlayerLogCategory` 枚举（9 项）、`PlayerLogEntry` 类；`GameRunState` 新增 `PlayerLogs` 列表 |
| `Assets/Scripts/Hotel/Runtime/State/PlayerLogManager.cs` | 创建 | `PlayerLogWriteDto`/`IPlayerLogQuery`/`PlayerLogManager`（静态服务）/`PlayerLogQuery`（内部只读视图） |
| `Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs` | 修改 | `RunSaveData.PlayerLogs` 字段；`RunSaveCodec.CreateSnapshot` 逐条克隆、`RestoreSnapshot` 还原（缺失 → 空列表）；`SchemaVersion` 保持 1 |
| `Assets/Tests/Hotel.Runtime.Tests/PlayerLogModelTests.cs` | 创建 | T1 数据模型：枚举/条目/默认值/EffectTarget 归属 |
| `Assets/Tests/Hotel.Runtime.Tests/PlayerLogManagerTests.cs` | 创建 | T2/T3 记录行为：Sequence 自 1 起单调递增、空 state/空 Summary 防呆、每次调用恰好追加一条 |
| `Assets/Tests/Hotel.Runtime.Tests/PlayerLogQueryTests.cs` | 创建 | T2 查询：All/ByDay/ByCategory/Since/Get 过滤与排序、只读视图 |
| `Assets/Tests/Hotel.Runtime.Tests/PlayerLogSaveCodecTests.cs` | 创建 | T1 序列化往返、旧存档空列表、跨加载 Sequence 延续 |
| `Assets/Tests/Hotel.Runtime.Tests/PlayerLogNonIntrusionTests.cs` | 创建 | T4 无侵入：不改 StateVersion/租客/资源/Buff、日志量不影响任何校验 |
| `Assets/Scripts/Hotel/Managers/EventManager.cs` | 修改 | `TrySettleProcessedEvent`（394-402）成功汇点落事件/特殊故事卡；`RecordEventLog`/`ResolveOptionText` |
| `Assets/Scripts/Hotel/Managers/EventEffectManager.cs` | 修改 | `TrySettle`（11-62）签名加 `out PlayerLogWriteDto effectSummary`/`out bool committed`；完整提交成功（46 行）后产效果汇总卡；`TickBuffs`（64-124）提交成功（122-123 行）后按 buff 各记一条 |
| `Assets/Scripts/Hotel/Managers/SettlementBridge.cs` | 修改 | `OnPhaseEntered`（89-119）空引用检查后落阶段卡；`ExecuteFoodSettlement`（121-181）提交成功（152 行）后落食物结算卡（短缺并入同一卡） |
| `Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs` | 修改 | `OnConfirm`（255-297）成功（282-283 行）后落招募卡；`OnReject`（299-332）成功（320-321 行）后落拒绝卡 |
| `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs` | 修改 | `TryAssign`（144-179）成功（170 行）后落分配卡；`TryMoveToEmptyRoom`（181-220）成功（211 行）后落移动卡 |
| `Assets/Scripts/Hotel/UI/PlayerLogCardView.cs` | 创建 | `PlayerLogCardView`/`PlayerLogDayGroup` 只读卡片视图模型 |
| `Assets/Scripts/Hotel/UI/PlayerLogPanelController.cs` | 创建 | 只读 UI 控制器：`IPlayerLogQuery` 消费、分类筛选、Sequence 倒序 + Day 分组、Console 数据接缝 |
| `Assets/Scenes/MainScene.unity` | 修改（仅 unitymaster） | `GameManager`（fileID 1918893930）挂 `PlayerLogPanelController` 并接 `onPhaseEntered` |

## 设计决策（已定，供各任务对齐）

- **只读查询的防御性克隆**：`PlayerLogQuery` 的所有返回列表均为新 `List<PlayerLogEntry>`，`Get` 也返回克隆，UI/测试改写返回对象不影响 `state.PlayerLogs`。
- **排序约定**：`All()` 按 Sequence **升序**（时间线顺序，规格 §5）；`ByDay(int)` 按 Sequence **降序**（规格 §5 注明「降序」）；`ByCategory(...)` 按 Sequence **降序**（UI 最新在上，与 §6「UI 展示按 Sequence 降序」一致）；`Since(int)` 按 Sequence **升序**（增量轮询自然顺序）。
- **事件结算的幂等锚点**：`EventEffectManager.TrySettle` 新增 `out bool committed`，区分「成功提交（含降级 resolve-only）」与「无操作返回 Settled（payload 无效 / 事件已解决去重）」。`EventManager` **只在 `committed == true` 时**落事件/特殊故事卡与效果汇总卡，保证每起事件恰好记录一次（`OnEventProcessed`/`Update` 共用单一成功汇点）。
- **效果汇总卡的产出规则**：`TrySettle` 完整变更集提交成功后，仅当 `changes.Count > 0` 时构建 `EffectSettlement` 卡（`Summary = BuildEffectSummaryText(changes)`）；降级 resolve-only 路径与无效果路径产出 `default` DTO（`Summary == null`），`EventManager` 据此跳过。
- **Buff 卡产出规则**：`TickBuffs` 为每个发生 tick 的 buff 记录一条 `BuffTick` 卡；到期移除在同一卡内以「已到期移除」文案体现（不单列第二条）。
- **权威值来源**：`Record` 的 `Day`/`Phase` 一律由调用方给出（规格 §4）——事件/效果/Buff 用 `state.Day`/`state.Phase.Current`；阶段卡与食物卡用 `PhaseEnterData.day`/`ToHotelPhase(data.phase)`；招募/拒绝用协调器批次权威值 `_activeDay`/`_activePhase`；分配/移动用记录时 `_runState.Day`/`_runState.Phase.Current`。

---

### Task 1: 恢复/创建 Hotel.Runtime.Tests EditMode 测试程序集

**Files:**
- Create（若缺失）: `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`
- Create: `Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: 无（本任务为全部 NUnit 任务的前置）。
- Produces: 程序集 `Hotel.Runtime.Tests`（Editor-only，引用 `Hotel.Runtime` 与 `Hotel.Authoring`，`UNITY_INCLUDE_TESTS` 约束），供 Task 2–5 的测试文件落位与运行。

- [ ] **Step 1: 检查测试程序集现状**

1. Unity 中打开项目，Project 窗口确认 `Assets/Tests/` 与 `Assets/Tests/Hotel.Runtime.Tests/` 是否存在。
2. 打开 Window → General → Test Runner → EditMode。
Expected: Test Runner 中**没有** `Hotel.Runtime.Tests` 程序集（与研究结论一致：`Assets/Tests` 不存在，仓库根 `Hotel.Runtime.Tests.csproj` 为旧残留，不要手工删除）。若已存在同名程序集且可运行，跳过本任务其余步骤并报告「已存在」。

- [ ] **Step 2: 创建测试程序集定义**

Project 窗口右键 `Assets/` → Create → Folder，命名 `Tests`；再右键 `Assets/Tests` → Create → Folder，命名 `Hotel.Runtime.Tests`。创建 `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`，内容精确为：

```json
{
  "name": "Hotel.Runtime.Tests",
  "rootNamespace": "Hotel.Runtime.Tests",
  "references": [
    "Hotel.Runtime",
    "Hotel.Authoring"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

等待 Unity 导入。Expected: Console 无 asmdef 相关错误；Unity 在 `Assets/Tests/Hotel.Runtime.Tests/` 下生成 `Hotel.Runtime.Tests.csproj`。

- [ ] **Step 3: 编写冒烟测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class SmokeTests
    {
        [Test]
        public void Assembly_References_HotelRuntime()
        {
            GameRunState state = GameRunState.New(new RunId("smoke"), 1);
            Assert.AreEqual("smoke", state.RunId.Value);
        }
    }
}
```

- [ ] **Step 4: 运行冒烟测试**

Window → General → Test Runner → EditMode → 选中 `Hotel.Runtime.Tests` 程序集 → Run。
Expected: `SmokeTests` 1 项全部 PASS；Console 0 错误（测试程序集本任务即为绿灯，Task 2 起各任务才进入红灯）。

- [ ] **Step 5: 评审门**

将改动清单交给评审者复核：
- 改动文件仅限：`Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`、`Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs`（若 asmdef 已存在则仅 SmokeTests.cs）；
- 测试程序集在 Test Runner EditMode 可见、可运行，Console 0 asmdef 错误；
- 未触碰任何 `Assets/Scripts`、`Assets/Scenes` 文件；
- 通过标准：评审者确认后进入 Task 2；不通过则在本任务内修复后重新复核。

---

### Task 2: PlayerLogCategory / PlayerLogEntry / GameRunState.PlayerLogs（数据模型）

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/State/RunModel.cs`（`HotelPhase` 枚举后插入枚举；`ReviewDecisionRecord` 类后插入 `PlayerLogEntry`；`GameRunState` 末尾追加字段）
- Test: `Assets/Tests/Hotel.Runtime.Tests/PlayerLogModelTests.cs`

**Interfaces:**
- Consumes: `HotelPhase`（既有，RunModel.cs）。
- Produces:
  - `PlayerLogCategory : enum`（9 项，逐字：`EventChoice`/`SpecialStory`/`EffectSettlement`/`BuffTick`/`TenantRecruit`/`TenantReject`/`RoomAssignment`/`ResourceFood`/`PhaseTransition`）——Task 3/6/7/8/9 使用。
  - `PlayerLogEntry : class`（`[Serializable]`，字段逐字：`int Sequence`/`int Day`/`HotelPhase Phase`/`PlayerLogCategory Category`/`string Title`/`string Summary`/`string DetailKey`）——Task 3/4/6/7/8 使用。
  - `GameRunState.PlayerLogs : List<PlayerLogEntry>`（默认空列表）——Task 3/4 使用。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/PlayerLogModelTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class PlayerLogModelTests
    {
        [Test]
        public void All_Nine_Categories_Are_Defined()
        {
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.EventChoice));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.SpecialStory));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.EffectSettlement));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.BuffTick));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.TenantRecruit));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.TenantReject));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.RoomAssignment));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.ResourceFood));
            Assert.True(System.Enum.IsDefined(typeof(PlayerLogCategory), PlayerLogCategory.PhaseTransition));
            Assert.AreEqual(9, System.Enum.GetNames(typeof(PlayerLogCategory)).Length);
        }

        [Test]
        public void PlayerLogEntry_Carries_All_Fields()
        {
            var entry = new PlayerLogEntry
            {
                Sequence = 3,
                Day = 2,
                Phase = HotelPhase.Night,
                Category = PlayerLogCategory.BuffTick,
                Title = "Buff 结算",
                Summary = "b1：侵蚀 1 / 剩余 2 天",
                DetailKey = "b1"
            };
            Assert.AreEqual(3, entry.Sequence);
            Assert.AreEqual(2, entry.Day);
            Assert.AreEqual(HotelPhase.Night, entry.Phase);
            Assert.AreEqual(PlayerLogCategory.BuffTick, entry.Category);
            Assert.AreEqual("Buff 结算", entry.Title);
            Assert.AreEqual("b1：侵蚀 1 / 剩余 2 天", entry.Summary);
            Assert.AreEqual("b1", entry.DetailKey);
        }

        [Test]
        public void GameRunState_Defaults_PlayerLogs_Empty()
        {
            GameRunState state = GameRunState.New(new RunId("model"), 7);
            Assert.NotNull(state.PlayerLogs);
            Assert.AreEqual(0, state.PlayerLogs.Count);
        }

        [Test]
        public void EffectTarget_Remains_In_HotelRuntime()
        {
            Assert.AreEqual("Hotel.Runtime", typeof(EffectTarget).Assembly.GetName().Name);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `PlayerLogModelTests`。
Expected: 编译失败——`PlayerLogCategory`/`PlayerLogEntry` 不存在、`GameRunState.PlayerLogs` 不存在（CS0103/CS0117），即红灯。

- [ ] **Step 3: 最小实现**

`Assets/Scripts/Hotel/Runtime/State/RunModel.cs` 三处修改：

(a) `HotelPhase` 枚举（第 31-37 行）之后插入：

```csharp
    public enum PlayerLogCategory
    {
        EventChoice,
        SpecialStory,
        EffectSettlement,
        BuffTick,
        TenantRecruit,
        TenantReject,
        RoomAssignment,
        ResourceFood,
        PhaseTransition
    }
```

(b) `ReviewDecisionRecord` 类（第 111-119 行）之后插入：

```csharp
    [Serializable]
    public sealed class PlayerLogEntry
    {
        public int Sequence;
        public int Day;
        public HotelPhase Phase;
        public PlayerLogCategory Category;
        public string Title;
        public string Summary;
        public string DetailKey;
    }
```

(c) `GameRunState`（第 216-244 行）在 `public List<ReviewDecisionRecord> ReviewHistory ...` 之后追加：

```csharp
        public List<PlayerLogEntry> PlayerLogs = new List<PlayerLogEntry>();
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `PlayerLogModelTests`。
Expected: 4 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

聚焦 Unity，等待重编译。Console 0 错误、0 新增警告。全局搜索 `EffectTarget` 定义仍唯一位于 `Assets/Scripts/Hotel/Runtime/State/RunModel.cs`（未迁移）。

- [ ] **Step 6: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Runtime/State/RunModel.cs`、`Assets/Tests/Hotel.Runtime.Tests/PlayerLogModelTests.cs`；
- `PlayerLogModelTests` 4 项 PASS；Console 0 错误；
- 枚举成员顺序与规格 §3 逐字一致，`EffectTarget` 未被移动；
- 通过标准：评审者确认后进入 Task 3。

---

### Task 3: PlayerLogManager（Record/Query）+ 查询契约

**Files:**
- Create: `Assets/Scripts/Hotel/Runtime/State/PlayerLogManager.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/PlayerLogManagerTests.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/PlayerLogQueryTests.cs`

**Interfaces:**
- Consumes: `GameRunState.PlayerLogs`/`PlayerLogEntry`/`PlayerLogCategory`（Task 2）、`HotelPhase`（既有）。
- Produces:
  - `PlayerLogWriteDto : readonly struct`（字段逐字：`PlayerLogCategory Category`/`int Day`/`HotelPhase Phase`/`string Title`/`string Summary`/`string DetailKey`）——Task 6/7/8 使用。
  - `IPlayerLogQuery`（成员逐字：`int Count`/`IReadOnlyList<PlayerLogEntry> All()`/`ByDay(int day)`/`ByCategory(PlayerLogCategory category)`/`Since(int lastSeenSequence)`/`PlayerLogEntry Get(int sequence)`）——Task 6/7/8/9 使用。
  - `PlayerLogManager.Record(GameRunState state, PlayerLogWriteDto dto) : bool`——成功 true；state 为空 / Summary 为空 / 内部异常 → false，绝不抛出；内部 `Sequence = state.PlayerLogs.Count + 1` 后追加。
  - `PlayerLogManager.Query(GameRunState state) : IPlayerLogQuery`。
  - 内部 `PlayerLogQuery`（`internal sealed`）：所有返回为防御性克隆；排序遵循「设计决策」约定（All 升序、ByDay/ByCategory 降序、Since 升序、Get 返回克隆或 null）。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/PlayerLogManagerTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class PlayerLogManagerTests
    {
        private static GameRunState NewState()
        {
            return GameRunState.New(new RunId("logtest"), 7);
        }

        private static PlayerLogWriteDto Dto(string summary, PlayerLogCategory category = PlayerLogCategory.EventChoice)
        {
            return new PlayerLogWriteDto
            {
                Category = category,
                Day = 1,
                Phase = HotelPhase.Day,
                Title = "测试",
                Summary = summary,
                DetailKey = "evt_1"
            };
        }

        [Test]
        public void Record_First_Entry_Starts_At_Sequence_One()
        {
            GameRunState state = NewState();
            Assert.IsTrue(PlayerLogManager.Record(state, Dto("a")));
            IPlayerLogQuery query = PlayerLogManager.Query(state);
            Assert.AreEqual(1, query.Count);
            PlayerLogEntry entry = query.Get(1);
            Assert.NotNull(entry);
            Assert.AreEqual(1, entry.Sequence);
            Assert.AreEqual("a", entry.Summary);
        }

        [Test]
        public void Record_Sequence_Is_Monotonic_And_Consecutive()
        {
            GameRunState state = NewState();
            for (int i = 1; i <= 5; i++)
                Assert.IsTrue(PlayerLogManager.Record(state, Dto("log-" + i)));
            IPlayerLogQuery query = PlayerLogManager.Query(state);
            Assert.AreEqual(5, query.Count);
            Assert.AreEqual(5, query.Get(5).Sequence);
        }

        [Test]
        public void Record_NullState_Returns_False_Without_Throwing()
        {
            Assert.IsFalse(PlayerLogManager.Record(null, Dto("a")));
        }

        [Test]
        public void Record_EmptySummary_Is_Rejected()
        {
            GameRunState state = NewState();
            Assert.IsFalse(PlayerLogManager.Record(state, Dto("")));
            Assert.AreEqual(0, PlayerLogManager.Query(state).Count);
        }

        [Test]
        public void Record_Appends_Exactly_One_Entry_Per_Call()
        {
            GameRunState state = NewState();
            PlayerLogManager.Record(state, Dto("only"));
            Assert.AreEqual(1, PlayerLogManager.Query(state).Count);
            Assert.AreEqual(1, state.PlayerLogs.Count);
        }

        [Test]
        public void Record_Copies_Dto_Values_Into_Entry()
        {
            GameRunState state = NewState();
            var dto = new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.SpecialStory,
                Day = 4,
                Phase = HotelPhase.Night,
                Title = "特殊故事",
                Summary = "第 4 天 · 黑夜 开始",
                DetailKey = "evt_s"
            };
            Assert.IsTrue(PlayerLogManager.Record(state, dto));
            PlayerLogEntry entry = PlayerLogManager.Query(state).Get(1);
            Assert.AreEqual(PlayerLogCategory.SpecialStory, entry.Category);
            Assert.AreEqual(4, entry.Day);
            Assert.AreEqual(HotelPhase.Night, entry.Phase);
            Assert.AreEqual("特殊故事", entry.Title);
            Assert.AreEqual("evt_s", entry.DetailKey);
        }
    }
}
```

创建 `Assets/Tests/Hotel.Runtime.Tests/PlayerLogQueryTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class PlayerLogQueryTests
    {
        private static PlayerLogWriteDto Dto(string summary, PlayerLogCategory category, int day, HotelPhase phase)
        {
            return new PlayerLogWriteDto
            {
                Category = category,
                Day = day,
                Phase = phase,
                Title = "t",
                Summary = summary,
                DetailKey = null
            };
        }

        private static IPlayerLogQuery Build(params PlayerLogWriteDto[] dtos)
        {
            GameRunState state = GameRunState.New(new RunId("querytest"), 7);
            for (int i = 0; i < dtos.Length; i++)
                Assert.IsTrue(PlayerLogManager.Record(state, dtos[i]));
            return PlayerLogManager.Query(state);
        }

        [Test]
        public void All_Returns_Timeline_Order_Ascending()
        {
            IPlayerLogQuery query = Build(
                Dto("first", PlayerLogCategory.EventChoice, 1, HotelPhase.Day),
                Dto("second", PlayerLogCategory.TenantRecruit, 2, HotelPhase.Day),
                Dto("third", PlayerLogCategory.PhaseTransition, 3, HotelPhase.Night));
            var all = query.All();
            Assert.AreEqual(3, all.Count);
            Assert.AreEqual(1, all[0].Sequence);
            Assert.AreEqual(2, all[1].Sequence);
            Assert.AreEqual(3, all[2].Sequence);
        }

        [Test]
        public void ByDay_Filters_And_Returns_Descending()
        {
            IPlayerLogQuery query = Build(
                Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day),
                Dto("b", PlayerLogCategory.EventChoice, 2, HotelPhase.Day),
                Dto("c", PlayerLogCategory.EventChoice, 2, HotelPhase.Night));
            var day2 = query.ByDay(2);
            Assert.AreEqual(2, day2.Count);
            Assert.AreEqual("c", day2[0].Summary);
            Assert.AreEqual("b", day2[1].Summary);
        }

        [Test]
        public void ByCategory_Filters_And_Returns_Descending()
        {
            IPlayerLogQuery query = Build(
                Dto("e1", PlayerLogCategory.EventChoice, 1, HotelPhase.Day),
                Dto("recruit", PlayerLogCategory.TenantRecruit, 2, HotelPhase.Day),
                Dto("e2", PlayerLogCategory.EventChoice, 3, HotelPhase.Night));
            var events = query.ByCategory(PlayerLogCategory.EventChoice);
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual("e2", events[0].Summary);
            Assert.AreEqual("e1", events[1].Summary);
        }

        [Test]
        public void Since_Returns_Only_Newer_Entries_Ascending()
        {
            IPlayerLogQuery query = Build(
                Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day),
                Dto("b", PlayerLogCategory.EventChoice, 2, HotelPhase.Day),
                Dto("c", PlayerLogCategory.EventChoice, 3, HotelPhase.Night));
            var newer = query.Since(1);
            Assert.AreEqual(2, newer.Count);
            Assert.AreEqual(2, newer[0].Sequence);
            Assert.AreEqual(3, newer[1].Sequence);
        }

        [Test]
        public void Get_Returns_Entry_Or_Null()
        {
            IPlayerLogQuery query = Build(Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            Assert.NotNull(query.Get(1));
            Assert.IsNull(query.Get(99));
        }

        [Test]
        public void Mutating_Returned_List_Does_Not_Change_Log()
        {
            GameRunState state = GameRunState.New(new RunId("querytest"), 7);
            PlayerLogManager.Record(state, Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            IPlayerLogQuery query = PlayerLogManager.Query(state);
            var all = query.All() as List<PlayerLogEntry>;
            Assert.NotNull(all);
            all.Clear();
            Assert.AreEqual(1, query.Count);
        }

        [Test]
        public void Mutating_Returned_Entry_Does_Not_Change_Log()
        {
            GameRunState state = GameRunState.New(new RunId("querytest"), 7);
            PlayerLogManager.Record(state, Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            IPlayerLogQuery query = PlayerLogManager.Query(state);
            PlayerLogEntry copy = query.Get(1);
            copy.Title = "hacked";
            copy.Summary = "hacked";
            PlayerLogEntry stored = query.Get(1);
            Assert.AreEqual("t", stored.Title);
            Assert.AreEqual("a", stored.Summary);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `PlayerLogManagerTests` 与 `PlayerLogQueryTests`。
Expected: 编译失败（`PlayerLogWriteDto`/`IPlayerLogQuery`/`PlayerLogManager` 不存在），即红灯。

- [ ] **Step 3: 最小实现**

创建 `Assets/Scripts/Hotel/Runtime/State/PlayerLogManager.cs`：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotel.Runtime
{
    public readonly struct PlayerLogWriteDto
    {
        public PlayerLogCategory Category;
        public int Day;
        public HotelPhase Phase;
        public string Title;
        public string Summary;
        public string DetailKey;
    }

    public interface IPlayerLogQuery
    {
        int Count { get; }
        IReadOnlyList<PlayerLogEntry> All();
        IReadOnlyList<PlayerLogEntry> ByDay(int day);
        IReadOnlyList<PlayerLogEntry> ByCategory(PlayerLogCategory category);
        IReadOnlyList<PlayerLogEntry> Since(int lastSeenSequence);
        PlayerLogEntry Get(int sequence);
    }

    public static class PlayerLogManager
    {
        public static bool Record(GameRunState state, PlayerLogWriteDto dto)
        {
            if (state == null)
            {
                Debug.LogWarning("[PlayerLogManager] Record: state is null.");
                return false;
            }
            if (string.IsNullOrEmpty(dto.Summary))
            {
                Debug.LogWarning("[PlayerLogManager] Record: summary is empty; rejected.");
                return false;
            }
            try
            {
                if (state.PlayerLogs == null)
                    state.PlayerLogs = new List<PlayerLogEntry>();
                state.PlayerLogs.Add(new PlayerLogEntry
                {
                    Sequence = state.PlayerLogs.Count + 1,
                    Day = dto.Day,
                    Phase = dto.Phase,
                    Category = dto.Category,
                    Title = dto.Title ?? string.Empty,
                    Summary = dto.Summary,
                    DetailKey = dto.DetailKey
                });
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlayerLogManager] Record failed: {exception}");
                return false;
            }
        }

        public static IPlayerLogQuery Query(GameRunState state)
        {
            return new PlayerLogQuery(state);
        }
    }

    internal sealed class PlayerLogQuery : IPlayerLogQuery
    {
        private readonly GameRunState _state;

        public PlayerLogQuery(GameRunState state)
        {
            _state = state;
        }

        public int Count
        {
            get
            {
                if (_state == null || _state.PlayerLogs == null)
                    return 0;
                return _state.PlayerLogs.Count;
            }
        }

        public IReadOnlyList<PlayerLogEntry> All()
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
                result.Add(Clone(_state.PlayerLogs[i]));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> ByDay(int day)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Day == day)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> ByCategory(PlayerLogCategory category)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Category == category)
                    result.Add(Clone(entry));
            }
            result.Sort((a, b) => b.Sequence.CompareTo(a.Sequence));
            return result;
        }

        public IReadOnlyList<PlayerLogEntry> Since(int lastSeenSequence)
        {
            var result = new List<PlayerLogEntry>();
            if (_state == null || _state.PlayerLogs == null)
                return result;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Sequence > lastSeenSequence)
                    result.Add(Clone(entry));
            }
            return result;
        }

        public PlayerLogEntry Get(int sequence)
        {
            if (_state == null || _state.PlayerLogs == null)
                return null;
            for (int i = 0; i < _state.PlayerLogs.Count; i++)
            {
                PlayerLogEntry entry = _state.PlayerLogs[i];
                if (entry != null && entry.Sequence == sequence)
                    return Clone(entry);
            }
            return null;
        }

        private static PlayerLogEntry Clone(PlayerLogEntry entry)
        {
            if (entry == null)
                return null;
            return new PlayerLogEntry
            {
                Sequence = entry.Sequence,
                Day = entry.Day,
                Phase = entry.Phase,
                Category = entry.Category,
                Title = entry.Title,
                Summary = entry.Summary,
                DetailKey = entry.DetailKey
            };
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `PlayerLogManagerTests` 与 `PlayerLogQueryTests`。
Expected: 6 + 8 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。`PlayerLogQuery` 为 `internal`，测试程序集只能经 `IPlayerLogQuery` 使用——该约束即「接口边界」的编译期保证。

- [ ] **Step 6: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Runtime/State/PlayerLogManager.cs`、`Assets/Tests/Hotel.Runtime.Tests/PlayerLogManagerTests.cs`、`Assets/Tests/Hotel.Runtime.Tests/PlayerLogQueryTests.cs`；
- 14 项测试全部 PASS；Console 0 错误；
- `Record` 空 state/空 Summary 返回 false 不抛出；查询全部返回克隆；`All` 升序、`ByDay`/`ByCategory` 降序、`Since` 升序；
- 通过标准：评审者确认后进入 Task 4。

---

### Task 4: PlayerLogs 持久化（RunSaveData / RunSaveCodec）

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs`（`ReviewHistory` 字段后加 `PlayerLogs`；`CreateSnapshot` 逐条克隆追加；`RestoreSnapshot` 还原）
- Test: `Assets/Tests/Hotel.Runtime.Tests/PlayerLogSaveCodecTests.cs`

**Interfaces:**
- Consumes: `PlayerLogEntry`/`GameRunState.PlayerLogs`（Task 2）、`PlayerLogManager.Record`（Task 3）。
- Produces: `RunSaveData.PlayerLogs : List<PlayerLogEntry>`；`RunSaveCodec` 对 `PlayerLogs` 的逐条克隆保存/还原；`SchemaVersion` **保持 1**（`public const int CurrentSchemaVersion = 1;` 不动）——Task 5/11 验证无迁移行为。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/PlayerLogSaveCodecTests.cs`：

```csharp
using System;
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class PlayerLogSaveCodecTests
    {
        private static PlayerLogWriteDto Dto(string summary, PlayerLogCategory category, int day, HotelPhase phase)
        {
            return new PlayerLogWriteDto
            {
                Category = category,
                Day = day,
                Phase = phase,
                Title = "t",
                Summary = summary,
                DetailKey = "dk"
            };
        }

        [Test]
        public void RoundTrip_Preserves_All_Entry_Fields()
        {
            GameRunState state = GameRunState.New(new RunId("savetest"), 7);
            PlayerLogManager.Record(state, new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.SpecialStory,
                Day = 3,
                Phase = HotelPhase.Night,
                Title = "特殊故事",
                Summary = "第 3 天 · 黑夜 开始",
                DetailKey = "evt_special"
            });
            string json = RunSaveCodec.ToJson(state, DateTime.UtcNow);
            GameRunState restored = RunSaveCodec.FromJson(json);
            IPlayerLogQuery query = PlayerLogManager.Query(restored);
            Assert.AreEqual(1, query.Count);
            PlayerLogEntry entry = query.Get(1);
            Assert.NotNull(entry);
            Assert.AreEqual(1, entry.Sequence);
            Assert.AreEqual(3, entry.Day);
            Assert.AreEqual(HotelPhase.Night, entry.Phase);
            Assert.AreEqual(PlayerLogCategory.SpecialStory, entry.Category);
            Assert.AreEqual("特殊故事", entry.Title);
            Assert.AreEqual("第 3 天 · 黑夜 开始", entry.Summary);
            Assert.AreEqual("evt_special", entry.DetailKey);
        }

        [Test]
        public void RoundTrip_Preserves_Timeline_Order()
        {
            GameRunState state = GameRunState.New(new RunId("savetest"), 7);
            for (int i = 1; i <= 4; i++)
                PlayerLogManager.Record(state, Dto("log-" + i, PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            GameRunState restored = RunSaveCodec.FromJson(RunSaveCodec.ToJson(state, DateTime.UtcNow));
            var all = PlayerLogManager.Query(restored).All();
            Assert.AreEqual(4, all.Count);
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(i + 1, all[i].Sequence);
        }

        [Test]
        public void Legacy_Save_Without_PlayerLogs_Loads_As_Empty_List()
        {
            const string legacy = "{\"SchemaVersion\":1,\"RunId\":\"r\",\"StateVersion\":0,\"Day\":3,\"Seed\":7,\"Phase\":0,\"PhaseLifecycle\":0,\"PhaseOccurrence\":1}";
            GameRunState state = RunSaveCodec.FromJson(legacy);
            Assert.NotNull(state.PlayerLogs);
            Assert.AreEqual(0, state.PlayerLogs.Count);
            Assert.AreEqual(0, PlayerLogManager.Query(state).Count);
        }

        [Test]
        public void Sequence_Continues_After_Save_Load()
        {
            GameRunState state = GameRunState.New(new RunId("savetest"), 7);
            PlayerLogManager.Record(state, Dto("a", PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            PlayerLogManager.Record(state, Dto("b", PlayerLogCategory.EventChoice, 1, HotelPhase.Day));
            GameRunState restored = RunSaveCodec.FromJson(RunSaveCodec.ToJson(state, DateTime.UtcNow));
            Assert.IsTrue(PlayerLogManager.Record(restored, Dto("c", PlayerLogCategory.EventChoice, 2, HotelPhase.Day)));
            Assert.AreEqual(3, PlayerLogManager.Query(restored).Count);
            Assert.AreEqual(3, PlayerLogManager.Query(restored).Get(3).Sequence);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `PlayerLogSaveCodecTests`。
Expected: 断言失败——`RoundTrip_Preserves_All_Entry_Fields`/`RoundTrip_Preserves_Timeline_Order`/`Sequence_Continues_After_Save_Load` FAIL（还原后日志为空，因为 `RunSaveData` 尚无 `PlayerLogs` 字段），`Legacy_Save_Without_PlayerLogs_Loads_As_Empty_List` 通过，即红灯。

- [ ] **Step 3: 最小实现**

`Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs` 三处修改：

(a) `RunSaveData` 类第 30 行 `ReviewHistory` 之后追加：

```csharp
        public List<PlayerLogEntry> PlayerLogs = new List<PlayerLogEntry>();
```

(b) `CreateSnapshot`（第 64-99 行）中 `save.ReviewHistory.AddRange(state.ReviewHistory);`（第 83 行）之后追加：

```csharp
            foreach (var entry in state.PlayerLogs)
                save.PlayerLogs.Add(CloneLogEntry(entry));
```

(c) `RestoreSnapshot`（第 101-153 行）中 `state.ReviewHistory = save.ReviewHistory ?? new List<ReviewDecisionRecord>();`（第 114 行）之后追加：

```csharp
            state.PlayerLogs = new List<PlayerLogEntry>();
            if (save.PlayerLogs != null)
            {
                foreach (var entry in save.PlayerLogs)
                {
                    if (entry == null)
                        continue;
                    state.PlayerLogs.Add(CloneLogEntry(entry));
                }
            }
```

(d) 在 `CloneSummary` 方法之后（类末尾）追加克隆方法：

```csharp
        private static PlayerLogEntry CloneLogEntry(PlayerLogEntry value)
        {
            if (value == null)
                return null;
            return new PlayerLogEntry
            {
                Sequence = value.Sequence,
                Day = value.Day,
                Phase = value.Phase,
                Category = value.Category,
                Title = value.Title,
                Summary = value.Summary,
                DetailKey = value.DetailKey
            };
        }
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `PlayerLogSaveCodecTests`。
Expected: 4 项全部 PASS。确认 `RunSaveData.CurrentSchemaVersion` 仍为 `1`（未改动）。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

- [ ] **Step 6: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs`、`Assets/Tests/Hotel.Runtime.Tests/PlayerLogSaveCodecTests.cs`；
- 4 项测试全部 PASS；`SchemaVersion` 未改动（仍为 1）；`CreateSnapshot`/`RestoreSnapshot` 对 `PlayerLogs` 逐条克隆（列表元素不共享引用）；
- 通过标准：评审者确认后进入 Task 5。

---

### Task 5: 无侵入测试（T4）+ 运行时内核组评审门

**Files:**
- Test: `Assets/Tests/Hotel.Runtime.Tests/PlayerLogNonIntrusionTests.cs`

**Interfaces:**
- Consumes: `PlayerLogManager.Record`（Task 3）、`StateReducer`/`AuthorizedChangeSet`/`AdjustResourceChange`（既有）、`GameRunState`（既有 + Task 2）。
- Produces: 无新生产代码；验证「日志不触碰 `StateReducer`/`StateVersion`、不改玩法数值、日志量不影响任何校验」。

- [ ] **Step 1: 编写测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/PlayerLogNonIntrusionTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class PlayerLogNonIntrusionTests
    {
        private static GameRunState NewState()
        {
            var state = GameRunState.New(new RunId("nointrusion"), 7);
            state.Resources["food"] = new ResourceRunState { ResourceId = "food", DefinitionId = "food", Amount = 10 };
            state.Tenants["t1"] = new TenantRunState { TenantId = "t1", DefinitionId = "cand_1", RoomId = "room_01" };
            state.Buffs["b1"] = new BuffRunState { BuffId = "b1", RemainingTicks = 3, LastTickDay = 0 };
            return state;
        }

        private static PlayerLogWriteDto Dto()
        {
            return new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.PhaseTransition,
                Day = 1,
                Phase = HotelPhase.Day,
                Title = "t",
                Summary = "第 1 天 · 白天 开始"
            };
        }

        [Test]
        public void Record_Does_Not_Change_StateVersion()
        {
            GameRunState state = NewState();
            long before = state.StateVersion;
            for (int i = 0; i < 10; i++)
                PlayerLogManager.Record(state, Dto());
            Assert.AreEqual(before, state.StateVersion);
        }

        [Test]
        public void Record_Does_Not_Change_Tenants_Resources_Buffs()
        {
            GameRunState state = NewState();
            float erosionBefore = state.Tenants["t1"].TrueErosion;
            int foodBefore = state.Resources["food"].Amount;
            int buffTicksBefore = state.Buffs["b1"].RemainingTicks;
            PlayerLogManager.Record(state, Dto());
            Assert.AreEqual(erosionBefore, state.Tenants["t1"].TrueErosion);
            Assert.AreEqual(foodBefore, state.Resources["food"].Amount);
            Assert.AreEqual(buffTicksBefore, state.Buffs["b1"].RemainingTicks);
        }

        [Test]
        public void Log_Count_Does_Not_Affect_StateReducer_Commit()
        {
            GameRunState state = NewState();
            for (int i = 0; i < 25; i++)
                PlayerLogManager.Record(state, Dto());
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "Test", "AdjustFood");
            set.Add(new AdjustResourceChange("food", -3));
            Assert.IsTrue(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.AreEqual(7, state.Resources["food"].Amount);
            Assert.AreEqual(1, state.StateVersion);
        }

        [Test]
        public void State_Without_Logs_Commits_Normally()
        {
            GameRunState state = NewState();
            state.PlayerLogs = null;
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "Test", "AdjustFood");
            set.Add(new AdjustResourceChange("food", -1));
            Assert.IsTrue(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.IsNull(state.PlayerLogs);
        }
    }
}
```

- [ ] **Step 2: 运行测试**

Test Runner → EditMode → 运行 `PlayerLogNonIntrusionTests`。
Expected: 4 项全部 PASS（Task 3 的 `Record` 与既有 `StateReducer` 保证该性质；本任务固化回归）。

- [ ] **Step 3: 运行时内核组评审门**

- 改动文件仅限：`Assets/Tests/Hotel.Runtime.Tests/PlayerLogNonIntrusionTests.cs`；
- 全量 EditMode 测试（Smoke 1 + Model 4 + Manager 6 + Query 8 + SaveCodec 4 + NonIntrusion 4 = **27 项**）全部 PASS；Console 0 错误；
- 生产代码累计改动仅限：`RunModel.cs`、`PlayerLogManager.cs`、`RunSaveData.cs`（无其他生产文件）；
- `git status` 核对改动清单（只读核对，不做任何提交）；
- 通过标准：评审者确认后进入 Task 6（Assembly-CSharp 钩子）。

---

### Task 6: 事件/效果钩子（EventManager 成功汇点 + EventEffectManager.TrySettle 效果汇总）

**Files:**
- Modify: `Assets/Scripts/Hotel/Managers/EventManager.cs:394-402`（`TrySettleProcessedEvent` 整体替换 + 其后插入两个私有方法）
- Modify: `Assets/Scripts/Hotel/Managers/EventEffectManager.cs:11-62`（`TrySettle` 签名与成功分支；其后插入两个私有方法）
- Unity 验证：编译 + Play 人工（Assembly-CSharp，不属 NUnit 覆盖范围；Play 仅 Task 11 且用户明确要求时执行）

**Interfaces:**
- Consumes: `PlayerLogManager.Record`/`PlayerLogWriteDto`/`PlayerLogCategory`（Task 3）、`EventSettleResult`/`EventProcessedData`/`RunChange` 子类型（既有）、`ChoiceOption`/`EventKind`/`TriggerSpec.kind`（既有，EventConfig.cs）。
- Produces:
  - `EventEffectManager.TrySettle(GameRunState state, StateReducer reducer, EventProcessedData payload, out PlayerLogWriteDto effectSummary, out bool committed) : EventSettleResult`——完整提交成功置 `committed = true` 且 `effectSummary` = 效果汇总 DTO（`changes.Count > 0` 时，否则 `default`）；降级 resolve-only 成功置 `committed = true` 且 `effectSummary = default`；payload 无效 / 事件已解决去重返回 Settled 且 `committed = false`；失败返回 Pending 且 `committed = false`。
  - `EventManager.TrySettleProcessedEvent`：仅当 `committed == true` 时 `RecordEventLog`（事件/特殊故事卡），并在 `effectSummary.Summary != null` 时追加 `EffectSettlement` 卡；返回语义与既有完全一致（Settled → true）。
  - 事件卡分类规则（规格 §2）：`_currentConfig.trigger.kind ∈ {SpecialVisitor, Personal, ChainStep}` → `SpecialStory`，否则（当前管线仅 `Normal`）→ `EventChoice`。
  - `EffectSettlement` 卡文本 = `BuildEffectSummaryText(changes)`（对 `AdjustTenantErosionChange`/`AdjustResourceChange`/`AddBuffChange` 生成玩家可见中文，`；` 连接）。

- [ ] **Step 1: 修改 EventEffectManager.TrySettle**

`Assets/Scripts/Hotel/Managers/EventEffectManager.cs` 第 11 行签名改为：

```csharp
    public EventSettleResult TrySettle(GameRunState state, StateReducer reducer, EventProcessedData payload, out PlayerLogWriteDto effectSummary, out bool committed)
    {
        effectSummary = default;
        committed = false;
```

第 24-43 行 `set.Add(...)` 区域保持逻辑，仅把效果 DTO 预构建与提交成功分支替换。第 27 行 `EventEffect[] effects = payload.effects;` 之后的结构改为（`ownerTenantId`/`changes` 仍在 else 块内，`pendingEffectSummary` 提升到 if/else 之前）：

```csharp
        EventEffect[] effects = payload.effects;
        int effectCount = effects != null ? effects.Length : 0;
        PlayerLogWriteDto pendingEffectSummary = default;
        if (effectCount == 0)
        {
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: no effects to apply");
        }
        else
        {
            string ownerTenantId = payload.ownerTenantId;
            if (!string.IsNullOrEmpty(ownerTenantId) && !state.Tenants.ContainsKey(ownerTenantId))
                ownerTenantId = null;
            List<RunChange> changes = EventEffectExecutor.BuildChanges(
                effects, state, ownerTenantId, eventId, optionId, state.Day, RoomFloorRegistry.Instance);
            LogEffects(state, eventId, optionId, effects, changes, ownerTenantId);
            for (int i = 0; i < changes.Count; i++)
                set.Add(changes[i]);
            if (changes.Count > 0)
            {
                pendingEffectSummary = new PlayerLogWriteDto
                {
                    Category = PlayerLogCategory.EffectSettlement,
                    Day = state.Day,
                    Phase = state.Phase.Current,
                    Title = "效果结算",
                    Summary = BuildEffectSummaryText(changes),
                    DetailKey = eventId
                };
            }
        }

        CommitResult result = reducer.TryCommit(state, set);
        if (result.Succeeded)
        {
            committed = true;
            effectSummary = pendingEffectSummary;
            return EventSettleResult.Settled;
        }

        var resolveOnly = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "EventEffectManager", "ResolveEventHistoryOnly");
        resolveOnly.Add(new ResolveEventHistoryChange(eventId, optionId));
        CommitResult degraded = reducer.TryCommit(state, resolveOnly);
        if (degraded.Succeeded)
        {
            committed = true;
            return EventSettleResult.Settled;
        }

        string failureKey = $"{eventId}|{optionId}|{state.StateVersion}";
        if (_lastFailureKey != failureKey)
        {
            _lastFailureKey = failureKey;
            Debug.Log($"[EventEffectManager] event={eventId} option={optionId}: settle commit failed; pending retry");
        }
        return EventSettleResult.Pending;
    }
```

在 `LogEffects` 方法（第 149-191 行）之后、类末尾插入：

```csharp
    private static string BuildEffectSummaryText(List<RunChange> changes)
    {
        var parts = new List<string>();
        for (int i = 0; i < changes.Count; i++)
        {
            RunChange change = changes[i];
            if (change is AdjustTenantErosionChange erosion)
                parts.Add($"侵蚀 {erosion.Delta:+#;-#;0}");
            else if (change is AdjustResourceChange resource)
                parts.Add($"{ResourceName(resource.ResourceId)} {resource.Delta:+#;-#;0}");
            else if (change is AddBuffChange buff)
                parts.Add($"状态「{buff.Value.BuffId}」{buff.Value.RemainingTicks} 天");
        }
        return string.Join("；", parts);
    }

    private static string ResourceName(string resourceId)
    {
        switch (resourceId)
        {
            case "food": return "食物";
            case "currency": return "货币";
            case "ingredients": return "食材";
            case "resources": return "物资";
            case "medicine": return "药品";
            default: return resourceId;
        }
    }
```

- [ ] **Step 2: 修改 EventManager.TrySettleProcessedEvent**

`Assets/Scripts/Hotel/Managers/EventManager.cs` 第 394-402 行整个方法替换为：

```csharp
    private bool TrySettleProcessedEvent(EventProcessedData payload)
    {
        var bridge = SettlementBridge.Instance;
        if (bridge == null || bridge.RunState == null || bridge.Reducer == null)
            return false;
        if (_effectManager == null)
            return false;

        if (_effectManager.TrySettle(bridge.RunState, bridge.Reducer, payload, out PlayerLogWriteDto effectSummary, out bool committed) != EventSettleResult.Settled)
            return false;
        if (!committed)
            return true;

        RecordEventLog(bridge.RunState, payload);
        if (effectSummary.Summary != null)
            PlayerLogManager.Record(bridge.RunState, effectSummary);
        return true;
    }
```

在 `TrySettleProcessedEvent` 之后插入两个私有方法：

```csharp
    private void RecordEventLog(GameRunState state, EventProcessedData payload)
    {
        if (_currentConfig == null)
            return;

        EventKind kind = _currentConfig.trigger != null ? _currentConfig.trigger.kind : EventKind.Normal;
        PlayerLogCategory category = kind == EventKind.Normal
            ? PlayerLogCategory.EventChoice
            : PlayerLogCategory.SpecialStory;
        string optionText = ResolveOptionText(payload.optionId);

        PlayerLogManager.Record(state, new PlayerLogWriteDto
        {
            Category = category,
            Day = state.Day,
            Phase = state.Phase.Current,
            Title = _currentConfig.eventTitle,
            Summary = $"选择『{optionText}』",
            DetailKey = _currentConfig.eventId
        });
    }

    private string ResolveOptionText(string optionId)
    {
        if (string.IsNullOrEmpty(optionId))
            return "确认";
        if (_currentConfig == null)
            return optionId;
        for (int i = 0; i < _currentConfig.choices.Count; i++)
        {
            ChoiceOption choice = _currentConfig.choices[i];
            if (choice != null && choice.choiceId == optionId && !string.IsNullOrEmpty(choice.choiceText))
                return choice.choiceText;
        }
        return optionId;
    }
```

- [ ] **Step 3: Unity 编译验证**

聚焦 Unity，等待重编译。Console 0 错误、0 新增警告。静态核对：`_effectManager.TrySettle(` 全文唯一调用点为 `EventManager.cs:401`（已确认）；`EventSettleResult` 返回语义对 `OnEventProcessed`（第 334 行）/`Update`（第 354 行）调用方逐字节不变。

- [ ] **Step 4: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Managers/EventManager.cs`、`Assets/Scripts/Hotel/Managers/EventEffectManager.cs`；
- Console 0 错误；既有 `LogEffects` 调试日志与降级 resolve-only 路径行为不变；
- `Record` 只出现在 `committed == true` 分支（EventManager 侧）与 `TrySettle` 成功分支内部（无记录发生在 EventEffectManager 侧）；
- 通过标准：评审者确认后进入 Task 7。

---

### Task 7: Buff 与阶段/食物结算钩子（EventEffectManager.TickBuffs + SettlementBridge）

**Files:**
- Modify: `Assets/Scripts/Hotel/Managers/EventEffectManager.cs:64-124`（`TickBuffs`）
- Modify: `Assets/Scripts/Hotel/Managers/SettlementBridge.cs:89-119`（`OnPhaseEntered` 插入阶段卡）、`:121-181`（`ExecuteFoodSettlement` 签名与成功分支）、`:101`（调用点）
- Unity 验证：编译 + Play 人工（Assembly-CSharp；Play 仅 Task 11 且用户明确要求时执行）

**Interfaces:**
- Consumes: `PlayerLogManager.Record`/`PlayerLogWriteDto`/`PlayerLogCategory`（Task 3）、`PhaseEnterData`/`GamePhase`（既有）、`RoomFloorRegistry`（既有）。
- Produces:
  - `TickBuffs` 提交成功后为每个发生 tick 的 buff 记录一条 `BuffTick` 卡（`Day = state.Day`/`Phase = state.Phase.Current`）；到期移除以同一卡「已到期移除」文案体现；`SameRoomOtherTenants` 跳过路径与 `changes.Count == 0` 路径不记录。
  - `OnPhaseEntered` 空引用检查后（第 95 行后）、食物结算（第 97-106 行）与 `TickBuffs`（第 113-114 行）**之前**，以载荷 `data.day`/`ToHotelPhase(data.phase)` 为权威值记录 `PhaseTransition` 卡。
  - `ExecuteFoodSettlement(int day, HotelPhase phase)` 提交成功后记录 `ResourceFood` 卡；`countTenants == 0` 提前返回路径不记录；短缺 > 0 时「短缺 N」并入同一卡文案。

- [ ] **Step 1: 修改 TickBuffs**

`Assets/Scripts/Hotel/Managers/EventEffectManager.cs` 第 64-124 行：

(a) 第 72 行 `var expired = new List<string>();` 之后追加：

```csharp
        var pendingBuffs = new List<PlayerLogWriteDto>();
```

(b) 主循环内第 103-104 行（`if (buff.RemainingTicks > 0 && newRemaining == 0) expired.Add(buff.BuffId);`）之后、第 106 行 `string targetsText ...` 之前插入：

```csharp
            bool willExpire = buff.RemainingTicks > 0 && newRemaining == 0;
            pendingBuffs.Add(new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.BuffTick,
                Day = state.Day,
                Phase = state.Phase.Current,
                Title = "Buff 结算",
                Summary = willExpire
                    ? $"{buff.BuffId}：已到期移除"
                    : $"{buff.BuffId}：侵蚀 {buff.ErosionPerTick:0.##} / 剩余 {newRemaining} 天",
                DetailKey = buff.BuffId
            });
```

(c) 第 122-123 行提交结果处理改为：

```csharp
        CommitResult result = reducer.TryCommit(state, set);
        if (result.Succeeded)
        {
            for (int i = 0; i < pendingBuffs.Count; i++)
                PlayerLogManager.Record(state, pendingBuffs[i]);
        }
        return result.Succeeded;
```

- [ ] **Step 2: 修改 SettlementBridge.OnPhaseEntered**

`Assets/Scripts/Hotel/Managers/SettlementBridge.cs` 第 95 行（空引用检查的 `}`）之后、第 97 行 `bool crossedNewDayBoundary = ...` 之前插入：

```csharp
        PlayerLogManager.Record(_runState, new PlayerLogWriteDto
        {
            Category = PlayerLogCategory.PhaseTransition,
            Day = data.day,
            Phase = ToHotelPhase(data.phase),
            Title = "阶段推进",
            Summary = $"第 {data.day} 天 · {PhaseName(data.phase)} 开始",
            DetailKey = null
        });
```

并在 `ToHotelPhase` 方法之后新增：

```csharp
    private static string PhaseName(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Dawn: return "黎明";
            case GamePhase.Dusk: return "黄昏";
            case GamePhase.Night: return "黑夜";
            default: return "白天";
        }
    }
```

- [ ] **Step 3: 修改 ExecuteFoodSettlement**

(a) 第 101 行调用点改为：

```csharp
            if (ExecuteFoodSettlement(data.day, ToHotelPhase(data.phase)))
```

(b) 第 121 行签名改为：

```csharp
    private bool ExecuteFoodSettlement(int day, HotelPhase phase)
```

(c) 第 152 行 `if (result.Succeeded)` 块内、第 154 行 `if (onResourceAdjusted != null ...)` 之前插入：

```csharp
            PlayerLogManager.Record(_runState, new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.ResourceFood,
                Day = day,
                Phase = phase,
                Title = "食物结算",
                Summary = shortage > 0
                    ? $"第 {day} 天食物结算：消耗 {consumed}、短缺 {shortage}"
                    : $"第 {day} 天食物结算：消耗 {consumed}",
                DetailKey = "food"
            });
```

- [ ] **Step 4: Unity 编译验证**

Console 0 错误、0 新增警告。静态核对：`ExecuteFoodSettlement(` 唯一调用点已同步（第 101 行）；`TickBuffs(` 唯一调用点 `SettlementBridge.cs:114` 无需改动；`OnPhaseEntered` 内 `_runState.Day = data.day`（第 108 行）与 `_runState.Phase.Current = ...`（第 109 行）赋值仍在阶段卡记录之后，阶段卡使用载荷权威值不受影响。

- [ ] **Step 5: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Managers/EventEffectManager.cs`、`Assets/Scripts/Hotel/Managers/SettlementBridge.cs`；
- Console 0 错误；`TickBuffs` 返回语义（提交成功 true / 失败 false）与 `OnPhaseEntered` 对 `SaveGameService.TrySave` 的调用时序不变；
- 阶段卡在食物结算与 `TickBuffs` 之前落卡，食物卡只在提交成功后落卡，`countTenants == 0` 与提交失败不产生记录；
- 通过标准：评审者确认后进入 Task 8。

---

### Task 8: 租客评审/房间分配钩子（TenantReviewCoordinator + TenantAssignmentCoordinator）

**Files:**
- Modify: `Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs:255-297`（`OnConfirm`）、`:299-332`（`OnReject`）
- Modify: `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs:144-179`（`TryAssign`）、`:181-220`（`TryMoveToEmptyRoom`）
- Unity 验证：编译 + Play 人工（Assembly-CSharp；Play 仅 Task 11 且用户明确要求时执行）

**Interfaces:**
- Consumes: `PlayerLogManager.Record`/`PlayerLogWriteDto`/`PlayerLogCategory`（Task 3）、`_activeDay`/`_activePhase`（既有）、`_displayLookup`/`TenantAssignmentItemView.DisplayName`（既有）。
- Produces:
  - `OnConfirm` 提交成功（282-283 行）后记录 `TenantRecruit` 卡：`Day = _activeDay`/`Phase = _activePhase`，`Summary = $"招募 {candidate.displayName}（初始侵蚀 {initialErosion:0.##}）"`，`DetailKey = candidate.candidateId`。
  - `OnReject` 提交成功（320-321 行）后记录 `TenantReject` 卡：`Summary = $"拒绝 {candidate.displayName}"`，`DetailKey = candidate.candidateId`。
  - `TryAssign` 提交成功（170 行）后记录 `RoomAssignment` 卡：`Day = _runState.Day`/`Phase = _runState.Phase.Current`，`Summary = $"{displayName} → {roomId}"`，`DetailKey = tenantId`。
  - `TryMoveToEmptyRoom` 提交成功（211 行）后记录 `RoomAssignment` 卡：`Summary = $"{displayName} → {targetRoomId}"`（Title =「房间移动」）。
  - 两个协调器各新增一个私有显示名解析（招募用 `candidate.displayName`；分配用 `_displayLookup`，缺失回退 `tenantId`）。

- [ ] **Step 1: 修改 TenantReviewCoordinator**

`Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs`：

(a) `OnConfirm` 第 283 行 `if (result.Succeeded)` 块内、第 285 行 `if (TenantAssignmentCoordinator.Instance != null)` 之前插入：

```csharp
            PlayerLogManager.Record(_runState, new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.TenantRecruit,
                Day = _activeDay,
                Phase = _activePhase,
                Title = "租客招募",
                Summary = $"招募 {candidate.displayName}（初始侵蚀 {initialErosion:0.##}）",
                DetailKey = candidate.candidateId
            });
```

(b) `OnReject` 第 321 行 `if (result.Succeeded)` 块内、第 323 行 `Debug.Log(...)` 之前插入：

```csharp
            PlayerLogManager.Record(_runState, new PlayerLogWriteDto
            {
                Category = PlayerLogCategory.TenantReject,
                Day = _activeDay,
                Phase = _activePhase,
                Title = "租客拒绝",
                Summary = $"拒绝 {candidate.displayName}",
                DetailKey = candidate.candidateId
            });
```

- [ ] **Step 2: 修改 TenantAssignmentCoordinator**

`Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`：

(a) `TryAssign` 第 170 行 `if (result.Succeeded)` 块内首行插入：

```csharp
            RecordRoomAssignment(tenantId, roomId);
```

(b) `TryMoveToEmptyRoom` 第 211 行 `if (result.Succeeded)` 块内首行插入：

```csharp
            RecordRoomMove(tenantId, targetRoomId);
```

(c) 在类末尾（`GetRoomOccupantId` 方法之后）插入三个私有方法：

```csharp
    private void RecordRoomAssignment(string tenantId, string roomId)
    {
        PlayerLogManager.Record(_runState, new PlayerLogWriteDto
        {
            Category = PlayerLogCategory.RoomAssignment,
            Day = _runState.Day,
            Phase = _runState.Phase.Current,
            Title = "房间分配",
            Summary = $"{TenantDisplayName(tenantId)} → {roomId}",
            DetailKey = tenantId
        });
    }

    private void RecordRoomMove(string tenantId, string targetRoomId)
    {
        PlayerLogManager.Record(_runState, new PlayerLogWriteDto
        {
            Category = PlayerLogCategory.RoomAssignment,
            Day = _runState.Day,
            Phase = _runState.Phase.Current,
            Title = "房间移动",
            Summary = $"{TenantDisplayName(tenantId)} → {targetRoomId}",
            DetailKey = tenantId
        });
    }

    private string TenantDisplayName(string tenantId)
    {
        if (_displayLookup.TryGetValue(tenantId, out TenantAssignmentItemView view))
            return view.DisplayName;
        return tenantId;
    }
```

- [ ] **Step 3: Unity 编译验证**

Console 0 错误、0 新增警告。静态核对：四个提交成功分支内各恰好一处 `PlayerLogManager.Record`；`OnConfirm`/`OnReject` 的 `AdvanceBatch()` 调用时序（第 295-296/330-331 行）不变；`TryAssign`/`TryMoveToEmptyRoom` 的 `return result.Succeeded`（第 178/219 行）不变。

- [ ] **Step 4: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs`、`Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`；
- Console 0 错误；提交失败（else 分支 `Debug.LogError`）路径无任何记录；
- 未改动协调器的既有行为、事件/UI 通道与 `HasRecruitmentCapacity`/`IsRoomOccupied` 等判定；
- 通过标准：评审者确认后进入 Task 9。

---

### Task 9: 只读 UI 控制器/视图（PlayerLogPanelController + PlayerLogCardView）

**Files:**
- Create: `Assets/Scripts/Hotel/UI/PlayerLogCardView.cs`
- Create: `Assets/Scripts/Hotel/UI/PlayerLogPanelController.cs`
- Unity 验证：编译 + Play 人工（Assembly-CSharp；Play 仅 Task 11 且用户明确要求时执行）

**Interfaces:**
- Consumes: `PlayerLogManager.Query`/`IPlayerLogQuery`/`PlayerLogCategory`/`PlayerLogEntry`/`HotelPhase`（Hotel.Runtime）、`PhaseEnteredEvent`/`PhaseEnterData`（既有）、`SettlementBridge.Instance.RunState`（既有）。
- Produces:
  - `PlayerLogCardView`（public sealed class，字段：`int Sequence`/`int Day`/`string PhaseText`/`PlayerLogCategory Category`/`string Title`/`string Summary`）——可视化布局阶段的卡片数据源。
  - `PlayerLogDayGroup`（public sealed class，字段：`int Day` + `List<PlayerLogCardView> Cards`）——按 Day 分组的数据源。
  - `PlayerLogPanelController : MonoBehaviour`：序列化字段 `onPhaseEntered : PhaseEnteredEvent`；只读属性 `VisibleCards : IReadOnlyList<PlayerLogCardView>`（Sequence 倒序 = 最新在上）；`SetCategoryFilter(PlayerLogCategory, bool)`/`ClearCategoryFilter()`；`RefreshTimeline()`（重建卡片 + Console 输出）；`BuildDayGroups() : List<PlayerLogDayGroup>`；`Update()` 每帧经 `Since(_lastSeenSequence)` 增量轮询。控制器**只读**，不写入日志。

- [ ] **Step 1: 创建 PlayerLogCardView**

创建 `Assets/Scripts/Hotel/UI/PlayerLogCardView.cs`：

```csharp
using System.Collections.Generic;
using Hotel.Runtime;

public sealed class PlayerLogCardView
{
    public int Sequence;
    public int Day;
    public string PhaseText;
    public PlayerLogCategory Category;
    public string Title;
    public string Summary;
}

public sealed class PlayerLogDayGroup
{
    public int Day;
    public readonly List<PlayerLogCardView> Cards = new List<PlayerLogCardView>();
}
```

- [ ] **Step 2: 创建 PlayerLogPanelController**

创建 `Assets/Scripts/Hotel/UI/PlayerLogPanelController.cs`：

```csharp
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

public class PlayerLogPanelController : MonoBehaviour
{
    [Header("Event Listener")]
    public PhaseEnteredEvent onPhaseEntered;

    private readonly HashSet<PlayerLogCategory> _categoryFilter = new HashSet<PlayerLogCategory>();
    private readonly List<PlayerLogCardView> _visibleCards = new List<PlayerLogCardView>();
    private IPlayerLogQuery _query;
    private int _lastSeenSequence;
    private int _lastRefreshedDay = int.MinValue;
    private HotelPhase _lastRefreshedPhase = HotelPhase.Dawn;

    public IReadOnlyList<PlayerLogCardView> VisibleCards => _visibleCards;

    private void OnEnable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void Start()
    {
        RefreshTimeline();
    }

    private void Update()
    {
        PollIncremental();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        RefreshTimeline();
    }

    public void SetCategoryFilter(PlayerLogCategory category, bool active)
    {
        if (active)
            _categoryFilter.Add(category);
        else
            _categoryFilter.Remove(category);
        RefreshTimeline();
    }

    public void ClearCategoryFilter()
    {
        _categoryFilter.Clear();
        RefreshTimeline();
    }

    public void RefreshTimeline()
    {
        _query = PlayerLogManager.Query(SettlementBridge.Instance != null ? SettlementBridge.Instance.RunState : null);
        _visibleCards.Clear();

        if (_query != null)
        {
            var all = _query.All();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                PlayerLogEntry entry = all[i];
                if (!MatchesFilter(entry.Category))
                    continue;
                _visibleCards.Add(new PlayerLogCardView
                {
                    Sequence = entry.Sequence,
                    Day = entry.Day,
                    PhaseText = PhaseText(entry.Phase),
                    Category = entry.Category,
                    Title = entry.Title,
                    Summary = entry.Summary
                });
            }
        }

        if (_visibleCards.Count > 0)
            _lastSeenSequence = _visibleCards[0].Sequence;

        if (SettlementBridge.Instance != null && SettlementBridge.Instance.RunState != null)
        {
            _lastRefreshedDay = SettlementBridge.Instance.RunState.Day;
            _lastRefreshedPhase = SettlementBridge.Instance.RunState.Phase.Current;
        }

        LogVisibleCards();
    }

    public List<PlayerLogDayGroup> BuildDayGroups()
    {
        var groups = new List<PlayerLogDayGroup>();
        PlayerLogDayGroup current = null;
        for (int i = 0; i < _visibleCards.Count; i++)
        {
            PlayerLogCardView card = _visibleCards[i];
            if (current == null || current.Day != card.Day)
            {
                current = new PlayerLogDayGroup { Day = card.Day };
                groups.Add(current);
            }
            current.Cards.Add(card);
        }
        return groups;
    }

    private void PollIncremental()
    {
        if (_query == null)
            return;
        IReadOnlyList<PlayerLogEntry> newer = _query.Since(_lastSeenSequence);
        if (newer == null || newer.Count == 0)
            return;
        _lastSeenSequence = newer[newer.Count - 1].Sequence;
        RefreshTimeline();
    }

    private bool MatchesFilter(PlayerLogCategory category)
    {
        return _categoryFilter.Count == 0 || _categoryFilter.Contains(category);
    }

    private void LogVisibleCards()
    {
        var parts = new List<string>(_visibleCards.Count);
        for (int i = 0; i < _visibleCards.Count; i++)
        {
            PlayerLogCardView card = _visibleCards[i];
            parts.Add($"#{card.Sequence}[{card.Category}]D{card.Day}{card.PhaseText}「{card.Title}」{card.Summary}");
        }
        Debug.Log($"[PlayerLogUI] day={_lastRefreshedDay} phase={_lastRefreshedPhase} cards={_visibleCards.Count} " + string.Join(" | ", parts));
    }

    private static string PhaseText(HotelPhase phase)
    {
        switch (phase)
        {
            case HotelPhase.Dawn: return "黎明";
            case HotelPhase.Day: return "白天";
            case HotelPhase.Dusk: return "黄昏";
            default: return "黑夜";
        }
    }
}
```

- [ ] **Step 3: Unity 编译验证**

Console 0 错误、0 新增警告。静态核对：控制器内无任何对 `state.PlayerLogs` 的直接写操作（全部经 `IPlayerLogQuery` 只读接口）；`using Hotel.Runtime;` 已覆盖 `PlayerLogCategory`/`HotelPhase` 等类型，无 Assembly-CSharp 泄漏。

- [ ] **Step 4: 评审门**

- 改动文件仅限：`Assets/Scripts/Hotel/UI/PlayerLogCardView.cs`、`Assets/Scripts/Hotel/UI/PlayerLogPanelController.cs`；
- Console 0 错误；控制器为纯只读（无 `Record` 调用、无 `PlayerLogs` 写入）；
- 未改动任何既有 UI 文件（PhaseUI/EventUI/TenantReviewPanel/TenantAssignmentPanel/UIManager 等只读）；
- 通过标准：评审者确认后进入 Task 10。

---

### Task 10: MainScene 接线（unitymaster）

**Files:**
- Modify（仅 unitymaster）: `Assets/Scenes/MainScene.unity`（GameManager GameObject fileID 1918893930 新增组件 + 序列化引用）

**Interfaces:**
- Consumes: `PlayerLogPanelController`（Task 9）、`Assets/Data/Events/PhaseEnteredEvent.asset`（既有，guid `cb56d0eb6bffd7f4fa67bee60451ce51`）。
- Produces: 运行时场景内 `PlayerLogPanelController` 实例，`onPhaseEntered` 指向既有 PhaseEnteredEvent 资产，使阶段推进时自动刷新时间线；`Update` 轮询 `Since` 提供增量刷新。

- [ ] **Step 1: 挂载组件并接线（unitymaster 编辑器操作）**

1. 打开 `Assets/Scenes/MainScene.unity`。
2. Hierarchy 选中 `GameManager`（GameObject fileID 1918893930）。
3. Add Component → `PlayerLogPanelController`。
4. Inspector 中把 `On Phase Entered` 设为 `Assets/Data/Events/PhaseEnteredEvent.asset`（与 `GamePhaseManager`/`EventManager`/`SettlementBridge` 组件 `onPhaseEntered` 同一资产，序列化引用 `{fileID: 11400000, guid: cb56d0eb6bffd7f4fa67bee60451ce51, type: 2}`）。
5. 保存场景（Ctrl+S）。

Expected: `&1918893930` 序列化块的组件列表新增 `PlayerLogPanelController` 一项，其 `onPhaseEntered` 指向上述 guid；Console 0 错误；除该组件块外场景无任何改动（无新 GameObject、无 Canvas、无 UI 布局）。

- [ ] **Step 2: Unity 编译验证**

聚焦 Unity，等待重编译。Console 0 错误、0 新增警告、无 MissingScript。

- [ ] **Step 3: 评审门**

- 改动文件仅限：`Assets/Scenes/MainScene.unity`（仅 `&1918893930` 组件块）；
- 由 unitymaster 确认：未做任何 UI 布局/美术/样式改动、未新建 Prefab、未触碰既有组件序列化字段；
- 通过标准：评审者确认后进入 Task 11。

---

### Task 11: 全量编译 + EditMode 测试 + （用户明确要求时）Play 人工验证

**Files:**
- 只读验证，不修改任何文件。

**Interfaces:**
- Consumes: Task 1–10 全部成果；`SaveGameService.SavePath`（`Application.persistentDataPath/hotel-save-slot-1.json`）、`GameLaunchContext`（既有）。

- [ ] **Step 1: 全量 Unity 编译验证**

聚焦 Unity，等待全量重编译。
Expected: Console 0 错误、0 新增警告；无 MissingReference/MissingScript；`git status` 显示的改动仅限本计划 File Structure 列出的文件（只读核对，不做任何提交）。

- [ ] **Step 2: 全量 EditMode 测试**

Window → General → Test Runner → EditMode → Run All。
Expected: `Hotel.Runtime.Tests` 程序集 **27 项全部 PASS**（Smoke 1 + PlayerLogModel 4 + PlayerLogManager 6 + PlayerLogQuery 8 + PlayerLogSaveCodec 4 + PlayerLogNonIntrusion 4）。

可选命令行替代（若编辑器未打开；路径以本机 Unity 安装为准，本机 ARCHITECTURE.md 声明 2022.3.62f3c1）：

```powershell
& "C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe" -batchmode -nographics -projectPath "E:\UnityProjects\260725GJ" -runTests -testPlatform EditMode -testResults "C:\Users\31087\AppData\Local\Temp\opencode\player-log-editmode.xml" -logFile "C:\Users\31087\AppData\Local\Temp\opencode\player-log-editmode.log"
```

Expected: 退出码 0，`player-log-editmode.xml` 中 `result="Passed"` 且 `failed="0"`。

- [ ] **Step 3: Play 模式人工验证（仅当用户明确要求时执行；默认跳过）**

> 门条件：用户明确要求 Play 模式验证才执行本步骤；未要求则直接进入 Step 4。

1. 打开 `MainScene`，进入 Play（新局）。
2. 观察 Console 中 `[PlayerLogUI]` 输出与各钩子记录：
   - 每次阶段进入（含隐藏阶段跳过路径的 Dawn/Day/Dusk/Night）出现 `PhaseTransition` 卡（`第 N 天 · 黎明/白天/黄昏/黑夜 开始`）；
   - 跨夜食物结算出现 `ResourceFood` 卡（有租客时 `第 N 天食物结算：消耗 X、短缺 Y`，无租客的提前返回不产生）；
   - Dawn 阶段有 buff 时出现 `BuffTick` 卡；招募/拒绝出现 `TenantRecruit`/`TenantReject` 卡；拖拽分配/移动出现 `RoomAssignment` 卡；
   - 触发任意事件（确认/选择）后出现 `EventChoice` 卡（`选择『…』`），若该事件带有效效果再出现一条 `EffectSettlement` 卡；
   - 上述每类在同一事务中**恰好一条**；时间线卡 `#Sequence` 严格连续、跨类别混合排序。
3. 存档验证：Dawn 自动存档后打开 `Application.persistentDataPath/hotel-save-slot-1.json`，确认存在 `"PlayerLogs": [...]` 且 `"SchemaVersion": 1`。
4. 继续游戏（从主菜单「继续」）验证：载入后 `[PlayerLogUI]` 显示既有时间线，新推进产生的 Sequence 在既有最大序号后延续；无重复卡。
5. 退出 Play。Expected: 全程无报错、无 `MissingReferenceException`；既有玩法（评审/分配/事件/结算）行为与改动前一致。

- [ ] **Step 4: 最终评审门**

- 改动文件仅限本计划 File Structure 列出的全部 18 个文件（含 5 个 EditMode 测试文件、3 个 `Hotel.Runtime` 运行时文件、5 个既有管理器文件、2 个 UI 文件、1 个场景文件、2 个测试基础设施文件）；
- Console 0 错误；EditMode 27 项全部 PASS；
- 复查 Global Constraints 逐条成立：接口边界、提交成功前不落日志、永久存档、不触碰 StateReducer/StateVersion、仅混合时间线/标签/摘要卡、UI 只读、默认不进入 Play 模式、新增代码无注释、场景改动仅限 Task 10、无 git 提交；
- 通过标准：评审者全量复核确认后，本计划完成。

---

## Self-Review

- **规格覆盖（§1–§11）**：
  - §1 设计原则（player-facing / per-run 永久 / 混合时间线 / 分类标签 / 摘要卡）→ Task 2（`PlayerLogCategory`/`PlayerLogEntry` 标签与摘要字段）、Task 3（`Sequence` 自 1 起、跨存档延续）、Task 6/7/8（全部钩子产出玩家可见中文摘要卡，含「初始侵蚀」「短缺 N」「已到期移除」等文案）、Task 9（时间线倒序 + Day 分组 + 分类筛选的只读数据）。
  - §2 钩子表（9 个记录方 + 精确行号）→ Task 6（`EventManager.TrySettleProcessedEvent` 394-402 成功汇点 + `EventEffectManager.TrySettle` 46 行效果汇总，降级 resolve-only 49-53 行不产效果卡）、Task 7（`TickBuffs` 122-123 行按 buff 记录、`OnPhaseEntered` 95 行后阶段卡、`ExecuteFoodSettlement` 152 行食物卡）、Task 8（`OnConfirm` 282-283 / `OnReject` 320-321 / `TryAssign` 170 / `TryMoveToEmptyRoom` 211）。特殊故事判定（`trigger.kind ∈ {SpecialVisitor, Personal, ChainStep}`）→ Task 6 `RecordEventLog`。存档通道（SaveGameService/SaveAndQuitFlow/Dawn 自动存档）不改动 → 全部任务仅加 `Record` 调用点。
  - §3 Schema 与类别枚举（Hotel.Runtime/RunModel.cs）→ Task 2 逐字实现（9 项枚举 + `[Serializable] PlayerLogEntry` 七字段）。
  - §4 DTO Record 契约 → Task 3（`PlayerLogWriteDto` readonly struct、`Record` 防呆：空 state/空 Summary/异常 → false 不抛出、`Sequence = Count + 1`）。
  - §5 查询契约 → Task 3（`IPlayerLogQuery` 六成员逐字；`All` 升序、`ByDay` 降序、`Since` 升序、`Get` 克隆/null）。
  - §6 排序与幂等 → Task 3（Sequence 单调/持久化延续）、Task 4（跨存档延续测试）、Task 6（`out bool committed` 使事件经单一成功汇点恰好记录一次，天然去重）。
  - §7 失败处理 → Task 3（Record 失败不影响玩法）、Task 6/7/8（所有 `Record` 仅在 `Succeeded` 分支、无待补记队列/不重试）。
  - §8 持久化与迁移 → Task 2（`GameRunState.PlayerLogs`）、Task 4（`RunSaveData.PlayerLogs` 逐条克隆、缺失 → 空列表、`SchemaVersion` 保持 1、不回溯补写）；日志不参与 `StateReducer`/`RunChange`、不递增 `StateVersion` → Task 5 无侵入测试。
  - §9 UI 集成范围 → Task 9（只读控制器/视图 + 时间线倒序/Day 分组/分类筛选数据接缝 + Console 输出）、Task 10（MainScene 仅组件接线）；可视化布局（预制体/美术/样式）另行批准 → Global Constraints 范围门 + Task 9/10 评审门。
  - §10 测试计划（T1–T5）→ T1 序列化往返/旧存档空列表 → Task 4；T2 记录/查询/只读 → Task 3；T3 失败与防呆/单条纪律 → Task 3；T4 无侵入 → Task 5；T5 钩子位置（编译+人工）→ Task 6/7/8 编译验证 + Task 11 Step 3 人工验证。
  - §11 明确非目标 → Global Constraints（不实施 UI 布局、不改既有系统/事件通道、不走 StateReducer、不替换 Debug.Log/AuditLog、日志不参与玩法规则、无回溯补写、无多语言/统计/撤回）+ 各任务评审门。
- **占位符扫描**：全文无 TODO/TBD/「待定」「类似 Task N」；每个代码步骤给出完整代码或精确插入点；钩子行号逐一对齐已读源码；验证步骤给出具体 Expected。
- **签名一致性**：`PlayerLogCategory`（9 项）/`PlayerLogEntry`（七字段）/`GameRunState.PlayerLogs`/`PlayerLogWriteDto`/`IPlayerLogQuery`/`PlayerLogManager.Record`/`PlayerLogManager.Query`/`PlayerLogQuery` 在 Task 2/3/4 与 Task 6/7/8/9 的 Consumes/Produces/实现中逐字一致；`TrySettle(..., out PlayerLogWriteDto effectSummary, out bool committed)` 在 Task 6 的 Produces 与两个实现文件的代码中一致；`ExecuteFoodSettlement(int day, HotelPhase phase)` 在 Task 7 的三个修改点一致；`RecordEventLog`/`ResolveOptionText`/`BuildEffectSummaryText`/`ResourceName`/`PhaseName`/`RecordRoomAssignment`/`RecordRoomMove`/`TenantDisplayName` 均在各自任务内定义且被消费。排序约定（All 升序、ByDay/ByCategory 降序、Since 升序）在「设计决策」、Task 3 实现与 Task 3 测试断言三处一致。
