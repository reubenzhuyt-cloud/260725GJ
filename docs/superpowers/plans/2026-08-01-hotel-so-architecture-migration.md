# 《旅馆·侵蚀日》SO 架构迁移 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不破坏当前循环的前提下，迁移至由静态 SO 定义、非 SO `GameRunState`、授权变更集、唯一 `GamePhaseCoordinator` 与提交后结果组成的旅馆运行时架构。

**Architecture:** 以 M1–M6 独立验收的垂直里程碑实施。协调器和领域服务只创建 `AuthorizedChangeSet`；`IStateReducer` 是唯一写入 `GameRunState` 的组件。组合根注册结算/决策提供者并创建单局服务图；UI 只能提交命令、读投影、订阅提交后事实。旧 `TimeManager` 先被可逆地限制为纯展示时钟，只有协调器已独占阶段转换后才弃用和移动。

**Tech Stack:** Unity 2022.3.62f3c1、C#、URP 14.0.12、Unity Test Framework 1.1.33、NUnit、现有 `GameEvent<T>` SO 通道、MCP for Unity。

## Global Constraints

- 静态规则、内容和数值使用 SO；局内可变状态只能是可序列化非 SO `GameRunState`，不得跨局共享或保存为 Project 资产。
- `GameRunState` 只包含稳定 ID、值对象、集合和基础可序列化数据；不得包含 `MonoBehaviour`、`GameObject`、`Component`、UI 或可变 SO 引用。
- 固定循环是 `Dawn → Day → Dusk → Night → 次日 Dawn`；只允许 `DayCycleDefinition` 的显式顺序决定，不得依据枚举声明顺序。
- `GamePhaseCoordinator` 是唯一阶段授权者，但**绝不直接写 `GameRunState`**；它只能创建/授权变更集，所有 Enter、Settled、Waiting、Exiting、Completed、决策、日志、事件计划和总结状态均由归约器提交。
- `IStateReducer.TryCommit` 是唯一状态写入口；任一变更集中的一项验证失败，整个变更集、版本、日志和结果均不改变。
- 只有协调器可以创建包含 `SetPhaseLifecycleChange`、`SetCurrentPhaseChange` 或 `SetRunSummaryChange` 的变更集；领域服务只能创建自己的领域变更。
- 网关只路由命令：推进到协调器，事件/房间/岗位/资源/房客到对应服务；SO 结果通道只发布提交成功后的事实，不能路由命令。
- 阶段依赖命令先请求阶段访问裁决；完成阻塞决策还请求完成裁决。重复、过期、错 run、错版本、错阶段和拒绝请求不得产生副作用。
- UI/表现只读投影、提交明确命令、消费已提交结果；不得解释效果或直接修改阶段、资源、侵蚀、房客。
- 每阶段最多结算一次、每个阻塞决策最多完成一次。每个 `(Day, Night)` occurrence 至少有一条事件计划/结算历史；第 30 日 Night 结算直接生成总结，不得创建第 31 日。
- 真实侵蚀按房客保存并限制为 0–100；颜色由真实侵蚀和 `ErosionDefinition` 推导；玩家标记不改变真实侵蚀。
- 新 `Assets/Scripts/Hotel/{Authoring,Runtime,Presentation,Integration}` 不能依赖 `Legacy/TimeSystem`。仅短期适配层可将旧入口单向转换为新命令，不能反向写入 `TimeState`。
- 当前工作区有无关未提交的场景、事件/时间脚本、SO 资产、相机资源和 `docs/` 变更。不得覆盖、还原、删除、暂存它们；不得使用 `git add .`、`git add -A` 或宽泛暂存。
- 提交只是用户批准后的建议；本计划阶段不得执行 Git 写操作，也不得修改 Unity C#、资产、Prefab 或场景。

---

## 实施状态（2026-08-01）

- **M1 部分已创建，尚未完成：**已编写 `Runtime/Authoring` asmdef、测试 asmdef、`RunModel.cs`、`DayCycleDefinition.cs`，以及当前五个 EditMode 测试。后续 M1 跟进改动前曾报告测试 3/3 通过；但 Unity MCP 在后续验证中无响应，最终全量验证仍待完成。不得勾选 Task 1 或标记 M1 完成。
- **独立范围的 MainScene `NextPhasePanel` 接线缺陷已修复：**已从 `GameCanvas/UIManager` 移除 `NextPhasePanel`，唯一组件现位于 `GameCanvas/NextPhasePanel`，并带有 `CanvasGroup` 和按钮。此前报告回归测试 `MainSceneNextPhasePanelWiringTests` 1/1 通过，尚未独立复核；Play Mode 运行时及 Console 验证因 MCP 断开而受阻，仍待完成。
- 此缺陷修复是 **M5 之外的例外**，不得解释为 M5 完成或 Legacy 迁移完成。

## 0. 当前映射、程序集与验证约定

**当前事实：** `Assets/Scripts/Hotel/Managers/GamePhaseManager.cs` 与 `TimeManager.cs` 都公开 `AdvancePhase()`；`EventManager.cs` 以队列/预生成事件决定 Dawn/Dusk；`EventUI.cs` 直接调用 `ErosionManager.Instance.ModifyErosion()`；`ErosionManager.cs` 保存全局侵蚀。当前没有 asmdef 或 Unity 测试源。

**创建程序集：**

```json
// Assets/Scripts/Hotel/Runtime/Hotel.Runtime.asmdef
{"name":"Hotel.Runtime","rootNamespace":"Hotel.Runtime","references":[],"includePlatforms":[],"excludePlatforms":[],"allowUnsafeCode":false,"autoReferenced":true,"overrideReferences":false,"precompiledReferences":[],"defineConstraints":[],"versionDefines":[],"noEngineReferences":false}
// Assets/Scripts/Hotel/Authoring/Hotel.Authoring.asmdef
{"name":"Hotel.Authoring","rootNamespace":"Hotel.Authoring","references":["Hotel.Runtime"],"includePlatforms":[],"excludePlatforms":[],"allowUnsafeCode":false,"autoReferenced":true,"overrideReferences":false,"precompiledReferences":[],"defineConstraints":[],"versionDefines":[],"noEngineReferences":false}
// Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef
{"name":"Hotel.Runtime.Tests","rootNamespace":"Hotel.Runtime.Tests","references":["Hotel.Runtime","Hotel.Authoring"],"includePlatforms":["Editor"],"excludePlatforms":[],"allowUnsafeCode":false,"autoReferenced":false,"overrideReferences":false,"precompiledReferences":[],"defineConstraints":[],"versionDefines":[],"noEngineReferences":false,"optionalUnityReferences":["TestAssemblies"]}
```

不为现有 `Assets/Scripts/Core/Events/` 新建 asmdef：它目前与 Assembly-CSharp 的旧酒店类型双向相连。强类型结果通道放在无 asmdef 的 `Assets/Scripts/Hotel/Presentation/Results/`，由表现层使用；纯运行时测试不引用它。

### 项目结构优化方向

保留 `Authoring / Runtime / Presentation / Integration` 四层边界；`Runtime` 内统一采用 **`Kernel + 领域纵向切片`**，不再以全局 `State / Reducers / Services / Queries` 作为顶层职责目录：

```text
Assets/Scripts/Hotel/
├─ Authoring/
├─ Runtime/
│  ├─ Kernel/             # 跨领域状态、Changes、Reducer、阶段协调、命令门面、投影契约
│  ├─ Events/             # 事件命令、规划、结算及领域结果
│  ├─ Tenants/            # 房客领域
│  ├─ Rooms/              # 房间领域
│  ├─ Jobs/               # 岗位领域
│  ├─ Resources/          # 资源领域
│  └─ Erosion/            # 侵蚀领域
├─ Presentation/
└─ Integration/
```

- `Kernel` 只容纳跨两个及以上领域共享的运行时状态与值对象、`RunChange`/`AuthorizedChangeSet`、`IStateReducer`/`StateReducer`、阶段协调、跨领域命令门面和只读投影契约；领域规则、命令与服务必须落入对应纵向切片。
- 迁移新增文件按上述目录落位；已创建文件在所属里程碑内按同一映射调整，移动时保留 `.meta`。不得仅为整理目录跨越里程碑门或扩大当次修改范围。
- 当前迁移阶段仍只使用单一 `Hotel.Runtime.asmdef` 覆盖整个 `Runtime`，不拆分多领域程序集；待 M1–M6 全部完成并通过门禁后，再根据稳定依赖和编译收益评估是否拆分。
- 这是目录与职责优化，不改变第 1 节冻结契约、`Authoring → Runtime` 等既定依赖方向、M1–M6 顺序或任何现有行为。

