# 租客工作系统（运行时基础）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **本计划特殊约定：** 按既有计划约定与工作区策略，本计划**不包含任何 bash / git 命令**（不提交、不改动 git 状态），每个任务统一以「Unity 编译验证」＋「EditMode Test Runner 通过」＋（对 Assembly-CSharp 代码）「Play 模式人工验证」作为完成检查点。

**Goal:** 实现规格《租客工作系统设计（2026-08-09）》的第一阶段运行时基础：JobId 运行时真相、半日工作结算账本（exactly-once）、事件侵蚀缓解拦截、确定性探索种子、新资源与全局设施耐久、存档 v2 迁移、WorkCatalog 配置资产与内核校验规则，全部不涉及 UI 场景布局与事件资产迁移。

**Architecture:** 纯 C# 内核（`Hotel.Runtime`）承载所有可测试逻辑——工作结算计算器（`WorkSettlementCalculator`）、缓解解析器（`WorkMitigationResolver`）、确定性种子（`WorkDeterminism`）、账本/耐久/JobId 校验（`StateReducer` 扩展）与存档编解码（`RunSaveCodec` v2 迁移）；`Hotel.Authoring` 只新增数据定义（`JobDefinition`/`TeamComboDefinition`/`WorkCatalog`）与两个纯静态求值器（`JobCompatibility`/`TeamComboEvaluator`）；Assembly-CSharp 层新增两个协调器（`WorkSettlementCoordinator` 监听 `PhaseEnteredEvent` 执行结算并冻结半日快照、`WorkAssignmentCoordinator` 作为 `AssignJobChange` 唯一入口），并通过 `IJobIdRegistry` 接口向内核注入已注册 JobId 白名单，保证 `Hotel.Runtime` 不依赖 `Hotel.Authoring`。配置数值全部来自 SO 资产字段，内核通过 `WorkSettlementConfig` 纯数据桥接收。

**Tech Stack:** Unity 2022.3.62f3c1 LTS、C#、ScriptableObject（Hotel.Authoring）、UnityEngine.TestRunner（NUnit EditMode 测试，`Assets/Tests/Hotel.Runtime.Tests`）、UGUI/TMP（仅既有 pinned 面板内的数据绑定，无新建布局）。

## Global Constraints

- **范围门（第一阶段不做，除非另行批准）**：UI 场景布局（职业分配面板预制体/接线/美术/样式）、资源/设施/组合展示 HUD 场景布局、`Event_*.asset`（EVENTS.md N/D 目录条目）迁移、`TenantAbility.Carpenter` 移除与相关候选/事件资产迁移、`ApplyBuff` 逐 tick 侵蚀缓解、天气/线索/楼层阻断等未实现系统、平衡数值定稿。
- **9 个固有标签（已确认）**：`Doctor`、`Cook`、`Engineer`、`NightWatch`、`FormerEmployee`、`Merchant`、`Farmer`、`Driver`、`Teacher`，另有无标签 `None`；**保留** `Carpenter`（不得强制移除或改配既有 Carpenter 数据）。
- **事件资格与职业无关**：`ChoiceOption.requiredTags` / `GamePopupEvent.choiceRequiredTags`（`EventUI.GetOwnedAbilities`）继续按固有标签判定，改职业不改变事件资格；拦截只改变侵蚀数值。
- **10 个职业 id（逐字固定）**：`cooking`、`medical`、`repair`、`watch`、`patrol`、`trade`、`farming`、`exploration`、`organizing`、`chores`。
- **兼容规则**：无标签租客只能分配 `chores`（`allowedTags` 空列表 = 仅 `None`）；带标签租客按 `JobDefinition.allowedTags` 判定（第一阶段初始配置一一对应）。**改职业不阻塞阶段推进**（与房间分配 `HasUnassignedTenants` 阻塞无关），同一阶段内修改次数不限，**只影响下一次结算**。
- **结算时段语义（已确认）**：`DayActive` 仅 Day、`NightActive` 仅 Night、`AllDay` 两者；`watch` 仅夜间；进入 Day/Night 时结算，Dawn/Dusk 不结算。
- **运行时真相**：`TenantRunState.JobId` 是唯一分配真相（null/空串 = 未分配，不参与结算）；活跃团队由快照动态推导、**不写入状态、不保存**。
- **确定性探索种子（已确认算法）**：参数 = runSeed、day、phase、tenantId、jobId、成功结算序号；种子 = `WorkDeterminism.StableHash` + fmix（与 `EventSelectionService.DeriveSeed` 同款）；产出用 `new System.Random(seed)`，**禁用 `UnityEngine.Random`**。成功结算序号定义：结算计算时 `Sequence = WorkSettlementSequence + 1`（即本次将写入账本的序号）。
- **已确认数值**：`watchNightLossMitigationPercent = 40`；`security_team` 的 `NightLossMitigationOverride = 60`（**取代而非叠加** 40%，多守夜不叠加）。其余全部数值为可调初始默认值，必须是配置资产字段，**不允许在代码里发明常量**。
- **持久化**：`RunSaveData.CurrentSchemaVersion` 1 → 2；新增 `FacilityDurability`（默认 100）、`WorkSettlementSequence`（默认 0）、`WorkSettlements`（List 序列化）；v1 存档迁移；新资源 `ingredients`/`resources` 缺失时沿用 `SettlementBridge.Awake` 既有默认行为（有定义按 `initialAmount`，无定义以 Amount=0 创建）；载入中途阶段不重算已入账 (day, phase)。
- **资源 id（逐字固定）**：`food`、`currency`、`ingredients`（食材，新增）、`resources`（物资，新增；UI 显示「物资」）。
- **依赖方向硬约束**：`Hotel.Runtime` **不得**引用 `Hotel.Authoring`；JobId 校验通过 `IJobIdRegistry` 接口注入（详见下文「设计决策」）。`Hotel.Authoring` 依赖 `Hotel.Runtime`（既有）；测试程序集依赖 `Hotel.Runtime` 与 `Hotel.Authoring`。
- **测试范围约束**：`Hotel.Runtime.Tests` 的 EditMode 测试只覆盖 `Hotel.Runtime`/`Hotel.Authoring` 程序集；Assembly-CSharp 的协调器/UI/拦截逻辑以「Unity 编译验证 + Play 模式人工验证」为准（沿用 ARCHITECTURE.md 约定）。**该测试程序集当前缺失**（`Assets/Tests/Hotel.Runtime.Tests/` 不存在，仓库根残留 `Hotel.Runtime.Tests.csproj`），Task 1 必须先以 Unity 操作检查并恢复，不得假设它已存在。
- **文件落地**：脚本一律位于 `Assets/Scripts/...`；配置资产一律位于 `Assets/Data/...`；不新建任何 Prefab / UI 场景布局；`MainScene.unity` 仅允许「管理器组件新增 + 序列化引用接线」（GameManager 与 SettlementBridge 组件块），禁止任何 UI 布局改动。
- 每个任务以 Unity 检查点收尾：Console 0 错误、EditMode 测试（若适用）全部通过、Play 人工验证输出符合 Expected。不做任何 git 提交。

---

## 设计决策：JobId 安全校验与依赖方向

- 约束：`Hotel.Runtime` 无外部 asmdef 引用（ARCHITECTURE.md），不能引用 `Hotel.Authoring` 的 `JobDefinition`/`WorkCatalog`。
- 方案：在 `Hotel.Runtime` 定义最小接口 `IJobIdRegistry { bool IsRegistered(string jobId); }`。`StateReducer` 新增构造函数重载 `StateReducer(IJobIdRegistry registry)`；校验时**只**调用该接口，不感知任何 SO 类型。具体注册表 `WorkCatalogJobIdRegistry`（Assembly-CSharp）在 `SettlementBridge.Awake` 由场景中的 `WorkCatalog` 构建并注入。
- **向后兼容（显式定义）**：`new StateReducer()` 等价于 `new StateReducer(null)`。未注入注册表时，`AssignJobChange` 保持既有行为——只校验租客存在性、接受任意非空字符串（旧行为）。因此：`SettlementBridge` 未接线 `workCatalog` 时、以及既有测试/调用方，行为逐字节不变；接线后自动升级为白名单校验。
- 注入时的校验规则：`jobId` 为空串（解除分配）**或** ∈ 已注册集合 → 通过；其余拒绝（`false`）。
- 双层校验：协调器层（`WorkAssignmentCoordinator`，`AssignJobChange` 唯一提交入口）额外校验 `allowedTags` 兼容（含「无标签仅 chores」）；内核层只做白名单存在性校验，不复制兼容规则（规格 §12.3/§12.4）。

## File Structure

| 文件 | 操作 | 职责 |
| --- | --- | --- |
| `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef` | **创建** | 恢复缺失的 EditMode 测试程序集（引用 Hotel.Runtime、Hotel.Authoring，Editor-only，Test Assemblies） |
| `Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs` | **创建** | 冒烟测试：证明测试程序集可编译可运行 |
| `Assets/Scripts/Hotel/Runtime/State/RunModel.cs` | **修改** | `TenantAbility` 追加 `Driver`/`Teacher`；新增 `WorkSettlementRecord`；`GameRunState` 新增 `FacilityDurability`/`WorkSettlementSequence`/`WorkSettlements` |
| `Assets/Scripts/Hotel/UI/AbilityDisplayName.cs` | **修改** | `Driver`→司机、`Teacher`→教师 映射 |
| `Assets/Scripts/Hotel/UI/TenantReviewFontPrewarmer.cs` | **修改** | `FixedPanelStrings` 能力标签补「司机」「教师」 |
| `Assets/Scripts/Hotel/Runtime/State/WorkSnapshot.cs` | **创建** | `WorkSnapshotTenant`/`WorkSnapshot` 半日冻结快照数据 |
| `Assets/Scripts/Hotel/Runtime/State/WorkSettlementLedger.cs` | **创建** | `WorkSettlementLedger.Key(day, phase)` 账本 key = `"{day}|{phase}"` |
| `Assets/Scripts/Hotel/Runtime/Kernel/Changes/RunChanges.cs` | **修改** | 新增 `AdjustFacilityDurabilityChange`、`AddWorkSettlementChange`、`IJobIdRegistry` |
| `Assets/Scripts/Hotel/Runtime/Kernel/Reduction/StateReducer.cs` | **修改** | 注册表注入构造、`AssignJobChange` 白名单校验、新变更的校验与原子应用 |
| `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkDeterminism.cs` | **创建** | `StableHash`/`DeriveExplorationSeed`/`RollExplorationOutput`（确定性探索） |
| `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationContext.cs` | **创建** | 缓解上下文纯数据（含配置值 + 激活计数） |
| `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationResolver.cs` | **创建** | `Compute(delta, target, context)` 恶化方向侵蚀缓解 |
| `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkSettlementCalculator.cs` | **创建** | `WorkSettlementConfig`/`WorkSettlementInputs`/`WorkSettlementPlan`/`WorkSettlementCalculator.Compute` 全部产出/消耗/治疗数学 |
| `Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs` | **修改** | SchemaVersion 2、新字段快照/还原、v1→v2 迁移、`ReadMetadata` 兼容 v1/v2、`CloneTenant` JobId 归一化为空串 |
| `Assets/Scripts/Hotel/Authoring/Work/JobDefinition.cs` | **创建** | 职业 SO：jobId/displayName/activityWindow/allowedTags + §3.2 全部可配置数值字段 |
| `Assets/Scripts/Hotel/Authoring/Work/TeamComboDefinition.cs` | **创建** | 团队 SO：`TeamEffectKind`/`TeamRole`/`TeamEffect`/comboId/roles/effects + `TryGetEffect` |
| `Assets/Scripts/Hotel/Authoring/Work/WorkCatalog.cs` | **创建** | 组合根资产：`jobs`/`teams` 列表 + `FindJob` |
| `Assets/Scripts/Hotel/Authoring/Work/JobCompatibility.cs` | **创建** | `IsAllowed(JobDefinition, TenantAbility)`：空标签=仅 None，否则 ∈ allowedTags |
| `Assets/Scripts/Hotel/Authoring/Work/TeamComboEvaluator.cs` | **创建** | `IsActive(TeamComboDefinition, IReadOnlyList<WorkSnapshotTenant>)` 团队激活推导 |
| `Assets/Scripts/Hotel/Managers/WorkCatalogJobIdRegistry.cs` | **创建** | `IJobIdRegistry` 的 WorkCatalog 适配器（Assembly-CSharp） |
| `Assets/Scripts/Hotel/Managers/WorkSettlementCoordinator.cs` | **创建** | 监听 PhaseEntered、冻结快照/缓解上下文、计算并提交结算变更集、账本幂等、失败重试、只读查询接口 |
| `Assets/Scripts/Hotel/Managers/WorkAssignmentCoordinator.cs` | **创建** | `AssignJobChange` 唯一入口：TryAssignJob 校验 + GetJobEntries + 调试 ContextMenu |
| `Assets/Scripts/Hotel/Managers/WorkJobEntryView.cs` | **创建** | `JobEntryView` 职业列表视图模型 |
| `Assets/Scripts/Hotel/UI/WorkJobDisplay.cs` | **创建** | `GetNextEffectText(JobDefinition)` 下次效果文案 |
| `Assets/Scripts/Hotel/Managers/SettlementBridge.cs` | **修改** | 注入 `WorkCatalogJobIdRegistry`、`workCatalog` 序列化字段、缺失 `ingredients`/`resources` 以 0 兜底 |
| `Assets/Scripts/Hotel/Services/EventEffectExecutor.cs` | **修改** | `AddErosionChanges` 在生成 `AdjustTenantErosionChange` 前调用 `WorkMitigationResolver.Compute` |
| `Assets/Scripts/Hotel/UI/TenantInfoPanel.cs` | **修改** | `ShowPinned` 末尾调用 `RefreshJobSection()`（数据绑定 + Console 只读输出，非布局） |
| `Assets/Tests/Hotel.Runtime.Tests/*.cs` | **创建** | AbilityCatalogTests / WorkModelTests / WorkSettlementReducerTests / WorkDeterminismTests / WorkMitigationResolverTests / WorkSettlementCalculatorTests / WorkAuthoringDefinitionTests / WorkSaveCodecTests / WorkCatalogAssetTests |
| `Assets/Data/Configs/Work/Jobs/Job_*.asset` ×10 | **创建**（unitymaster） | 10 个 `JobDefinition` 资产（§3.2 初始值） |
| `Assets/Data/Configs/Work/Teams/Team_*.asset` ×3 | **创建**（unitymaster） | 3 个 `TeamComboDefinition` 资产 |
| `Assets/Data/Configs/Work/WorkCatalog.asset` | **创建**（unitymaster） | 组合根，引用上述 13 个资产 |
| `Assets/Data/Resources/Ingredients.asset` | **创建**（unitymaster） | `ResourceDefinition`，resourceId=`ingredients`，initialAmount=0 |
| `Assets/Data/Resources/Resources.asset` | **创建**（unitymaster） | `ResourceDefinition`，resourceId=`resources`，initialAmount=0 |
| `Assets/Scenes/MainScene.unity` | **修改**（仅 unitymaster） | GameManager（fileID 1918893930）加 WorkSettlementCoordinator + WorkAssignmentCoordinator 并接线；SettlementBridge 组件（fileID 481458030）接 `workCatalog` |

### Task 1: 恢复 Hotel.Runtime.Tests EditMode 测试程序集

> 前置研究结论：`Assets/Tests/Hotel.Runtime.Tests/` 目录不存在（glob 0 结果），仓库根残留 `Hotel.Runtime.Tests.csproj`。**不得假设测试程序集已存在**；本任务先检查、后恢复，全部通过 Unity 编辑器操作完成。

**Files:**
- Create: `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`
- Create: `Assets/Tests/Hotel.Runtime.Tests/SmokeTests.cs`

**Interfaces:**
- Consumes: 无（本任务为后续全部 NUnit 任务的前置）。
- Produces: 程序集 `Hotel.Runtime.Tests`（Editor-only，引用 `Hotel.Runtime` 与 `Hotel.Authoring`，`UNITY_INCLUDE_TESTS` 约束），供 Task 2–9 的测试文件落位与运行。

- [ ] **Step 1: 检查测试程序集现状**

1. Unity 中打开项目，在 Project 窗口确认 `Assets/Tests/` 是否存在、`Assets/Tests/Hotel.Runtime.Tests/` 是否存在。
2. 打开 Window → General → Test Runner → EditMode。
Expected: Test Runner 中**没有**名为 `Hotel.Runtime.Tests` 的程序集；`Assets/Tests/` 下无 asmdef（与 glob 研究结果一致：测试程序集缺失）。若已存在同名程序集且可运行，跳过本任务其余步骤并向执行方报告「已存在」。

- [ ] **Step 2: 创建测试程序集定义**

在 Project 窗口右键 `Assets/` → Create → Folder，命名 `Tests`；右键 `Assets/Tests` → Create → Folder，命名 `Hotel.Runtime.Tests`。然后两种方式二选一：

方式 A（推荐，编辑器 UI）：右键 `Assets/Tests/Hotel.Runtime.Tests` → Create → Assembly Definition，命名 `Hotel.Runtime.Tests`；在 Inspector 勾选 **Test Assemblies**，Platforms 仅勾选 **Editor**（其余全不勾），References 添加 `Hotel.Runtime` 与 `Hotel.Authoring`，保存。

方式 B（直接写文件）：创建 `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`，内容精确为：

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

