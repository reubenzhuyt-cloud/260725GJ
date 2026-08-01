# 资源结算与遗留系统清理 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除已废弃的遗留时间系统和全局侵蚀系统，新增 Food/Medicine 资源及其 SO 定义、Night→Dawn 每日结算桥、资源 UI 显示，并将事件效果从全局侵蚀迁移至租户运行时路径。

**Architecture:** SettlementBridge 作为窄桥持有 GameRunState/StateReducer 唯一实例，监听 PhaseEnteredEvent 在 Night→Dawn 时执行食物结算。ResourceService 提供静态便捷方法。InfoPanelResourceDisplay 监听事件更新 UI。不修改 GamePhaseManager/EventManager 内部逻辑。

**Tech Stack:** Unity 2022+，C#，ScriptableObject 事件通道，TextMeshPro，UnityEngine.UI

## 全局约束

- 不添加代码注释（除非规格明确要求的 Debug.Log/Debug.LogWarning）
- 不执行 Git 操作（无 commit/push/merge）
- 不创建/运行自动化测试（用户明确跳过）；每个委派任务完成后仅做 Unity 编译 + Console 检查
- 不修改 InfoPanel 现有 HorizontalLayoutGroup 参数、TimePanel 或 Spacer 的 RectTransform
- 不修改 GamePhaseManager 或 EventManager 内部逻辑
- SettlementBridge 的 OnEnable 必须先于 EventManager 的 OnEnable 执行（使用 `[DefaultExecutionOrder(-100)]`）
- 所有资源变更通过 `AuthorizedChangeSet` + `StateReducer.TryCommit` 原子提交
- `EffectType.ModifyErosion` 重命名为 `EffectType.ModifyTenantErosion` 后，EventUI.ApplyEffects 中移除对 ErosionManager.Instance 的调用，改为 Debug.LogWarning

---

## 文件清单

### 新增文件

| 文件 | 绝对路径 | 职责 |
|------|---------|------|
| `ResourceDefinition.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Authoring\Resources\ResourceDefinition.cs` | Authoring SO，定义资源 ID/显示名/初始数量/图标 |
| `FoodShortageEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\FoodShortageEvent.cs` | 食物不足事件通道 SO + FoodShortageData 载荷 |
| `ResourceAdjustedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ResourceAdjustedEvent.cs` | 资源调整事件通道 SO + ResourceAdjustedData 载荷 |
| `ResourceService.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Services\ResourceService.cs` | 静态工具类，封装资源查询和变更提交（位于默认程序集，因需引用 ResourceAdjustedEvent） |
| `SettlementBridge.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\SettlementBridge.cs` | 窄桥 MonoBehaviour，持有 GameRunState，监听 Night→Dawn 执行结算 |
| `InfoPanelResourceDisplay.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\InfoPanelResourceDisplay.cs` | InfoPanel 资源显示 UI，监听 ResourceAdjustedEvent 更新 |

### 新增 SO 资产

| 资产 | 绝对路径 |
|------|---------|
| `Food.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Resources\Food.asset` |
| `Medicine.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Resources\Medicine.asset` |
| `FoodShortageEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\FoodShortageEvent.asset` |
| `ResourceAdjustedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\ResourceAdjustedEvent.asset` |

### 修改文件

| 文件 | 绝对路径 | 修改内容 |
|------|---------|---------|
| `EventConfig.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\EventConfig.cs` | `EffectType.ModifyErosion` → `EffectType.ModifyTenantErosion` |
| `EventUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\EventUI.cs` | ApplyEffects 中移除 ErosionManager.Instance 调用，改为日志警告 |
| `TenantAssignmentCoordinator.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TenantAssignmentCoordinator.cs` | 改为使用 SettlementBridge.Instance 持有的 GameRunState/StateReducer（不再自行创建） |
| `MainScene.unity` | `E:\UnityProjects\260725GJ\Assets\Scenes\MainScene.unity` | 删除 TimeManager/ErosionManager 场景对象，新增 SettlementBridge + ResourcePanel |

### 删除文件（遗留时间系统）