**测试命令：** 单测试：`unityMCP_run_tests(mode="EditMode", test_names=["Hotel.Runtime.Tests.Type.Method"], init_timeout=120000)`；随后 `unityMCP_get_test_job(job_id, wait_timeout=60, include_failed_tests=true)`。全量：`unityMCP_run_tests(mode="EditMode", assembly_names=["Hotel.Runtime.Tests"], init_timeout=120000)`，预期 `failed=0`。每次脚本修改后确认 `mcpforunity://editor/state` 的 `data.compilation.is_compiling=false` 并检查 Console 无新增 error。首次失败测试允许 `CS0246`/`CS0103` 缺失类型诊断；实现后必须变为测试断言 PASS。

|里程碑|独立成果|后续门|
|---|---|---|
|M1|状态、变更、归约器、测试基础|无场景/UI/旧管理器改动；纯 EditMode 全绿。|
|M2|严格 reducer 驱动的阶段管线|Enter/settle/decision/wait/exit 无直接写状态。|
|M3|网关、事件定义、确定性计划/结算|事件只经裁决、变更集、提交后结果。|
|M4|房客/房间/岗位/资源/侵蚀领域|全部领域写入均经归约器。|
|M5|组合根、只读表现、计时器 containment|协调器为活跃场景唯一阶段提交者。|
|M6|Legacy 隔离、深快照、30 天回归|无新运行时 Legacy 引用。|

## 1. 冻结的核心模型、RunChange 与跨任务契约

所有本节运行时类型位于 `Hotel.Runtime`，先在 Task 1 建立；Task 2 以后不得改变签名或绕过它们。

```csharp
// Assets/Scripts/Hotel/Runtime/Kernel/State/RunModel.cs
using System; using System.Collections.Generic;
namespace Hotel.Runtime {
 public readonly struct RunId { public RunId(string value){Value=value;} public string Value {get;} }
 public enum HotelPhase { Dawn, Day, Dusk, Night }
 public enum PhaseLifecycleState { Entered, Settled, WaitingForDecisions, Exiting, Completed }
 public interface IPhaseCycle { HotelPhase GetNext(HotelPhase phase); }
 [Serializable] public sealed class PhaseRunState { public HotelPhase Current=HotelPhase.Dawn; public PhaseLifecycleState Lifecycle=PhaseLifecycleState.Entered; public int Occurrence=1; }
 [Serializable] public sealed class DecisionRunState { public string DecisionId; public HotelPhase Phase; public int Day; public bool IsBlocking; public string SourceId; public bool IsCompleted; }
 [Serializable] public sealed class EventHistoryRecord { public string EventId; public string DefinitionId; public int Day; public HotelPhase Phase; public int Occurrence; public bool RequiresDecision; public bool Resolved; public string OptionId; }
 [Serializable] public sealed class TenantRunState { public string TenantId; public string DefinitionId; public float TrueErosion; public bool PlayerMarked; public string RoomId; public string JobId; }
 [Serializable] public sealed class RoomRunState { public string RoomId; public string DefinitionId; public List<string> OccupantIds=new List<string>(); }
 [Serializable] public sealed class ResourceRunState { public string ResourceId; public string DefinitionId; public int Amount; }
 [Serializable] public sealed class RunSummaryState { public bool IsComplete; public int CompletedDay; public int MisclassificationCount; public int FinalTenantCount; }
 [Serializable] public sealed class GameRunState {
  public RunId RunId; public long StateVersion; public int Day; public int Seed; public PhaseRunState Phase=new PhaseRunState();
  public List<DecisionRunState> Decisions=new List<DecisionRunState>(); public List<EventHistoryRecord> EventHistory=new List<EventHistoryRecord>(); public List<string> AuditLog=new List<string>();
  public Dictionary<string,TenantRunState> Tenants=new Dictionary<string,TenantRunState>(); public Dictionary<string,RoomRunState> Rooms=new Dictionary<string,RoomRunState>(); public Dictionary<string,ResourceRunState> Resources=new Dictionary<string,ResourceRunState>(); public RunSummaryState Summary=new RunSummaryState();
  public static GameRunState New(RunId id,int seed=1)=>new GameRunState{RunId=id,Day=1,Seed=seed,StateVersion=0};
 }
}
```

```csharp
// Assets/Scripts/Hotel/Runtime/Kernel/Changes/RunChanges.cs
using System; using System.Collections.Generic;
namespace Hotel.Runtime {
 public abstract class RunChange { }
 public sealed class SetPhaseLifecycleChange:RunChange { public SetPhaseLifecycleChange(PhaseLifecycleState value){Value=value;} public PhaseLifecycleState Value{get;} }
 public sealed class SetCurrentPhaseChange:RunChange { public SetCurrentPhaseChange(HotelPhase phase,int day,int occurrence){Phase=phase;Day=day;Occurrence=occurrence;} public HotelPhase Phase{get;} public int Day{get;} public int Occurrence{get;} }
 public sealed class CreateDecisionChange:RunChange { public CreateDecisionChange(DecisionRunState value){Value=value;} public DecisionRunState Value{get;} }
 public sealed class CompleteDecisionChange:RunChange { public CompleteDecisionChange(string id){DecisionId=id;} public string DecisionId{get;} }
 public sealed class AppendAuditLogChange:RunChange { public AppendAuditLogChange(string value){Value=value;} public string Value{get;} }
 public sealed class SetRunSummaryChange:RunChange { public SetRunSummaryChange(RunSummaryState value){Value=value;} public RunSummaryState Value{get;} }
 public sealed class PlanEventHistoryChange:RunChange { public PlanEventHistoryChange(EventHistoryRecord value){Value=value;} public EventHistoryRecord Value{get;} }
 public sealed class ResolveEventHistoryChange:RunChange { public ResolveEventHistoryChange(string id,string option){EventId=id;OptionId=option;} public string EventId{get;} public string OptionId{get;} }
 public sealed class SetTenantMarkChange:RunChange { public SetTenantMarkChange(string id,bool value){TenantId=id;Value=value;} public string TenantId{get;} public bool Value{get;} }
 public sealed class AdjustTenantErosionChange:RunChange { public AdjustTenantErosionChange(string id,float delta){TenantId=id;Delta=delta;} public string TenantId{get;} public float Delta{get;} }
 public sealed class AssignRoomChange:RunChange { public AssignRoomChange(string tenant,string room){TenantId=tenant;RoomId=room;} public string TenantId{get;} public string RoomId{get;} }
 public sealed class AssignJobChange:RunChange { public AssignJobChange(string tenant,string job){TenantId=tenant;JobId=job;} public string TenantId{get;} public string JobId{get;} }
 public sealed class AdjustResourceChange:RunChange { public AdjustResourceChange(string id,int delta){ResourceId=id;Delta=delta;} public string ResourceId{get;} public int Delta{get;} }
 }
```