等待 Unity 导入完成。Expected: Console 无 asmdef 相关错误；Unity 在 `Assets/Tests/Hotel.Runtime.Tests/` 下生成 `Hotel.Runtime.Tests.csproj`；仓库根残留的 `Hotel.Runtime.Tests.csproj` 为旧版残留产物，**不要手工删除**，以 Unity 生成的为准，若其继续存在且干扰，仅报告执行方复核。

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

        [Test]
        public void Assembly_References_HotelAuthoring()
        {
            var def = UnityEngine.ScriptableObject.CreateInstance<Hotel.Authoring.Resources.ResourceDefinition>();
            Assert.NotNull(def);
        }
    }
}
```

- [ ] **Step 4: 运行冒烟测试**

Window → General → Test Runner → EditMode → Run All（或选中 `Hotel.Runtime.Tests` 程序集运行）。
Expected: `SmokeTests` 2 项全部 PASS；Console 0 错误。

---

### Task 2: TenantAbility 新增 Driver/Teacher + AbilityDisplayName 映射 + 字体预热补全

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/State/RunModel.cs:45-56`（TenantAbility 枚举）
- Modify: `Assets/Scripts/Hotel/UI/AbilityDisplayName.cs:5-19`
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewFontPrewarmer.cs:34-42`（FixedPanelStrings）
- Test: `Assets/Tests/Hotel.Runtime.Tests/AbilityCatalogTests.cs`

**Interfaces:**
- Consumes: `TenantAbility` 枚举（Hotel.Runtime）；`AbilityDisplayName.Get(TenantAbility) : string`（既有）；`TenantReviewFontPrewarmer.FixedPanelStrings`（既有，补齐字形预热）。
- Produces: `TenantAbility.Driver`（枚举值 9）与 `TenantAbility.Teacher`（枚举值 10），**追加在枚举末尾，既有成员序数不变**（`Carpenter` 保持 7、`Farmer` 保持 8，既有候选资产序列化不失效）。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/AbilityCatalogTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class AbilityCatalogTests
    {
        [Test]
        public void Driver_And_Teacher_Exist_As_Members()
        {
            Assert.True(System.Enum.IsDefined(typeof(TenantAbility), TenantAbility.Driver));
            Assert.True(System.Enum.IsDefined(typeof(TenantAbility), TenantAbility.Teacher));
        }

        [Test]
        public void Carpenter_Is_Preserved()
        {
            Assert.True(System.Enum.IsDefined(typeof(TenantAbility), TenantAbility.Carpenter));
        }

        [Test]
        public void Existing_Enum_Indices_Are_Unchanged()
        {
            Assert.AreEqual(1, (int)TenantAbility.Doctor);
            Assert.AreEqual(7, (int)TenantAbility.Carpenter);
            Assert.AreEqual(8, (int)TenantAbility.Farmer);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `AbilityCatalogTests`。
Expected: 编译失败——`TenantAbility` 不包含 `Driver`/`Teacher`（CS0103/CS0117），即红灯状态。这是预期的红状态。

- [ ] **Step 3: 最小实现**

`Assets/Scripts/Hotel/Runtime/State/RunModel.cs` 第 45-56 行枚举改为：

```csharp
    public enum TenantAbility
    {
        None,
        Doctor,
        Cook,
        Engineer,
        NightWatch,
        FormerEmployee,
        Merchant,
        Carpenter,
        Farmer,
        Driver,
        Teacher
    }
```

`Assets/Scripts/Hotel/UI/AbilityDisplayName.cs` 的 `Get` 方法内、`case TenantAbility.Farmer` 之后追加两行：

```csharp
            case TenantAbility.Driver: return "司机";
            case TenantAbility.Teacher: return "教师";
```

`Assets/Scripts/Hotel/UI/TenantReviewFontPrewarmer.cs` 第 37 行能力标签数组改为：

```csharp
        "医生", "厨师", "工程师", "守夜人", "前员工", "商贩", "木工", "农民", "司机", "教师", "无标签",
```

（顺序与 `AbilityDisplayName.Get` 一致；其余数组元素与文件其余部分不动。）

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `AbilityCatalogTests`。
Expected: 3 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

聚焦 Unity，等待重编译；Console 0 错误、0 新增警告。全局搜索 `case TenantAbility.`（覆盖 `Assets/`）：唯一 switch 在 `AbilityDisplayName.cs` 且含 default 分支，无遗漏编译错误。

### Task 3: 运行时工作状态模型（WorkSettlementRecord + WorkSnapshot + GameRunState 扩展）

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/State/RunModel.cs`
- Create: `Assets/Scripts/Hotel/Runtime/State/WorkSnapshot.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkModelTests.cs`