| 文件 | 绝对路径 |
|------|---------|
| `TimeManager.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TimeManager.cs` |
| `TimeManager.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TimeManager.cs.meta` |
| `TimeState.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeState.cs` |
| `TimeState.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeState.cs.meta` |
| `TimeUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeUI.cs` |
| `TimeUI.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeUI.cs.meta` |
| `TimeControlUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeControlUI.cs` |
| `TimeControlUI.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeControlUI.cs.meta` |
| `TimePhaseChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimePhaseChangedEvent.cs` |
| `TimePhaseChangedEvent.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimePhaseChangedEvent.cs.meta` |
| `DayStartedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\DayStartedEvent.cs` |
| `DayStartedEvent.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\DayStartedEvent.cs.meta` |
| `TimeSpeedChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeSpeedChangedEvent.cs` |
| `TimeSpeedChangedEvent.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeSpeedChangedEvent.cs.meta` |
| `TimeEventAssetCreator.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Editor\TimeEventAssetCreator.cs` |
| `TimeEventAssetCreator.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Editor\TimeEventAssetCreator.cs.meta` |
| `TimePhaseChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimePhaseChangedEvent.asset` |
| `TimePhaseChangedEvent.asset.meta` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimePhaseChangedEvent.asset.meta` |
| `DayStartedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\DayStartedEvent.asset` |
| `DayStartedEvent.asset.meta` | `E:\UnityProjects\260725GJ\Assets\Data\Events\DayStartedEvent.asset.meta` |
| `TimeSpeedChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimeSpeedChangedEvent.asset` |
| `TimeSpeedChangedEvent.asset.meta` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimeSpeedChangedEvent.asset.meta` |

### 删除文件（全局侵蚀系统）

| 文件 | 绝对路径 |
|------|---------|
| `ErosionManager.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\ErosionManager.cs` |
| `ErosionManager.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\ErosionManager.cs.meta` |
| `ErosionState.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionState.cs` |
| `ErosionState.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionState.cs.meta` |
| `ErosionConfig.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionConfig.cs` |
| `ErosionConfig.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionConfig.cs.meta` |
| `ErosionChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionChangedEvent.cs` |
| `ErosionChangedEvent.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionChangedEvent.cs.meta` |
| `ErosionUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\ErosionUI.cs` |
| `ErosionUI.cs.meta` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\ErosionUI.cs.meta` |
| `ErosionChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\ErosionChangedEvent.asset` |
| `ErosionChangedEvent.asset.meta` | `E:\UnityProjects\260725GJ\Assets\Data\Events\ErosionChangedEvent.asset.meta` |

---

## Task 1: 新增 Authoring/Runtime 文件与 SO 事件通道

**目标:** 创建所有新增源文件（不包含 SettlementBridge 和 InfoPanelResourceDisplay，它们在后续任务中处理）和 SO 事件通道资产。此任务完成后编译应无错误。

**Files:**
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Authoring\Resources\ResourceDefinition.cs`
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\FoodShortageEvent.cs`
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ResourceAdjustedEvent.cs`
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Services\ResourceService.cs`

**Interfaces:**
- Produces: `ResourceDefinition` (SO with `string resourceId`, `string displayName`, `int initialAmount`, `Sprite icon`)
- Produces: `FoodShortageEvent : GameEvent<FoodShortageData>` with `FoodShortageData { int day; int shortageAmount; }`
- Produces: `ResourceAdjustedEvent : GameEvent<ResourceAdjustedData>` with `ResourceAdjustedData { string resourceId; int delta; int newAmount; }`
- Produces: `ResourceService` static class with `static int GetAmount(GameRunState state, string resourceId)` and `static CommitResult TryAdjust(GameRunState state, StateReducer reducer, string resourceId, int delta, string authorizer, ResourceAdjustedEvent channel)`

- [ ] **Step 1: 创建 ResourceDefinition.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Authoring\Resources\ResourceDefinition.cs`

```csharp
using UnityEngine;

namespace Hotel.Authoring.Resources
{
    [CreateAssetMenu(menuName = "Hotel/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        public string resourceId;
        public string displayName;
        public int initialAmount;
        public Sprite icon;
    }
}
```

注意: 需要创建 `Resources` 子目录。该文件位于 `Hotel.Authoring` asmdef 范围内，但 `ResourceDefinition` 是纯 SO，不依赖 `Hotel.Runtime`，无跨程序集问题。

- [ ] **Step 2: 创建 FoodShortageEvent.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\FoodShortageEvent.cs`

```csharp
using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Events/FoodShortageEvent")]
public class FoodShortageEvent : GameEvent<FoodShortageData> {}

[Serializable]
public struct FoodShortageData
{
    public int day;
    public int shortageAmount;
}
```

- [ ] **Step 3: 创建 ResourceAdjustedEvent.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ResourceAdjustedEvent.cs`

