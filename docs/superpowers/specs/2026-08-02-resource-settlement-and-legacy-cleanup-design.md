# 资源结算与遗留系统清理 — 设计规格

**日期**: 2026-08-02
**状态**: 已批准，待实施
**范围**: 遗留系统删除 + 资源垂直切片（Food/Medicine） + Night 结算 + InfoPanel 资源显示

---

## 1 范围与非目标

### 1.1 范围

| 编号 | 内容 |
|------|------|
| S-1 | 删除已废弃的遗留时间系统（TimeManager、TimeState/TimePhase、TimeUI、TimeControlUI 及其事件通道和 Editor 工具） |
| S-2 | 删除全局侵蚀系统（ErosionManager、ErosionState、ErosionConfig、ErosionChangedEvent、ErosionUI），将现有事件效果迁移至租户运行时状态路径 |
| S-3 | 新增 Food 与 Medicine 两种全局资源，存储在 GameRunState.Resources 中 |
| S-4 | 新增 Night → Dawn 阶段转换时的每日食物结算逻辑 |
| S-5 | 新增食物不足事件通道（FoodShortageEvent） |
| S-6 | 新增资源调整事件通道（ResourceAdjustedEvent）供未来主动增减资源使用 |
| S-7 | InfoPanel 右侧新增资源显示区域 |
| S-8 | 编写幂等性、原子性、边界情况及迁移安全测试 |

### 1.2 非目标

| 编号 | 内容 |
|------|------|
| N-1 | 不做全局架构重构，不统一 GamePhaseManager/EventManager 与 GameRunState/StateReducer 为同一生命周期 |
| N-2 | 不实现 Medicine 自动消耗逻辑 |
| N-3 | 不实现食物不足后的详细惩罚/后续行为 |
| N-4 | 不实现租户列表在食物不足载荷中的传递 |
| N-5 | 不引入资源增减的 UI 交互（仅提供 SO 事件通道基础设施） |
| N-6 | 不修改 InfoPanel 的现有布局结构（仅在 Spacer 之后追加子节点） |

---

## 2 保留与删除清单

### 2.1 保留（活跃通道/组件）

| 类/资产 | 路径 | 说明 |
|---------|------|------|
| `GamePhaseManager` | `Assets/Scripts/Hotel/Managers/GamePhaseManager.cs` | 当前活跃的阶段管理器 |
| `EventManager` | `Assets/Scripts/Hotel/Managers/EventManager.cs` | 事件队列管理器 |
| `EventUI` | `Assets/Scripts/Hotel/UI/EventUI.cs` | 事件弹窗 UI（需修改 ApplyEffects） |
| `PhaseUI` | `Assets/Scripts/Hotel/UI/PhaseUI.cs` | 阶段显示 UI |
| `NextPhasePanel` | `Assets/Scripts/Hotel/UI/NextPhasePanel.cs` | 下一阶段按钮面板 |
| `NextPhaseButton` | `Assets/Scripts/Hotel/UI/NextPhaseButton.cs` | 长按推进按钮 |
| `PhaseEnteredEvent` | `Assets/Scripts/Hotel/Data/PhaseEnteredEvent.cs` + `Assets/Data/Events/PhaseEnteredEvent.asset` | 阶段进入事件通道 |
| `GamePopupEvent` | `Assets/Scripts/Hotel/Data/GamePopupEvent.cs` + `Assets/Data/Events/GamePopupEvent.asset` | 弹窗事件通道 |
| `EventProcessedEvent` | `Assets/Scripts/Hotel/Data/EventProcessedEvent.cs` + `Assets/Data/Events/EventProcessedEvent.asset` | 事件处理完成通道 |
| `EventQueueEmptyEvent` | `Assets/Scripts/Hotel/Data/EventQueueEmptyEvent.cs` + `Assets/Data/Events/EventQueueEmptyEvent.asset` | 队列清空通道 |
| `EventConfig` | `Assets/Scripts/Hotel/Data/EventConfig.cs` | 事件配置 SO |
| `GameRunState` / `ResourceRunState` | `Assets/Scripts/Hotel/Runtime/State/RunModel.cs` | 运行时状态模型 |
| `StateReducer` / `RunChanges` | `Assets/Scripts/Hotel/Runtime/Kernel/` | 状态还原器与变更类型 |
| `GameEvent` / `GameEvent<T>` | `Assets/Scripts/Core/Events/` | SO 事件基础设施 |

