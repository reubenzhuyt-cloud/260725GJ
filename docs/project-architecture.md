# 酒店经营游戏 — 项目架构文档

> 供协作 Agent 参考，包含完整架构设计、接口规范、已有系统状态和待实现需求。

---

## 一、项目概述

Unity 2D 项目，核心玩法为**酒店房间经营 + 房客审查**双系统协作。

- **引擎**: Unity (URP 2D)
- **通信方式**: ScriptableObject 事件通道（松耦合）

---

## 二、目录结构

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── Events/           # SO 事件框架（通用基类）
│   │       ├── GameEvent.cs
│   │       ├── GameEventT.cs
│   │       ├── GameEventListener.cs
│   │       └── GameEventListenerT.cs
│   ├── Hotel/                # 房间经营模块
│   │   ├── Data/             # 数据定义（SO 配置、运行时状态、事件类型）
│   │   ├── Managers/         # 管理器（单例）
│   │   └── UI/               # UI 控制器
│   ├── Editor/               # 编辑器工具
│   └── Review/               # 审查模块（另一个人负责）
├── Data/
│   ├── Events/               # SO 事件资产
│   └── Configs/              # SO 配置资产
├── Scenes/
│   └── MainScene.unity       # 主场景
└── Prefabs/                  # 预制体
```

---

## 三、核心架构：SO 事件系统

### 设计原则

所有系统间通信通过 **ScriptableObject 事件通道** 实现解耦：
- 发布者持有 SO 引用，调用 `Raise(data)`
- 订阅者持有同一 SO 引用，调用 `Register(callback)`
- 发布者和订阅者互不知道对方存在

### 基类定义

**无参事件**: `GameEvent` (ScriptableObject)
- `Raise()` — 触发事件
- `Register(GameEventListener)` / `Unregister(GameEventListener)`

**泛型事件**: `GameEvent<T>` (ScriptableObject)
- `Raise(T data)` — 触发事件并传递数据
- `Register(Action<T>)` / `Unregister(Action<T>)`
- `Register(GameEventListener<T>)` / `Unregister(GameEventListener<T>)`

### 使用模式

```csharp
// 定义事件数据
[Serializable]
public struct PhaseData { public int day; public TimePhase phase; }

// 定义 SO 事件类型
[CreateAssetMenu(menuName = "Events/TimePhaseChangedEvent")]
public class TimePhaseChangedEvent : GameEvent<PhaseData> {}

// 发布者
public class TimeManager : MonoBehaviour {
    public TimePhaseChangedEvent onPhaseChanged;  // 拖入 SO 资产
    void SomeMethod() {
        onPhaseChanged.Raise(new PhaseData { day = 1, phase = TimePhase.Dawn });
    }
}

// 订阅者
public class TimeUI : MonoBehaviour {
    public TimePhaseChangedEvent onPhaseChanged;  // 同一个 SO 资产
    void OnEnable() => onPhaseChanged.Register(OnPhaseChanged);
    void OnDisable() => onPhaseChanged.Unregister(OnPhaseChanged);
    void OnPhaseChanged(PhaseData data) { /* 更新 UI */ }
}
```

---

## 四、已实现系统

### 4.1 时间系统

**文件**:
- `Scripts/Hotel/Data/TimeState.cs` — 运行时数据
- `Scripts/Hotel/Data/TimePhaseChangedEvent.cs` — 阶段变化事件
- `Scripts/Hotel/Data/DayStartedEvent.cs` — 新一天事件
- `Scripts/Hotel/Data/TimeSpeedChangedEvent.cs` — 速度变化事件
- `Scripts/Hotel/Managers/TimeManager.cs` — 管理器（单例）

**TimeState 数据结构**:
```csharp
public class TimeState {
    public int currentDay = 1;        // 当前天数
    public int hour = 5;              // 当前小时 (0-23)
    public int minute = 0;            // 当前分钟 (0-59)
    public TimePhase currentPhase;    // 当前阶段
}
```

**阶段划分**:
| 阶段 | 时间范围 |
|------|---------|
| Dawn 黎明 | 05:00 - 07:00 |
| Daytime 白昼 | 07:00 - 17:00 |
| Dusk 黄昏 | 17:00 - 19:00 |
| Night 黑夜 | 19:00 - 05:00 |

**时间流速**: 1 真实秒 = 20 游戏分钟 × 速度倍率

**TimeManager 公开接口**:
```csharp
// 单例
public static TimeManager Instance;