```csharp
// Assets/Scripts/Hotel/Runtime/Kernel/Changes/AuthorizedChangeSet.cs and Kernel/Reduction/StateReducer.cs
using System; using System.Collections.Generic; using System.Linq; using UnityEngine;
namespace Hotel.Runtime {
 public sealed class AuthorizedChangeSet {
  private readonly List<RunChange> _changes=new List<RunChange>(); private AuthorizedChangeSet(RunId run,long version,string authorizer,string command){RunId=run;ExpectedStateVersion=version;AuthorizerId=authorizer;CommandId=command;}
  public RunId RunId{get;} public long ExpectedStateVersion{get;} public string AuthorizerId{get;} public string CommandId{get;} public IReadOnlyList<RunChange> Changes=>_changes;
  public static AuthorizedChangeSet Coordinator(RunId r,long v,string command)=>new AuthorizedChangeSet(r,v,"GamePhaseCoordinator",command);
  public static AuthorizedChangeSet Domain(RunId r,long v,string authorizer,string command)=>new AuthorizedChangeSet(r,v,authorizer,command);
  public void Add(RunChange change)=>_changes.Add(change);
 }
 public readonly struct CommitResult { public CommitResult(bool succeeded){Succeeded=succeeded;} public bool Succeeded{get;} }
 public interface IStateReducer { CommitResult TryCommit(GameRunState state,AuthorizedChangeSet changes); }
 public sealed class StateReducer:IStateReducer {
  public CommitResult TryCommit(GameRunState state,AuthorizedChangeSet set) {
   if(state.RunId.Value!=set.RunId.Value||state.StateVersion!=set.ExpectedStateVersion||!Validate(state,set)) return new CommitResult(false);
   foreach(var c in set.Changes) Apply(state,c); state.StateVersion++; return new CommitResult(true);
  }
  private static bool Validate(GameRunState s,AuthorizedChangeSet set) {
   foreach(var c in set.Changes) {
    if((c is SetPhaseLifecycleChange||c is SetCurrentPhaseChange||c is SetRunSummaryChange) && set.AuthorizerId!="GamePhaseCoordinator") return false;
    if(c is CompleteDecisionChange done && !s.Decisions.Any(x=>x.DecisionId==done.DecisionId&&!x.IsCompleted)) return false;
    if(c is PlanEventHistoryChange plan && s.EventHistory.Any(x=>x.EventId==plan.Value.EventId)) return false;
    if(c is ResolveEventHistoryChange resolved && !s.EventHistory.Any(x=>x.EventId==resolved.EventId&&!x.Resolved)) return false;
    if(c is SetTenantMarkChange mark&&!s.Tenants.ContainsKey(mark.TenantId)) return false;
    if(c is AdjustTenantErosionChange erosion&&!s.Tenants.ContainsKey(erosion.TenantId)) return false;
    if(c is AssignRoomChange room&&(!s.Tenants.ContainsKey(room.TenantId)||!s.Rooms.ContainsKey(room.RoomId))) return false;
    if(c is AssignJobChange job&&!s.Tenants.ContainsKey(job.TenantId)) return false;
    if(c is AdjustResourceChange resource&&!s.Resources.ContainsKey(resource.ResourceId)) return false;
   } return true;
  }
  private static void Apply(GameRunState s,RunChange c) {
   switch(c) {
    case SetPhaseLifecycleChange x: s.Phase.Lifecycle=x.Value; break;
    case SetCurrentPhaseChange x: s.Phase.Current=x.Phase; s.Day=x.Day; s.Phase.Occurrence=x.Occurrence; break;
    case CreateDecisionChange x: s.Decisions.Add(x.Value); break;
    case CompleteDecisionChange x: s.Decisions.Single(d=>d.DecisionId==x.DecisionId).IsCompleted=true; break;
    case AppendAuditLogChange x: s.AuditLog.Add(x.Value); break;
    case SetRunSummaryChange x: s.Summary=x.Value; break;
    case PlanEventHistoryChange x: s.EventHistory.Add(x.Value); break;
    case ResolveEventHistoryChange x: var e=s.EventHistory.Single(h=>h.EventId==x.EventId); e.Resolved=true; e.OptionId=x.OptionId; break;
    case SetTenantMarkChange x: s.Tenants[x.TenantId].PlayerMarked=x.Value; break;
    case AdjustTenantErosionChange x: s.Tenants[x.TenantId].TrueErosion=Mathf.Clamp(s.Tenants[x.TenantId].TrueErosion+x.Delta,0f,100f); break;
    case AssignRoomChange x: s.Tenants[x.TenantId].RoomId=x.RoomId; s.Rooms[x.RoomId].OccupantIds.Add(x.TenantId); break;
    case AssignJobChange x: s.Tenants[x.TenantId].JobId=x.JobId; break;
    case AdjustResourceChange x: s.Resources[x.ResourceId].Amount+=x.Delta; break;
   }
  }
 }
}
```

```csharp
// Assets/Scripts/Hotel/Runtime/Kernel/Coordination/PhaseContracts.cs
using System.Collections.Generic;
namespace Hotel.Runtime {
 public readonly struct PhaseExecutionContext { public PhaseExecutionContext(GameRunState state){State=state;Day=state.Day;Phase=state.Phase.Current;Occurrence=state.Phase.Occurrence;} public GameRunState State{get;} public int Day{get;} public HotelPhase Phase{get;} public int Occurrence{get;} }
 public interface IPhaseSettlementProvider { string ProviderId{get;} AuthorizedChangeSet BuildSettlement(PhaseExecutionContext context); }
 public interface IDecisionProvider { string ProviderId{get;} IReadOnlyList<DecisionRunState> Collect(PhaseExecutionContext context); }
 public readonly struct AdvancePhaseCommand { public AdvancePhaseCommand(string id,RunId run,long version){CommandId=id;RunId=run;ExpectedStateVersion=version;} public string CommandId{get;} public RunId RunId{get;} public long ExpectedStateVersion{get;} }
 public readonly struct PhaseAccessRequest { public PhaseAccessRequest(RunId run,long version,HotelPhase phase){RunId=run;ExpectedStateVersion=version;Phase=phase;} public RunId RunId{get;} public long ExpectedStateVersion{get;} public HotelPhase Phase{get;} }
 public readonly struct DecisionCompletionRequest { public DecisionCompletionRequest(string id,RunId run,long version,HotelPhase phase,string command){DecisionId=id;RunId=run;ExpectedStateVersion=version;Phase=phase;CommandId=command;} public string DecisionId{get;} public RunId RunId{get;} public long ExpectedStateVersion{get;} public HotelPhase Phase{get;} public string CommandId{get;} }
 public enum CommandResult { Accepted, Rejected } public enum PhaseAccessDecision { Allowed, Denied } public enum DecisionCompletionDecision { Approved, Denied }
 public interface IPhaseAccessAuthority { PhaseAccessDecision RequestAccess(PhaseAccessRequest request); }
 public interface IPhaseDecisionAuthority { DecisionCompletionDecision RequestCompletion(DecisionCompletionRequest request); }
}
```

**注册与严格顺序：** 只有 `HotelRunCompositionRoot` 在每次创建新 run 时注册 provider。`RegisterSettlementProvider` 与 `RegisterDecisionProvider` 都以 `ProviderId` 的 `StringComparer.Ordinal` 排序，重复 ID 抛 `InvalidOperationException`。每次推进严格提交：Enter lifecycle → 所有 settlement provider（每个集合各自提交）→ Settled lifecycle → 从结算后快照收集决策并提交全部 `CreateDecisionChange` → Waiting 或 Exiting lifecycle → Exit audit/phase transition/Completed lifecycle 或 run summary。provider 不能调用 reducer、不能发布结果、不能创建 phase/lifecycle/summary 变更。

---

### Task 1: M1 — 状态、授权变更与机械归约器

**Files:**
- Create: `Assets/Scripts/Hotel/Runtime/Hotel.Runtime.asmdef`, `Assets/Scripts/Hotel/Authoring/Hotel.Authoring.asmdef`, `Assets/Tests/Hotel.Runtime.Tests/Hotel.Runtime.Tests.asmdef`
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/State/RunModel.cs`, `Assets/Scripts/Hotel/Runtime/Kernel/Changes/RunChanges.cs`, `AuthorizedChangeSet.cs`, `Assets/Scripts/Hotel/Runtime/Kernel/Reduction/StateReducer.cs`
- Create: `Assets/Scripts/Hotel/Authoring/DayCycle/DayCycleDefinition.cs`
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/StateReducerTests.cs`

**Interfaces:** 产出第 1 节全部 model/reducer 契约。新运行时代码不得使用旧 `GamePhase`/`TimePhase`。

- [ ] **Step 1: 写失败测试。**

```csharp
using NUnit.Framework; using Hotel.Runtime;
namespace Hotel.Runtime.Tests { public sealed class StateReducerTests {
 [Test] public void CoordinatorLifecycleCommit_IsReducerOnlyMutation() {
  var state=GameRunState.New(new RunId("r")); var set=AuthorizedChangeSet.Coordinator(state.RunId,state.StateVersion,"enter"); set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered));
  var result=new StateReducer().TryCommit(state,set);
  Assert.That(result.Succeeded,Is.True); Assert.That(state.StateVersion,Is.EqualTo(1)); Assert.That(state.Phase.Lifecycle,Is.EqualTo(PhaseLifecycleState.Entered));
 }
 [Test] public void DomainSet_CannotSubmitPhaseLifecycle() {
  var state=GameRunState.New(new RunId("r")); var set=AuthorizedChangeSet.Domain(state.RunId,state.StateVersion,"events","bad"); set.Add(new SetPhaseLifecycleChange(PhaseLifecycleState.Settled));
  var result=new StateReducer().TryCommit(state,set);
  Assert.That(result.Succeeded,Is.False); Assert.That(state.StateVersion,Is.EqualTo(0));
 }
} }
```