```csharp
using UnityEngine;
using System;

[CreateAssetMenu(menuName = "Events/ResourceAdjustedEvent")]
public class ResourceAdjustedEvent : GameEvent<ResourceAdjustedData> {}

[Serializable]
public struct ResourceAdjustedData
{
    public string resourceId;
    public int delta;
    public int newAmount;
}
```

- [ ] **Step 4: 创建 ResourceService.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Services\ResourceService.cs`

需要创建 `Services` 子目录（在 `Assets/Scripts/Hotel/` 下）。

注意: `ResourceAdjustedEvent` 类型定义在默认程序集（`Assets/Scripts/Hotel/Data/`）。`Hotel.Runtime` asmdef（`Assets/Scripts/Hotel/Runtime/`）设置了 `autoReferenced: true`，但 asmdef 不能反向引用默认程序集。因此 `ResourceService.cs` 必须放在默认程序集路径下，即 `Assets/Scripts/Hotel/Services/`。

```csharp
using Hotel.Runtime;

public static class ResourceService
{
    public static int GetAmount(GameRunState state, string resourceId)
    {
        if (state == null) return 0;
        if (!state.Resources.TryGetValue(resourceId, out var res)) return 0;
        return res.Amount;
    }

    public static CommitResult TryAdjust(
        GameRunState state,
        StateReducer reducer,
        string resourceId,
        int delta,
        string authorizer,
        ResourceAdjustedEvent channel)
    {
        if (state == null || reducer == null)
            return new CommitResult(false);

        if (!state.Resources.ContainsKey(resourceId))
            return new CommitResult(false);

        var changeSet = AuthorizedChangeSet.Domain(
            state.RunId,
            state.StateVersion,
            authorizer,
            "ResourceAdjust");
        changeSet.Add(new AdjustResourceChange(resourceId, delta));

        CommitResult result = reducer.TryCommit(state, changeSet);

        if (result.Succeeded && channel != null)
        {
            channel.Raise(new ResourceAdjustedData
            {
                resourceId = resourceId,
                delta = delta,
                newAmount = state.Resources[resourceId].Amount
            });
        }

        return result;
    }
}
```

- [ ] **Step 5: 创建 SO 资产**

在 Unity Editor 中:
1. 右键 `Assets/Data/Resources/` → Create → Hotel → Resource Definition，创建 `Food.asset`：resourceId=`food`, displayName=`食物`, initialAmount=`10`, icon=白色圆形 Sprite
2. 同上创建 `Medicine.asset`：resourceId=`medicine`, displayName=`药品`, initialAmount=`10`
3. 右键 `Assets/Data/Events/` → Create → Events → FoodShortageEvent，创建 `FoodShortageEvent.asset`
4. 同上创建 ResourceAdjustedEvent → `ResourceAdjustedEvent.asset`

注意: `Assets/Data/Resources/` 目录需新建。

- [ ] **Step 6: Unity 编译验证**

打开 Unity，确认 Console 无新编译错误。确认 Food.asset、Medicine.asset、FoodShortageEvent.asset、ResourceAdjustedEvent.asset 在 Inspector 中可正常显示字段。

---

## Task 2: EffectType 枚举迁移（ModifyErosion → ModifyTenantErosion）

**目标:** 将 `EffectType.ModifyErosion` 重命名为 `EffectType.ModifyTenantErosion`，更新 EventUI.ApplyEffects 移除对已删除的 ErosionManager 的依赖。此步骤在删除侵蚀系统文件之前执行，确保编译链完整。

**Files:**
- Modify: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\EventConfig.cs:8` — 枚举值重命名
- Modify: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\EventUI.cs:169-176` — ApplyEffects case 分支

**Interfaces:**
- Consumes: `EffectType` enum (from EventConfig.cs)
- Produces: `EffectType.ModifyTenantErosion` — 所有引用 `EffectType.ModifyErosion` 的代码和资产必须更新

- [ ] **Step 1: 修改 EventConfig.cs 枚举**

将第 8 行:
```csharp
public enum EffectType { None, ModifyErosion }
```
改为:
```csharp
public enum EffectType { None, ModifyTenantErosion }
```

注意: 枚举值从 `ModifyErosion`（值=1）重命名为 `ModifyTenantErosion`（值=1）。Unity SO 资产中若使用整数序列化则值不变；若使用枚举名序列化则需在 Inspector 中手动更新所有 Event_*.asset 中的 effectType 字段。

- [ ] **Step 2: 修改 EventUI.ApplyEffects**

将 `EventUI.cs` 第 163-178 行的 `ApplyEffects` 方法:
```csharp
private void ApplyEffects(EventEffect[] effects)
{
    if (effects == null) return;

    foreach (var effect in effects)
    {
        switch (effect.effectType)
        {
            case EffectType.ModifyErosion:
                if (ErosionManager.Instance != null)
                    ErosionManager.Instance.ModifyErosion(effect.floatValue);
                Debug.Log($"[EventUI] Applied: ModifyErosion {effect.floatValue:+0.0;-0.0}");
                break;
        }
    }
}
```
改为:
```csharp
private void ApplyEffects(EventEffect[] effects)
{
    if (effects == null) return;

    foreach (var effect in effects)
    {
        switch (effect.effectType)
        {
            case EffectType.ModifyTenantErosion:
                Debug.LogWarning("[EventUI] ModifyTenantErosion effect requires tenant context — deferred");
                break;
        }
    }
}
```

- [ ] **Step 3: 更新事件资产中的枚举引用**

在 Unity Editor 中检查 `Assets/Data/Configs/` 下所有 `Event_*.asset`（共 12 个：Event_Dawn_1~2, Event_Day_1~4, Event_Dusk_1~2, Event_Night_1~4）。若某个 asset 的 `confirmEffects` 或 `choices[].choiceEffects` 中有 `effectType = ModifyErosion`，在 Inspector 中将其改为 `ModifyTenantErosion`。

若枚举值序列化为整数（Unity 默认行为），则值 1 自动映射到 `ModifyTenantErosion`，无需手动修改。

- [ ] **Step 4: Unity 编译验证**

确认 Console 无编译错误。确认 EventUI 不再引用 ErosionManager。

---

## Task 3: 删除遗留时间系统

**目标:** 删除所有已标记 `[Obsolete]` 的时间系统文件、SO 资产和场景组件。删除顺序：先场景组件，再 SO 资产，最后源文件（含 .meta）。

**Files:**
- Delete (scene): MainScene.unity 中 TimeManager GameObject (fileID: 163388801) 及其所有组件
- Delete (asset): `E:\UnityProjects\260725GJ\Assets\Data\Events\TimePhaseChangedEvent.asset` + `.meta`
- Delete (asset): `E:\UnityProjects\260725GJ\Assets\Data\Events\DayStartedEvent.asset` + `.meta`
- Delete (asset): `E:\UnityProjects\260725GJ\Assets\Data\Events\TimeSpeedChangedEvent.asset` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TimeManager.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeState.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimePhaseChangedEvent.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\DayStartedEvent.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeSpeedChangedEvent.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeUI.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeControlUI.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Editor\TimeEventAssetCreator.cs` + `.meta`