// 状态
public TimeState timeState;
public bool isPaused;
public TimeSpeed currentSpeed;  // Normal=1, Fast=2, Faster=3, Fastest=5

// 方法
public void SetSpeed(int multiplier);       // 设置速度 (1/2/3/4/5)
public void PauseTime();                    // 暂停
public void ResumeTime();                   // 恢复
public void TogglePause();                  // 切换暂停
public TimeState GetTimeState();            // 获取当前时间状态
public string GetTimeString();              // 获取 "HH:MM" 格式字符串

// 事件通道（拖入 SO 资产）
public TimePhaseChangedEvent onPhaseChanged;
public DayStartedEvent onDayStarted;
public TimeSpeedChangedEvent onTimeSpeedChanged;
```

**事件数据**:
```csharp
public struct PhaseData { public int day; public int hour; public int minute; public TimePhase phase; }
public struct DayData { public int day; }
public struct TimeSpeedData { public int speedMultiplier; public bool isPaused; public bool isWaitingAtNode; }
```

---

### 4.2 侵蚀度系统

**文件**:
- `Scripts/Hotel/Data/ErosionState.cs` — 运行时数据
- `Scripts/Hotel/Data/ErosionConfig.cs` — SO 配置（各阶段侵蚀速率）
- `Scripts/Hotel/Data/ErosionChangedEvent.cs` — 侵蚀度变化事件
- `Scripts/Hotel/Managers/ErosionManager.cs` — 管理器（单例）

**ErosionState 数据结构**:
```csharp
public class ErosionState {
    public float erosionValue = 0f;   // 0-100
    public void Set(float value);     // 设置绝对值（自动 Clamp）
    public void Add(float delta);     // 增减（自动 Clamp）
}
```

**ErosionManager 公开接口**:
```csharp
public static ErosionManager Instance;

public ErosionState erosionState;
public ErosionChangedEvent onErosionChanged;  // SO 事件通道

public void ModifyErosion(float delta);   // 增减侵蚀度
public void SetErosion(float value);      // 设置绝对值
public float GetErosion();                // 获取当前值
```

**事件数据**:
```csharp
public struct ErosionData { public float oldValue; public float newValue; public float delta; }
```

**ErosionConfig (SO)**:
```csharp
public class ErosionConfig : ScriptableObject {
    public float dawnRate = 0f;
    public float daytimeRate = 0f;
    public float duskRate = 0f;
    public float nightRate = 2f;
}
```
> 注意：ErosionConfig 尚未接入自动 tick 逻辑，目前侵蚀度只能通过 `ErosionManager.ModifyErosion()` 手动修改。

---

### 4.3 事件系统（游戏事件弹窗）

**文件**:
- `Scripts/Hotel/Data/EventConfig.cs` — SO 配置（事件定义）
- `Scripts/Hotel/Data/GamePopupEvent.cs` — SO 事件通道
- `Scripts/Hotel/Managers/EventManager.cs` — 管理器（单例）
- `Scripts/Hotel/UI/EventUI.cs` — 弹窗 UI 控制器

**EventConfig (SO) 数据结构**:
```csharp
public class EventConfig : ScriptableObject {
    public int eventIndex;                      // 事件编号
    public string eventId;                      // 事件唯一ID
    public string eventTitle;                   // 标题
    [TextArea] public string eventDescription;  // 描述
    public Sprite eventImage;                   // 配图
    public int triggerHour;                     // 触发小时
    public int triggerMinute;                   // 触发分钟
    public GameEventType eventType;             // Confirm 或 Choice
    public List<EventEffect> confirmEffects;    // Confirm 模式的效果列表
    public List<ChoiceOption> choices;          // Choice 模式的选项列表
}
```

**效果系统**:
```csharp
public enum EffectType { None, ModifyErosion }