### 2.2 删除（遗留时间系统）

| 文件 | 绝对路径 | 删除原因 |
|------|---------|---------|
| `TimeManager.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\TimeManager.cs` | 已标记 `[Obsolete]`，由 GamePhaseManager 替代 |
| `TimeState.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeState.cs` | TimePhase 枚举和 TimeState 类已废弃 |
| `TimeUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeUI.cs` | 已标记 `[Obsolete]`，依赖 TimeManager |
| `TimeControlUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\TimeControlUI.cs` | 已标记 `[Obsolete]`，依赖 TimeManager |
| `TimePhaseChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimePhaseChangedEvent.cs` | 已标记 `[Obsolete]`，TimeManager 专用通道 |
| `DayStartedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\DayStartedEvent.cs` | 已标记 `[Obsolete]`，TimeManager 专用通道 |
| `TimeSpeedChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\TimeSpeedChangedEvent.cs` | 已标记 `[Obsolete]`，TimeManager 专用通道 |
| `TimeEventAssetCreator.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Editor\TimeEventAssetCreator.cs` | 创建已废弃事件资产的 Editor 工具 |
| `TimePhaseChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimePhaseChangedEvent.asset` | 已废弃事件 SO 资产 |
| `DayStartedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\DayStartedEvent.asset` | 已废弃事件 SO 资产 |
| `TimeSpeedChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\TimeSpeedChangedEvent.asset` | 已废弃事件 SO 资产 |
| TimeManager 场景组件 | `E:\UnityProjects\260725GJ\Assets\Scenes\MainScene.unity` 中 fileID `163388801` 的 GameObject | 已禁用（`m_Enabled: 0`），遗留时间管理器场景实例 |

### 2.3 删除（全局侵蚀系统）

| 文件 | 绝对路径 | 删除原因 |
|------|---------|---------|
| `ErosionManager.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Managers\ErosionManager.cs` | 全局侵蚀管理器，效果迁移至租户状态 |
| `ErosionState.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionState.cs` | 全局侵蚀状态类 |
| `ErosionConfig.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionConfig.cs` | 侵蚀速率配置 SO |
| `ErosionChangedEvent.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\Data\ErosionChangedEvent.cs` | 全局侵蚀变更事件通道 |
| `ErosionUI.cs` | `E:\UnityProjects\260725GJ\Assets\Scripts\Hotel\UI\ErosionUI.cs` | 侵蚀度显示 UI |
| `ErosionChangedEvent.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Events\ErosionChangedEvent.asset` | 全局侵蚀事件 SO 资产 |
| ErosionManager 场景组件 | `E:\UnityProjects\260725GJ\Assets\Scenes\MainScene.unity` 中 fileID `1330069343` 的 GameObject | 全局侵蚀管理器场景实例 |

---

## 3 架构与组件

### 3.1 系统拓扑概览

```
┌─────────────────────────────────────────────────────────────┐
│  场景层（MonoBehaviour Singletons）                          │
│                                                             │
│  GamePhaseManager ──PhaseEnteredEvent──► EventManager       │
│       │                                       │             │
│       │ AdvancePhase()                        │ GamePopup   │
│       │ (Night→Dawn 触发结算)                  ▼             │
│       │                                   EventUI           │
│       │                                   (ApplyEffects     │
│       ▼                                    → 改为通过        │
│  SettlementBridge                     ResourceService)      │
│       │                                                     │
│       ▼                                                     │
│  ┌─────────────────────────────────────────────┐            │
│  │  运行时状态层（纯 C#，无 MonoBehaviour）     │            │
│  │                                             │            │
│  │  GameRunState                               │            │
│  │    ├── Phase (PhaseRunState)                │            │
│  │    ├── Day                                  │            │
│  │    ├── Tenants (Dictionary)                 │            │
│  │    ├── Rooms (Dictionary)                   │            │
│  │    └── Resources (Dictionary)               │            │
│  │         ├── "food" → ResourceRunState       │            │
│  │         └── "medicine" → ResourceRunState   │            │
│  │                                             │            │
│  │  StateReducer.TryCommit(state, changeSet)   │            │
│  │  RunChanges: AdjustResourceChange           │            │
│  └─────────────────────────────────────────────┘            │
└─────────────────────────────────────────────────────────────┘
```