**Interfaces:**
- 无新增接口。删除后确认无编译器错误（无其他文件引用被删除的类型）。

- [ ] **Step 1: 删除 MainScene 中的 TimeManager 场景对象**

在 Unity Editor 中打开 `Assets/Scenes/MainScene.unity`，在 Hierarchy 中找到 `TimeManager` GameObject（已禁用，`m_Enabled: 0`），右键 Delete。保存场景。

或者直接编辑 `MainScene.unity` YAML：删除 fileID `163388801` 的 GameObject 条目及其关联的 Transform/MonoBehaviour 组件条目，以及父节点 m_Children 中对它的引用。

- [ ] **Step 2: 删除时间系统 SO 资产**

在 Unity Editor 的 Project 窗口中删除:
- `Assets/Data/Events/TimePhaseChangedEvent.asset`
- `Assets/Data/Events/DayStartedEvent.asset`
- `Assets/Data/Events/TimeSpeedChangedEvent.asset`

Unity 会自动删除对应的 .meta 文件。

- [ ] **Step 3: 删除时间系统源文件**

在 Unity Editor 的 Project 窗口中删除:
- `Assets/Scripts/Hotel/Managers/TimeManager.cs`
- `Assets/Scripts/Hotel/Data/TimeState.cs`
- `Assets/Scripts/Hotel/Data/TimePhaseChangedEvent.cs`
- `Assets/Scripts/Hotel/Data/DayStartedEvent.cs`
- `Assets/Scripts/Hotel/Data/TimeSpeedChangedEvent.cs`
- `Assets/Scripts/Hotel/UI/TimeUI.cs`
- `Assets/Scripts/Hotel/UI/TimeControlUI.cs`
- `Assets/Scripts/Editor/TimeEventAssetCreator.cs`

- [ ] **Step 4: Unity 编译验证**

确认 Console 无编译错误。搜索代码库确认无残留引用 `TimeManager`、`TimeState`、`TimePhase`、`PhaseData`、`DayData`、`TimeSpeedData`、`TimePhaseChangedEvent`、`DayStartedEvent`、`TimeSpeedChangedEvent`。