public class EventEffect {
    public EffectType effectType;
    public float floatValue;    // 效果数值（正负均可）
}

public class ChoiceOption {
    public string choiceId;
    public string choiceText;
    [TextArea] public string choiceResult;
    public List<EventEffect> choiceEffects;  // 每个选项独立的效果列表
}
```

**弹窗数据传递**:
```csharp
public struct PopupData {
    public int eventIndex;
    public string eventId;
    public string title;
    public string description;
    public Sprite image;
    public GameEventType eventType;
    public EventEffect[] confirmEffects;      // Confirm 类型
    public string[] choiceTexts;              // Choice 类型
    public string[] choiceResults;            // Choice 类型
    public EventEffect[][] choiceEffects;     // 每个选项独立的效果数组
}
```

**EventManager 公开接口**:
```csharp
public static EventManager Instance;

public List<EventConfig> scheduledEvents;        // 拖入 SO 配置资产
public GamePopupEvent onPopupEvent;              // SO 事件通道

// 每游戏分钟自动检查，命中则触发
public void CheckTimeEvents(int hour, int minute, int day);
public void ResetToday();                        // 重置今日已触发记录
```

**触发流程**:
```
TimeManager 时钟推进 → EventManager.Update() 检测时间
→ 命中事件 → 暂停时间 → onPopupEvent.Raise(data)
→ EventUI 收到弹窗 → 显示面板 → 玩家操作 → 应用效果 → 恢复时间
```

**事件类型**:
| 类型 | UI 行为 | 效果触发时机 |
|------|---------|-------------|
| Confirm | 显示确认按钮 | 点击确认时 |
| Choice | 动态生成选项按钮 | 点击对应选项时 |

---

### 4.4 UI 系统

**文件**:
- `Scripts/Hotel/UI/TimeUI.cs` — 时间显示（左上角）
- `Scripts/Hotel/UI/TimeControlUI.cs` — 时间控制按钮（暂停/速度）
- `Scripts/Hotel/UI/ErosionUI.cs` — 侵蚀度显示（右上角）
- `Scripts/Hotel/UI/EventUI.cs` — 事件弹窗（全屏覆盖）

**场景层级**:
```
GameManager
├── TimeManager
├── ErosionManager
└── EventManager

GameCanvas (Canvas Scaler: Scale With Screen Size, 1920×1080)
├── UIManager
│   ├── TimeUI
│   ├── TimeControlUI
│   ├── ErosionUI
│   └── EventUI
├── InfoPanel (顶部通栏, HorizontalLayoutGroup)
│   ├── TimePanel
│   │   ├── DayText ("Day 1")
│   │   └── DetailTime
│   │       ├── ClockText ("05:00")
│   │       └── PhaseText ("黎明")
│   ├── Spacer (flexWidth=1)
│   ├── ControlPanel
│   │   └── ToggleGroup (4个 Toggle 互斥)
│   │       ├── PauseToggle
│   │       ├── Speed1xToggle (默认选中)
│   │       ├── Speed2xToggle
│   │       └── Speed4xToggle
│   └── ErosionPanel
│       └── ErosionText ("侵蚀度: 0.0%")
└── EventOverlay (默认隐藏, 全屏半透明覆盖)
    └── EventPanel
        ├── EventLeftInfo
        │   ├── EventTitleText
        │   ├── EventImage
        │   └── EventDescriptionText
        ├── EventRightContent
        │   └── ConfirmButton
        └── ChoiceButtonContainer (VerticalLayoutGroup)
            └── ChoiceButtonPrefab (模板, 默认隐藏)