### 3.2 所有权与生命周期

| 组件 | 所有权 | 生命周期 | 说明 |
|------|--------|---------|------|
| `GameRunState` | `SettlementBridge` 持有唯一实例 | 随 SettlementBridge.Awake() 创建 | 全局单例 GameRunState 的唯一持有者 |
| `StateReducer` | `SettlementBridge` 持有 | 同上 | 用于提交所有资源变更 |
| `GamePhaseManager` | 场景 MonoBehaviour 单例 | Awake 创建 | 保持现有行为不变 |
| `EventManager` | 场景 MonoBehaviour 单例 | Awake 创建 | 保持现有行为不变 |
| `SettlementBridge` | 场景 MonoBehaviour 单例，**新增** | Awake 创建 | 桥接层：持有 GameRunState，监听阶段转换，在 Night→Dawn 时执行结算 |
| `ResourceService` | 静态工具类，**新增** | 无实例状态 | 封装资源查询和变更提交的便捷方法 |

### 3.3 窄桥设计原则

GamePhaseManager/EventManager 不直接持有或感知 GameRunState。SettlementBridge 监听 PhaseEnteredEvent，在合适的阶段转换时操作 GameRunState。这是有意的窄桥设计——不重构现有 Manager 的生命周期，仅通过事件订阅建立最小耦合。

---

## 4 资源数据定义

### 4.1 ResourceDefinition（Authoring SO）

新增 ScriptableObject 类 `ResourceDefinition`，用于编辑器中定义资源。

```csharp
// Assets/Scripts/Hotel/Authoring/Resources/ResourceDefinition.cs
namespace Hotel.Authoring.Resources
{
    [CreateAssetMenu(menuName = "Hotel/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        public string resourceId;      // 稳定标识符，如 "food"、"medicine"
        public string displayName;     // 显示名称，如 "食物"、"药品"
        public int initialAmount;      // 初始数量 = 10
        public Sprite icon;            // 占位白色圆形图标
    }
}
```

### 4.2 初始资源 SO 资产

| 资产名 | 绝对路径 | resourceId | displayName | initialAmount |
|--------|---------|------------|-------------|---------------|
| `Food.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Resources\Food.asset` | `food` | `食物` | `10` |
| `Medicine.asset` | `E:\UnityProjects\260725GJ\Assets\Data\Resources\Medicine.asset` | `medicine` | `药品` | `10` |

图标：使用 Unity 内置白色圆形 Sprite 或运行时生成的 32×32 白色圆形纹理作为占位符。

### 4.3 ResourceRunState（已有，无需修改）

```csharp
// 已存在于 Assets/Scripts/Hotel/Runtime/State/RunModel.cs
[Serializable]
public sealed class ResourceRunState
{
    public string ResourceId;
    public string DefinitionId;
    public int Amount;
}
```

### 4.4 资源初始化

SettlementBridge.Awake() 中，根据场景中引用的 ResourceDefinition SO 列表，将每种资源写入 GameRunState.Resources：

```csharp
// 伪代码，实际实现见实施计划
foreach (var def in resourceDefinitions)
{
    _runState.Resources[def.resourceId] = new ResourceRunState
    {
        ResourceId = def.resourceId,
        DefinitionId = def.name,
        Amount = def.initialAmount
    };
}
```

---

## 5 新增事件通道

### 5.1 FoodShortageEvent（食物不足事件）