---

## Task 4: 删除全局侵蚀系统并迁移至租户运行时路径

**目标:** 删除 ErosionManager、ErosionState、ErosionConfig、ErosionChangedEvent、ErosionUI 及其 SO 资产和场景组件。侵蚀效果已由 Task 2 迁移至 `EffectType.ModifyTenantErosion`。

**Files:**
- Delete (scene): MainScene.unity 中 ErosionManager GameObject (fileID: 1330069343) 及其所有组件
- Delete (asset): `E:\UnityProjects\260725GJ\Assets\Data\Events\ErosionChangedEvent.asset` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\ErosionManager.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionState.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionConfig.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionChangedEvent.cs` + `.meta`
- Delete: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\ErosionUI.cs` + `.meta`

**Interfaces:**
- 无新增接口。EventUI.ApplyEffects 已在 Task 2 中移除对 ErosionManager 的依赖。

- [ ] **Step 1: 删除 MainScene 中的 ErosionManager 场景对象**

在 Unity Editor 中打开 `Assets/Scenes/MainScene.unity`，在 Hierarchy 中找到 `ErosionManager` GameObject，右键 Delete。保存场景。

- [ ] **Step 2: 删除侵蚀系统 SO 资产**

在 Unity Editor 中删除:
- `Assets/Data/Events/ErosionChangedEvent.asset`

- [ ] **Step 3: 删除侵蚀系统源文件**

在 Unity Editor 中删除:
- `Assets/Scripts/Hotel/Managers/ErosionManager.cs`
- `Assets/Scripts/Hotel/Data/ErosionState.cs`
- `Assets/Scripts/Hotel/Data/ErosionConfig.cs`
- `Assets/Scripts/Hotel/Data/ErosionChangedEvent.cs`
- `Assets/Scripts/Hotel/UI/ErosionUI.cs`

- [ ] **Step 4: Unity 编译验证**

确认 Console 无编译错误。搜索代码库确认无残留引用 `ErosionManager`、`ErosionState`、`ErosionConfig`、`ErosionChangedEvent`、`ErosionUI`、`ErosionData`。

---

## Task 5: SettlementBridge — 窄桥与 Night→Dawn 每日结算

**目标:** 创建 SettlementBridge MonoBehaviour，作为 GameRunState/StateReducer 的唯一持有者，监听 PhaseEnteredEvent 在 Night→Dawn 时执行食物结算。同时修改 TenantAssignmentCoordinator 改为引用 SettlementBridge 的状态。

**Files:**
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\SettlementBridge.cs`
- Modify: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TenantAssignmentCoordinator.cs` — 移除自有 GameRunState/StateReducer，改为引用 SettlementBridge.Instance

**Interfaces:**
- Consumes: `PhaseEnteredEvent` (SO event channel), `ResourceDefinition` (SO list), `FoodShortageEvent` (SO event channel), `ResourceAdjustedEvent` (SO event channel)
- Consumes: `GameRunState`, `StateReducer`, `AuthorizedChangeSet`, `AdjustResourceChange`, `AppendAuditLogChange`, `ResourceRunState` (from Hotel.Runtime)
- Consumes: `GamePhase` enum (from EventConfig.cs), `PhaseEnterData` struct (from PhaseEnteredEvent.cs)
- Produces: `SettlementBridge.Instance` (static singleton) with:
  - `GameRunState RunState { get; }` — 供 TenantAssignmentCoordinator 和 InfoPanelResourceDisplay 使用
  - `StateReducer Reducer { get; }` — 供 TenantAssignmentCoordinator 使用
  - `int GetResourceAmount(string resourceId)` — 供 UI 查询