- [ ] **Step 2: 运行失败测试。** Run `StateReducerTests.CoordinatorLifecycleCommit_IsReducerOnlyMutation`; expected compile failure `CS0246` for `Hotel.Runtime`/`GameRunState` before implementation.
- [ ] **Step 3: 实现第 1 节 `RunModel`、`RunChanges`、`AuthorizedChangeSet`、`StateReducer` 和如下 cycle。**

```csharp
using System; using Hotel.Runtime; using UnityEngine;
namespace Hotel.Authoring.DayCycle { [CreateAssetMenu(menuName="Hotel/Day Cycle")] public sealed class DayCycleDefinition:ScriptableObject,IPhaseCycle {
 [SerializeField] private HotelPhase[] ordered={HotelPhase.Dawn,HotelPhase.Day,HotelPhase.Dusk,HotelPhase.Night};
 public static DayCycleDefinition CreateDefault()=>CreateInstance<DayCycleDefinition>();
 public HotelPhase GetNext(HotelPhase p){var i=Array.IndexOf(ordered,p);if(i<0)throw new ArgumentOutOfRangeException(nameof(p));return ordered[(i+1)%ordered.Length];}
 public string Validate()=>ordered.Length==4&&ordered[0]==HotelPhase.Dawn&&ordered[1]==HotelPhase.Day&&ordered[2]==HotelPhase.Dusk&&ordered[3]==HotelPhase.Night?string.Empty:"Cycle must be Dawn,Day,Dusk,Night.";
} }
```

- [ ] **Step 4: 回归与门。** Run all `Hotel.Runtime.Tests`; expected PASS. Add tests for atomic rejection of mixed valid/invalid changes, duplicate event IDs, phase change by non-coordinator, and no state version/log mutation on reject. Do not modify scene/UI/old manager.

### Task 2: M2 — 完全 reducer 驱动的阶段协调器

**Files:**
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Coordination/PhaseContracts.cs`, `GamePhaseCoordinator.cs`
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/TestDoubles.cs`, `GamePhaseCoordinatorTests.cs`
- Modify: no existing Unity source.

**Interfaces:** Consumes section 1 exactly. Produces `RegisterSettlementProvider`, `RegisterDecisionProvider`, `RequestAdvance`, `RequestAccess`, `RequestCompletion`. Task 6 composition root is the production registration owner; tests may register pure doubles only.

- [ ] **Step 1: 写失败测试和 doubles。**

```csharp
using System.Collections.Generic; using NUnit.Framework; using Hotel.Runtime; using Hotel.Authoring.DayCycle;
namespace Hotel.Runtime.Tests {
 internal sealed class NoOpSettlementProvider:IPhaseSettlementProvider { public NoOpSettlementProvider(string id){ProviderId=id;} public string ProviderId{get;} public AuthorizedChangeSet BuildSettlement(PhaseExecutionContext c)=>AuthorizedChangeSet.Domain(c.State.RunId,c.State.StateVersion,ProviderId,"settle-"+ProviderId); }
 internal sealed class FixedDecisionProvider:IDecisionProvider { private readonly IReadOnlyList<DecisionRunState> _items; public FixedDecisionProvider(string id,IReadOnlyList<DecisionRunState> items){ProviderId=id;_items=items;} public string ProviderId{get;} public IReadOnlyList<DecisionRunState> Collect(PhaseExecutionContext c)=>_items; }
 public sealed class GamePhaseCoordinatorTests {
  [Test] public void EmptyDawn_UsesCommittedEnterSettleExitAndTransition() {
   var s=GameRunState.New(new RunId("r")); var c=new GamePhaseCoordinator(s,DayCycleDefinition.CreateDefault(),new StateReducer()); c.RegisterSettlementProvider(new NoOpSettlementProvider("settle")); c.RegisterDecisionProvider(new FixedDecisionProvider("decisions",new List<DecisionRunState>()));
   var result=c.RequestAdvance(new AdvancePhaseCommand("advance",s.RunId,s.StateVersion));
   Assert.That(result,Is.EqualTo(CommandResult.Accepted)); Assert.That(s.Phase.Current,Is.EqualTo(HotelPhase.Day)); Assert.That(s.Phase.Lifecycle,Is.EqualTo(PhaseLifecycleState.Completed)); Assert.That(s.AuditLog,Has.Some.Contains("无待处理决策"));
  }
  [Test] public void BlockingDecision_WaitsThenCompletesOnce() {
   var s=GameRunState.New(new RunId("r")); var d=new DecisionRunState{DecisionId="d",Day=1,Phase=HotelPhase.Dawn,IsBlocking=true,SourceId="events"}; var c=new GamePhaseCoordinator(s,DayCycleDefinition.CreateDefault(),new StateReducer()); c.RegisterDecisionProvider(new FixedDecisionProvider("events",new[]{d}));
   Assert.That(c.RequestAdvance(new AdvancePhaseCommand("a",s.RunId,s.StateVersion)),Is.EqualTo(CommandResult.Accepted)); Assert.That(s.Phase.Lifecycle,Is.EqualTo(PhaseLifecycleState.WaitingForDecisions));
   var decision=c.RequestCompletion(new DecisionCompletionRequest("d",s.RunId,s.StateVersion,HotelPhase.Dawn,"resolve")); Assert.That(decision,Is.EqualTo(DecisionCompletionDecision.Approved));
   Assert.That(c.RequestCompletion(new DecisionCompletionRequest("d",s.RunId,s.StateVersion,HotelPhase.Dawn,"again")),Is.EqualTo(DecisionCompletionDecision.Denied));
  }
 } }
```

- [ ] **Step 2: 运行失败测试。** Expected `CS0246` for `GamePhaseCoordinator`/contracts, not a passing test.
- [ ] **Step 3: 实现协调器；每个状态变化都创建并提交变更集。**