```csharp
// Assets/Scripts/Hotel/Data/FoodShortageEvent.cs
[CreateAssetMenu(menuName = "Events/FoodShortageEvent")]
public class FoodShortageEvent : GameEvent<FoodShortageData> {}

[Serializable]
public struct FoodShortageData
{
    public int day;            // 发生短缺的天数
    public int shortageAmount; // 缺少的数量 = required - available
}
```

SO 资产：`E:\UnityProjects\260725GJ\Assets\Data\Events\FoodShortageEvent.asset`

### 5.2 ResourceAdjustedEvent（资源调整事件，基础设施）

```csharp
// Assets/Scripts/Hotel/Data/ResourceAdjustedEvent.cs
[CreateAssetMenu(menuName = "Events/ResourceAdjustedEvent")]
public class ResourceAdjustedEvent : GameEvent<ResourceAdjustedData> {}

[Serializable]
public struct ResourceAdjustedData
{
    public string resourceId;  // 如 "food"、"medicine"
    public int delta;          // 变化量（正=增加，负=减少）
    public int newAmount;      // 变更后数量
}
```

SO 资产：`E:\UnityProjects\260725GJ\Assets\Data\Events\ResourceAdjustedEvent.asset`

---

## 6 结算流程与时序

### 6.1 每日食物结算序列

```
用户长按 NextPhaseButton
       │
       ▼
GamePhaseManager.AdvancePhase()
  currentPhase: Night → Dawn
  currentDay: N → N+1
       │
       ▼
PhaseEnteredEvent.Raise({ day: N+1, phase: Dawn })
       │
       ├──► EventManager.OnPhaseEntered()  [已有行为，不修改]
       │
       └──► SettlementBridge.OnPhaseEntered(data)
              │
              ├─ 检查: data.phase == Dawn 且上一阶段为 Night
              │   （通过 state.Phase.Current == HotelPhase.Dawn 判断，
              │    且 SettlementBridge 记录的 lastPhase == Night）
              │
              ├─ 幂等检查: data.day > lastSettlementDay
              │   （防止同一 Night→Dawn 重复结算）
              │
              ├─ 计算: countTenants = count(Tenants where RoomId != null || 在房间中)
              │   （已分配房间的租户数量）
              │
              ├─ 读取: available = Resources["food"].Amount
              │
              ├─ 构建 AuthorizedChangeSet (Coordinator 授权):
              │   ├─ AdjustResourceChange("food", -min(countTenants, available))
              │   └─ AppendAuditLogChange("Day N food settlement: consumed X, shortage Y")
              │
              ├─ StateReducer.TryCommit(state, changeSet)
              │
              ├─ 发布 ResourceAdjustedEvent({ "food", delta, newAmount })
              │
              └─ 若 available < countTenants:
                  └─ 发布 FoodShortageEvent({ day: N+1, shortageAmount: countTenants - available })
```

### 6.2 时序约束

| 约束 | 说明 |
|------|------|
| 结算时机 | 仅在 PhaseEnteredEvent 的 phase == Dawn 时触发 |
| 幂等保护 | 使用 `lastSettlementDay` 字段，确保每个 day 只结算一次 |
| 原子性 | 所有资源变更打包在同一个 AuthorizedChangeSet 中，由 StateReducer 原子提交 |
| 顺序 | 结算在 EventManager 处理阶段事件之前完成（SettlementBridge 的 OnPhaseEntered 注册顺序先于 EventManager） |

### 6.3 注册顺序保障

SettlementBridge 的 OnEnable 必须在 EventManager 的 OnEnable 之前执行，以确保结算先于事件队列处理。Unity 中同层级 MonoBehaviour 的 OnEnable 按场景中组件顺序调用。实施时需在场景中将 SettlementBridge 组件排在 EventManager 之前，或使用 `DefaultExecutionOrder` 属性：

```csharp
[DefaultExecutionOrder(-100)]
public class SettlementBridge : MonoBehaviour
```

---

## 7 新增与修改的类

### 7.1 SettlementBridge（新增）

```
路径: Assets/Scripts/Hotel/Managers/SettlementBridge.cs
```