- [ ] **Step 1: 创建 SettlementBridge.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\SettlementBridge.cs`

```csharp
using System.Collections.Generic;
using Hotel.Runtime;
using Hotel.Authoring.Resources;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class SettlementBridge : MonoBehaviour
{
    public static SettlementBridge Instance { get; private set; }

    [Header("Resource Definitions")]
    public List<ResourceDefinition> resourceDefinitions = new List<ResourceDefinition>();

    [Header("Event Channels")]
    public PhaseEnteredEvent onPhaseEntered;
    public FoodShortageEvent onFoodShortage;
    public ResourceAdjustedEvent onResourceAdjusted;

    public GameRunState RunState => _runState;
    public StateReducer Reducer => _reducer;

    private GameRunState _runState;
    private StateReducer _reducer;
    private int _lastSettlementDay;
    private GamePhase _lastPhase;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _reducer = new StateReducer();
        _runState = GameRunState.New(new RunId("main_run"), 1);

        foreach (var def in resourceDefinitions)
        {
            if (def == null) continue;
            _runState.Resources[def.resourceId] = new ResourceRunState
            {
                ResourceId = def.resourceId,
                DefinitionId = def.name,
                Amount = def.initialAmount
            };
        }
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

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (_runState == null)
        {
            Debug.LogError("[SettlementBridge] GameRunState is null, skipping settlement");
            return;
        }

        if (data.phase == GamePhase.Dawn && _lastPhase == GamePhase.Night)
        {
            if (data.day > _lastSettlementDay)
            {
                ExecuteFoodSettlement(data.day);
                _lastSettlementDay = data.day;
            }
        }

        _lastPhase = data.phase;
    }

    private void ExecuteFoodSettlement(int day)
    {
        int countTenants = 0;
        foreach (var kvp in _runState.Tenants)
        {
            if (!string.IsNullOrEmpty(kvp.Value.RoomId))
                countTenants++;
        }

        if (countTenants == 0)
        {
            Debug.Log("[SettlementBridge] No assigned tenants, skipping food settlement");
            return;
        }

        int available = 0;
        if (_runState.Resources.TryGetValue("food", out var foodRes))
            available = foodRes.Amount;

        int consumed = Mathf.Min(countTenants, available);
        int shortage = countTenants - available;

        var changeSet = AuthorizedChangeSet.Coordinator(
            _runState.RunId,
            _runState.StateVersion,
            $"Day{day}FoodSettlement");
        changeSet.Add(new AdjustResourceChange("food", -consumed));
        changeSet.Add(new AppendAuditLogChange($"Day {day} food settlement: consumed {consumed}, shortage {Mathf.Max(0, shortage)}"));

        CommitResult result = _reducer.TryCommit(_runState, changeSet);

        if (result.Succeeded)
        {
            if (onResourceAdjusted != null && _runState.Resources.TryGetValue("food", out var foodAfter))
            {
                onResourceAdjusted.Raise(new ResourceAdjustedData
                {
                    resourceId = "food",
                    delta = -consumed,
                    newAmount = foodAfter.Amount
                });
            }

            if (shortage > 0 && onFoodShortage != null)
            {
                onFoodShortage.Raise(new FoodShortageData
                {
                    day = day,
                    shortageAmount = shortage
                });
            }

            Debug.Log($"[SettlementBridge] Day {day} settlement: consumed={consumed}, shortage={Mathf.Max(0, shortage)}");
        }
        else
        {
            Debug.LogError("[SettlementBridge] Food settlement commit failed");
        }
    }

    public int GetResourceAmount(string resourceId)
    {
        return ResourceService.GetAmount(_runState, resourceId);
    }
}
```

- [ ] **Step 2: 修改 TenantAssignmentCoordinator 引用 SettlementBridge 状态**

将 `TenantAssignmentCoordinator.cs` 中自行创建 GameRunState/StateReducer 的逻辑改为引用 SettlementBridge.Instance。

修改 Awake 方法（当前第 26-57 行）:

原代码:
```csharp
private void Awake()
{
    Instance = this;

    _reducer = new StateReducer();
    _runState = GameRunState.New(new RunId("tenant_assignment_demo"), 1);

    for (int i = 1; i <= 9; i++)
    {
        string roomId = string.Format("room_{0:D2}", i);
        _runState.Rooms[roomId] = new RoomRunState
        {
            RoomId = roomId,
            DefinitionId = roomId
        };
    }

    AddTenant("tenant_alpha", "Alpha", new Color(0.90f, 0.30f, 0.30f, 1f));
    // ... 其他租户 ...
    AddTenant("tenant_iota", "Iota", new Color(0.75f, 0.75f, 0.75f, 1f));

    RebuildUnassigned();

    AnchorDropTarget.RefreshAll();
    TenantAssignmentPanel.RefreshAll();
}
```

改为:
```csharp
private void Awake()
{
    Instance = this;
}