```csharp
using System; using System.Collections.Generic; using System.Linq;
namespace Hotel.Runtime { public sealed class GamePhaseCoordinator:IPhaseAccessAuthority,IPhaseDecisionAuthority {
 private readonly GameRunState _state; private readonly IPhaseCycle _cycle; private readonly IStateReducer _reducer; private readonly List<IPhaseSettlementProvider> _settlers=new List<IPhaseSettlementProvider>(); private readonly List<IDecisionProvider> _deciders=new List<IDecisionProvider>();
 public GamePhaseCoordinator(GameRunState state,IPhaseCycle cycle,IStateReducer reducer){_state=state;_cycle=cycle;_reducer=reducer;}
 public void RegisterSettlementProvider(IPhaseSettlementProvider p){if(_settlers.Any(x=>x.ProviderId==p.ProviderId))throw new InvalidOperationException();_settlers.Add(p);_settlers.Sort((a,b)=>StringComparer.Ordinal.Compare(a.ProviderId,b.ProviderId));}
 public void RegisterDecisionProvider(IDecisionProvider p){if(_deciders.Any(x=>x.ProviderId==p.ProviderId))throw new InvalidOperationException();_deciders.Add(p);_deciders.Sort((a,b)=>StringComparer.Ordinal.Compare(a.ProviderId,b.ProviderId));}
 private bool Commit(params RunChange[] changes){var set=AuthorizedChangeSet.Coordinator(_state.RunId,_state.StateVersion,"phase-"+_state.StateVersion);foreach(var c in changes)set.Add(c);return _reducer.TryCommit(_state,set).Succeeded;}
 public CommandResult RequestAdvance(AdvancePhaseCommand command){
  if(command.RunId.Value!=_state.RunId.Value||command.ExpectedStateVersion!=_state.StateVersion||_state.Summary.IsComplete)return CommandResult.Rejected;
  if(_state.Phase.Lifecycle==PhaseLifecycleState.WaitingForDecisions)return _state.Decisions.Where(x=>x.Day==_state.Day&&x.Phase==_state.Phase.Current).All(x=>x.IsCompleted)?Exit(command.CommandId):CommandResult.Rejected;
  if(!Commit(new SetPhaseLifecycleChange(PhaseLifecycleState.Entered)))return CommandResult.Rejected;
  foreach(var p in _settlers){var set=p.BuildSettlement(new PhaseExecutionContext(_state));if(!_reducer.TryCommit(_state,set).Succeeded)return CommandResult.Rejected;}
  if(!Commit(new SetPhaseLifecycleChange(PhaseLifecycleState.Settled)))return CommandResult.Rejected;
  var decisions=_deciders.SelectMany(p=>p.Collect(new PhaseExecutionContext(_state))).OrderBy(x=>x.DecisionId,StringComparer.Ordinal).ToArray();
  if(decisions.Length>0&&!Commit(decisions.Select(x=>(RunChange)new CreateDecisionChange(x)).ToArray()))return CommandResult.Rejected;
  if(decisions.Any(x=>x.IsBlocking))return Commit(new SetPhaseLifecycleChange(PhaseLifecycleState.WaitingForDecisions))?CommandResult.Accepted:CommandResult.Rejected;
  return Exit(command.CommandId);
 }
 private CommandResult Exit(string commandId){
  if(!Commit(new SetPhaseLifecycleChange(PhaseLifecycleState.Exiting)))return CommandResult.Rejected;
  if(_state.Day==30&&_state.Phase.Current==HotelPhase.Night){var summary=new RunSummaryState{IsComplete=true,CompletedDay=30,FinalTenantCount=_state.Tenants.Count};return Commit(new AppendAuditLogChange("第 30 日黑夜：生成对局总结"),new SetRunSummaryChange(summary),new SetPhaseLifecycleChange(PhaseLifecycleState.Completed))?CommandResult.Accepted:CommandResult.Rejected;}
  var next=_cycle.GetNext(_state.Phase.Current);var day=next==HotelPhase.Dawn?_state.Day+1:_state.Day;var occurrence=next==HotelPhase.Dawn?_state.Phase.Occurrence+1:_state.Phase.Occurrence;
  return Commit(new AppendAuditLogChange($"第 {_state.Day} 日 {_state.Phase.Current}：无待处理决策，自动进入 {next}"),new SetCurrentPhaseChange(next,day,occurrence),new SetPhaseLifecycleChange(PhaseLifecycleState.Completed))?CommandResult.Accepted:CommandResult.Rejected;
 }
 public PhaseAccessDecision RequestAccess(PhaseAccessRequest r)=>r.RunId.Value==_state.RunId.Value&&r.ExpectedStateVersion==_state.StateVersion&&r.Phase==_state.Phase.Current?PhaseAccessDecision.Allowed:PhaseAccessDecision.Denied;
 public DecisionCompletionDecision RequestCompletion(DecisionCompletionRequest r){if(r.RunId.Value!=_state.RunId.Value||r.ExpectedStateVersion!=_state.StateVersion||r.Phase!=_state.Phase.Current)return DecisionCompletionDecision.Denied;var item=_state.Decisions.SingleOrDefault(x=>x.DecisionId==r.DecisionId&&!x.IsCompleted);if(item==null)return DecisionCompletionDecision.Denied;var set=AuthorizedChangeSet.Coordinator(_state.RunId,_state.StateVersion,r.CommandId);set.Add(new CompleteDecisionChange(r.DecisionId));return _reducer.TryCommit(_state,set).Succeeded?DecisionCompletionDecision.Approved:DecisionCompletionDecision.Denied;}
} }
```

- [ ] **Step 4: 回归与门。** Add named tests `NightToDawn_IncrementsDayExactlyOnce`, `DayThirtyNight_CommitsRunSummaryWithoutDayThirtyOne`, `ProviderOrder_IsOrdinal`, and `CoordinatorSource_HasNoStateAssignmentOutsideConstructor`. Full test suite PASS; no old manager or scene change.

### Task 3: M3 — 命令网关、无副作用事件规划、提交后结果

**Files:**
- Create: `Assets/Scripts/Hotel/Authoring/Events/EventDefinition.cs`, `EventConditionDefinition.cs`, `EventEffectDefinition.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Commands/CommandGateway.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Events/EventCommands.cs`, `EventPlanningService.cs`, `EventResolutionService.cs`, `DeterministicRandomSource.cs`, `IResultSink.cs`
- Create: `Assets/Scripts/Hotel/Presentation/Results/EventResolvedResultChannel.cs`
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/EventServiceTests.cs`, `CommandGatewayTests.cs`

**Interfaces:** `EventPlanningService` implements both provider interfaces but only returns changes/decisions. `EventResolutionService.Resolve` obtains coordinator access/completion decisions, creates a domain set, commits it, then invokes `IResultSink.Publish`. Gateway routes, never mutates.

- [ ] **Step 1: 写失败测试。**

```csharp
using System.Collections.Generic; using NUnit.Framework; using Hotel.Runtime;
namespace Hotel.Runtime.Tests { public sealed class EventServiceTests {
 [Test] public void NightGuarantee_IsPerDayAndNightOccurrence() {
  var s=GameRunState.New(new RunId("r")); s.Day=2;s.Phase.Current=HotelPhase.Night;s.Phase.Occurrence=2; var p=new EventPlanningService(new DeterministicRandomSource(7));
  var first=p.BuildSettlement(new PhaseExecutionContext(s)); Assert.That(first.Changes.Count,Is.EqualTo(1)); Assert.That(first.Changes[0],Is.TypeOf<PlanEventHistoryChange>());
  var reducer=new StateReducer();Assert.That(reducer.TryCommit(s,first).Succeeded,Is.True);
  var second=p.BuildSettlement(new PhaseExecutionContext(s)); Assert.That(second.Changes.Count,Is.EqualTo(0));
  s.Day=3;s.Phase.Occurrence=3;var third=p.BuildSettlement(new PhaseExecutionContext(s));Assert.That(third.Changes.Count,Is.EqualTo(1));
 }
 [Test] public void RejectedChoice_DoesNotCommitOrPublish() {
  var s=GameRunState.New(new RunId("r")); var sink=new RecordingResultSink(); var service=new EventResolutionService(s,new StateReducer(),sink);
  var result=service.Resolve(new ChooseEventOptionCommand("c",s.RunId,s.StateVersion,"event","option"),PhaseAccessDecision.Denied,DecisionCompletionDecision.Denied);
  Assert.That(result,Is.EqualTo(CommandResult.Rejected));Assert.That(s.StateVersion,Is.EqualTo(0));Assert.That(sink.Count,Is.EqualTo(0));
 }
 } }