**Interfaces:**
- Consumes: `GameRunState`（RunModel.cs）、`TenantAbility`（Task 2 扩展后）。
- Produces:
  - `WorkSettlementRecord { int Day; HotelPhase Phase; int Sequence; }`（可序列化 class，`Hotel.Runtime`）——Task 4/8/10 使用。
  - `GameRunState.FacilityDurability : float`（默认 100）、`GameRunState.WorkSettlementSequence : int`（默认 0）、`GameRunState.WorkSettlements : Dictionary<string, WorkSettlementRecord>`（默认空字典）——Task 4/8/10 使用。
  - `WorkSnapshotTenant { string TenantId; string JobId; TenantAbility Ability; }`、`WorkSnapshot { int Day; HotelPhase Phase; List<WorkSnapshotTenant> Tenants; }`——Task 6/7/10 使用。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkModelTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkModelTests
    {
        [Test]
        public void GameRunState_Defaults_WorkFields()
        {
            var state = GameRunState.New(new RunId("r"), 7);
            Assert.AreEqual(100f, state.FacilityDurability);
            Assert.AreEqual(0, state.WorkSettlementSequence);
            Assert.NotNull(state.WorkSettlements);
            Assert.AreEqual(0, state.WorkSettlements.Count);
        }

        [Test]
        public void WorkSettlementRecord_Carries_Fields()
        {
            var record = new WorkSettlementRecord { Day = 3, Phase = HotelPhase.Night, Sequence = 2 };
            Assert.AreEqual(3, record.Day);
            Assert.AreEqual(HotelPhase.Night, record.Phase);
            Assert.AreEqual(2, record.Sequence);
        }

        [Test]
        public void WorkSnapshot_Stores_Participants()
        {
            var snapshot = new WorkSnapshot { Day = 2, Phase = HotelPhase.Day };
            snapshot.Tenants.Add(new WorkSnapshotTenant { TenantId = "t1", JobId = "cooking", Ability = TenantAbility.Cook });
            Assert.AreEqual(1, snapshot.Tenants.Count);
            Assert.AreEqual("cooking", snapshot.Tenants[0].JobId);
            Assert.AreEqual(TenantAbility.Cook, snapshot.Tenants[0].Ability);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkModelTests`。
Expected: 编译失败（`WorkSettlementRecord`/`WorkSnapshot` 不存在、`FacilityDurability` 字段不存在），即红灯。

- [ ] **Step 3: 最小实现**

`Assets/Scripts/Hotel/Runtime/State/RunModel.cs`：

(a) 在 `ReviewDecisionRecord` 类（约第 120 行）之后插入：

```csharp
    [Serializable]
    public sealed class WorkSettlementRecord
    {
        public int Day;
        public HotelPhase Phase;
        public int Sequence;
    }
```

(b) `GameRunState`（第 216-244 行）在 `public List<ReviewDecisionRecord> ReviewHistory ...` 之后追加三个字段：

```csharp
        public float FacilityDurability = 100f;
        public int WorkSettlementSequence;
        public Dictionary<string, WorkSettlementRecord> WorkSettlements = new Dictionary<string, WorkSettlementRecord>();
```

创建 `Assets/Scripts/Hotel/Runtime/State/WorkSnapshot.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    [Serializable]
    public sealed class WorkSnapshotTenant
    {
        public string TenantId;
        public string JobId;
        public TenantAbility Ability;
    }

    [Serializable]
    public sealed class WorkSnapshot
    {
        public int Day;
        public HotelPhase Phase;
        public List<WorkSnapshotTenant> Tenants = new List<WorkSnapshotTenant>();
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkModelTests`。
Expected: 3 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

---

### Task 4: 内核变更与校验（RunChanges + StateReducer 扩展 + IJobIdRegistry）

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/Kernel/Changes/RunChanges.cs`
- Modify: `Assets/Scripts/Hotel/Runtime/Kernel/Reduction/StateReducer.cs`
- Create: `Assets/Scripts/Hotel/Runtime/State/WorkSettlementLedger.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkSettlementReducerTests.cs`

**Interfaces:**
- Consumes: `WorkSettlementRecord`（Task 3）；`GameRunState` 新字段（Task 3）；`AssignJobChange`（既有）。
- Produces:
  - `AdjustFacilityDurabilityChange(float delta) { float Delta }`（应用时 clamp [0,100]）。
  - `AddWorkSettlementChange(WorkSettlementRecord record) { WorkSettlementRecord Record }`（authorizer 必须为 `"WorkSettlementCoordinator"`；`Phase` 仅 Day/Night；key 不得重复（含同集内重复）；`Sequence == WorkSettlementSequence + 1`；应用时写账本并递增序号）。
  - `IJobIdRegistry { bool IsRegistered(string jobId); }`（`Hotel.Runtime` 命名空间）。
  - `StateReducer()` / `StateReducer(IJobIdRegistry registry)`——未注入注册表时 `AssignJobChange` 保持旧行为（仅租客存在性），注入时 `jobId` 空串或 ∈ 已注册集合才通过。
  - `WorkSettlementLedger.Key(int day, HotelPhase phase) : string`，返回 `"{day}|{phase}"`（如 `"3|Night"`）——Task 8/10 复用。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkSettlementReducerTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkSettlementReducerTests
    {
        private sealed class StaticRegistry : IJobIdRegistry
        {
            private readonly HashSet<string> _ids;
            public StaticRegistry(params string[] ids) { _ids = new HashSet<string>(ids); }
            public bool IsRegistered(string jobId) => _ids.Contains(jobId);
        }

        private static GameRunState NewState()
        {
            var state = GameRunState.New(new RunId("r"), 7);
            state.Resources["food"] = new ResourceRunState { ResourceId = "food", DefinitionId = "food", Amount = 10 };
            state.Resources["currency"] = new ResourceRunState { ResourceId = "currency", DefinitionId = "currency", Amount = 10 };
            state.Resources["ingredients"] = new ResourceRunState { ResourceId = "ingredients", DefinitionId = "ingredients", Amount = 10 };
            state.Resources["resources"] = new ResourceRunState { ResourceId = "resources", DefinitionId = "resources", Amount = 10 };
            state.Tenants["t1"] = new TenantRunState { TenantId = "t1", DefinitionId = "cand_1", RoomId = "room_01" };
            return state;
        }

        [Test]
        public void FacilityDurability_Clamps_UpperBound()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            set.Add(new AdjustFacilityDurabilityChange(150f));
            Assert.IsTrue(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.AreEqual(100f, state.FacilityDurability);
        }

        [Test]
        public void FacilityDurability_Clamps_LowerBound()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            set.Add(new AdjustFacilityDurabilityChange(-999f));
            Assert.IsTrue(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.AreEqual(0f, state.FacilityDurability);
        }

        [Test]
        public void AddWorkSettlement_Accepts_FirstSequence()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            set.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Day, Sequence = 1 }));
            Assert.IsTrue(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.AreEqual(1, state.WorkSettlementSequence);
            Assert.IsTrue(state.WorkSettlements.ContainsKey("1|Day"));
        }

        [Test]
        public void AddWorkSettlement_Rejects_DuplicateKey()
        {
            var state = NewState();
            var reducer = new StateReducer();
            var first = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            first.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Day, Sequence = 1 }));
            Assert.IsTrue(reducer.TryCommit(state, first).Succeeded);

            var second = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            second.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Day, Sequence = 2 }));
            Assert.IsFalse(reducer.TryCommit(state, second).Succeeded);
            Assert.AreEqual(1, state.WorkSettlementSequence);
            Assert.AreEqual(1, state.WorkSettlements.Count);
        }

        [Test]
        public void AddWorkSettlement_Rejects_SequenceGap()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Day");
            set.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Day, Sequence = 5 }));
            Assert.IsFalse(new StateReducer().TryCommit(state, set).Succeeded);
            Assert.AreEqual(0, state.WorkSettlementSequence);
        }

        [Test]
        public void AddWorkSettlement_Rejects_WrongAuthorizer()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "SomeOtherSystem", "WorkSettlement|1|Day");
            set.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Day, Sequence = 1 }));
            Assert.IsFalse(new StateReducer().TryCommit(state, set).Succeeded);
        }

        [Test]
        public void AddWorkSettlement_Rejects_Dawn()
        {
            var state = NewState();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkSettlementCoordinator", "WorkSettlement|1|Dawn");
            set.Add(new AddWorkSettlementChange(new WorkSettlementRecord { Day = 1, Phase = HotelPhase.Dawn, Sequence = 1 }));
            Assert.IsFalse(new StateReducer().TryCommit(state, set).Succeeded);
        }

        [Test]
        public void AssignJob_Rejects_UnregisteredId_WhenRegistryInjected()
        {
            var state = NewState();
            var reducer = new StateReducer(new StaticRegistry("cooking"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkAssignmentCoordinator", "AssignJob");
            set.Add(new AssignJobChange("t1", "bogus_job"));
            Assert.IsFalse(reducer.TryCommit(state, set).Succeeded);
        }

        [Test]
        public void AssignJob_Accepts_RegisteredId_And_Empty()
        {
            var state = NewState();
            var reducer = new StateReducer(new StaticRegistry("cooking"));
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkAssignmentCoordinator", "AssignJob");
            set.Add(new AssignJobChange("t1", "cooking"));
            Assert.IsTrue(reducer.TryCommit(state, set).Succeeded);
            Assert.AreEqual("cooking", state.Tenants["t1"].JobId);

            var unset = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkAssignmentCoordinator", "AssignJob");
            unset.Add(new AssignJobChange("t1", ""));
            Assert.IsTrue(reducer.TryCommit(state, unset).Succeeded);
            Assert.AreEqual("", state.Tenants["t1"].JobId);
        }

        [Test]
        public void AssignJob_Accepts_AnyId_WhenRegistryNull_Legacy()
        {
            var state = NewState();
            var reducer = new StateReducer();
            var set = AuthorizedChangeSet.Domain(state.RunId, state.StateVersion, "WorkAssignmentCoordinator", "AssignJob");
            set.Add(new AssignJobChange("t1", "anything_legacy"));
            Assert.IsTrue(reducer.TryCommit(state, set).Succeeded);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkSettlementReducerTests`。
Expected: 编译失败（`AdjustFacilityDurabilityChange`/`AddWorkSettlementChange`/`IJobIdRegistry`/`WorkSettlementLedger` 不存在；`StateReducer` 无注册表构造），即红灯。

- [ ] **Step 3: 最小实现**

创建 `Assets/Scripts/Hotel/Runtime/State/WorkSettlementLedger.cs`：

```csharp
namespace Hotel.Runtime
{
    public static class WorkSettlementLedger
    {
        public static string Key(int day, HotelPhase phase)
        {
            return $"{day}|{phase}";
        }
    }
}
```

`Assets/Scripts/Hotel/Runtime/Kernel/Changes/RunChanges.cs` 末尾（`IStateReducer` 之前）追加：

```csharp
    public sealed class AdjustFacilityDurabilityChange : RunChange
    {
        public AdjustFacilityDurabilityChange(float delta) { Delta = delta; }
        public float Delta { get; }
    }

    public sealed class AddWorkSettlementChange : RunChange
    {
        public AddWorkSettlementChange(WorkSettlementRecord record) { Record = record; }
        public WorkSettlementRecord Record { get; }
    }

    public interface IJobIdRegistry
    {
        bool IsRegistered(string jobId);
    }
```

`Assets/Scripts/Hotel/Runtime/Kernel/Reduction/StateReducer.cs`：

(a) 类字段与构造函数（第 6-7 行类声明之后）：

```csharp
    public sealed class StateReducer : IStateReducer
    {
        private readonly IJobIdRegistry _jobIdRegistry;

        public StateReducer() : this(null) { }

        public StateReducer(IJobIdRegistry jobIdRegistry)
        {
            _jobIdRegistry = jobIdRegistry;
        }
```

(b) `Validate` 方法开头（`var reviewRecords = new List<ReviewDecisionRecord>();` 之后）新增：

```csharp
            var plannedSettlementKeys = new HashSet<string>();
```

(c) `AssignJobChange` 校验分支（现第 130-135 行）改为：

```csharp
                    case AssignJobChange job:
                    {
                        if (!s.Tenants.ContainsKey(job.TenantId))
                            return false;
                        if (_jobIdRegistry != null
                            && !string.IsNullOrEmpty(job.JobId)
                            && !_jobIdRegistry.IsRegistered(job.JobId))
                            return false;
                        break;
                    }
```

(d) 在 `case AdjustResourceChange resource:` 分支之后插入两个新分支：

```csharp
                case AdjustFacilityDurabilityChange durability:
                {
                    if (float.IsNaN(durability.Delta))
                        return false;
                    break;
                }
                case AddWorkSettlementChange settlement:
                {
                    if (settlement.Record == null)
                        return false;
                    if (set.AuthorizerId != "WorkSettlementCoordinator")
                        return false;
                    if (settlement.Record.Phase != HotelPhase.Day && settlement.Record.Phase != HotelPhase.Night)
                        return false;
                    string settlementKey = WorkSettlementLedger.Key(settlement.Record.Day, settlement.Record.Phase);
                    if (s.WorkSettlements.ContainsKey(settlementKey))
                        return false;
                    if (!plannedSettlementKeys.Add(settlementKey))
                        return false;
                    if (settlement.Record.Sequence != s.WorkSettlementSequence + 1)
                        return false;
                    break;
                }
```

(e) `Apply` 中 `case AdjustResourceChange x:` 分支之后插入两个新分支：

```csharp
                case AdjustFacilityDurabilityChange x:
                {
                    float clamped = s.FacilityDurability + x.Delta;
                    if (clamped < 0f) clamped = 0f;
                    if (clamped > 100f) clamped = 100f;
                    s.FacilityDurability = clamped;
                    break;
                }
                case AddWorkSettlementChange x:
                {
                    s.WorkSettlements[WorkSettlementLedger.Key(x.Record.Day, x.Record.Phase)] = new WorkSettlementRecord
                    {
                        Day = x.Record.Day,
                        Phase = x.Record.Phase,
                        Sequence = x.Record.Sequence
                    };
                    s.WorkSettlementSequence = x.Record.Sequence;
                    break;
                }
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkSettlementReducerTests`。
Expected: 10 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。既有 `SettlementBridge`/`TenantAssignmentCoordinator`/`EventManager` 等调用方仍走 `new StateReducer()` 默认构造，行为不变（向后兼容）。

### Task 5: WorkDeterminism（确定性探索种子）+ WorkMitigationResolver（侵蚀缓解）

**Files:**
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkDeterminism.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationContext.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationResolver.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkDeterminismTests.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkMitigationResolverTests.cs`

**Interfaces:**
- Consumes: `EffectTarget`（既有，RunModel.cs）、`HotelPhase`。
- Produces:
  - `WorkDeterminism.StableHash(string) : int`、`WorkDeterminism.DeriveExplorationSeed(int runSeed, int day, HotelPhase phase, string tenantId, string jobId, int sequence) : int`、`WorkDeterminism.RollExplorationOutput(int seed, int min, int max) : int`——Task 6/10 使用。
  - `WorkMitigationContext`（struct：`IsValid`、`Phase`、`ActiveWatchCount`、`SecurityTeamActive`、`ActivePatrolCount`、`ActiveOrganizingCount`、`WatchMitigationPercent`、`SecurityOverridePercent`、`PatrolReductionPercentPerActive`、`PatrolReductionCapPercent`、`OrganizingReductionPercentPerActive`、`OrganizingReductionCapPercent`）——Task 10 构造、Task 11 消费。
  - `WorkMitigationResolver.Compute(float delta, EffectTarget target, WorkMitigationContext? context) : float`——上下文缺失/无效或 delta ≤ 0（治疗方向）一律返回原 delta；`OwnerTenant` 且 Night 且 watch>0 → 档位 = 安保队激活 ? override : watch；`SameFloorTenants` 且 Night 且 patrol>0 → 累减 cap 75；`AllAssignedTenants` 且 organizing>0 → 累减 cap 60；其余目标（`SameRoomOtherTenants`/`ByPlayerFlag`/`RandomAssignedTenants`）不缓解。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkDeterminismTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkDeterminismTests
    {
        [Test]
        public void SameInputs_Produce_SameOutput()
        {
            int a = WorkDeterminism.DeriveExplorationSeed(42, 3, HotelPhase.Day, "t1", "exploration", 5);
            int b = WorkDeterminism.DeriveExplorationSeed(42, 3, HotelPhase.Day, "t1", "exploration", 5);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentComponent_Changes_Result()
        {
            int a = WorkDeterminism.DeriveExplorationSeed(42, 3, HotelPhase.Day, "t1", "exploration", 5);
            int b = WorkDeterminism.DeriveExplorationSeed(43, 3, HotelPhase.Day, "t1", "exploration", 5);
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void Roll_Within_Range()
        {
            int seed = WorkDeterminism.DeriveExplorationSeed(1, 1, HotelPhase.Day, "t", "exploration", 0);
            int roll = WorkDeterminism.RollExplorationOutput(seed, 1, 3);
            Assert.GreaterOrEqual(roll, 1);
            Assert.LessOrEqual(roll, 3);
        }

        [Test]
        public void Roll_Reload_Recomputes_SameValue()
        {
            int s1 = WorkDeterminism.DeriveExplorationSeed(9, 2, HotelPhase.Night, "tA", "exploration", 4);
            int r1 = WorkDeterminism.RollExplorationOutput(s1, 1, 3);
            int s2 = WorkDeterminism.DeriveExplorationSeed(9, 2, HotelPhase.Night, "tA", "exploration", 4);
            int r2 = WorkDeterminism.RollExplorationOutput(s2, 1, 3);
            Assert.AreEqual(r1, r2);
        }
    }
}
```

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkMitigationResolverTests.cs`：

```csharp
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkMitigationResolverTests
    {
        private static WorkMitigationContext NightContext(
            int watch = 0, bool security = false, int patrol = 0, int organizing = 0)
        {
            return new WorkMitigationContext
            {
                IsValid = true,
                Phase = HotelPhase.Night,
                ActiveWatchCount = watch,
                SecurityTeamActive = security,
                ActivePatrolCount = patrol,
                ActiveOrganizingCount = organizing,
                WatchMitigationPercent = 40,
                SecurityOverridePercent = 60,
                PatrolReductionPercentPerActive = 25,
                PatrolReductionCapPercent = 75,
                OrganizingReductionPercentPerActive = 20,
                OrganizingReductionCapPercent = 60
            };
        }

        [Test]
        public void MissingContext_Returns_OriginalDelta()
        {
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, null));
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, default(WorkMitigationContext)));
        }

        [Test]
        public void HealingDirection_Is_Not_Mitigated()
        {
            WorkMitigationContext ctx = NightContext(watch: 1);
            Assert.AreEqual(-5f, WorkMitigationResolver.Compute(-5f, EffectTarget.OwnerTenant, ctx));
        }

        [Test]
        public void Watch_Applies_40Percent_At_Night()
        {
            WorkMitigationContext ctx = NightContext(watch: 1);
            Assert.AreEqual(6.0, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, ctx), 0.001);
        }

        [Test]
        public void Security_Team_Overrides_To_60Percent()
        {
            WorkMitigationContext ctx = NightContext(watch: 1, security: true);
            Assert.AreEqual(4.0, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, ctx), 0.001);
        }

        [Test]
        public void Security_Replaces_Not_Adds()
        {
            WorkMitigationContext ctx = NightContext(watch: 1, security: true);
            // 若错误叠加会得到 0（10 - 100%）；正确语义是取代 40% → 4
            Assert.AreEqual(4.0, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, ctx), 0.001);
        }

        [Test]
        public void Multiple_Watchers_Do_Not_Stack()
        {
            WorkMitigationContext ctx = NightContext(watch: 3);
            Assert.AreEqual(6.0, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, ctx), 0.001);
        }

        [Test]
        public void Watch_Does_Not_Mitigate_At_Day()
        {
            var ctx = NightContext(watch: 1);
            ctx.Phase = HotelPhase.Day;
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.OwnerTenant, ctx));
        }

        [Test]
        public void Patrol_Accumulates_With_Cap_75()
        {
            WorkMitigationContext ctx = NightContext(patrol: 2);
            Assert.AreEqual(50.0, WorkMitigationResolver.Compute(100f, EffectTarget.SameFloorTenants, ctx), 0.001);

            WorkMitigationContext capped = NightContext(patrol: 4);
            Assert.AreEqual(25.0, WorkMitigationResolver.Compute(100f, EffectTarget.SameFloorTenants, capped), 0.001);
        }

        [Test]
        public void Organizing_Accumulates_With_Cap_60()
        {
            WorkMitigationContext ctx = NightContext(organizing: 2);
            Assert.AreEqual(60.0, WorkMitigationResolver.Compute(100f, EffectTarget.AllAssignedTenants, ctx), 0.001);

            WorkMitigationContext capped = NightContext(organizing: 3);
            Assert.AreEqual(40.0, WorkMitigationResolver.Compute(100f, EffectTarget.AllAssignedTenants, capped), 0.001);
        }

        [Test]
        public void Unsupported_Targets_Are_Not_Mitigated()
        {
            WorkMitigationContext ctx = NightContext(watch: 1, patrol: 2, organizing: 2);
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.SameRoomOtherTenants, ctx));
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.ByPlayerFlag, ctx));
            Assert.AreEqual(10f, WorkMitigationResolver.Compute(10f, EffectTarget.RandomAssignedTenants, ctx));
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkDeterminismTests` 与 `WorkMitigationResolverTests`。
Expected: 编译失败（`WorkDeterminism`/`WorkMitigationContext`/`WorkMitigationResolver` 不存在），即红灯。

- [ ] **Step 3: 最小实现**

创建 `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkDeterminism.cs`：

```csharp
namespace Hotel.Runtime
{
    public static class WorkDeterminism
    {
        public static int StableHash(string s)
        {
            unchecked
            {
                int h = 17;
                if (s != null)
                {
                    for (int i = 0; i < s.Length; i++)
                        h = h * 31 + (int)s[i];
                }
                return h;
            }
        }

        public static int DeriveExplorationSeed(int runSeed, int day, HotelPhase phase, string tenantId, string jobId, int sequence)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + runSeed;
                h = h * 31 + day;
                h = h * 31 + ((int)phase + 1);
                h = h * 31 + StableHash(tenantId);
                h = h * 31 + StableHash(jobId);
                h = h * 31 + sequence;

                uint z = (uint)h ^ 0x9E3779B9u;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z ^= z >> 16;
                return (int)z;
            }
        }

        public static int RollExplorationOutput(int seed, int min, int max)
        {
            if (max < min) max = min;
            return min + new System.Random(seed).Next(0, max - min + 1);
        }
    }
}
```

创建 `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationContext.cs`：

```csharp
namespace Hotel.Runtime
{
    public struct WorkMitigationContext
    {
        public bool IsValid;
        public HotelPhase Phase;
        public int ActiveWatchCount;
        public bool SecurityTeamActive;
        public int ActivePatrolCount;
        public int ActiveOrganizingCount;
        public int WatchMitigationPercent;
        public int SecurityOverridePercent;
        public int PatrolReductionPercentPerActive;
        public int PatrolReductionCapPercent;
        public int OrganizingReductionPercentPerActive;
        public int OrganizingReductionCapPercent;
    }
}
```

创建 `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkMitigationResolver.cs`：

```csharp
using System;

namespace Hotel.Runtime
{
    public static class WorkMitigationResolver
    {
        public static float Compute(float delta, EffectTarget target, WorkMitigationContext? context)
        {
            if (context == null || !context.Value.IsValid)
                return delta;
            if (delta <= 0f)
                return delta;

            WorkMitigationContext ctx = context.Value;
            switch (target)
            {
                case EffectTarget.OwnerTenant:
                    if (ctx.Phase == HotelPhase.Night && ctx.ActiveWatchCount > 0)
                    {
                        int percent = ctx.SecurityTeamActive ? ctx.SecurityOverridePercent : ctx.WatchMitigationPercent;
                        return delta * (1f - percent / 100f);
                    }
                    return delta;
                case EffectTarget.SameFloorTenants:
                    if (ctx.Phase == HotelPhase.Night && ctx.ActivePatrolCount > 0)
                    {
                        int reduction = Math.Min(ctx.ActivePatrolCount * ctx.PatrolReductionPercentPerActive, ctx.PatrolReductionCapPercent);
                        return delta * (1f - reduction / 100f);
                    }
                    return delta;
                case EffectTarget.AllAssignedTenants:
                    if (ctx.ActiveOrganizingCount > 0)
                    {
                        int reduction = Math.Min(ctx.ActiveOrganizingCount * ctx.OrganizingReductionPercentPerActive, ctx.OrganizingReductionCapPercent);
                        return delta * (1f - reduction / 100f);
                    }
                    return delta;
                default:
                    return delta;
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkDeterminismTests` 与 `WorkMitigationResolverTests`。
Expected: 4 + 11 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

---

### Task 6: WorkSettlementCalculator（半日结算生产/消耗/治疗纯数学）

**Files:**
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkSettlementCalculator.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkSettlementCalculatorTests.cs`

**Interfaces:**
- Consumes: `WorkSnapshotTenant`（Task 3）、`WorkDeterminism`（Task 5）。
- Produces:
  - `WorkSettlementConfig`（public sealed class，字段默认值即规格 §3.2 初始值）：`IngredientCostPerSettlement=1`、`FoodPerIngredient=2`、`RepairCostCurrency=2`、`RepairRestoreDurability=10`、`TradeCostCurrency=2`、`TradeOutputResources=1`、`FarmOutputIngredients=2`、`ExplorationMin=1`、`ExplorationMax=3`、`HealPercentPerSettlement=2`。
  - `WorkSettlementInputs { int Day; HotelPhase Phase; int RunSeed; int Sequence; IReadOnlyList<WorkSnapshotTenant> Participants; WorkSettlementConfig Config; int IngredientsAmount; int CurrencyAmount; float FacilityDurability; float OutputMultiplier; int HealPercentBonus; IReadOnlyDictionary<string,float> AssignedTenantErosion; }`。
  - `TenantHealOp { string TenantId; int Heal; }`、`WorkSettlementPlan { int FoodDelta; int IngredientsDelta; int CurrencyDelta; int ResourcesDelta; float FacilityDurabilityDelta; List<TenantHealOp> Heals; int ProducedFood; int ProducedIngredients; int ProducedResources; int ConsumedIngredients; int ConsumedCurrency; float RestoredDurability; int SkippedRepairInsufficientCurrency; int SkippedTradeInsufficientCurrency; }`。
  - `WorkSettlementCalculator.Compute(WorkSettlementInputs) : WorkSettlementPlan`——Task 10 消费并映射为 `RunChange`。
  - 结算语义（与规格 §4.5 一致）：cooking 每租客消耗 `min(1, 食材存量)` 产出食物 `消耗量×2`；repair 在 `耐久<100` 且货币足时消耗 2 恢复 10（上限 100），不足跳过；trade 货币足时消耗 2 产出 `floor(1×倍率)` 物资；farming 产出 `floor(2×倍率)` 食材；exploration 产出 `floor(种子掷点×倍率)` 物资；medical 对每个已分配租客 `heal = floor(侵蚀 × (healPercentPerSettlement + HealPercentBonus) / 100)` 逐医疗租客求和；`watch`/`patrol`/`organizing`/`chores`/未知/空 JobId 无直接效果。取整：资源产出向下取整；消耗取 `min(需求, 存量)`。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkSettlementCalculatorTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkSettlementCalculatorTests
    {
        private static WorkSnapshotTenant Tenant(string id, string job) =>
            new WorkSnapshotTenant { TenantId = id, JobId = job };

        private static WorkSettlementInputs Inputs(
            IReadOnlyList<WorkSnapshotTenant> participants,
            int ingredients = 10,
            int currency = 10,
            float durability = 100f,
            float multiplier = 1f,
            int healBonus = 0,
            IReadOnlyDictionary<string, float> erosion = null)
        {
            return new WorkSettlementInputs
            {
                Day = 2,
                Phase = HotelPhase.Day,
                RunSeed = 5,
                Sequence = 3,
                Participants = participants,
                Config = new WorkSettlementConfig(),
                IngredientsAmount = ingredients,
                CurrencyAmount = currency,
                FacilityDurability = durability,
                OutputMultiplier = multiplier,
                HealPercentBonus = healBonus,
                AssignedTenantErosion = erosion ?? new Dictionary<string, float>()
            };
        }

        [Test]
        public void Cooking_ConsumesIngredients_ProducesFood()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "cooking") }, ingredients: 3));
            Assert.AreEqual(-1, plan.IngredientsDelta);
            Assert.AreEqual(2, plan.FoodDelta);
            Assert.AreEqual(1, plan.ConsumedIngredients);
            Assert.AreEqual(2, plan.ProducedFood);
        }

        [Test]
        public void Cooking_Shortage_PartialOutput()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "cooking") }, ingredients: 0));
            Assert.AreEqual(0, plan.IngredientsDelta);
            Assert.AreEqual(0, plan.FoodDelta);
        }

        [Test]
        public void Repair_ConsumesCurrency_RestoresUpTo100()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "repair") }, currency: 5, durability: 90f));
            Assert.AreEqual(-2, plan.CurrencyDelta);
            Assert.AreEqual(10f, plan.FacilityDurabilityDelta, 0.001f);
        }

        [Test]
        public void Repair_CapsAt100()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "repair") }, currency: 5, durability: 95f));
            Assert.AreEqual(-2, plan.CurrencyDelta);
            Assert.AreEqual(5f, plan.FacilityDurabilityDelta, 0.001f);
        }

        [Test]
        public void Repair_InsufficientCurrency_Skips()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "repair") }, currency: 1, durability: 50f));
            Assert.AreEqual(0, plan.CurrencyDelta);
            Assert.AreEqual(0f, plan.FacilityDurabilityDelta);
            Assert.AreEqual(1, plan.SkippedRepairInsufficientCurrency);
        }

        [Test]
        public void Repair_AtFullDurability_Skips()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "repair") }, currency: 5, durability: 100f));
            Assert.AreEqual(0, plan.CurrencyDelta);
            Assert.AreEqual(0f, plan.FacilityDurabilityDelta);
        }

        [Test]
        public void Trade_ConsumesCurrency_ProducesResources()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "trade") }, currency: 5));
            Assert.AreEqual(-2, plan.CurrencyDelta);
            Assert.AreEqual(1, plan.ResourcesDelta);
        }

        [Test]
        public void Trade_InsufficientCurrency_Skips()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "trade") }, currency: 1));
            Assert.AreEqual(0, plan.CurrencyDelta);
            Assert.AreEqual(0, plan.ResourcesDelta);
            Assert.AreEqual(1, plan.SkippedTradeInsufficientCurrency);
        }

        [Test]
        public void Farming_ProducesIngredients()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(new[] { Tenant("t1", "farming") }));
            Assert.AreEqual(2, plan.IngredientsDelta);
            Assert.AreEqual(2, plan.ProducedIngredients);
        }

        [Test]
        public void Exploration_Output_WithinRange_And_Deterministic()
        {
            var participants = new[] { Tenant("tX", "exploration") };
            var a = WorkSettlementCalculator.Compute(Inputs(participants));
            var b = WorkSettlementCalculator.Compute(Inputs(participants));
            Assert.AreEqual(a.ResourcesDelta, b.ResourcesDelta);
            Assert.GreaterOrEqual(a.ResourcesDelta, 1);
            Assert.LessOrEqual(a.ResourcesDelta, 3);
        }

        [Test]
        public void Logistics_Multiplier_Floors_Outputs()
        {
            var plan = WorkSettlementCalculator.Compute(Inputs(
                new[] { Tenant("t1", "trade"), Tenant("t2", "farming") }, currency: 10, multiplier: 1.5f));
            Assert.AreEqual(1, plan.ResourcesDelta);   // floor(1*1.5)
            Assert.AreEqual(3, plan.IngredientsDelta); // floor(2*1.5)
        }

        [Test]
        public void Medical_Heals_Each_Assigned_Tenant()
        {
            var erosion = new Dictionary<string, float> { { "t1", 50f }, { "t2", 0f } };
            var plan = WorkSettlementCalculator.Compute(Inputs(
                new[] { Tenant("m1", "medical") }, erosion: erosion));
            Assert.AreEqual(1, plan.Heals.Count);
            Assert.AreEqual("t1", plan.Heals[0].TenantId);
            Assert.AreEqual(1, plan.Heals[0].Heal); // floor(50*2/100)
        }

        [Test]
        public void Medical_TeamBonus_Adds_To_Percent()
        {
            var erosion = new Dictionary<string, float> { { "t1", 50f } };
            var plan = WorkSettlementCalculator.Compute(Inputs(
                new[] { Tenant("m1", "medical") }, healBonus: 2, erosion: erosion));
            Assert.AreEqual(1, plan.Heals.Count);
            Assert.AreEqual(2, plan.Heals[0].Heal); // floor(50*4/100)
        }

        [Test]
        public void Medical_Sums_Across_Medical_Tenants()
        {
            var erosion = new Dictionary<string, float> { { "t1", 50f } };
            var plan = WorkSettlementCalculator.Compute(Inputs(
                new[] { Tenant("m1", "medical"), Tenant("m2", "medical") }, erosion: erosion));
            Assert.AreEqual(1, plan.Heals.Count);
            Assert.AreEqual(2, plan.Heals[0].Heal); // 1 + 1
        }

        [Test]
        public void Chores_And_Unassigned_Have_No_Effect()
        {
            var participants = new[]
            {
                Tenant("c1", "chores"),
                Tenant("u1", null),
                Tenant("u2", ""),
                Tenant("w1", "watch"),
                Tenant("p1", "patrol"),
                Tenant("o1", "organizing"),
                Tenant("z1", "unknown_job")
            };
            var plan = WorkSettlementCalculator.Compute(Inputs(participants));
            Assert.AreEqual(0, plan.FoodDelta);
            Assert.AreEqual(0, plan.IngredientsDelta);
            Assert.AreEqual(0, plan.CurrencyDelta);
            Assert.AreEqual(0, plan.ResourcesDelta);
            Assert.AreEqual(0f, plan.FacilityDurabilityDelta);
            Assert.AreEqual(0, plan.Heals.Count);
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkSettlementCalculatorTests`。
Expected: 编译失败（`WorkSettlementCalculator` 等类型不存在），即红灯。

- [ ] **Step 3: 最小实现**

创建 `Assets/Scripts/Hotel/Runtime/Kernel/Work/WorkSettlementCalculator.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Hotel.Runtime
{
    public sealed class WorkSettlementConfig
    {
        public int IngredientCostPerSettlement = 1;
        public int FoodPerIngredient = 2;
        public int RepairCostCurrency = 2;
        public int RepairRestoreDurability = 10;
        public int TradeCostCurrency = 2;
        public int TradeOutputResources = 1;
        public int FarmOutputIngredients = 2;
        public int ExplorationMin = 1;
        public int ExplorationMax = 3;
        public int HealPercentPerSettlement = 2;
    }

    public sealed class WorkSettlementInputs
    {
        public int Day;
        public HotelPhase Phase;
        public int RunSeed;
        public int Sequence;
        public IReadOnlyList<WorkSnapshotTenant> Participants = new List<WorkSnapshotTenant>();
        public WorkSettlementConfig Config = new WorkSettlementConfig();
        public int IngredientsAmount;
        public int CurrencyAmount;
        public float FacilityDurability = 100f;
        public float OutputMultiplier = 1f;
        public int HealPercentBonus;
        public IReadOnlyDictionary<string, float> AssignedTenantErosion = new Dictionary<string, float>();
    }

    public struct TenantHealOp
    {
        public string TenantId;
        public int Heal;
    }

    public sealed class WorkSettlementPlan
    {
        public int FoodDelta;
        public int IngredientsDelta;
        public int CurrencyDelta;
        public int ResourcesDelta;
        public float FacilityDurabilityDelta;
        public List<TenantHealOp> Heals = new List<TenantHealOp>();
        public int ProducedFood;
        public int ProducedIngredients;
        public int ProducedResources;
        public int ConsumedIngredients;
        public int ConsumedCurrency;
        public float RestoredDurability;
        public int SkippedRepairInsufficientCurrency;
        public int SkippedTradeInsufficientCurrency;
    }

    public static class WorkSettlementCalculator
    {
        public static WorkSettlementPlan Compute(WorkSettlementInputs inputs)
        {
            var plan = new WorkSettlementPlan();
            if (inputs == null)
                return plan;

            WorkSettlementConfig cfg = inputs.Config ?? new WorkSettlementConfig();
            int ingredientsLeft = inputs.IngredientsAmount;
            int currencyLeft = inputs.CurrencyAmount;
            float durability = inputs.FacilityDurability;

            IReadOnlyList<WorkSnapshotTenant> participants = inputs.Participants ?? new List<WorkSnapshotTenant>();
            for (int i = 0; i < participants.Count; i++)
            {
                WorkSnapshotTenant p = participants[i];
                if (p == null || string.IsNullOrEmpty(p.JobId))
                    continue;

                switch (p.JobId)
                {
                    case "cooking":
                    {
                        int consume = Math.Min(cfg.IngredientCostPerSettlement, ingredientsLeft);
                        if (consume > 0)
                        {
                            int produce = consume * cfg.FoodPerIngredient;
                            plan.ConsumedIngredients += consume;
                            plan.ProducedFood += produce;
                            plan.FoodDelta += produce;
                            plan.IngredientsDelta -= consume;
                            ingredientsLeft -= consume;
                        }
                        break;
                    }
                    case "repair":
                    {
                        if (durability >= 100f)
                            break;
                        if (currencyLeft < cfg.RepairCostCurrency)
                        {
                            plan.SkippedRepairInsufficientCurrency++;
                            break;
                        }
                        float restore = Math.Min(cfg.RepairRestoreDurability, 100f - durability);
                        plan.ConsumedCurrency += cfg.RepairCostCurrency;
                        plan.CurrencyDelta -= cfg.RepairCostCurrency;
                        currencyLeft -= cfg.RepairCostCurrency;
                        plan.RestoredDurability += restore;
                        plan.FacilityDurabilityDelta += restore;
                        durability += restore;
                        break;
                    }
                    case "trade":
                    {
                        if (currencyLeft < cfg.TradeCostCurrency)
                        {
                            plan.SkippedTradeInsufficientCurrency++;
                            break;
                        }
                        plan.ConsumedCurrency += cfg.TradeCostCurrency;
                        plan.CurrencyDelta -= cfg.TradeCostCurrency;
                        currencyLeft -= cfg.TradeCostCurrency;
                        int output = (int)Math.Floor(inputs.OutputMultiplier * cfg.TradeOutputResources);
                        plan.ProducedResources += output;
                        plan.ResourcesDelta += output;
                        break;
                    }
                    case "farming":
                    {
                        int output = (int)Math.Floor(inputs.OutputMultiplier * cfg.FarmOutputIngredients);
                        plan.ProducedIngredients += output;
                        plan.IngredientsDelta += output;
                        break;
                    }
                    case "exploration":
                    {
                        int seed = WorkDeterminism.DeriveExplorationSeed(
                            inputs.RunSeed, inputs.Day, inputs.Phase, p.TenantId, p.JobId, inputs.Sequence);
                        int roll = WorkDeterminism.RollExplorationOutput(seed, cfg.ExplorationMin, cfg.ExplorationMax);
                        int output = (int)Math.Floor(inputs.OutputMultiplier * roll);
                        plan.ProducedResources += output;
                        plan.ResourcesDelta += output;
                        break;
                    }
                    default:
                        break; // watch / patrol / organizing / chores / unknown: no direct effect
                }
            }

            ApplyHealing(inputs, cfg, plan, participants);
            return plan;
        }

        private static void ApplyHealing(WorkSettlementInputs inputs, WorkSettlementConfig cfg, WorkSettlementPlan plan, IReadOnlyList<WorkSnapshotTenant> participants)
        {
            int medicalCount = 0;
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i] != null && participants[i].JobId == "medical")
                    medicalCount++;
            }
            if (medicalCount == 0)
                return;

            int healPercent = cfg.HealPercentPerSettlement + inputs.HealPercentBonus;
            if (healPercent <= 0)
                return;

            IReadOnlyDictionary<string, float> erosion = inputs.AssignedTenantErosion ?? new Dictionary<string, float>();
            foreach (var pair in erosion)
            {
                if (pair.Value <= 0f)
                    continue;
                int healSum = 0;
                for (int m = 0; m < medicalCount; m++)
                    healSum += (int)Math.Floor(pair.Value * healPercent / 100.0);
                if (healSum > 0)
                    plan.Heals.Add(new TenantHealOp { TenantId = pair.Key, Heal = healSum });
            }
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkSettlementCalculatorTests`。
Expected: 14 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

### Task 7: Hotel.Authoring 配置定义（JobDefinition / TeamComboDefinition / WorkCatalog / 求值器）

**Files:**
- Create: `Assets/Scripts/Hotel/Authoring/Work/JobDefinition.cs`
- Create: `Assets/Scripts/Hotel/Authoring/Work/TeamComboDefinition.cs`
- Create: `Assets/Scripts/Hotel/Authoring/Work/WorkCatalog.cs`
- Create: `Assets/Scripts/Hotel/Authoring/Work/JobCompatibility.cs`
- Create: `Assets/Scripts/Hotel/Authoring/Work/TeamComboEvaluator.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkAuthoringDefinitionTests.cs`

**Interfaces:**
- Consumes: `TenantAbility`/`TenantActivityType`/`WorkSnapshotTenant`（Hotel.Runtime）、`ScriptableObject`。
- Produces（均属命名空间 `Hotel.Authoring.Work`）：
  - `JobDefinition : ScriptableObject`（`jobId`、`displayName`、`activityWindow : TenantActivityType`、`allowedTags : List<TenantAbility>` + §3.2 全部数值字段：`ingredientCostPerSettlement`/`foodPerIngredient`/`repairCostCurrency`/`repairRestoreDurability`/`tradeCostCurrency`/`tradeOutputResources`/`farmOutputIngredients`/`explorationMin`/`explorationMax`/`healPercentPerSettlement`/`watchNightLossMitigationPercent`/`floorSpreadReductionPerPatrol`/`floorSpreadReductionCap`/`buildingSpreadReductionPerOrganizer`/`buildingSpreadReductionCap`）。
  - `TeamEffectKind : enum { NightLossMitigationOverride, HealPercentBonus, OutputMultiplier }`、`TeamRole { TenantAbility tag; string jobId; }`、`TeamEffect { TeamEffectKind kind; float value; }`、`TeamComboDefinition : ScriptableObject`（`comboId`、`displayName`、`roles : List<TeamRole>`、`effects : List<TeamEffect>`、`bool TryGetEffect(TeamEffectKind kind, out float value)`）。
  - `WorkCatalog : ScriptableObject`（`List<JobDefinition> jobs`、`List<TeamComboDefinition> teams`、`JobDefinition FindJob(string jobId)`）——Task 9/10/12 使用。
  - `JobCompatibility.IsAllowed(JobDefinition job, TenantAbility ability) : bool`（空/Null 列表 → 仅 `None`；否则 `allowedTags.Contains(ability)`）——Task 10/12 使用。
  - `TeamComboEvaluator.IsActive(TeamComboDefinition combo, IReadOnlyList<WorkSnapshotTenant> participants) : bool`（每个 role 都存在「能力==role.tag 且 JobId==role.jobId」的参与者；缺任一 role 即 false；combo/roles 为空 → false）——Task 10 使用。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkAuthoringDefinitionTests.cs`：

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Hotel.Runtime;
using Hotel.Authoring.Work;

namespace Hotel.Runtime.Tests
{
    public class WorkAuthoringDefinitionTests
    {
        private static JobDefinition Job(string id, TenantActivityType window, params TenantAbility[] tags)
        {
            var job = ScriptableObject.CreateInstance<JobDefinition>();
            job.jobId = id;
            job.displayName = id;
            job.activityWindow = window;
            job.allowedTags = new List<TenantAbility>(tags);
            return job;
        }

        [Test]
        public void Chores_EmptyTags_Means_NoneOnly()
        {
            JobDefinition chores = Job("chores", TenantActivityType.AllDay);
            Assert.AreEqual(0, chores.allowedTags.Count);
            Assert.IsTrue(JobCompatibility.IsAllowed(chores, TenantAbility.None));
            Assert.IsFalse(JobCompatibility.IsAllowed(chores, TenantAbility.Doctor));
        }

        [Test]
        public void Tagged_Job_Accepts_Only_Its_Tag()
        {
            JobDefinition cooking = Job("cooking", TenantActivityType.DayActive, TenantAbility.Cook);
            Assert.IsTrue(JobCompatibility.IsAllowed(cooking, TenantAbility.Cook));
            Assert.IsFalse(JobCompatibility.IsAllowed(cooking, TenantAbility.None));
            Assert.IsFalse(JobCompatibility.IsAllowed(cooking, TenantAbility.Doctor));
        }

        [Test]
        public void WorkCatalog_FindJob_Returns_MatchingOrNull()
        {
            var catalog = ScriptableObject.CreateInstance<WorkCatalog>();
            JobDefinition cooking = Job("cooking", TenantActivityType.DayActive, TenantAbility.Cook);
            catalog.jobs.Add(cooking);
            Assert.AreEqual(cooking, catalog.FindJob("cooking"));
            Assert.IsNull(catalog.FindJob("bogus"));
        }

        [Test]
        public void TeamCombo_AllRoles_Match_Activates()
        {
            var combo = ScriptableObject.CreateInstance<TeamComboDefinition>();
            combo.comboId = "medical_team";
            combo.roles = new List<TeamRole>
            {
                new TeamRole { tag = TenantAbility.Doctor, jobId = "medical" },
                new TeamRole { tag = TenantAbility.Cook, jobId = "cooking" }
            };
            var participants = new List<WorkSnapshotTenant>
            {
                new WorkSnapshotTenant { TenantId = "a", JobId = "medical", Ability = TenantAbility.Doctor },
                new WorkSnapshotTenant { TenantId = "b", JobId = "cooking", Ability = TenantAbility.Cook }
            };
            Assert.IsTrue(TeamComboEvaluator.IsActive(combo, participants));
        }

        [Test]
        public void TeamCombo_MissingRole_Deactivates()
        {
            var combo = ScriptableObject.CreateInstance<TeamComboDefinition>();
            combo.comboId = "medical_team";
            combo.roles = new List<TeamRole>
            {
                new TeamRole { tag = TenantAbility.Doctor, jobId = "medical" },
                new TeamRole { tag = TenantAbility.Cook, jobId = "cooking" }
            };
            var participants = new List<WorkSnapshotTenant>
            {
                new WorkSnapshotTenant { TenantId = "a", JobId = "medical", Ability = TenantAbility.Doctor }
            };
            Assert.IsFalse(TeamComboEvaluator.IsActive(combo, participants));
        }

        [Test]
        public void TeamCombo_SameTag_WrongJob_Does_Not_Match()
        {
            var combo = ScriptableObject.CreateInstance<TeamComboDefinition>();
            combo.comboId = "medical_team";
            combo.roles = new List<TeamRole>
            {
                new TeamRole { tag = TenantAbility.Doctor, jobId = "medical" }
            };
            var participants = new List<WorkSnapshotTenant>
            {
                new WorkSnapshotTenant { TenantId = "a", JobId = "cooking", Ability = TenantAbility.Doctor }
            };
            Assert.IsFalse(TeamComboEvaluator.IsActive(combo, participants));
        }

        [Test]
        public void TeamCombo_EmptyRoles_Deactivates()
        {
            var combo = ScriptableObject.CreateInstance<TeamComboDefinition>();
            combo.comboId = "empty_team";
            combo.roles = new List<TeamRole>();
            var participants = new List<WorkSnapshotTenant>
            {
                new WorkSnapshotTenant { TenantId = "a", JobId = "medical", Ability = TenantAbility.Doctor }
            };
            Assert.IsFalse(TeamComboEvaluator.IsActive(combo, participants));
        }

        [Test]
        public void TeamCombo_TryGetEffect_Returns_ConfigValue()
        {
            var combo = ScriptableObject.CreateInstance<TeamComboDefinition>();
            combo.effects = new List<TeamEffect>
            {
                new TeamEffect { kind = TeamEffectKind.HealPercentBonus, value = 2f }
            };
            float value;
            Assert.IsTrue(combo.TryGetEffect(TeamEffectKind.HealPercentBonus, out value));
            Assert.AreEqual(2f, value);
            Assert.IsFalse(combo.TryGetEffect(TeamEffectKind.OutputMultiplier, out value));
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkAuthoringDefinitionTests`。
Expected: 编译失败（`Hotel.Authoring.Work` 命名空间不存在），即红灯。

- [ ] **Step 3: 最小实现**

创建 `Assets/Scripts/Hotel/Authoring/Work/JobDefinition.cs`：

```csharp
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

namespace Hotel.Authoring.Work
{
    [CreateAssetMenu(fileName = "JobDefinition", menuName = "Hotel/Job Definition")]
    public sealed class JobDefinition : ScriptableObject
    {
        public string jobId;
        public string displayName;
        public TenantActivityType activityWindow = TenantActivityType.DayActive;
        public List<TenantAbility> allowedTags = new List<TenantAbility>();

        [Header("cooking")]
        public int ingredientCostPerSettlement = 1;
        public int foodPerIngredient = 2;

        [Header("repair")]
        public int repairCostCurrency = 2;
        public int repairRestoreDurability = 10;

        [Header("trade")]
        public int tradeCostCurrency = 2;
        public int tradeOutputResources = 1;

        [Header("farming")]
        public int farmOutputIngredients = 2;

        [Header("exploration")]
        public int explorationMin = 1;
        public int explorationMax = 3;

        [Header("medical")]
        public int healPercentPerSettlement = 2;

        [Header("watch")]
        public int watchNightLossMitigationPercent = 40;

        [Header("patrol")]
        public int floorSpreadReductionPerPatrol = 25;
        public int floorSpreadReductionCap = 75;

        [Header("organizing")]
        public int buildingSpreadReductionPerOrganizer = 20;
        public int buildingSpreadReductionCap = 60;
    }
}
```

创建 `Assets/Scripts/Hotel/Authoring/Work/TeamComboDefinition.cs`：

```csharp
using System;
using System.Collections.Generic;
using Hotel.Runtime;
using UnityEngine;

namespace Hotel.Authoring.Work
{
    public enum TeamEffectKind
    {
        NightLossMitigationOverride,
        HealPercentBonus,
        OutputMultiplier
    }

    [Serializable]
    public sealed class TeamRole
    {
        public TenantAbility tag;
        public string jobId;
    }

    [Serializable]
    public sealed class TeamEffect
    {
        public TeamEffectKind kind;
        public float value;
    }

    [CreateAssetMenu(fileName = "TeamComboDefinition", menuName = "Hotel/Team Combo Definition")]
    public sealed class TeamComboDefinition : ScriptableObject
    {
        public string comboId;
        public string displayName;
        public List<TeamRole> roles = new List<TeamRole>();
        public List<TeamEffect> effects = new List<TeamEffect>();

        public bool TryGetEffect(TeamEffectKind kind, out float value)
        {
            value = 0f;
            if (effects == null) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                TeamEffect effect = effects[i];
                if (effect != null && effect.kind == kind)
                {
                    value = effect.value;
                    return true;
                }
            }
            return false;
        }
    }
}
```

创建 `Assets/Scripts/Hotel/Authoring/Work/WorkCatalog.cs`：

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Hotel.Authoring.Work
{
    [CreateAssetMenu(fileName = "WorkCatalog", menuName = "Hotel/Work Catalog")]
    public sealed class WorkCatalog : ScriptableObject
    {
        public List<JobDefinition> jobs = new List<JobDefinition>();
        public List<TeamComboDefinition> teams = new List<TeamComboDefinition>();

        public JobDefinition FindJob(string jobId)
        {
            if (string.IsNullOrEmpty(jobId) || jobs == null) return null;
            for (int i = 0; i < jobs.Count; i++)
            {
                JobDefinition job = jobs[i];
                if (job != null && job.jobId == jobId) return job;
            }
            return null;
        }
    }
}
```

创建 `Assets/Scripts/Hotel/Authoring/Work/JobCompatibility.cs`：

```csharp
using System.Collections.Generic;
using Hotel.Runtime;

namespace Hotel.Authoring.Work
{
    public static class JobCompatibility
    {
        public static bool IsAllowed(JobDefinition job, TenantAbility ability)
        {
            if (job == null) return false;
            if (job.allowedTags == null || job.allowedTags.Count == 0)
                return ability == TenantAbility.None;
            return job.allowedTags.Contains(ability);
        }
    }
}
```

创建 `Assets/Scripts/Hotel/Authoring/Work/TeamComboEvaluator.cs`：

```csharp
using System.Collections.Generic;
using Hotel.Runtime;

namespace Hotel.Authoring.Work
{
    public static class TeamComboEvaluator
    {
        public static bool IsActive(TeamComboDefinition combo, IReadOnlyList<WorkSnapshotTenant> participants)
        {
            if (combo == null || combo.roles == null || combo.roles.Count == 0)
                return false;
            if (participants == null)
                return false;

            for (int r = 0; r < combo.roles.Count; r++)
            {
                TeamRole role = combo.roles[r];
                if (role == null || string.IsNullOrEmpty(role.jobId))
                    return false;
                bool found = false;
                for (int p = 0; p < participants.Count; p++)
                {
                    WorkSnapshotTenant tenant = participants[p];
                    if (tenant == null)
                        continue;
                    if (tenant.Ability == role.tag && tenant.JobId == role.jobId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkAuthoringDefinitionTests`。
Expected: 8 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

---

### Task 8: RunSaveData SchemaVersion 2 与 v1→v2 迁移

**Files:**
- Modify: `Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkSaveCodecTests.cs`

**Interfaces:**
- Consumes: `WorkSettlementRecord`/`WorkSettlementLedger`（Task 3/4）。
- Produces:
  - `RunSaveData.CurrentSchemaVersion = 2`；新增 `RunSaveData.FacilityDurability : float = 100f`、`WorkSettlementSequence : int`、`WorkSettlements : List<WorkSettlementRecord>`。
  - `RunSaveCodec.FromJson`：接受 schema 1（先迁移再还原）与 2；其他 schema 抛 `InvalidOperationException`。迁移规则：`FacilityDurability = 100`、`WorkSettlementSequence = 0`、`WorkSettlements = 空`；`CloneTenant` 的 `JobId` 归一化为空串（null → `""`，未分配）。
  - `RunSaveCodec.ReadMetadata`：schema 1 或 2 均返回 save（供 `SaveGameService.TryGetSummary` 兼容 v1）。
  - `CreateSnapshot` 写入三个新字段（`WorkSettlements` 转 List 并按 key 排序）；`RestoreSnapshot` 还原为 `Dictionary<string, WorkSettlementRecord>`。

- [ ] **Step 1: 编写失败测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkSaveCodecTests.cs`：

```csharp
using System;
using NUnit.Framework;
using Hotel.Runtime;

namespace Hotel.Runtime.Tests
{
    public class WorkSaveCodecTests
    {
        [Test]
        public void V2_RoundTrip_Preserves_WorkFields()
        {
            var state = GameRunState.New(new RunId("r"), 7);
            state.Resources["ingredients"] = new ResourceRunState { ResourceId = "ingredients", DefinitionId = "ingredients", Amount = 4 };
            state.Resources["resources"] = new ResourceRunState { ResourceId = "resources", DefinitionId = "resources", Amount = 2 };
            state.Tenants["t1"] = new TenantRunState { TenantId = "t1", DefinitionId = "cand_1", RoomId = "room_01", JobId = "cooking" };
            state.FacilityDurability = 55f;
            state.WorkSettlementSequence = 3;
            state.WorkSettlements["3|Night"] = new WorkSettlementRecord { Day = 3, Phase = HotelPhase.Night, Sequence = 3 };

            string json = RunSaveCodec.ToJson(state, DateTime.UtcNow);
            GameRunState restored = RunSaveCodec.FromJson(json);

            Assert.AreEqual(55f, restored.FacilityDurability);
            Assert.AreEqual(3, restored.WorkSettlementSequence);
            Assert.AreEqual(1, restored.WorkSettlements.Count);
            Assert.IsTrue(restored.WorkSettlements.ContainsKey("3|Night"));
            Assert.AreEqual("cooking", restored.Tenants["t1"].JobId);
            Assert.AreEqual(4, restored.Resources["ingredients"].Amount);
        }

        [Test]
        public void V1_Save_Migrates_WorkDefaults()
        {
            const string v1 = "{\"SchemaVersion\":1,\"RunId\":\"r\",\"StateVersion\":0,\"Day\":3,\"Seed\":7,\"Phase\":0,\"PhaseLifecycle\":0,\"PhaseOccurrence\":1}";
            GameRunState state = RunSaveCodec.FromJson(v1);

            Assert.AreEqual(100f, state.FacilityDurability);
            Assert.AreEqual(0, state.WorkSettlementSequence);
            Assert.AreEqual(0, state.WorkSettlements.Count);
        }

        [Test]
        public void V1_Tenant_JobId_Normalized_To_Empty()
        {
            const string v1 = "{\"SchemaVersion\":1,\"RunId\":\"r\",\"Day\":3,\"Seed\":7,\"Phase\":0,\"Tenants\":[{\"TenantId\":\"t1\",\"DefinitionId\":\"c\",\"TrueErosion\":0,\"RoomId\":\"room_01\"}]}";
            GameRunState state = RunSaveCodec.FromJson(v1);
            Assert.IsNotNull(state.Tenants["t1"]);
            Assert.AreEqual(string.Empty, state.Tenants["t1"].JobId);
        }

        [Test]
        public void Unsupported_Schema_Throws()
        {
            const string v3 = "{\"SchemaVersion\":3,\"RunId\":\"r\"}";
            Assert.Throws<InvalidOperationException>(() => RunSaveCodec.FromJson(v3));
        }

        [Test]
        public void ReadMetadata_Accepts_V1_And_V2()
        {
            Assert.NotNull(RunSaveCodec.ReadMetadata("{\"SchemaVersion\":1,\"RunId\":\"r\"}"));
            Assert.NotNull(RunSaveCodec.ReadMetadata("{\"SchemaVersion\":2,\"RunId\":\"r\"}"));
            Assert.IsNull(RunSaveCodec.ReadMetadata("{\"SchemaVersion\":9,\"RunId\":\"r\"}"));
        }
    }
}
```

- [ ] **Step 2: 运行测试确认失败**

Test Runner → EditMode → 运行 `WorkSaveCodecTests`。
Expected: 编译/断言失败（`CurrentSchemaVersion` 仍为 1，`FromJson` 对 schema 1 抛异常、无新字段），即红灯。

- [ ] **Step 3: 最小实现**

`Assets/Scripts/Hotel/Runtime/State/RunSaveData.cs`：

(a) 第 10 行改为 `public const int CurrentSchemaVersion = 2;`。

(b) 第 30 行（`ReviewHistory` 之后）追加：

```csharp
        public float FacilityDurability = 100f;
        public int WorkSettlementSequence;
        public List<WorkSettlementRecord> WorkSettlements = new List<WorkSettlementRecord>();
```

(c) `FromJson`（第 41-55 行）改为：

```csharp
        public static GameRunState FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Save JSON is empty.", nameof(json));

            var save = JsonUtility.FromJson<RunSaveData>(json);
            if (save == null)
                throw new InvalidOperationException("Save JSON could not be read.");
            if (save.SchemaVersion == 1)
                MigrateV1ToV2(save);
            else if (save.SchemaVersion != RunSaveData.CurrentSchemaVersion)
                throw new InvalidOperationException($"Unsupported save schema {save.SchemaVersion}.");
            if (string.IsNullOrWhiteSpace(save.RunId))
                throw new InvalidOperationException("Save is missing its run id.");

            return RestoreSnapshot(save);
        }

        private static void MigrateV1ToV2(RunSaveData save)
        {
            save.SchemaVersion = 2;
            save.FacilityDurability = 100f;
            save.WorkSettlementSequence = 0;
            if (save.WorkSettlements == null)
                save.WorkSettlements = new List<WorkSettlementRecord>();
        }
```

(d) `ReadMetadata`（第 57-62 行）改为：

```csharp
        public static RunSaveData ReadMetadata(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var save = JsonUtility.FromJson<RunSaveData>(json);
            if (save == null) return null;
            return save.SchemaVersion == 1 || save.SchemaVersion == RunSaveData.CurrentSchemaVersion ? save : null;
        }
```

(e) `CreateSnapshot`（第 64-99 行）中 `Summary = CloneSummary(state.Summary)` 之后追加：

```csharp
                FacilityDurability = state.FacilityDurability,
                WorkSettlementSequence = state.WorkSettlementSequence,
```

并在 `save.Buffs.Sort(...)` 之后追加：

```csharp
            foreach (var pair in state.WorkSettlements)
                save.WorkSettlements.Add(new WorkSettlementRecord
                {
                    Day = pair.Value.Day,
                    Phase = pair.Value.Phase,
                    Sequence = pair.Value.Sequence
                });
            save.WorkSettlements.Sort((a, b) => string.CompareOrdinal(
                a.Day + "|" + a.Phase, b.Day + "|" + b.Phase));
```

(f) `RestoreSnapshot`（第 101-153 行）中 `state.Summary = ...` 之后追加：

```csharp
            state.FacilityDurability = save.FacilityDurability;
            state.WorkSettlementSequence = save.WorkSettlementSequence;
            if (save.WorkSettlements != null)
            {
                foreach (var record in save.WorkSettlements)
                {
                    if (record == null) continue;
                    state.WorkSettlements[WorkSettlementLedger.Key(record.Day, record.Phase)] = new WorkSettlementRecord
                    {
                        Day = record.Day,
                        Phase = record.Phase,
                        Sequence = record.Sequence
                    };
                }
            }
```

(g) `CloneTenant`（第 155-167 行）的 `JobId = value.JobId` 改为：

```csharp
                JobId = value.JobId ?? string.Empty,
```

- [ ] **Step 4: 运行测试确认通过**

Test Runner → EditMode → 运行 `WorkSaveCodecTests`。
Expected: 5 项全部 PASS。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

---

### Task 9: 配置资产创建（unitymaster）与资产级测试

**Files:**
- Create（unitymaster，编辑器操作）:
  - `Assets/Data/Configs/Work/Jobs/Job_cooking.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_medical.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_repair.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_watch.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_patrol.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_trade.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_farming.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_exploration.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_organizing.asset`
  - `Assets/Data/Configs/Work/Jobs/Job_chores.asset`
  - `Assets/Data/Configs/Work/Teams/Team_medical_team.asset`
  - `Assets/Data/Configs/Work/Teams/Team_security_team.asset`
  - `Assets/Data/Configs/Work/Teams/Team_logistics_team.asset`
  - `Assets/Data/Configs/Work/WorkCatalog.asset`
  - `Assets/Data/Resources/Ingredients.asset`
  - `Assets/Data/Resources/Resources.asset`
- Test: `Assets/Tests/Hotel.Runtime.Tests/WorkCatalogAssetTests.cs`

**Interfaces:**
- Consumes: Task 7 的 `JobDefinition`/`TeamComboDefinition`/`WorkCatalog`、既有 `ResourceDefinition`（`Hotel.Authoring.Resources`）。
- Produces: 运行时唯一入口 `Assets/Data/Configs/Work/WorkCatalog.asset`（含 10 职业 + 3 团队）；Task 10（结算/缓解上下文/JobId 注册表）与 Task 12（职业分配）读取；`ingredients`/`resources` 资源定义供 `SettlementBridge` 初始化。

- [ ] **Step 1: 创建职业资产（unitymaster）**

在 Project 窗口右键 `Assets/Data/Configs/Work/Jobs`（不存在则逐级新建文件夹）→ Create → Hotel → Job Definition，逐项创建并按下表填写（未列出的数值字段保持脚本默认值不变）：

| 资产 | jobId | displayName | activityWindow | allowedTags | 必须改动的数值字段 |
| --- | --- | --- | --- | --- | --- |
| Job_cooking | `cooking` | 烹饪 | DayActive | `[Cook]` | 默认值即 1/2，无需改 |
| Job_medical | `medical` | 医疗 | AllDay | `[Doctor]` | 默认 2 |
| Job_repair | `repair` | 维修 | DayActive | `[Engineer]` | 默认 2/10 |
| Job_watch | `watch` | 守夜 | NightActive | `[NightWatch]` | `watchNightLossMitigationPercent = 40`（默认即 40） |
| Job_patrol | `patrol` | 巡逻 | NightActive | `[FormerEmployee]` | 默认 25/75 |
| Job_trade | `trade` | 交易 | DayActive | `[Merchant]` | 默认 2/1 |
| Job_farming | `farming` | 农耕 | DayActive | `[Farmer]` | 默认 2 |
| Job_exploration | `exploration` | 探索 | DayActive | `[Driver]` | 默认 1/3 |
| Job_organizing | `organizing` | 整理 | AllDay | `[Teacher]` | 默认 20/60 |
| Job_chores | `chores` | 杂务 | AllDay | （空列表） | — |

全部保存。Expected: 10 个资产在 `Assets/Data/Configs/Work/Jobs/` 下，Inspector 字段与表一致。

- [ ] **Step 2: 创建团队资产（unitymaster）**

右键 `Assets/Data/Configs/Work/Teams` → Create → Hotel → Team Combo Definition，逐项创建：

| 资产 | comboId | displayName | roles（tag → jobId） | effects（kind → value） |
| --- | --- | --- | --- | --- |
| Team_medical_team | `medical_team` | 医疗队 | `Doctor→medical`、`Cook→cooking` | `HealPercentBonus → 2` |
| Team_security_team | `security_team` | 安保队 | `NightWatch→watch`、`FormerEmployee→patrol` | `NightLossMitigationOverride → 60`（已确认） |
| Team_logistics_team | `logistics_team` | 物流队 | `Merchant→trade`、`Farmer→farming`、`Driver→exploration` | `OutputMultiplier → 1.5` |

全部保存。Expected: 3 个资产存在且字段一致。

- [ ] **Step 3: 创建 WorkCatalog 与资源定义（unitymaster）**

1. 右键 `Assets/Data/Configs/Work` → Create → Hotel → Work Catalog，命名 `WorkCatalog`。把 10 个 JobDefinition 拖入 `Jobs` 列表、3 个 TeamComboDefinition 拖入 `Teams` 列表，保存。
2. 右键 `Assets/Data/Resources` → Create → Hotel → Resource Definition：
   - `Ingredients`：`resourceId = ingredients`、`displayName = 食材`、`initialAmount = 0`。
   - `Resources`：`resourceId = resources`、`displayName = 物资`、`initialAmount = 0`。
   保存。

Expected: `Assets/Data/Configs/Work/WorkCatalog.asset` 引用全部 13 个资产；两个资源定义存在。

- [ ] **Step 4: 编写资产级测试**

创建 `Assets/Tests/Hotel.Runtime.Tests/WorkCatalogAssetTests.cs`：

```csharp
using NUnit.Framework;
using UnityEditor;
using Hotel.Runtime;
using Hotel.Authoring.Work;
using Hotel.Authoring.Resources;

namespace Hotel.Runtime.Tests
{
    public class WorkCatalogAssetTests
    {
        private const string CatalogPath = "Assets/Data/Configs/Work/WorkCatalog.asset";

        private static WorkCatalog LoadCatalog()
        {
            WorkCatalog catalog = AssetDatabase.LoadAssetAtPath<WorkCatalog>(CatalogPath);
            Assert.NotNull(catalog, "WorkCatalog asset missing at " + CatalogPath);
            return catalog;
        }

        private static JobDefinition Find(WorkCatalog catalog, string jobId)
        {
            JobDefinition job = catalog.FindJob(jobId);
            Assert.NotNull(job, "Missing job " + jobId);
            return job;
        }

        private static TeamComboDefinition FindTeam(WorkCatalog catalog, string comboId)
        {
            TeamComboDefinition team = null;
            for (int i = 0; i < catalog.teams.Count; i++)
            {
                if (catalog.teams[i] != null && catalog.teams[i].comboId == comboId)
                    team = catalog.teams[i];
            }
            Assert.NotNull(team, "Missing team " + comboId);
            return team;
        }

        [Test]
        public void Catalog_Has_Ten_Jobs_And_Three_Teams()
        {
            WorkCatalog catalog = LoadCatalog();
            Assert.AreEqual(10, catalog.jobs.Count);
            Assert.AreEqual(3, catalog.teams.Count);
        }

        [Test]
        public void Jobs_Have_Expected_ActivityWindows()
        {
            WorkCatalog catalog = LoadCatalog();
            Assert.AreEqual(TenantActivityType.DayActive, Find(catalog, "cooking").activityWindow);
            Assert.AreEqual(TenantActivityType.AllDay, Find(catalog, "medical").activityWindow);
            Assert.AreEqual(TenantActivityType.DayActive, Find(catalog, "repair").activityWindow);
            Assert.AreEqual(TenantActivityType.NightActive, Find(catalog, "watch").activityWindow);
            Assert.AreEqual(TenantActivityType.NightActive, Find(catalog, "patrol").activityWindow);
            Assert.AreEqual(TenantActivityType.DayActive, Find(catalog, "trade").activityWindow);
            Assert.AreEqual(TenantActivityType.DayActive, Find(catalog, "farming").activityWindow);
            Assert.AreEqual(TenantActivityType.DayActive, Find(catalog, "exploration").activityWindow);
            Assert.AreEqual(TenantActivityType.AllDay, Find(catalog, "organizing").activityWindow);
            Assert.AreEqual(TenantActivityType.AllDay, Find(catalog, "chores").activityWindow);
        }

        [Test]
        public void Chores_Only_Accepts_None()
        {
            WorkCatalog catalog = LoadCatalog();
            JobDefinition chores = Find(catalog, "chores");
            Assert.AreEqual(0, chores.allowedTags.Count);
            Assert.IsTrue(JobCompatibility.IsAllowed(chores, TenantAbility.None));
            Assert.IsFalse(JobCompatibility.IsAllowed(chores, TenantAbility.Doctor));
        }

        [Test]
        public void Tags_Map_OneToOne()
        {
            WorkCatalog catalog = LoadCatalog();
            AssertTags(Find(catalog, "cooking"), TenantAbility.Cook);
            AssertTags(Find(catalog, "medical"), TenantAbility.Doctor);
            AssertTags(Find(catalog, "repair"), TenantAbility.Engineer);
            AssertTags(Find(catalog, "watch"), TenantAbility.NightWatch);
            AssertTags(Find(catalog, "patrol"), TenantAbility.FormerEmployee);
            AssertTags(Find(catalog, "trade"), TenantAbility.Merchant);
            AssertTags(Find(catalog, "farming"), TenantAbility.Farmer);
            AssertTags(Find(catalog, "exploration"), TenantAbility.Driver);
            AssertTags(Find(catalog, "organizing"), TenantAbility.Teacher);
        }

        [Test]
        public void Confirmed_Values_Are_Configured()
        {
            WorkCatalog catalog = LoadCatalog();
            Assert.AreEqual(40, Find(catalog, "watch").watchNightLossMitigationPercent);
            TeamComboDefinition security = FindTeam(catalog, "security_team");
            float overrideValue;
            Assert.IsTrue(security.TryGetEffect(TeamEffectKind.NightLossMitigationOverride, out overrideValue));
            Assert.AreEqual(60f, overrideValue);
        }

        [Test]
        public void Team_Roles_Are_Configured()
        {
            WorkCatalog catalog = LoadCatalog();
            AssertRoles(FindTeam(catalog, "medical_team"),
                new TeamRole { tag = TenantAbility.Doctor, jobId = "medical" },
                new TeamRole { tag = TenantAbility.Cook, jobId = "cooking" });
            AssertRoles(FindTeam(catalog, "security_team"),
                new TeamRole { tag = TenantAbility.NightWatch, jobId = "watch" },
                new TeamRole { tag = TenantAbility.FormerEmployee, jobId = "patrol" });
            AssertRoles(FindTeam(catalog, "logistics_team"),
                new TeamRole { tag = TenantAbility.Merchant, jobId = "trade" },
                new TeamRole { tag = TenantAbility.Farmer, jobId = "farming" },
                new TeamRole { tag = TenantAbility.Driver, jobId = "exploration" });
        }

        [Test]
        public void Resource_Definitions_Exist()
        {
            var ingredients = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Ingredients.asset");
            var resources = AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/Data/Resources/Resources.asset");
            Assert.NotNull(ingredients);
            Assert.NotNull(resources);
            Assert.AreEqual("ingredients", ingredients.resourceId);
            Assert.AreEqual("resources", resources.resourceId);
            Assert.AreEqual(0, ingredients.initialAmount);
            Assert.AreEqual(0, resources.initialAmount);
        }

        private static void AssertTags(JobDefinition job, TenantAbility expected)
        {
            Assert.AreEqual(1, job.allowedTags.Count);
            Assert.IsTrue(job.allowedTags.Contains(expected));
        }

        private static void AssertRoles(TeamComboDefinition team, params TeamRole[] expected)
        {
            Assert.AreEqual(expected.Length, team.roles.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].tag, team.roles[i].tag);
                Assert.AreEqual(expected[i].jobId, team.roles[i].jobId);
            }
        }
    }
}
```

- [ ] **Step 5: 运行资产级测试**

Test Runner → EditMode → 运行 `WorkCatalogAssetTests`。
Expected: 8 项全部 PASS（若资产未建齐，对应断言 FAIL 提示缺失项）。

- [ ] **Step 6: Unity 编译验证**

Console 0 错误、0 新增警告。

### Task 10: WorkSettlementCoordinator（半日结算执行）+ SettlementBridge 接线 + 场景接线

**Files:**
- Create: `Assets/Scripts/Hotel/Managers/WorkCatalogJobIdRegistry.cs`
- Create: `Assets/Scripts/Hotel/Managers/WorkSettlementCoordinator.cs`
- Modify: `Assets/Scripts/Hotel/Managers/SettlementBridge.cs`
- Modify（unitymaster）: `Assets/Scenes/MainScene.unity`（GameManager 组件新增 + SettlementBridge 序列化字段接线，不做任何 UI 布局改动）
- Unity 验证：编译 + Play 人工（Assembly-CSharp，不属 NUnit 覆盖范围）

**Interfaces:**
- Consumes: `WorkCatalog`/`TeamComboDefinition`/`TeamComboEvaluator`/`JobCompatibility`（Task 7）、`IJobIdRegistry`（Task 4）、`WorkSettlementCalculator`/`WorkSettlementInputs`/`WorkSettlementPlan`（Task 6）、`WorkMitigationContext`（Task 5）、`WorkSnapshot`（Task 3）、`PhaseEnteredEvent`/`PhaseEnterData`（既有）、`SettlementBridge.Instance`（既有）、`TenantReviewCoordinator.Instance.candidates`（既有）。
- Produces:
  - `WorkCatalogJobIdRegistry : IJobIdRegistry`（构造参数 `WorkCatalog`；`IsRegistered` = 目录中存在同 id 职业）。
  - `WorkSettlementCoordinator : MonoBehaviour`（静态 `Instance`；`workCatalog`/`onPhaseEntered` 序列化字段；`ActiveSnapshot : WorkSnapshot`；`ActiveContext : WorkMitigationContext?`；`GetFacilityDurability() : float`；`GetActiveTeamSummaries() : List<string>`）。监听 `PhaseEnteredEvent`：仅 Day/Night 处理；冻结快照与缓解上下文（整半日不变，因此半日内改职业只影响下一个 Day/Night 结算）；账本 key 已存在 → 跳过；否则以 `AuthorizedChangeSet.Domain(runId, version, "WorkSettlementCoordinator", $"WorkSettlement|{day}|{phase}")` 一次性提交（资源/耐久/治疗/`AddWorkSettlementChange`/审计），失败进入 Update 重试直至成功或阶段切换。
  - `SettlementBridge` 新增序列化字段 `public WorkCatalog workCatalog;`；`Awake` 中 `_reducer = new StateReducer(workCatalog != null ? new WorkCatalogJobIdRegistry(workCatalog) : null)`；资源兜底 `EnsureResourceExists("ingredients")`/`EnsureResourceExists("resources")`（无定义时 Amount=0 创建）。
  - 场景：`GameManager`（fileID 1918893930）挂 `WorkSettlementCoordinator` 与 `WorkAssignmentCoordinator`（后者 Task 12 实现，本任务只挂组件并接 `workCatalog`）；`SettlementBridge` 组件（fileID 481458030）接 `workCatalog`。

- [ ] **Step 1: 创建 JobId 注册表适配器**

创建 `Assets/Scripts/Hotel/Managers/WorkCatalogJobIdRegistry.cs`：

```csharp
using Hotel.Authoring.Work;
using Hotel.Runtime;

public sealed class WorkCatalogJobIdRegistry : IJobIdRegistry
{
    private readonly WorkCatalog _catalog;

    public WorkCatalogJobIdRegistry(WorkCatalog catalog)
    {
        _catalog = catalog;
    }

    public bool IsRegistered(string jobId)
    {
        if (_catalog == null || string.IsNullOrEmpty(jobId))
            return false;
        return _catalog.FindJob(jobId) != null;
    }
}
```

- [ ] **Step 2: 创建 WorkSettlementCoordinator**

创建 `Assets/Scripts/Hotel/Managers/WorkSettlementCoordinator.cs`：

```csharp
using System;
using System.Collections.Generic;
using Hotel.Authoring.Work;
using Hotel.Runtime;
using UnityEngine;

public class WorkSettlementCoordinator : MonoBehaviour
{
    public static WorkSettlementCoordinator Instance { get; private set; }

    [Header("Work Config")]
    public WorkCatalog workCatalog;

    [Header("Event Channels")]
    public PhaseEnteredEvent onPhaseEntered;

    public WorkSnapshot ActiveSnapshot => _activeSnapshot;
    public WorkMitigationContext? ActiveContext => _activeContext;

    private GameRunState _runState;
    private StateReducer _reducer;
    private WorkSnapshot _activeSnapshot;
    private WorkMitigationContext? _activeContext;
    private List<TeamComboDefinition> _activeTeams = new List<TeamComboDefinition>();
    private string _pendingSettlementKey;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        TryBindRuntimeState();
    }

    private void Start()
    {
        TryBindRuntimeState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

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

    private void Update()
    {
        if (_pendingSettlementKey == null)
            return;
        TryBindRuntimeState();
        if (_runState == null || _reducer == null || _activeSnapshot == null)
            return;
        if (TrySettle(_activeSnapshot.Day, _activeSnapshot.Phase))
            _pendingSettlementKey = null;
    }

    private void TryBindRuntimeState()
    {
        if (_runState != null)
            return;
        if (SettlementBridge.Instance == null)
            return;
        _reducer = SettlementBridge.Instance.Reducer;
        _runState = SettlementBridge.Instance.RunState;
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (workCatalog == null)
            return;
        TryBindRuntimeState();
        if (_runState == null || _reducer == null)
            return;

        HotelPhase phase = ToHotelPhase(data.phase);
        if (phase != HotelPhase.Day && phase != HotelPhase.Night)
        {
            _pendingSettlementKey = null;
            return;
        }

        _activeSnapshot = FreezeSnapshot(_runState, data.day, phase);
        _activeTeams = ComputeActiveTeams(_activeSnapshot);
        _activeContext = BuildMitigationContext(_activeSnapshot, _activeTeams);

        if (TrySettle(data.day, phase))
            _pendingSettlementKey = null;
        else
            _pendingSettlementKey = WorkSettlementLedger.Key(data.day, phase);
    }

    private bool TrySettle(int day, HotelPhase phase)
    {
        string key = WorkSettlementLedger.Key(day, phase);
        if (_runState.WorkSettlements.ContainsKey(key))
        {
            Debug.Log($"[WorkSettlementCoordinator] {key} already settled, skipping");
            return true;
        }

        WorkSettlementInputs inputs = BuildInputs(day, phase);
        WorkSettlementPlan plan = WorkSettlementCalculator.Compute(inputs);
        if (plan == null)
            return false;

        var set = AuthorizedChangeSet.Domain(
            _runState.RunId,
            _runState.StateVersion,
            "WorkSettlementCoordinator",
            $"WorkSettlement|{day}|{phase}");

        AddPlanChanges(set, plan);
        set.Add(new AddWorkSettlementChange(new WorkSettlementRecord
        {
            Day = day,
            Phase = phase,
            Sequence = _runState.WorkSettlementSequence + 1
        }));
        set.Add(new AppendAuditLogChange(
            $"[WorkSettlement] Day {day} {phase}: produced food={plan.ProducedFood}, ingredients={plan.ProducedIngredients}, resources={plan.ProducedResources}; consumed ingredients={plan.ConsumedIngredients}, currency={plan.ConsumedCurrency}; restoredDurability={plan.RestoredDurability}; healed={plan.Heals.Count}"));

        CommitResult result = _reducer.TryCommit(_runState, set);
        if (result.Succeeded)
        {
            Debug.Log($"[WorkSettlementCoordinator] {key} committed, sequence={_runState.WorkSettlementSequence}");
            return true;
        }

        Debug.LogError($"[WorkSettlementCoordinator] {key} commit failed; will retry");
        return false;
    }

    private void AddPlanChanges(AuthorizedChangeSet set, WorkSettlementPlan plan)
    {
        if (plan.FoodDelta != 0) set.Add(new AdjustResourceChange("food", plan.FoodDelta));
        if (plan.IngredientsDelta != 0) set.Add(new AdjustResourceChange("ingredients", plan.IngredientsDelta));
        if (plan.CurrencyDelta != 0) set.Add(new AdjustResourceChange("currency", plan.CurrencyDelta));
        if (plan.ResourcesDelta != 0) set.Add(new AdjustResourceChange("resources", plan.ResourcesDelta));
        if (plan.FacilityDurabilityDelta != 0f) set.Add(new AdjustFacilityDurabilityChange(plan.FacilityDurabilityDelta));
        for (int i = 0; i < plan.Heals.Count; i++)
            set.Add(new AdjustTenantErosionChange(plan.Heals[i].TenantId, -plan.Heals[i].Heal));
    }

    private WorkSettlementInputs BuildInputs(int day, HotelPhase phase)
    {
        return new WorkSettlementInputs
        {
            Day = day,
            Phase = phase,
            RunSeed = _runState.Seed,
            Sequence = _runState.WorkSettlementSequence + 1,
            Participants = _activeSnapshot != null ? _activeSnapshot.Tenants : new List<WorkSnapshotTenant>(),
            Config = BuildSettlementConfig(),
            IngredientsAmount = GetResourceAmount("ingredients"),
            CurrencyAmount = GetResourceAmount("currency"),
            FacilityDurability = _runState.FacilityDurability,
            OutputMultiplier = GetLogisticsMultiplier(),
            HealPercentBonus = GetHealPercentBonus(),
            AssignedTenantErosion = AssignedErosion()
        };
    }

    private Dictionary<string, float> AssignedErosion()
    {
        var result = new Dictionary<string, float>();
        foreach (var pair in _runState.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            result[pair.Key] = pair.Value.TrueErosion;
        }
        return result;
    }

    private int GetResourceAmount(string resourceId)
    {
        if (_runState.Resources.TryGetValue(resourceId, out ResourceRunState res))
            return res.Amount;
        return 0;
    }

    private float GetLogisticsMultiplier()
    {
        float value;
        if (TryGetActiveTeamEffect("logistics_team", TeamEffectKind.OutputMultiplier, out value))
            return value;
        return 1f;
    }

    private int GetHealPercentBonus()
    {
        float value;
        if (TryGetActiveTeamEffect("medical_team", TeamEffectKind.HealPercentBonus, out value))
            return (int)value;
        return 0;
    }

    private bool TryGetActiveTeamEffect(string comboId, TeamEffectKind kind, out float value)
    {
        value = 0f;
        for (int i = 0; i < _activeTeams.Count; i++)
        {
            TeamComboDefinition combo = _activeTeams[i];
            if (combo == null || combo.comboId != comboId)
                continue;
            return combo.TryGetEffect(kind, out value);
        }
        return false;
    }

    private WorkSettlementConfig BuildSettlementConfig()
    {
        var cfg = new WorkSettlementConfig();
        JobDefinition cooking = FindJob("cooking");
        if (cooking != null)
        {
            cfg.IngredientCostPerSettlement = cooking.ingredientCostPerSettlement;
            cfg.FoodPerIngredient = cooking.foodPerIngredient;
        }
        JobDefinition repair = FindJob("repair");
        if (repair != null)
        {
            cfg.RepairCostCurrency = repair.repairCostCurrency;
            cfg.RepairRestoreDurability = repair.repairRestoreDurability;
        }
        JobDefinition trade = FindJob("trade");
        if (trade != null)
        {
            cfg.TradeCostCurrency = trade.tradeCostCurrency;
            cfg.TradeOutputResources = trade.tradeOutputResources;
        }
        JobDefinition farming = FindJob("farming");
        if (farming != null)
            cfg.FarmOutputIngredients = farming.farmOutputIngredients;
        JobDefinition exploration = FindJob("exploration");
        if (exploration != null)
        {
            cfg.ExplorationMin = exploration.explorationMin;
            cfg.ExplorationMax = exploration.explorationMax;
        }
        JobDefinition medical = FindJob("medical");
        if (medical != null)
            cfg.HealPercentPerSettlement = medical.healPercentPerSettlement;
        return cfg;
    }

    private JobDefinition FindJob(string jobId)
    {
        if (workCatalog == null || string.IsNullOrEmpty(jobId))
            return null;
        return workCatalog.FindJob(jobId);
    }

    private WorkSnapshot FreezeSnapshot(GameRunState state, int day, HotelPhase phase)
    {
        var snapshot = new WorkSnapshot { Day = day, Phase = phase };
        foreach (var pair in state.Tenants)
        {
            TenantRunState tenant = pair.Value;
            if (tenant == null)
                continue;
            if (string.IsNullOrEmpty(tenant.RoomId))
                continue;
            if (string.IsNullOrEmpty(tenant.JobId))
                continue;
            snapshot.Tenants.Add(new WorkSnapshotTenant
            {
                TenantId = tenant.TenantId,
                JobId = tenant.JobId,
                Ability = LookupAbility(tenant.TenantId)
            });
        }
        return snapshot;
    }

    private TenantAbility LookupAbility(string tenantId)
    {
        if (TenantReviewCoordinator.Instance != null
            && TenantReviewCoordinator.Instance.candidates != null)
        {
            var candidates = TenantReviewCoordinator.Instance.candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                TenantReviewCandidateSO candidate = candidates[i];
                if (candidate != null && candidate.candidateId == tenantId)
                    return candidate.ability;
            }
        }
        return TenantAbility.None;
    }

    private List<TeamComboDefinition> ComputeActiveTeams(WorkSnapshot snapshot)
    {
        var result = new List<TeamComboDefinition>();
        if (workCatalog == null || snapshot == null)
            return result;
        for (int i = 0; i < workCatalog.teams.Count; i++)
        {
            TeamComboDefinition combo = workCatalog.teams[i];
            if (combo != null && TeamComboEvaluator.IsActive(combo, snapshot.Tenants))
                result.Add(combo);
        }
        return result;
    }

    private WorkMitigationContext? BuildMitigationContext(WorkSnapshot snapshot, List<TeamComboDefinition> activeTeams)
    {
        if (snapshot == null)
            return null;

        JobDefinition watch = FindJob("watch");
        JobDefinition patrol = FindJob("patrol");
        JobDefinition organizing = FindJob("organizing");

        int watchCount = 0, patrolCount = 0, organizingCount = 0;
        for (int i = 0; i < snapshot.Tenants.Count; i++)
        {
            WorkSnapshotTenant p = snapshot.Tenants[i];
            if (p == null)
                continue;
            if (p.JobId == "watch" && IsActiveAt("watch", snapshot.Phase)) watchCount++;
            else if (p.JobId == "patrol" && IsActiveAt("patrol", snapshot.Phase)) patrolCount++;
            else if (p.JobId == "organizing" && IsActiveAt("organizing", snapshot.Phase)) organizingCount++;
        }

        bool securityTeam = false;
        for (int i = 0; i < activeTeams.Count; i++)
        {
            if (activeTeams[i] != null && activeTeams[i].comboId == "security_team")
            {
                securityTeam = true;
                break;
            }
        }

        float securityOverride = 60f;
        if (securityTeam)
        {
            TeamComboDefinition security = FindTeam("security_team");
            if (security != null)
                security.TryGetEffect(TeamEffectKind.NightLossMitigationOverride, out securityOverride);
        }

        return new WorkMitigationContext
        {
            IsValid = true,
            Phase = snapshot.Phase,
            ActiveWatchCount = watchCount,
            SecurityTeamActive = securityTeam,
            ActivePatrolCount = patrolCount,
            ActiveOrganizingCount = organizingCount,
            WatchMitigationPercent = watch != null ? watch.watchNightLossMitigationPercent : 40,
            SecurityOverridePercent = (int)securityOverride,
            PatrolReductionPercentPerActive = patrol != null ? patrol.floorSpreadReductionPerPatrol : 25,
            PatrolReductionCapPercent = patrol != null ? patrol.floorSpreadReductionCap : 75,
            OrganizingReductionPercentPerActive = organizing != null ? organizing.buildingSpreadReductionPerOrganizer : 20,
            OrganizingReductionCapPercent = organizing != null ? organizing.buildingSpreadReductionCap : 60
        };
    }

    private bool IsActiveAt(string jobId, HotelPhase phase)
    {
        JobDefinition job = FindJob(jobId);
        if (job == null)
            return false;
        switch (job.activityWindow)
        {
            case TenantActivityType.DayActive: return phase == HotelPhase.Day;
            case TenantActivityType.NightActive: return phase == HotelPhase.Night;
            default: return true; // AllDay
        }
    }

    private TeamComboDefinition FindTeam(string comboId)
    {
        if (workCatalog == null)
            return null;
        for (int i = 0; i < workCatalog.teams.Count; i++)
        {
            if (workCatalog.teams[i] != null && workCatalog.teams[i].comboId == comboId)
                return workCatalog.teams[i];
        }
        return null;
    }

    public float GetFacilityDurability()
    {
        return _runState != null ? _runState.FacilityDurability : 100f;
    }

    public List<string> GetActiveTeamSummaries()
    {
        var result = new List<string>();
        for (int i = 0; i < _activeTeams.Count; i++)
        {
            TeamComboDefinition combo = _activeTeams[i];
            if (combo == null)
                continue;
            float v;
            if (combo.comboId == "security_team")
            {
                combo.TryGetEffect(TeamEffectKind.NightLossMitigationOverride, out v);
                result.Add($"{combo.displayName}：守夜 −{v:0}%");
            }
            else if (combo.comboId == "logistics_team")
            {
                combo.TryGetEffect(TeamEffectKind.OutputMultiplier, out v);
                result.Add($"{combo.displayName}：产出 ×{v:0.#}");
            }
            else if (combo.comboId == "medical_team")
            {
                combo.TryGetEffect(TeamEffectKind.HealPercentBonus, out v);
                result.Add($"{combo.displayName}：治疗 +{v:0.#}%");
            }
        }
        return result;
    }

    private static HotelPhase ToHotelPhase(GamePhase phase)
    {
        switch (phase)
        {
            case GamePhase.Dawn: return HotelPhase.Dawn;
            case GamePhase.Dusk: return HotelPhase.Dusk;
            case GamePhase.Night: return HotelPhase.Night;
            default: return HotelPhase.Day;
        }
    }
}
```

- [ ] **Step 3: 修改 SettlementBridge**

`Assets/Scripts/Hotel/Managers/SettlementBridge.cs`：

(a) 文件头 `using Hotel.Authoring.Resources;` 之后追加 `using Hotel.Authoring.Work;`。

(b) 第 13 行 `resourceDefinitions` 字段之后追加：

```csharp
    [Header("Work Config")]
    public WorkCatalog workCatalog;
```

(c) `Awake` 中 `_reducer = new StateReducer();` 改为：

```csharp
        _reducer = new StateReducer(workCatalog != null ? new WorkCatalogJobIdRegistry(workCatalog) : null);
```

(d) `Awake` 中资源定义循环之后追加：

```csharp
        EnsureResourceExists("ingredients");
        EnsureResourceExists("resources");
```

(e) `MigrateLegacyMedicineToCurrency` 方法之后新增：

```csharp
    private void EnsureResourceExists(string resourceId)
    {
        if (_runState.Resources.ContainsKey(resourceId))
            return;
        _runState.Resources[resourceId] = new ResourceRunState
        {
            ResourceId = resourceId,
            DefinitionId = resourceId,
            Amount = 0
        };
    }
```

- [ ] **Step 4: 场景接线（unitymaster，仅序列化接线，不做任何 UI 布局）**

1. 打开 `Assets/Scenes/MainScene.unity`。
2. 在 Hierarchy 选中 `GameManager`（GameObject fileID 1918893930）。
3. Add Component → `WorkSettlementCoordinator`。Inspector 中：`Work Catalog` = `Assets/Data/Configs/Work/WorkCatalog.asset`；`On Phase Entered` = `Assets/Data/Events/PhaseEnteredEvent.asset`（与 SettlementBridge 组件 `onPhaseEntered` 同一资产）。
4. Add Component → `WorkAssignmentCoordinator`。`Work Catalog` = 同一 WorkCatalog.asset（该组件脚本 Task 12 才创建，若此刻编译报「Missing script」，则本步骤延后到 Task 12 Step 1 完成后执行；Task 10 先只挂 WorkSettlementCoordinator）。
5. 在 Hierarchy 选中 `SettlementBridge`（GameObject fileID 481458029），在其组件 Inspector 中把新增的 `Work Catalog` 字段设为 WorkCatalog.asset。
6. 保存场景（Ctrl+S）。

Expected: 场景序列化块 `&1918893930` 的组件列表新增两项、`&481458030` 出现 `workCatalog: {fileID: 11400000, guid: <WorkCatalog.asset 的 guid>, type: 2}`；Console 0 错误；除上述两块外场景无任何改动。

- [ ] **Step 5: Unity 编译验证**

聚焦 Unity，等待重编译。Console 0 错误、0 新增警告。

- [ ] **Step 6: Play 模式验证（结算执行与账本幂等）**

1. 打开 `MainScene`，进入 Play。
2. 观察 Console。

Expected:
- 每次进入 Day/Night 时打印 `[WorkSettlementCoordinator] <day>|<Day|Night> committed, sequence=N`（新局 Day 1 开始，无租客也写账本并递增序号，序号严格连续 1、2、3…）；
- 同一阶段重复触发 `PhaseEnteredEvent`（例如 Dawn 自动存档后 `GamePhaseManager.Start` 再次广播）时打印 `[WorkSettlementCoordinator] <key> already settled, skipping`，**不产生第二次产出**；
- `[WorkSettlement] Day N <phase>: produced ... consumed ...` 审计行出现在 `GameRunState.AuditLog`（可在 `SettlementBridge` 组件调试查看或存档 JSON 中核对）；
- Dawn 自动存档后的 `hotel-save-slot-1.json` 包含 `"WorkSettlements"`、`"WorkSettlementSequence"`、`"FacilityDurability"` 字段，且 `"SchemaVersion": 2`。

3. 退出 Play。Expected: 全程无报错、无 MissingReferenceException。

---

### Task 11: 事件管线侵蚀拦截（EventEffectExecutor → WorkMitigationResolver）

**Files:**
- Modify: `Assets/Scripts/Hotel/Services/EventEffectExecutor.cs:62-69`（`AddErosionChanges`）
- Unity 验证：编译 + Play 人工（Assembly-CSharp）

**Interfaces:**
- Consumes: `WorkMitigationResolver.Compute` 与 `WorkMitigationContext`（Task 5）、`WorkSettlementCoordinator.Instance.ActiveContext`（Task 10）。
- Produces: 拦截语义——对 `ModifyTenantErosion` 的每个目标，在生成 `AdjustTenantErosionChange` 前将 delta 替换为 `Compute(delta, effect.target, context)`；`context` 缺失（协调器未就绪/测试环境）→ 返回原 delta（不缓解）；`ModifyResource`、`ApplyBuff`、`SameRoomOtherTenants`/`ByPlayerFlag`/`RandomAssignedTenants` 目标、治疗方向（delta ≤ 0）一律不拦截（由解析器保证）。不改变事件资格判定。

- [ ] **Step 1: 修改 AddErosionChanges**

`Assets/Scripts/Hotel/Services/EventEffectExecutor.cs` 第 62-69 行 `AddErosionChanges` 整体替换为：

```csharp
    private static void AddErosionChanges(EventEffect effect, GameRunState state, string ownerTenantId, int effectIndex, List<RunChange> changes, RoomFloorRegistry floorRegistry)
    {
        List<string> targets = ResolveTargets(effect.target, state, ownerTenantId, effect.intValue, effectIndex, floorRegistry);
        if (targets == null)
            return;

        WorkMitigationContext? context = WorkSettlementCoordinator.Instance != null
            ? WorkSettlementCoordinator.Instance.ActiveContext
            : (WorkMitigationContext?)null;

        for (int i = 0; i < targets.Count; i++)
        {
            float delta = WorkMitigationResolver.Compute(effect.floatValue, effect.target, context);
            changes.Add(new AdjustTenantErosionChange(targets[i], delta));
        }
    }
```

（文件头已 `using Hotel.Runtime;`，无需新增。）

- [ ] **Step 2: Unity 编译验证**

Console 0 错误、0 新增警告。静态核对：`Assets/Scripts/Hotel/Services/EventEffectExecutor.cs` 中 `AdjustTenantErosionChange` 只出现在上述循环内、且 delta 一律先过 `WorkMitigationResolver.Compute`。

- [ ] **Step 3: Play 验证（无职业时的回退路径）**

1. 进入 Play，新局推进到第一次 Night，触发任意夜间 `OwnerTenant` 侵蚀事件（如 N01 高烧，`requiredTags` 未满足时 +10）。
2. 观察 Console 中 `[EventEffectManager] ... erosionDelta=10`（快照已冻结但 `ActiveWatchCount == 0` → 不缓解，回退原 delta）。
3. 触发治疗方向效果（若候选能力满足，`delta` 为负）：`erosionDelta=-3` 等原样生效。

Expected: 事件结算正常、无异常；带 watch/patrol/organizing 职业的缓解数值验证并入 Task 14 端到端。

---

### Task 12: WorkAssignmentCoordinator（AssignJobChange 唯一入口）+ 职业列表视图模型

**Files:**
- Create: `Assets/Scripts/Hotel/Managers/WorkJobEntryView.cs`
- Create: `Assets/Scripts/Hotel/UI/WorkJobDisplay.cs`
- Create: `Assets/Scripts/Hotel/Managers/WorkAssignmentCoordinator.cs`
- Unity 验证：编译 + Play 人工（Assembly-CSharp）

**Interfaces:**
- Consumes: `WorkCatalog`/`JobDefinition`/`JobCompatibility`（Task 7）、`AssignJobChange`/`IJobIdRegistry` 白名单（Task 4，经 `SettlementBridge.Reducer` 注入）、`SettlementBridge.Instance`、`TenantReviewCoordinator.Instance.candidates`。
- Produces:
  - `JobEntryView { string JobId; string DisplayName; bool Compatible; bool IsCurrent; string NextEffectText; }`。
  - `WorkJobDisplay.GetNextEffectText(JobDefinition) : string`（按 jobId 输出规格 §11 效果文案；null/空 jobId → `"未分配职业"`）。
  - `WorkAssignmentCoordinator : MonoBehaviour`（静态 `Instance`；`workCatalog` 序列化字段；`event Action<string> JobAssignmentChanged`；`bool TryAssignJob(string tenantId, string jobId)`——**唯一**允许提交 `AssignJobChange` 的入口，校验租客存在 → jobId 空串（解除分配）或目录中存在 → `JobCompatibility.IsAllowed` 兼容；`List<JobEntryView> GetJobEntries(string tenantId)`；`TenantAbility GetAbility(string tenantId)`；调试 `[ContextMenu]` 两个）。提交走 `AuthorizedChangeSet.Domain(runId, version, "WorkAssignmentCoordinator", "AssignJob")`。
  - 未分配表示：`TryAssignJob(tenantId, "")` 解除；兼容性规则——无标签租客仅 `chores` 可分配（`JobCompatibility`），带标签租客按 `allowedTags` 一一对应。

- [ ] **Step 1: 创建 JobEntryView**

创建 `Assets/Scripts/Hotel/Managers/WorkJobEntryView.cs`：

```csharp
public sealed class JobEntryView
{
    public string JobId;
    public string DisplayName;
    public bool Compatible;
    public bool IsCurrent;
    public string NextEffectText;
}
```

- [ ] **Step 2: 创建 WorkJobDisplay**

创建 `Assets/Scripts/Hotel/UI/WorkJobDisplay.cs`：

```csharp
using Hotel.Authoring.Work;

public static class WorkJobDisplay
{
    public static string GetNextEffectText(JobDefinition job)
    {
        if (job == null || string.IsNullOrEmpty(job.jobId))
            return "未分配职业";
        switch (job.jobId)
        {
            case "cooking":
                return $"下一结算：消耗食材 {job.ingredientCostPerSettlement} → 产出食物 {job.ingredientCostPerSettlement * job.foodPerIngredient}";
            case "medical":
                return $"下一结算：治疗全楼已分配租客 {job.healPercentPerSettlement}%";
            case "repair":
                return $"下一结算：消耗货币 {job.repairCostCurrency} → 恢复耐久 {job.repairRestoreDurability}";
            case "watch":
                return $"夜间：个人侵蚀损失 −{job.watchNightLossMitigationPercent}%";
            case "patrol":
                return $"夜间：同楼层扩散 −{job.floorSpreadReductionPerPatrol}% / 个（上限 {job.floorSpreadReductionCap}%）";
            case "trade":
                return $"下一结算：消耗货币 {job.tradeCostCurrency} → 产出物资 {job.tradeOutputResources}";
            case "farming":
                return $"下一结算：产出食材 {job.farmOutputIngredients}";
            case "exploration":
                return $"下一结算：产出物资 {job.explorationMin}–{job.explorationMax}";
            case "organizing":
                return $"整楼扩散 −{job.buildingSpreadReductionPerOrganizer}% / 个（上限 {job.buildingSpreadReductionCap}%）";
            case "chores":
                return "下一结算：无产出、无消耗";
            default:
                return "未分配职业";
        }
    }
}
```

- [ ] **Step 3: 创建 WorkAssignmentCoordinator**

创建 `Assets/Scripts/Hotel/Managers/WorkAssignmentCoordinator.cs`：

```csharp
using System;
using System.Collections.Generic;
using Hotel.Authoring.Work;
using Hotel.Runtime;
using UnityEngine;

public class WorkAssignmentCoordinator : MonoBehaviour
{
    public static WorkAssignmentCoordinator Instance { get; private set; }

    [Header("Work Config")]
    public WorkCatalog workCatalog;

    public event Action<string> JobAssignmentChanged;

    private GameRunState _runState;
    private StateReducer _reducer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        TryBindRuntimeState();
    }

    private void Start()
    {
        TryBindRuntimeState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void TryBindRuntimeState()
    {
        if (_runState != null)
            return;
        if (SettlementBridge.Instance == null)
            return;
        _reducer = SettlementBridge.Instance.Reducer;
        _runState = SettlementBridge.Instance.RunState;
    }

    /// <summary>唯一允许提交 AssignJobChange 的入口。jobId 为空串 = 解除分配。</summary>
    public bool TryAssignJob(string tenantId, string jobId)
    {
        TryBindRuntimeState();
        if (_runState == null || _reducer == null)
            return false;
        if (string.IsNullOrEmpty(tenantId))
            return false;
        if (!_runState.Tenants.ContainsKey(tenantId))
            return false;

        string normalized = jobId ?? string.Empty;

        if (!string.IsNullOrEmpty(normalized))
        {
            JobDefinition job = FindJob(normalized);
            if (job == null)
            {
                Debug.LogWarning($"[WorkAssignmentCoordinator] Unknown job '{normalized}'");
                return false;
            }
            TenantAbility ability = GetAbility(tenantId);
            if (!JobCompatibility.IsAllowed(job, ability))
            {
                Debug.LogWarning($"[WorkAssignmentCoordinator] Job '{normalized}' not allowed for ability {ability}");
                return false;
            }
        }

        var set = AuthorizedChangeSet.Domain(_runState.RunId, _runState.StateVersion, "WorkAssignmentCoordinator", "AssignJob");
        set.Add(new AssignJobChange(tenantId, normalized));
        CommitResult result = _reducer.TryCommit(_runState, set);
        if (result.Succeeded)
        {
            JobAssignmentChanged?.Invoke(tenantId);
            Debug.Log($"[WorkAssignmentCoordinator] tenant={tenantId} job='{normalized}' assigned");
        }
        return result.Succeeded;
    }

    public List<JobEntryView> GetJobEntries(string tenantId)
    {
        var entries = new List<JobEntryView>();
        if (workCatalog == null || _runState == null)
            return entries;
        if (!_runState.Tenants.TryGetValue(tenantId, out TenantRunState tenant))
            return entries;

        TenantAbility ability = GetAbility(tenantId);
        string currentJobId = tenant.JobId ?? string.Empty;

        for (int i = 0; i < workCatalog.jobs.Count; i++)
        {
            JobDefinition job = workCatalog.jobs[i];
            if (job == null || string.IsNullOrEmpty(job.jobId))
                continue;
            entries.Add(new JobEntryView
            {
                JobId = job.jobId,
                DisplayName = job.displayName,
                Compatible = JobCompatibility.IsAllowed(job, ability),
                IsCurrent = job.jobId == currentJobId,
                NextEffectText = WorkJobDisplay.GetNextEffectText(job)
            });
        }
        return entries;
    }

    public JobDefinition FindJob(string jobId)
    {
        if (workCatalog == null || string.IsNullOrEmpty(jobId))
            return null;
        return workCatalog.FindJob(jobId);
    }

    public TenantAbility GetAbility(string tenantId)
    {
        if (TenantReviewCoordinator.Instance != null
            && TenantReviewCoordinator.Instance.candidates != null)
        {
            var candidates = TenantReviewCoordinator.Instance.candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                TenantReviewCandidateSO candidate = candidates[i];
                if (candidate != null && candidate.candidateId == tenantId)
                    return candidate.ability;
            }
        }
        return TenantAbility.None;
    }

    [ContextMenu("Log Job Entries (Debug)")]
    public void LogJobEntriesDebug()
    {
        TryBindRuntimeState();
        if (_runState == null)
        {
            Debug.Log("[WorkAssignmentCoordinator] No run state bound.");
            return;
        }
        foreach (var pair in _runState.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            var entries = GetJobEntries(pair.Key);
            var parts = new List<string>();
            for (int i = 0; i < entries.Count; i++)
                parts.Add($"{entries[i].DisplayName}(compatible={entries[i].Compatible},current={entries[i].IsCurrent},next='{entries[i].NextEffectText}')");
            Debug.Log($"[WorkUI] tenant={pair.Key} ability={GetAbility(pair.Key)} jobs=" + string.Join("; ", parts));
        }
    }

    [ContextMenu("Assign First Compatible Job (Debug)")]
    public void DebugAssignFirstTenantFirstJob()
    {
        TryBindRuntimeState();
        if (_runState == null || workCatalog == null)
            return;
        foreach (var pair in _runState.Tenants)
        {
            if (pair.Value == null || string.IsNullOrEmpty(pair.Value.RoomId))
                continue;
            TenantAbility ability = GetAbility(pair.Key);
            for (int i = 0; i < workCatalog.jobs.Count; i++)
            {
                JobDefinition job = workCatalog.jobs[i];
                if (job == null)
                    continue;
                if (!JobCompatibility.IsAllowed(job, ability))
                    continue;
                TryAssignJob(pair.Key, job.jobId);
                return;
            }
        }
    }
}
```

- [ ] **Step 4: 场景接线补齐（unitymaster）**

若 Task 10 Step 4 因脚本缺失而未挂 `WorkAssignmentCoordinator`，此时在 Hierarchy 选中 `GameManager`（fileID 1918893930）→ Add Component → `WorkAssignmentCoordinator`，`Work Catalog` = WorkCatalog.asset，保存场景。Expected: `&1918893930` 组件列表包含 WorkSettlementCoordinator 与 WorkAssignmentCoordinator 两项，其余场景块无改动。

- [ ] **Step 5: Unity 编译验证**

Console 0 错误、0 新增警告。

- [ ] **Step 6: Play 模式验证**

1. 进入 Play，招募一名候选（如 `Cook` 能力）并拖入房间。
2. 在 Hierarchy 选中 `GameManager`，Inspector 中 WorkAssignmentCoordinator 组件右键 → `Log Job Entries (Debug)`。

Expected Console：
- 该租客条目 `[WorkUI] tenant=<id> ability=Cook jobs=烹饪(compatible=True,current=False,next='下一结算：消耗食材 1 → 产出食物 2'); ...; 杂务(compatible=False,...)`（带标签租客对 `chores` 不兼容）；
- `current` 全部为 False（尚未分配）。

3. 右键 → `Assign First Compatible Job (Debug)`。

Expected Console：`[WorkAssignmentCoordinator] tenant=<id> job='cooking' assigned`；再次 `Log Job Entries (Debug)` 时 `烹饪` 项 `current=True`。
4. 对 `None` 能力租客执行同样操作。

Expected：分配到 `chores`（其 `allowedTags` 为空），其余职业 `compatible=False`。

---

### Task 13: pinned 面板职业数据绑定（TenantInfoPanel，无新建布局）

**Files:**
- Modify: `Assets/Scripts/Hotel/UI/TenantInfoPanel.cs`（`ShowPinned` 内调用 + 新私有方法）
- Unity 验证：编译 + Play 人工（Assembly-CSharp）

**Interfaces:**
- Consumes: `WorkAssignmentCoordinator.GetJobEntries`/`GetAbility`（Task 12）、`JobEntryView`（Task 12）、既有 `ShowPinned`/`CurrentTenantId`。
- Produces: `ShowPinned` 在 `ApplyFlagToPanel(tenantId)` 之后调用私有 `RefreshJobSection()`——读取 `GetJobEntries(_currentTenantId)` 并打印只读 Console 输出（职业列表/兼容性/当前标记/下次效果文案的数据绑定接缝）；阶段 2 的可视化 UI 将消费同一份 `JobEntryView` 数据。**不新增字段、不改场景、不建预制体。**

- [ ] **Step 1: 修改 ShowPinned**

`Assets/Scripts/Hotel/UI/TenantInfoPanel.cs` 第 128 行 `ApplyFlagToPanel(tenantId);` 之后插入一行：

```csharp
        RefreshJobSection();
```

- [ ] **Step 2: 新增 RefreshJobSection 方法**

在 `Hide()` 方法之前插入：

```csharp
    private void RefreshJobSection()
    {
        if (string.IsNullOrEmpty(_currentTenantId))
            return;
        WorkAssignmentCoordinator coordinator = WorkAssignmentCoordinator.Instance;
        if (coordinator == null)
            return;
        var entries = coordinator.GetJobEntries(_currentTenantId);
        if (entries == null || entries.Count == 0)
        {
            Debug.Log($"[WorkUI] tenant={_currentTenantId} jobs=<none>");
            return;
        }
        var parts = new List<string>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            JobEntryView e = entries[i];
            parts.Add($"{e.DisplayName}(compatible={e.Compatible},current={e.IsCurrent},next='{e.NextEffectText}')");
        }
        Debug.Log($"[WorkUI] tenant={_currentTenantId} ability={coordinator.GetAbility(_currentTenantId)} jobs=" + string.Join("; ", parts));
    }
```

（文件头已 `using System.Collections.Generic;`，无需新增。）

- [ ] **Step 3: Unity 编译验证**

Console 0 错误、0 新增警告。

- [ ] **Step 4: Play 模式验证**

1. 进入 Play，招募并分配房间后，右键该租客（列表项或房间头像）打开 pinned 面板（`TenantInfoHoverTrigger.OpenPinned` → `TenantInfoPanel.ShowPinned`）。
2. 观察 Console。

Expected：
- `[WorkUI] tenant=<id> ability=<能力> jobs=烹饪(...)...` 与 Task 12 Step 6 输出一致；
- 面板打开期间改职业（经 `TryAssignJob` 或调试菜单）后重新打开面板，`current` 标记跟随 `JobId` 变化；
- pinned 面板其余行为不变：`IsInternalHit` 判定、面板外左键点击关闭、面板内点击不关闭、按住左键不触发悬停（`Input.GetMouseButton(0)` 检查）均与改动前一致，`TenantAvatarDragTrigger`/`TenantDragOverlay`/`AnchorDropTarget` 左键拖拽流程未被触碰。

---

### Task 14: 端到端 Play 验证（含确定性、持久化、迁移、不重复生产）

**Files:**
- 只读验证，不修改任何文件。

**Interfaces:**
- Consumes: Task 1–13 全部成果；`SaveGameService.SavePath`（`Application.persistentDataPath/hotel-save-slot-1.json`）、`GameLaunchContext`（既有）。

- [ ] **Step 1: 组装职业与团队场景**

1. 进入 Play，招募并分配 6 名租客：`Doctor→medical`、`Cook→cooking`、`NightWatch→watch`、`FormerEmployee→patrol`、`Merchant→trade`、`Farmer→farming`（通过 Task 12 的调试菜单或 pinned 面板入口逐一分配）。
2. 让 `Doctor` 与 `Cook` 同楼（医疗队应激活）、`NightWatch` 与 `FormerEmployee` 同楼（安保队应激活）、`Merchant`+`Farmer`（物流队缺 `Driver`，应**不**激活）。

Expected Console（进 Day 后）：
- `[WorkSettlementCoordinator] 2|Day committed, sequence=N`；
- 审计行显示 `produced ingredients=2, resources=1, consumed currency=2`（farming/trade 生效、物流队未激活故倍率 1）；
- `[WorkSettlementCoordinator]` 的 `GetActiveTeamSummaries` 数据（可在 GameManager 组件调试查看）：激活列表含「医疗队：治疗 +2%」「安保队：守夜 −60%」，不含物流队。

- [ ] **Step 2: 验证团队效果（补 Driver 后）**

1. 继续招募 `Driver→exploration` 并分配（物流队激活）。
2. 推进到下一个 Day。

Expected Console：
- 审计行 `produced ingredients=3, resources=<floor((1+roll)*1.5)>`（farming `floor(2×1.5)=3`，trade `floor(1×1.5)=1`，exploration 倍率 1.5 向下取整）；
- 修理场景：把某事件或手工把 `FacilityDurability` 降至 <100（可通过存档 JSON 或临时 Debug 改值）后推进 Day，`repair` 消耗 2 货币、耐久回到 min(100, 原+10)。

- [ ] **Step 3: 验证侵蚀缓解（watch / 安保队 / patrol / organizing / medical）**

1. 夜间事件对 `OwnerTenant` 施加 +10（如 N01 高烧，医生未满足时）：存在 1 名 watch（非安保）→ Console `erosionDelta=6`（40%）。
2. 安保队激活时同类事件 → `erosionDelta=4`（60% 覆盖，非叠加）。
3. 夜间接力测试同楼层扩散（如 N04 精神崩溃无标签分支「同层房客侵蚀度+5」）：有 1 名 patrol → `erosionDelta=3.75`（−25%）；4 名 patrol → `erosionDelta=1.25`（cap 75%）。
4. 白天整楼扩散（如 N06 低语无标签分支「全楼房客侵蚀度+2」）：1 名 organizing → `erosionDelta=1.6`（−20%）；3 名 → `erosionDelta=0.8`（cap 60%）。
5. 治疗：有 medical 且某租客侵蚀 50 → Day/Night 结算后该租客侵蚀下降 `floor(50×(2+2)/100)=2`（医疗队激活时）。

Expected：以上 Console 数值逐项吻合；`SameRoomOtherTenants` 目标事件不受影响（原 delta）。

- [ ] **Step 4: 验证确定性探索与载入后不重复生产**

1. 记录某个 `exploration` 租客在某次 Day 结算后的 `resources` 增量（Console 审计）与 `WorkSettlementSequence`。
2. 退出 Play；删除 `Application.persistentDataPath` 下的存档前，先复制一份。
3. 重新进入 Play（继续该存档，`GameLaunchContext.ContinueWith` 路径）。

Expected：
- 载入后从中间阶段继续，已入账 (day, phase) 全部打印 `already settled, skipping`，资源/耐久数值与退出前一致，**不产生第二次产出**；
- 下一次 exploration 结算的增量与「同一 seed/day/phase/tenantId/jobId/sequence 重算」一致（同局内两次读取相同）。

- [ ] **Step 5: 验证 v1 存档迁移**

1. 手工构造 v1 存档 JSON（`SchemaVersion:1`、含 `food`/`currency` 资源、一名 `RoomId` 非空租客、无任何 work 字段），写入 `Application.persistentDataPath/hotel-save-slot-1.json`。
2. 从主菜单选择「继续游戏」。

Expected Console / 运行状态：
- 加载成功（不再抛 `Unsupported save schema 1`）；
- `FacilityDurability = 100`、`WorkSettlementSequence = 0`、`WorkSettlements` 空；
- 资源字典出现 `ingredients` 与 `resources`（Amount=0，因定义 initialAmount=0）；
- 租客 `JobId` 为空串（未分配），不参与任何结算；
- 继续推进 Day/Night 正常产生新账本（序号从 1 起）。

- [ ] **Step 6: 回归确认与完成检查点**

Expected（全程）：
- `git status` 显示的改动仅限本计划列出的脚本/资产/测试/场景文件（核对用，不做任何提交）；
- Unity Console 0 错误；EditMode 测试全量通过；
- 场景无 UI 布局改动、无新建 Prefab；事件资格（`EventUI.GetOwnedAbilities` 按固有标签）行为与改动前一致（改职业后事件选项可用性不变）。

---

## Self-Review

- **规格覆盖（§1–§13）**：§2 已确认规则（9 标签+None、10 职业、chores 仅无标签、时段语义、改职业只影响下次结算）→ Task 2/7/9/12；§3 数据驱动（JobDefinition/TeamComboDefinition/WorkCatalog、可配置数值、JobId 真相、团队动态推导不持久化）→ Task 7/9/10；§4 半日结算与账本（Day/Night 执行、快照冻结、exactly-once、原子变更集、职业效果）→ Task 3/4/6/10；§5 资源与设施耐久 → Task 3/4/9/10；§6 百分比缓解（方向规则、watch 40/安保 60/patrol 75/organizing 60/医疗向下取整）→ Task 5/10/11；§7 团队（roles、激活规则、效果语义）→ Task 7/10；§8 确定性种子 → Task 5/6/10；§9 持久化与迁移 → Task 8/10；§10 事件管线拦截 → Task 11；§11 UI 逻辑（冻结租客 ID、兼容性/当前职业/下次效果、唯一入口、无左键冲突、范围声明）→ Task 12/13（只做数据绑定接口，不建布局）；§12 错误处理与不变量 → Task 4（幂等/原子/JobId 引用/授权者/数值边界/取整/时段约束）；§13.1 最小源码增量（Driver/Teacher、AbilityDisplayName、保留 Carpenter）→ Task 2；§13.2 测试矩阵 T1–T9 → Task 1/2/3/4/5/6/7/8/9（EditMode）+ Task 10–14（编译+Play）。无缺口。
- **范围门落实**：全文无 UI 场景布局、无 `Event_*.asset` 迁移、无 Carpenter 移除；Task 10/12 的场景接线限定为「管理器组件 + 序列化引用」。
- **占位符扫描**：全文无 TODO/TBD/「待定」/「类似 Task N」；每个代码步骤给出完整代码或精确插入点；所有数值来自 §3.2 初始值或资产字段；错误处理均为具体行为（跳过/重试/拒绝并记日志）。
- **签名一致性**：`WorkSettlementRecord`/`WorkSnapshotTenant`/`WorkSnapshot`/`IJobIdRegistry`/`StateReducer(IJobIdRegistry)`/`AdjustFacilityDurabilityChange`/`AddWorkSettlementChange`/`WorkSettlementLedger.Key`/`WorkDeterminism.*`/`WorkMitigationContext`/`WorkMitigationResolver.Compute`/`WorkSettlementConfig`/`WorkSettlementInputs`/`WorkSettlementPlan`/`WorkSettlementCalculator.Compute`/`JobDefinition`/`TeamComboDefinition.TryGetEffect`/`WorkCatalog.FindJob`/`JobCompatibility.IsAllowed`/`TeamComboEvaluator.IsActive`/`WorkCatalogJobIdRegistry`/`WorkSettlementCoordinator.*`/`WorkAssignmentCoordinator.*`/`JobEntryView`/`WorkJobDisplay.GetNextEffectText` 在 Produces/Consumes 与实现代码中逐字一致；账本 key 格式 `"{day}|{phase}"`、`Sequence == WorkSettlementSequence + 1`、授权者 `"WorkSettlementCoordinator"`/命令 `"WorkSettlement|{day}|{phase}"` 在 Task 4/6/8/10 四处一致；`WatchMitigationPercent=40`/`SecurityOverridePercent=60` 在 Task 5 测试与 Task 9 资产、Task 10 构造一致。