private void Start()
{
    if (SettlementBridge.Instance == null)
    {
        Debug.LogError("[TenantAssignmentCoordinator] SettlementBridge.Instance is null!");
        return;
    }

    _reducer = SettlementBridge.Instance.Reducer;
    _runState = SettlementBridge.Instance.RunState;

    for (int i = 1; i <= 9; i++)
    {
        string roomId = string.Format("room_{0:D2}", i);
        _runState.Rooms[roomId] = new RoomRunState
        {
            RoomId = roomId,
            DefinitionId = roomId
        };
    }

    AddTenant("tenant_alpha", "Alpha", new Color(0.90f, 0.30f, 0.30f, 1f));
    AddTenant("tenant_beta", "Beta", new Color(0.30f, 0.80f, 0.30f, 1f));
    AddTenant("tenant_gamma", "Gamma", new Color(0.30f, 0.40f, 0.90f, 1f));
    AddTenant("tenant_delta", "Delta", new Color(0.95f, 0.75f, 0.20f, 1f));
    AddTenant("tenant_epsilon", "Epsilon", new Color(0.80f, 0.30f, 0.80f, 1f));
    AddTenant("tenant_zeta", "Zeta", new Color(0.30f, 0.85f, 0.85f, 1f));
    AddTenant("tenant_eta", "Eta", new Color(0.95f, 0.55f, 0.25f, 1f));
    AddTenant("tenant_theta", "Theta", new Color(0.60f, 0.40f, 0.20f, 1f));
    AddTenant("tenant_iota", "Iota", new Color(0.75f, 0.75f, 0.75f, 1f));

    RebuildUnassigned();

    AnchorDropTarget.RefreshAll();
    TenantAssignmentPanel.RefreshAll();
}
```

同时删除 `_reducer` 和 `_runState` 的 `private` 字段声明（第 13-14 行），改为属性:
```csharp
private StateReducer _reducer;
private GameRunState _runState;
```
保留字段声明，但在 Start 中赋值而非 Awake 中创建。

- [ ] **Step 3: Unity 编译验证**

确认 Console 无编译错误。确认 SettlementBridge 和 TenantAssignmentCoordinator 编译通过。

---

## Task 6: InfoPanelResourceDisplay — 资源 UI 与场景连接

**目标:** 创建 InfoPanelResourceDisplay 组件，在 MainScene 中创建 ResourcePanel 子对象并挂载，配置 Inspector 引用。

**Files:**
- Create: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\InfoPanelResourceDisplay.cs`
- Modify: `E:\UnityProjects\260725GJ\Assets\Scenes\MainScene.unity` — 新增 SettlementBridge + ResourcePanel GameObject

**Interfaces:**
- Consumes: `SettlementBridge.Instance.GetResourceAmount(string)` (from Task 5)
- Consumes: `ResourceAdjustedEvent` (SO event channel, from Task 1)
- Consumes: `PhaseEnteredEvent` (SO event channel, existing)
- Consumes: `ResourceDefinition` (SO, from Task 1) — 用于读取 displayName

- [ ] **Step 1: 创建 InfoPanelResourceDisplay.cs**

路径: `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\InfoPanelResourceDisplay.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InfoPanelResourceDisplay : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI foodAmountText;
    public TextMeshProUGUI medicineAmountText;
    public Image foodIcon;
    public Image medicineIcon;

    [Header("Event Channels")]
    public ResourceAdjustedEvent onResourceAdjusted;
    public PhaseEnteredEvent onPhaseEntered;

    private void OnEnable()
    {
        if (onResourceAdjusted != null)
            onResourceAdjusted.Register(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Register(OnPhaseEntered);
    }

    private void OnDisable()
    {
        if (onResourceAdjusted != null)
            onResourceAdjusted.Unregister(OnResourceAdjusted);
        if (onPhaseEntered != null)
            onPhaseEntered.Unregister(OnPhaseEntered);
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void OnResourceAdjusted(ResourceAdjustedData data)
    {
        if (data.resourceId == "food" && foodAmountText != null)
            foodAmountText.text = data.newAmount.ToString();
        else if (data.resourceId == "medicine" && medicineAmountText != null)
            medicineAmountText.text = data.newAmount.ToString();
    }

    private void OnPhaseEntered(PhaseEnterData data)
    {
        if (data.phase == GamePhase.Dawn)
            RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (SettlementBridge.Instance == null)
        {
            Debug.LogWarning("[InfoPanelResourceDisplay] SettlementBridge.Instance is null");
            return;
        }

        if (foodAmountText != null)
            foodAmountText.text = SettlementBridge.Instance.GetResourceAmount("food").ToString();
        if (medicineAmountText != null)
            medicineAmountText.text = SettlementBridge.Instance.GetResourceAmount("medicine").ToString();
    }
}
```

- [ ] **Step 2: 在 MainScene 中创建 SettlementBridge GameObject**