```

**TimeControlUI 状态机**:
```csharp
enum SpeedState { Normal(1x), Fast(2x), Faster(4x) }
```
- 4 个 Toggle 在一个 ToggleGroup 中互斥
- 事件触发时自动切回 1x
- 事件关闭后恢复规则：1x→1x, 2x→2x, 4x→2x, 暂停→1x

---

## 五、待实现系统（来自架构文档）

| 系统 | 说明 | 需要的接口 |
|------|------|-----------|
| **房间系统** | 房间布局、容量、解锁 | `RoomConfig`(SO), `RoomState`, `RoomManager` |
| **资源系统** | 食物、货币管理 | `ResourceState`, `ResourceManager` |
| **房客系统** | 房客状态、侵蚀度、颜色标签 | `TenantConfig`(SO), `TenantState`, `TenantManager` |
| **工作系统** | 工作分配、产出计算 | `WorkConfig`(SO), `WorkManager` |
| **驱逐系统** | 驱逐逻辑、反应触发 | `EvictionManager` |
| **存档系统** | 自动存档、读档 | `SaveManager` |

---

## 六、跨模块接口（与审查系统协作）

### 你暴露给审查系统的接口

```csharp
public interface IHotelSystem {
    int GetAvailableCapacity();           // 返回可用容量
    int GetFood();                        // 返回食物数量
    bool AddTenant(TenantData data);      // 添加房客
    List<TenantData> GetTenants();        // 返回房客列表
}
```

### 审查系统暴露给你的接口

```csharp
public interface IReviewSystem {
    List<VisitorData> GetCurrentVisitors();   // 返回当前访客列表
    bool RecruitVisitor(int visitorId);       // 招募访客
}
```

### 跨模块事件

| 事件 | 方向 | 说明 |
|------|------|------|
| `TenantRecruitedEvent` | 审查→经营 | 审查系统招募成功后触发，携带 TenantData |

---

## 七、扩展指南

### 新增 EffectType

1. 在 `EventConfig.cs` 的 `EffectType` 枚举中添加新类型
2. 在 `EventUI.cs` 的 `ApplyEffects()` 方法中添加对应 case
3. 在 EventConfig SO 的 Inspector 中即可选择新效果类型

### 新增事件类型

1. 在 `GameEventType` 枚举中添加新类型
2. 在 `EventConfig` 中添加对应的数据字段
3. 在 `EventManager.TriggerEvent()` 中打包数据
4. 在 `EventUI.OnPopupReceived()` 中添加新的显示分支
5. 创建对应的 UI 面板

### 新增 Manager

1. 创建 `Scripts/Hotel/Managers/XxxManager.cs`（单例模式）
2. 挂到场景 `GameManager` 下
3. 如需监听事件，在 OnEnable/OnDisable 中 Register/Unregister
4. 如需对外暴露事件，创建对应的 SO 事件资产

---

## 八、SO 资产清单

| 资产路径 | 类型 | 说明 |
|----------|------|------|
| `Data/Events/TimePhaseChangedEvent.asset` | TimePhaseChangedEvent | 时间阶段变化 |
| `Data/Events/DayStartedEvent.asset` | DayStartedEvent | 新一天开始 |
| `Data/Events/TimeSpeedChangedEvent.asset` | TimeSpeedChangedEvent | 速度/暂停变化 |
| `Data/Events/ErosionChangedEvent.asset` | ErosionChangedEvent | 侵蚀度变化 |
| `Data/Events/GamePopupEvent.asset` | GamePopupEvent | 游戏事件弹窗 |
| `Data/Configs/Event_700.asset` | EventConfig | 07:00 确认型事件 |
| `Data/Configs/Event_1230.asset` | EventConfig | 12:30 选择型事件 |

---

## 九、关键设计约束

1. **所有 Manager 使用单例模式** (`Awake` 中 `Instance` 去重)
2. **所有 Manager 挂在 GameManager 子物体下**
3. **所有 UI 脚本挂在 UIManager 子物体下**
4. **所有 UI 面板挂在 GameCanvas 下**
5. **系统间通信只走 SO 事件通道，不直接引用**
6. **配置数据用 ScriptableObject，运行时数据用普通类**
7. **Canvas Scaler 使用 Scale With Screen Size (1920×1080)**
8. **颜色/位置/尺寸在 Inspector 中调整，代码中不硬编码**