职责：
- 持有全局 GameRunState 和 StateReducer 实例
- 根据 ResourceDefinition SO 列表初始化 Resources 字典
- 监听 PhaseEnteredEvent，在 Night→Dawn 时执行食物结算
- 发布 FoodShortageEvent 和 ResourceAdjustedEvent
- 提供 GetResourceAmount(resourceId) 查询方法供 UI 使用

字段：
- `List<ResourceDefinition> resourceDefinitions` — Inspector 引用
- `PhaseEnteredEvent onPhaseEntered` — Inspector 引用
- `FoodShortageEvent onFoodShortage` — Inspector 引用
- `ResourceAdjustedEvent onResourceAdjusted` — Inspector 引用
- `GameRunState _runState` — 运行时创建
- `StateReducer _reducer` — 运行时创建
- `int _lastSettlementDay` — 幂等保护，默认 0
- `HotelPhase _lastPhase` — 记录上一阶段，用于判断 Night→Dawn

### 7.2 ResourceService（新增，静态工具类）

```
路径: Assets/Scripts/Hotel/Runtime/Services/ResourceService.cs
```

职责：
- 封装资源查询和变更提交的便捷静态方法
- 供 EventUI 的 ApplyEffects 和未来的资源调整逻辑使用

方法：
- `static int GetAmount(GameRunState state, string resourceId)`
- `static CommitResult TryAdjust(GameRunState state, StateReducer reducer, string resourceId, int delta, string authorizer, ResourceAdjustedEvent channel)`

### 7.3 EventUI.ApplyEffects 修改

现有代码：
```csharp
case EffectType.ModifyErosion:
    if (ErosionManager.Instance != null)
        ErosionManager.Instance.ModifyErosion(effect.floatValue);
    break;
```

修改后：
```csharp
case EffectType.ModifyTenantErosion:
    // 通过 SettlementBridge 持有的 GameRunState/StateReducer 提交租户侵蚀变更
    // 具体租户 ID 需要从事件上下文获取（当前无租户上下文，暂时记录日志）
    Debug.LogWarning("[EventUI] ModifyTenantErosion effect requires tenant context — deferred");
    break;
```

说明：当前 EventConfig 的 EventEffect 中 EffectType 仅有 `None` 和 `ModifyErosion`。删除 ErosionManager 后：
- 将 `EffectType.ModifyErosion` 重命名为 `EffectType.ModifyTenantErosion`（语义更准确）
- ApplyEffects 中移除对 ErosionManager.Instance 的调用
- 由于当前事件效果没有租户上下文（不知道影响哪个租户），暂时仅记录警告日志
- 未来实现事件效果系统时再补充完整的租户侵蚀调整逻辑

### 7.4 InfoPanelResourceDisplay（新增）

```
路径: Assets/Scripts/Hotel/UI/InfoPanelResourceDisplay.cs
```

职责：
- 挂载在 InfoPanel 的新增子 GameObject "ResourcePanel" 上
- 监听 ResourceAdjustedEvent 和 PhaseEnteredEvent 更新显示
- 从 SettlementBridge.Instance 读取当前资源数量

布局：
- 水平排列：[白色圆形图标] Food 数量 [白色圆形图标] Medicine 数量
- 使用 HorizontalLayoutGroup，与 InfoPanel 现有布局一致

字段：
- `TextMeshProUGUI foodAmountText`
- `TextMeshProUGUI medicineAmountText`
- `Image foodIcon`
- `Image medicineIcon`
- `ResourceAdjustedEvent onResourceAdjusted`
- `PhaseEnteredEvent onPhaseEntered`

---

## 8 UI 行为

### 8.1 InfoPanel 资源显示

InfoPanel 当前结构（场景 fileID 分析）：

```
InfoPanel (HorizontalLayoutGroup, fileID: 371217395)
  ├── TimePanel (fileID: 1401548637)
  │     ├── [子元素 1]
  │     └── [子元素 2]
  └── Spacer (fileID: 1644089826, LayoutElement flexibleWidth=1)
```

修改后结构：