在 Unity Editor 中:
1. 打开 `Assets/Scenes/MainScene.unity`
2. 在 Hierarchy 根级别创建空 GameObject，命名为 `SettlementBridge`
3. 添加 `SettlementBridge` 组件
4. 在 Inspector 中配置:
   - `resourceDefinitions`: 添加 Food.asset 和 Medicine.asset
   - `onPhaseEntered`: 拖入 PhaseEnteredEvent.asset
   - `onFoodShortage`: 拖入 FoodShortageEvent.asset
   - `onResourceAdjusted`: 拖入 ResourceAdjustedEvent.asset
5. 确保 SettlementBridge 在 Hierarchy 中排在 EventManager 之前（利用 `[DefaultExecutionOrder(-100)]` 也可，但场景顺序更直观）

- [ ] **Step 3: 在 MainScene 中创建 ResourcePanel GameObject**

在 Unity Editor 中:
1. 找到 InfoPanel GameObject（fileID: 371217395）
2. 在 InfoPanel 下创建空 GameObject，命名为 `ResourcePanel`
3. 添加 `RectTransform`、`HorizontalLayoutGroup`、`InfoPanelResourceDisplay` 组件
4. HorizontalLayoutGroup 配置: spacing=10, childAlignment=MiddleLeft, childForceExpandWidth=false, childForceExpandHeight=true
5. 在 ResourcePanel 下创建子对象:
   - `FoodIcon`: 添加 Image 组件，设置白色圆形 Sprite
   - `FoodAmountText`: 添加 TextMeshProUGUI 组件，设置文本 "10"
   - `MedicineIcon`: 添加 Image 组件，设置白色圆形 Sprite
   - `MedicineAmountText`: 添加 TextMeshProUGUI 组件，设置文本 "10"
6. InfoPanelResourceDisplay Inspector 配置:
   - `foodAmountText`: 拖入 FoodAmountText
   - `medicineAmountText`: 拖入 MedicineAmountText
   - `foodIcon`: 拖入 FoodIcon
   - `medicineIcon`: 拖入 MedicineIcon
   - `onResourceAdjusted`: 拖入 ResourceAdjustedEvent.asset
   - `onPhaseEntered`: 拖入 PhaseEnteredEvent.asset
7. 确认 InfoPanel 的 m_Children 中包含 ResourcePanel 的 RectTransform fileID（Unity 自动处理）
8. 确认 Spacer 的 LayoutElement.flexibleWidth 保持为 1，将 ResourcePanel 推到右侧

- [ ] **Step 4: Unity 编译与场景验证**

确认 Console 无编译错误。进入 Play Mode，确认 InfoPanel 右侧显示 Food=10, Medicine=10。

---

## Task 7: 最终编译与 Console 验证

**目标:** 全面验证所有变更后的编译状态和运行时行为。

**Files:**
- 无新增/修改文件。此任务为纯验证。

- [ ] **Step 1: 编译验证**

在 Unity Editor 中确认 Console 无编译错误（红色）。搜索代码库确认:
- 无残留引用 `TimeManager`、`TimeState`、`TimePhase`、`PhaseData`、`DayData`、`TimeSpeedData`
- 无残留引用 `ErosionManager`、`ErosionState`、`ErosionConfig`、`ErosionChangedEvent`、`ErosionUI`、`ErosionData`
- 无残留引用 `EffectType.ModifyErosion`（应全部为 `ModifyTenantErosion`）

- [ ] **Step 2: 场景结构验证**

在 Unity Editor 中确认 MainScene:
- 无 TimeManager GameObject
- 无 ErosionManager GameObject
- 有 SettlementBridge GameObject（根级别，组件已配置）
- InfoPanel 下有 ResourcePanel 子对象（在 Spacer 之后）
- InfoPanel 的 HorizontalLayoutGroup 参数未变（spacing=10, padding left=20, right=20）
- Spacer 的 LayoutElement.flexibleWidth=1

- [ ] **Step 3: Play Mode 运行时验证**

进入 Play Mode:
1. 启动后 InfoPanel 显示 Food=10, Medicine=10
2. 推进到 Night 阶段：无结算发生
3. 长按推进到 Dawn：Food 减少（= 已分配房间的租户数），Medicine 不变
4. Console 中有 `[SettlementBridge] Day N settlement: consumed=X, shortage=Y` 日志
5. 再次完整循环（Dawn→Day→Dusk→Night→Dawn）：Food 再次减少，无重复扣除
6. Console 无红色错误，无 MissingReferenceException

- [ ] **Step 4: 确认完成**

所有验证通过后，在 Console 中确认无警告以外的异常。标记计划完成。