```

- [ ] **Step 2: 运行失败测试。** Expected `CS0246` for event/gateway/result types.
- [ ] **Step 3: 实现作者定义、planner、sink、resolver 和 gateway。**

```csharp
// Authoring EventDefinition.cs
using System.Collections.Generic;using Hotel.Runtime;using UnityEngine;namespace Hotel.Authoring.Events { [CreateAssetMenu(menuName="Hotel/Event")]public sealed class EventDefinition:ScriptableObject{public string definitionId;public HotelPhase phase;public int weight=1;public bool requiresDecision;public List<string> optionIds=new List<string>();} }
// Runtime event support
using System;using System.Collections.Generic;using System.Linq;namespace Hotel.Runtime {
 public sealed class DeterministicRandomSource{private uint _x;public DeterministicRandomSource(uint seed){_x=seed;}public int Next(int max){_x=1664525*_x+1013904223;return(int)(_x%(uint)max);}}
 public interface IResultSink{void Publish(EventResolvedResult value);} public readonly struct EventResolvedResult{public EventResolvedResult(string id,long version){EventId=id;StateVersion=version;}public string EventId{get;}public long StateVersion{get;}}
 public sealed class EventPlanningService:IPhaseSettlementProvider,IDecisionProvider{private readonly DeterministicRandomSource _rng;public EventPlanningService(DeterministicRandomSource rng){_rng=rng;}public string ProviderId=>"events";
  public AuthorizedChangeSet BuildSettlement(PhaseExecutionContext c){var set=AuthorizedChangeSet.Domain(c.State.RunId,c.State.StateVersion,ProviderId,"plan-"+c.Day+"-"+c.Phase+"-"+c.Occurrence);var exists=c.State.EventHistory.Any(x=>x.Day==c.Day&&x.Phase==c.Phase&&x.Occurrence==c.Occurrence);if(c.Phase==HotelPhase.Night&&!exists)set.Add(new PlanEventHistoryChange(new EventHistoryRecord{EventId="night-"+c.Day+"-"+c.Occurrence,DefinitionId="night-guarantee",Day=c.Day,Phase=c.Phase,Occurrence=c.Occurrence,RequiresDecision=false}));return set;}
  public IReadOnlyList<DecisionRunState> Collect(PhaseExecutionContext c)=>c.State.EventHistory.Where(x=>x.Day==c.Day&&x.Phase==c.Phase&&x.Occurrence==c.Occurrence&&x.RequiresDecision&&!x.Resolved).Select(x=>new DecisionRunState{DecisionId="decision-"+x.EventId,Day=c.Day,Phase=c.Phase,IsBlocking=true,SourceId=ProviderId}).ToArray();}
 public readonly struct ChooseEventOptionCommand{public ChooseEventOptionCommand(string id,RunId run,long version,string eventId,string optionId){CommandId=id;RunId=run;ExpectedStateVersion=version;EventId=eventId;OptionId=optionId;}public string CommandId{get;}public RunId RunId{get;}public long ExpectedStateVersion{get;}public string EventId{get;}public string OptionId{get;}}
 public sealed class EventResolutionService{private readonly GameRunState _state;private readonly IStateReducer _reducer;private readonly IResultSink _sink;public EventResolutionService(GameRunState state,IStateReducer reducer,IResultSink sink){_state=state;_reducer=reducer;_sink=sink;}public CommandResult Resolve(ChooseEventOptionCommand c,PhaseAccessDecision access,DecisionCompletionDecision decision){if(access!=PhaseAccessDecision.Allowed||decision!=DecisionCompletionDecision.Approved)return CommandResult.Rejected;var set=AuthorizedChangeSet.Domain(_state.RunId,_state.StateVersion,"events",c.CommandId);set.Add(new ResolveEventHistoryChange(c.EventId,c.OptionId));if(!_reducer.TryCommit(_state,set).Succeeded)return CommandResult.Rejected;_sink.Publish(new EventResolvedResult(c.EventId,_state.StateVersion));return CommandResult.Accepted;}}
 public interface IHotelCommand{} public sealed class CommandGateway{private readonly GamePhaseCoordinator _coordinator;private readonly EventResolutionService _events;public CommandGateway(GamePhaseCoordinator coordinator,EventResolutionService events){_coordinator=coordinator;_events=events;}public CommandResult Submit(AdvancePhaseCommand c)=>_coordinator.RequestAdvance(c);public CommandResult Submit(ChooseEventOptionCommand c,PhaseAccessDecision access,DecisionCompletionDecision decision)=>_events.Resolve(c,access,decision);}
}
```

```csharp
// Test sink; EventServiceTests.cs
using Hotel.Runtime;namespace Hotel.Runtime.Tests { internal sealed class RecordingResultSink:IResultSink{public int Count{get;private set;}public EventResolvedResult Last{get;private set;}public void Publish(EventResolvedResult value){Count++;Last=value;} } }
```

- [ ] **Step 4: 回归与门。** Add `GatewayAdvance_RoutesOnlyToCoordinator`, `SameSeedAndContext_PlansSameEventId`, `SuccessfulChoice_CommitsHistoryBeforeSinkPublishes`, and `Planner_DoesNotMutateInputState`. Assert direct enum comparisons exactly: `Assert.That(result, Is.EqualTo(CommandResult.Accepted));` and `Assert.That(decision, Is.EqualTo(DecisionCompletionDecision.Approved));`. Full tests PASS; no new source references old `EventManager`, `EventUI`, `ErosionManager`, or `triggerCondition`.

### Task 4: M4 — 房客、房间、岗位、资源与侵蚀领域服务

**Files:**
- Create: `Assets/Scripts/Hotel/Authoring/Tenants/TenantDefinition.cs`, `Rooms/RoomDefinition.cs`, `Jobs/JobDefinition.cs`, `Resources/ResourceDefinition.cs`, `Erosion/ErosionDefinition.cs`, `Progression/ProgressionDefinition.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Tenants/TenantService.cs`, `Rooms/RoomService.cs`, `Jobs/JobService.cs`, `Resources/ResourceService.cs`, `Erosion/ErosionService.cs`
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/DomainServiceTests.cs`

**Interfaces:** 每个服务先取得 `PhaseAccessDecision.Allowed`，再创建 `AuthorizedChangeSet.Domain`，再让 reducer 提交；不直接写状态。`ErosionService` 是 settlement provider，ID 为 `erosion`。

- [ ] **Step 1: 写失败测试。**

```csharp
using NUnit.Framework;using Hotel.Runtime;namespace Hotel.Runtime.Tests { public sealed class DomainServiceTests {
  [Test] public void RoomCapacityAndResourceBounds_AreAtomic(){var s=GameRunState.New(new RunId("r"));s.Tenants["t"]=new TenantRunState{TenantId="t"};s.Rooms["r1"]=new RoomRunState{RoomId="r1"};s.Resources["cash"]=new ResourceRunState{ResourceId="cash",Amount=0};var reducer=new StateReducer();var rooms=new RoomService(s,reducer);var resources=new ResourceService(s,reducer);Assert.That(rooms.AssignRoom("t","r1",0,PhaseAccessDecision.Allowed),Is.EqualTo(CommandResult.Rejected));Assert.That(resources.Adjust("cash",-1,PhaseAccessDecision.Allowed),Is.EqualTo(CommandResult.Rejected));Assert.That(s.StateVersion,Is.EqualTo(0));}
 [Test] public void ErosionIsReducerCommittedAndClamped(){var s=GameRunState.New(new RunId("r"));s.Tenants["t"]=new TenantRunState{TenantId="t",TrueErosion=99};var set=new ErosionService(10).BuildSettlement(new PhaseExecutionContext(s));Assert.That(new StateReducer().TryCommit(s,set).Succeeded,Is.True);Assert.That(s.Tenants["t"].TrueErosion,Is.EqualTo(100f));}
} }
```

- [ ] **Step 2: 运行失败测试。** Expected missing service diagnostics.
- [ ] **Step 3: 实现服务。**

```csharp
using Hotel.Runtime;namespace Hotel.Runtime {
  public sealed class RoomService{private readonly GameRunState _s;private readonly IStateReducer _r;public RoomService(GameRunState s,IStateReducer r){_s=s;_r=r;}public CommandResult AssignRoom(string tenant,string room,int capacity,PhaseAccessDecision access){if(access!=PhaseAccessDecision.Allowed||!_s.Rooms.ContainsKey(room)||_s.Rooms[room].OccupantIds.Count>=capacity)return CommandResult.Rejected;var set=AuthorizedChangeSet.Domain(_s.RunId,_s.StateVersion,"rooms","room-"+tenant);set.Add(new AssignRoomChange(tenant,room));return _r.TryCommit(_s,set).Succeeded?CommandResult.Accepted:CommandResult.Rejected;}}
  public sealed class JobService{private readonly GameRunState _s;private readonly IStateReducer _r;public JobService(GameRunState s,IStateReducer r){_s=s;_r=r;}public CommandResult AssignJob(string tenant,string job,PhaseAccessDecision access){if(access!=PhaseAccessDecision.Allowed)return CommandResult.Rejected;var set=AuthorizedChangeSet.Domain(_s.RunId,_s.StateVersion,"jobs","job-"+tenant);set.Add(new AssignJobChange(tenant,job));return _r.TryCommit(_s,set).Succeeded?CommandResult.Accepted:CommandResult.Rejected;}}
 public sealed class ResourceService{private readonly GameRunState _s;private readonly IStateReducer _r;public ResourceService(GameRunState s,IStateReducer r){_s=s;_r=r;}public CommandResult Adjust(string id,int delta,PhaseAccessDecision access){if(access!=PhaseAccessDecision.Allowed||!_s.Resources.ContainsKey(id)||_s.Resources[id].Amount+delta<0)return CommandResult.Rejected;var set=AuthorizedChangeSet.Domain(_s.RunId,_s.StateVersion,"resources","resource-"+id);set.Add(new AdjustResourceChange(id,delta));return _r.TryCommit(_s,set).Succeeded?CommandResult.Accepted:CommandResult.Rejected;}}
 public sealed class ErosionService:IPhaseSettlementProvider{private readonly float _nightDelta;public ErosionService(float delta){_nightDelta=delta;}public string ProviderId=>"erosion";public AuthorizedChangeSet BuildSettlement(PhaseExecutionContext c){var set=AuthorizedChangeSet.Domain(c.State.RunId,c.State.StateVersion,ProviderId,"erosion-"+c.Day+"-"+c.Occurrence);if(c.Phase==HotelPhase.Night)foreach(var t in c.State.Tenants.Values)set.Add(new AdjustTenantErosionChange(t.TenantId,_nightDelta));return set;}}
 public sealed class TenantService{private readonly GameRunState _s;private readonly IStateReducer _r;public TenantService(GameRunState s,IStateReducer r){_s=s;_r=r;}public CommandResult SetMark(string id,bool value,PhaseAccessDecision access){if(access!=PhaseAccessDecision.Allowed)return CommandResult.Rejected;var set=AuthorizedChangeSet.Domain(_s.RunId,_s.StateVersion,"tenants","mark-"+id);set.Add(new SetTenantMarkChange(id,value));return _r.TryCommit(_s,set).Succeeded?CommandResult.Accepted:CommandResult.Rejected;}}
}
```