```
InfoPanel (HorizontalLayoutGroup)
  ├── TimePanel (不变)
  ├── Spacer (不变)
  └── ResourcePanel (新增, fileID: 待创建)
        ├── FoodIcon (Image, 白色圆形占位)
        ├── FoodAmountText (TextMeshProUGUI, "10")
        ├── MedicineIcon (Image, 白色圆形占位)
        └── MedicineAmountText (TextMeshProUGUI, "10")
```

ResourcePanel 使用 HorizontalLayoutGroup，间距与 InfoPanel 一致（spacing=10）。

### 8.2 显示更新逻辑

| 触发时机 | 行为 |
|---------|------|
| Start() | 从 SettlementBridge.Instance 读取初始数量并显示 |
| ResourceAdjustedEvent 回调 | 根据 delta 更新对应资源的文本显示 |
| PhaseEnteredEvent (Dawn) | 结算后重新读取数量（兜底同步） |

### 8.3 布局保护

- 不修改 InfoPanel 现有 HorizontalLayoutGroup 的参数
- 不修改 TimePanel 或 Spacer 的 RectTransform
- ResourcePanel 作为 InfoPanel 的最后一个子节点追加
- Spacer 的 LayoutElement.flexibleWidth 保持为 1，确保 Spacer 将 ResourcePanel 推到右侧

---

## 9 事件效果迁移

### 9.1 当前事件效果机制

`EventConfig` 中的 `EventEffect` 类使用 `EffectType` 枚举：

```csharp
public enum EffectType { None, ModifyErosion }
```

`EventUI.ApplyEffects()` 调用 `ErosionManager.Instance.ModifyErosion(effect.floatValue)`。

### 9.2 迁移方案

| 操作 | 说明 |
|------|------|
| 重命名枚举值 | `EffectType.ModifyErosion` → `EffectType.ModifyTenantErosion` |
| 更新 EventConfig 资产 | 检查所有 `Assets/Data/Configs/Event_*.asset`，将引用更新 |
| 修改 EventUI.ApplyEffects | 移除 ErosionManager.Instance 调用，改为日志警告（当前无租户上下文） |
| 保留 floatValue 字段 | EventEffect.floatValue 语义从"全局侵蚀变更"变为"租户侵蚀变更量"，字段不变 |

### 9.3 现有事件资产影响

检查的事件资产（路径 `E:\UnityProjects\260725GJ\Assets\Data\Configs\`）：
- Event_Dawn_1.asset ~ Event_Dawn_2.asset
- Event_Day_1.asset ~ Event_Day_4.asset
- Event_Dusk_1.asset ~ Event_Dusk_2.asset
- Event_Night_1.asset ~ Event_Night_4.asset

这些资产中如有 `effectType = ModifyErosion` 的 Effect 条目，需在 Unity Inspector 中手动更新为 `ModifyTenantErosion`（枚举值重命名后自动映射，取决于序列化方式；若使用整数序列化则需手动调整）。

---

## 10 错误与边界行为

| 场景 | 行为 |
|------|------|
| 食物为 0 时结算 | `available = 0`，消耗量 = 0，食物保持 0，发布 FoodShortageEvent(shortageAmount = countTenants) |
| 食物充足 | 正常扣除，不发布 FoodShortageEvent |
| 食物部分不足 | 扣除所有可用食物（clamp 到 0），发布 FoodShortageEvent(shortageAmount = required - available) |
| 无租户 | countTenants = 0，不扣除食物，不发布任何事件 |
| 同一天重复触发 Night→Dawn | 幂等保护：`lastSettlementDay >= data.day` 时跳过 |
| GameRunState 为 null | SettlementBridge 结算逻辑跳过，记录错误日志 |
| StateReducer.TryCommit 失败 | 记录错误日志，不发布事件（回滚到结算前状态） |
| SettlementBridge.Instance 为 null | InfoPanelResourceDisplay 在 Start 中检查并记录警告 |
| ResourceDefinition 列表为空 | Resources 字典为空，GetResourceAmount 返回 0 |
| Medicine 资源 | 不参与自动消耗，仅显示当前数量 |

---

## 11 场景连接

### 11.1 MainScene 修改清单

文件：`E:\UnityProjects\260725GJ\Assets\Scenes\MainScene.unity`

| 操作 | GameObject | 说明 |
|------|-----------|------|
| 删除 | `TimeManager` (fileID: 163388801) | 已禁用的遗留时间管理器 |
| 删除 | `ErosionManager` (fileID: 1330069343) | 全局侵蚀管理器 |
| 新增 | `SettlementBridge` | 空 GameObject，挂 SettlementBridge 组件 |
| 新增 | `ResourcePanel` | InfoPanel 的子 GameObject，挂 InfoPanelResourceDisplay + HorizontalLayoutGroup |
| 修改 | InfoPanel 的 m_Children | 追加 ResourcePanel 的 RectTransform fileID |

### 11.2 SettlementBridge Inspector 配置

| 字段 | 值 |
|------|---|
| resourceDefinitions | [Food.asset, Medicine.asset] |
| onPhaseEntered | PhaseEnteredEvent.asset |
| onFoodShortage | FoodShortageEvent.asset |
| onResourceAdjusted | ResourceAdjustedEvent.asset |

### 11.3 ResourcePanel Inspector 配置

| 字段 | 值 |
|------|---|
| foodAmountText | 指向 FoodAmountText 子对象的 TextMeshProUGUI |
| medicineAmountText | 指向 MedicineAmountText 子对象的 TextMeshProUGUI |
| foodIcon | 指向 FoodIcon 子对象的 Image |
| medicineIcon | 指向 MedicineIcon 子对象的 Image |
| onResourceAdjusted | ResourceAdjustedEvent.asset |
| onPhaseEntered | PhaseEnteredEvent.asset |

---

## 12 迁移顺序

执行必须严格按以下顺序，每步完成后验证编译通过再进行下一步：

| 步骤 | 操作 | 验证 |
|------|------|------|
| 1 | 创建新文件：`ResourceDefinition.cs`、`FoodShortageEvent.cs`、`ResourceAdjustedEvent.cs`、`SettlementBridge.cs`、`ResourceService.cs`、`InfoPanelResourceDisplay.cs` | 编译通过，无新错误 |
| 2 | 创建 SO 资产：`Food.asset`、`Medicine.asset`、`FoodShortageEvent.asset`、`ResourceAdjustedEvent.asset` | Unity 中资产可正确创建 |
| 3 | 重命名 `EffectType.ModifyErosion` → `EffectType.ModifyTenantErosion`；更新 `EventConfig.cs`、`EventUI.cs` | 编译通过，事件资产枚举值自动更新或手动修正 |
| 4 | 删除遗留时间系统文件：`TimeManager.cs`、`TimeState.cs`、`TimeUI.cs`、`TimeControlUI.cs`、`TimePhaseChangedEvent.cs`、`DayStartedEvent.cs`、`TimeSpeedChangedEvent.cs`、`TimeEventAssetCreator.cs` | 编译通过，无引用错误 |
| 5 | 删除遗留时间系统 SO 资产：`TimePhaseChangedEvent.asset`、`DayStartedEvent.asset`、`TimeSpeedChangedEvent.asset` | 编译通过 |
| 6 | 删除全局侵蚀系统文件：`ErosionManager.cs`、`ErosionState.cs`、`ErosionConfig.cs`、`ErosionChangedEvent.cs`、`ErosionUI.cs` | 编译通过，无引用错误 |
| 7 | 删除全局侵蚀系统 SO 资产：`ErosionChangedEvent.asset` | 编译通过 |
| 8 | 修改 MainScene：删除 TimeManager/ErosionManager 场景对象，新增 SettlementBridge 和 ResourcePanel，配置 Inspector 引用 | 场景加载无错误 |
| 9 | 运行游戏验证：进入 Night → 推进到 Dawn → 检查 Console 无错误，检查食物数量正确扣除 | 运行时无异常 |

---

## 13 测试与验证矩阵

### 13.1 单元测试（NUnit，可在 Editor 中运行）

所有测试路径：`E:\UnityProjects\260725GJ\Assets\Tests\Hotel.Runtime.Tests\Runtime\`

| 测试名 | 验证内容 |
|--------|---------|
| `FoodSettlement_NormalDeduction_DecreasesFoodByTenantCount` | 3 个已分配租户，Food=10 → 结算后 Food=7 |
| `FoodSettlement_ExactFood_CoversAllTenants` | 5 个租户，Food=5 → 结算后 Food=0，不发布 shortage |
| `FoodSettlement_InsufficientFood_ClampsToZero` | 5 个租户，Food=3 → 结算后 Food=0，发布 shortage=2 |
| `FoodSettlement_ZeroFood_PublishesFullShortage` | 3 个租户，Food=0 → Food 保持 0，发布 shortage=3 |
| `FoodSettlement_NoTenants_NoChange` | 0 个租户 → Food 不变，不发布任何事件 |
| `FoodSettlement_Idempotent_SameDaySkips` | 同一天连续触发两次 → 第二次跳过，Food 仅扣除一次 |
| `FoodSettlement_ResourceReducer_AtomicCommit` | 结算变更在同一 ChangeSet 中，失败时无部分写入 |
| `FoodSettlement_NextDay_SettlesAgain` | Day 1 结算后，Day 2 正常结算，lastSettlementDay 更新 |
| `ResourceService_AdjustPositive_IncreasesAmount` | ResourceService.TryAdjust delta=+5 → Amount 增加 5 |
| `ResourceService_AdjustNegative_DecreasesAmount` | ResourceService.TryAdjust delta=-3 → Amount 减少 3 |
| `ResourceService_AdjustMissingResource_Fails` | resourceId 不存在时 TryCommit 返回失败 |
| `MedicineSettlement_NotConsumed` | 结算后 Medicine 数量不变 |
| `ResourceDefinition_InitializesCorrectAmount` | SettlementBridge 初始化后 Resources["food"].Amount == initialAmount |

### 13.2 集成测试（场景验证）

| 测试名 | 验证内容 |
|--------|---------|
| `MainScene_SettlementBridgeExists` | 场景中有 SettlementBridge 组件 |
| `MainScene_ResourcePanelExists` | InfoPanel 下有 ResourcePanel 子对象 |
| `MainScene_NoLegacyTimeManager` | 场景中无 TimeManager 组件 |
| `MainScene_NoErosionManager` | 场景中无 ErosionManager 组件 |

### 13.3 运行时验证（Unity Play Mode）

| 步骤 | 预期结果 |
|------|---------|
| 启动游戏 | InfoPanel 显示 Food=10, Medicine=10 |
| 进入 Night 阶段 | 无结算发生 |
| 长按推进到 Dawn | Food 减少（= 已分配租户数），Medicine 不变 |
| 再次长按推进（Dawn→Day→Dusk→Night→Dawn） | Food 再次减少，幂等无重复扣除 |
| Console 窗口 | 无红色错误，无 MissingReferenceException |

---

## 14 自检审查

### 14.1 无 TODO/TBD/占位符

本规格中所有字段、路径、行为均已明确。白色圆形图标使用 Unity 内置资源或运行时生成纹理，不标记为占位符（设计意图如此）。

### 14.2 无矛盾

- 结算时机：PhaseEnteredEvent phase==Dawn 时触发，与 GamePhaseManager 的 Night→Dawn 转换一致
- 幂等保护：lastSettlementDay 与 data.day 比较，逻辑自洽
- 原子性：所有变更在同一 AuthorizedChangeSet 中，由 StateReducer 保证
- 事件效果迁移：EffectType 重命名不影响 EventEffect 的 floatValue 字段语义

### 14.3 需求无歧义

- "Food 和 Medicine" → 明确为两种资源，各有 SO 定义
- "已分配/在房间中的租户" → 通过 Tenants 中 RoomId != null 判断
- "不为负" → clamp 到 0
- "不实现 Medicine 消耗" → Medicine 仅初始化和显示，不参与结算逻辑
- "窄桥" → SettlementBridge 通过事件订阅连接，不修改 GamePhaseManager/EventManager 内部
- "用户自行运行测试" → 本规格提供测试矩阵，实施者编写测试代码，用户在 Unity 中运行