- [ ] **Step 4: 回归与门。** Add full tests `PlayerMark_DoesNotChangeTrueErosion`, `SameRoomSpread_ChangesOnlyCoOccupants`, `JobTagMismatch_IsRejectedWithoutMutation`, `ColorThreshold_IsDerivedFromTrueErosion`, and `RunSummary_ContainsMisclassificationStatistics`. All mutations must be asserted after `TryCommit`; no service may assign a `GameRunState` field directly.

### Task 5: M5 — 组合根、只读表现与可逆旧计时器 containment

**Files:**
- Create: `Assets/Scripts/Hotel/Integration/Composition/HotelRunCompositionRoot.cs`, `LegacyClockDisplayAdapter.cs`
- Create: `Assets/Scripts/Hotel/Runtime/Kernel/Projections/IHotelRunProjection.cs`, `Assets/Scripts/Hotel/Integration/Projections/HotelRunProjection.cs`
- Create: `Assets/Scripts/Hotel/Presentation/UI/NextPhaseCommandAdapter.cs`, `EventCommandAdapter.cs`
- Modify: `Assets/Scripts/Hotel/Managers/TimeManager.cs` only for containment; modify UI scripts and `MainScene.unity` only after runtime tests pass and reference audit is recorded.
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/LegacyTimerContainmentTests.cs`, `CompositionTests.cs`

**Interfaces:** Composition root exclusively registers providers. Projection exposes `Day`, `Phase`, `HasBlockingDecision`. `LegacyClockDisplayAdapter` cannot have gateway/coordinator/run-state fields; it shows projection values only.

- [ ] **Step 1: 写 containment 失败测试。**

```csharp
using NUnit.Framework;using UnityEngine;namespace Hotel.Runtime.Tests { public sealed class LegacyTimerContainmentTests {
 [Test] public void ContainedLegacyTimerCannotAdvanceLegacyPhase(){var type=System.Type.GetType("TimeManager, Assembly-CSharp");Assert.That(type,Is.Not.Null);var go=new GameObject("timer");var timer=go.AddComponent(type);var state=type.GetField("timeState").GetValue(timer);var stateType=state.GetType();var before=stateType.GetField("currentPhase").GetValue(state);type.GetField("legacyClockOnly").SetValue(timer,true);type.GetField("phaseAdvanceEnabled").SetValue(timer,false);type.GetMethod("AdvancePhase").Invoke(timer,null);Assert.That(stateType.GetField("currentPhase").GetValue(state),Is.EqualTo(before));Object.DestroyImmediate(go);}
} }
```

- [ ] **Step 2: 运行失败测试。** Expected reflective null-field failure before containment flags exist; the asmdef test stays compilable because it has no compile-time reference to Assembly-CSharp.
- [ ] **Step 3: 受控 containment 与表现迁移。** Replace `TimeManager.Update` and guard `AdvancePhase` exactly as follows; M5 does not add `[Obsolete]` or move any legacy file.

```csharp
[Header("Legacy Containment")] public bool legacyClockOnly=true;
[HideInInspector] public bool phaseAdvanceEnabled=false;
private void Update(){if(isPaused)return;if(legacyClockOnly){TickClockForPresentation();return;}TickLegacyAuthoritativeClock();}
public void TickClockForPresentation(){minuteAccumulator+=Time.deltaTime*20f*(int)currentSpeed;while(minuteAccumulator>=1f){minuteAccumulator-=1f;timeState.minute++;if(timeState.minute>=60){timeState.minute=0;timeState.hour=(timeState.hour+1)%24;}}}
private void TickLegacyAuthoritativeClock(){float minutes=Time.deltaTime*20f*(int)currentSpeed;minuteAccumulator+=minutes;while(minuteAccumulator>=1f){minuteAccumulator-=1f;AdvanceMinute();}}
public void AdvancePhase(){if(!phaseAdvanceEnabled||legacyClockOnly)return;TimePhase old=timeState.currentPhase;timeState.currentPhase=(TimePhase)(((int)timeState.currentPhase+1)%4);if(onPhaseChanged!=null)onPhaseChanged.Raise(new PhaseData{day=timeState.currentDay,hour=timeState.hour,minute=timeState.minute,phase=timeState.currentPhase});if(timeState.currentPhase==TimePhase.Dawn&&old==TimePhase.Night){timeState.currentDay++;if(onDayStarted!=null)onDayStarted.Raise(new DayData{day=timeState.currentDay});}}
```

Before active UI migration set the active-scene `TimeManager` component to `legacyClockOnly=true`, `phaseAdvanceEnabled=false`. It may keep hour/minute cosmetic display only; disable/remove `TimeUI` if it reads legacy day/phase. Attach a projection-only display adapter instead. Replace `NextPhaseButton` call with `NextPhaseCommandAdapter`; replace `EventUI.ApplyEffects` with event command submission; make phase/panel/background consume projection/results.

- [ ] **Step 4: 回归与硬门。** Tests prove 1,500 `TickClockForPresentation` calls do not change old day/phase, direct old advance is no-op, `TimeManager` has no gateway field, one composition root creates one coordinator, one button press submits one command, event confirmation does not mutate erosion directly. Manual Dawn→Day→Dusk→Night run and Console error=0 are required before M6.

### Task 6: M6 — 深快照、Legacy 隔离与完整 30 天模拟

**Files:**
- Create: `Assets/Scripts/Hotel/Integration/SaveLoadBoundary/GameRunSnapshotBoundary.cs`
- Create: `Assets/Tests/Hotel.Runtime.Tests/Runtime/GameRunSnapshotTests.cs`, `LegacyIsolationTests.cs`, `ThirtyDaySimulationTests.cs`
- Move only after Task 5 hard gate: `Assets/Scripts/Hotel/Managers/TimeManager.cs`, `Data/TimeState.cs`, `UI/TimeUI.cs`, `UI/TimeControlUI.cs`, `Data/TimePhaseChangedEvent.cs`, `TimeSpeedChangedEvent.cs`, `DayStartedEvent.cs` plus matching `.meta` to exact `Assets/Scripts/Legacy/TimeSystem/` paths.
- Audit then update exact discovered scene/Prefab/SO/editor helper references. Add obsolete attributes only in this task.

**Interfaces:** Snapshot captures all aggregates deeply and restores only after every stable definition ID resolves. No snapshot contains Unity references.

- [ ] **Step 1: 写失败快照和 30 天测试。**

```csharp
using System.Collections.Generic;using NUnit.Framework;using Hotel.Runtime;using Hotel.Authoring.DayCycle;
namespace Hotel.Runtime.Tests { public sealed class GameRunSnapshotTests {
 [Test] public void Restore_DeepCopiesAllAggregatesAndValidatesDefinitionIds(){var s=GameRunState.New(new RunId("r"));s.Tenants["t"]=new TenantRunState{TenantId="t",DefinitionId="tenant-a",TrueErosion=7};s.Rooms["room"]=new RoomRunState{RoomId="room",DefinitionId="room-a"};s.Resources["cash"]=new ResourceRunState{ResourceId="cash",DefinitionId="resource-a",Amount=2};var snapshot=GameRunSnapshotBoundary.Capture(new Reader(s));var restored=GameRunSnapshotBoundary.Restore(snapshot,new Resolver("tenant-a","room-a","resource-a"));restored.Tenants["t"].TrueErosion=99;Assert.That(s.Tenants["t"].TrueErosion,Is.EqualTo(7));Assert.That(restored.StateVersion,Is.EqualTo(s.StateVersion));}
 private sealed class Reader:IGameRunStateReader{public Reader(GameRunState state){Snapshot=state;}public GameRunState Snapshot{get;}}
 private sealed class Resolver:IDefinitionResolver{private readonly HashSet<string> _ids;public Resolver(params string[] ids){_ids=new HashSet<string>(ids);}public bool HasDefinition(string id)=>_ids.Contains(id);}
} public sealed class ThirtyDaySimulationTests {
 [Test] public void ThreeFixedSeeds_ReachSummaryWithoutNightGapsOrDay31(){foreach(var seed in new[]{1,7,99}){var s=GameRunState.New(new RunId("run-"+seed),seed);var c=new GamePhaseCoordinator(s,DayCycleDefinition.CreateDefault(),new StateReducer());c.RegisterSettlementProvider(new EventPlanningService(new DeterministicRandomSource((uint)seed)));for(var step=0;step<200&&!s.Summary.IsComplete;step++){var result=c.RequestAdvance(new AdvancePhaseCommand("a-"+step,s.RunId,s.StateVersion));Assert.That(result,Is.EqualTo(CommandResult.Accepted));if(s.Phase.Lifecycle==PhaseLifecycleState.WaitingForDecisions)foreach(var d in s.Decisions.FindAll(x=>!x.IsCompleted))Assert.That(c.RequestCompletion(new DecisionCompletionRequest(d.DecisionId,s.RunId,s.StateVersion,s.Phase.Current,"resolve-"+d.DecisionId)),Is.EqualTo(DecisionCompletionDecision.Approved));}Assert.That(s.Summary.IsComplete,Is.True);Assert.That(s.Day,Is.EqualTo(30));for(var day=1;day<=30;day++)Assert.That(s.EventHistory.Exists(x=>x.Day==day&&x.Phase==HotelPhase.Night),Is.True);}} }
}
```

- [ ] **Step 2: 运行失败测试。** Expected `CS0246` for snapshot contracts/boundary. Before any move run read-only searches for each legacy type in code, scenes, Prefabs and SOs; list exact paths. Missing audit blocks moves.
- [ ] **Step 3: 实现深 DTO、fail-fast restore。**

```csharp
using System;using System.Collections.Generic;using System.Linq;namespace Hotel.Runtime {
 public interface IGameRunStateReader{GameRunState Snapshot{get;}} public interface IDefinitionResolver{bool HasDefinition(string id);}
 [Serializable] public sealed class GameRunSnapshot{public string RunId;public long StateVersion;public int Day;public int Seed;public HotelPhase Phase;public PhaseLifecycleState Lifecycle;public int Occurrence;public List<DecisionRunState> Decisions;public List<EventHistoryRecord> Events;public List<string> Audit;public List<TenantRunState> Tenants;public List<RoomRunState> Rooms;public List<ResourceRunState> Resources;public RunSummaryState Summary;}
 public static class GameRunSnapshotBoundary{
  public static GameRunSnapshot Capture(IGameRunStateReader reader){var s=reader.Snapshot;return new GameRunSnapshot{RunId=s.RunId.Value,StateVersion=s.StateVersion,Day=s.Day,Seed=s.Seed,Phase=s.Phase.Current,Lifecycle=s.Phase.Lifecycle,Occurrence=s.Phase.Occurrence,Decisions=s.Decisions.Select(CloneDecision).ToList(),Events=s.EventHistory.Select(CloneEvent).ToList(),Audit=new List<string>(s.AuditLog),Tenants=s.Tenants.Values.Select(CloneTenant).ToList(),Rooms=s.Rooms.Values.Select(CloneRoom).ToList(),Resources=s.Resources.Values.Select(CloneResource).ToList(),Summary=CloneSummary(s.Summary)};}
  public static GameRunState Restore(GameRunSnapshot x,IDefinitionResolver resolver){if(x==null||string.IsNullOrEmpty(x.RunId))throw new ArgumentException("Invalid snapshot.");foreach(var id in x.Tenants.Select(t=>t.DefinitionId).Concat(x.Rooms.Select(r=>r.DefinitionId)).Concat(x.Resources.Select(r=>r.DefinitionId)).Concat(x.Events.Select(e=>e.DefinitionId)).Where(id=>!string.IsNullOrEmpty(id)).Distinct())if(!resolver.HasDefinition(id))throw new InvalidOperationException("Missing definition: "+id);var s=GameRunState.New(new RunId(x.RunId),x.Seed);s.StateVersion=x.StateVersion;s.Day=x.Day;s.Phase.Current=x.Phase;s.Phase.Lifecycle=x.Lifecycle;s.Phase.Occurrence=x.Occurrence;s.Decisions=x.Decisions.Select(CloneDecision).ToList();s.EventHistory=x.Events.Select(CloneEvent).ToList();s.AuditLog=new List<string>(x.Audit);s.Tenants=x.Tenants.ToDictionary(t=>t.TenantId,CloneTenant);s.Rooms=x.Rooms.ToDictionary(r=>r.RoomId,CloneRoom);s.Resources=x.Resources.ToDictionary(r=>r.ResourceId,CloneResource);s.Summary=CloneSummary(x.Summary);return s;}
  private static DecisionRunState CloneDecision(DecisionRunState x)=>new DecisionRunState{DecisionId=x.DecisionId,Day=x.Day,Phase=x.Phase,IsBlocking=x.IsBlocking,SourceId=x.SourceId,IsCompleted=x.IsCompleted}; private static EventHistoryRecord CloneEvent(EventHistoryRecord x)=>new EventHistoryRecord{EventId=x.EventId,DefinitionId=x.DefinitionId,Day=x.Day,Phase=x.Phase,Occurrence=x.Occurrence,RequiresDecision=x.RequiresDecision,Resolved=x.Resolved,OptionId=x.OptionId};private static TenantRunState CloneTenant(TenantRunState x)=>new TenantRunState{TenantId=x.TenantId,DefinitionId=x.DefinitionId,TrueErosion=x.TrueErosion,PlayerMarked=x.PlayerMarked,RoomId=x.RoomId,JobId=x.JobId};private static RoomRunState CloneRoom(RoomRunState x)=>new RoomRunState{RoomId=x.RoomId,DefinitionId=x.DefinitionId,OccupantIds=new List<string>(x.OccupantIds)};private static ResourceRunState CloneResource(ResourceRunState x)=>new ResourceRunState{ResourceId=x.ResourceId,DefinitionId=x.DefinitionId,Amount=x.Amount};private static RunSummaryState CloneSummary(RunSummaryState x)=>new RunSummaryState{IsComplete=x.IsComplete,CompletedDay=x.CompletedDay,MisclassificationCount=x.MisclassificationCount,FinalTenantCount=x.FinalTenantCount};
 }
}
```

- [ ] **Step 4: Legacy move and final gate.** Only after tests, source audit, manual loop and user approval: apply `[Obsolete]`, move exact listed legacy files and `.meta`, update audited serialized references, compile, run full test assembly, run three-seed simulation, and verify new `Authoring/Runtime/Presentation/Integration` contains no Legacy type references. If a serialized reference cannot be safely migrated, revert only the attempted move and retain M5 containment; never force delete.

## 2. Self-review

|检查|结果|证据|
|---|---|---|
|协调器直接写状态|通过|Task 2 coordinator 仅通过 `Commit`→`AuthorizedChangeSet`→`StateReducer`；Enter、Settled、Waiting、Exiting、Completed、决策、日志、转场、总结均为显式 `RunChange`。|
|事件 planner 原子性|通过|Task 3 planner 只返回 `PlanEventHistoryChange` 和 decision 列表；每个 `(day, Night, occurrence)` 以历史记录查重；不写输入 state。|
|枚举断言|通过|所有示例直接比较 `CommandResult.Accepted/Rejected` 和 `DecisionCompletionDecision.Approved/Denied`，不访问不存在的布尔成员。|
|关键实现/测试完整性|通过|网关、sink、planner、resolver、房间/岗位/资源/侵蚀服务、containment、深快照、三种固定种子 30 天模拟均有命名空间完整 C# 片段。|
|快照完整性|通过|DTO 深拷贝 phase、决策、事件、日志、房客、房间、资源、总结；restore 对所有非空稳定 definition ID fail-fast。|
|类型/asmdef一致性|通过|Runtime 不依赖 Authoring，Authoring 依赖 Runtime；测试依赖两者；旧 Assembly-CSharp 只用反射测试 containment。|
|范围与工作区保护|通过|只规划后续精确路径；禁止本阶段 Unity/Git 写入和宽泛暂存。|

执行时建议按任务逐项实施、验证并通过里程碑门；不得跨越失败门进入下一任务。
