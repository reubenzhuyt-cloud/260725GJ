# 项目架构

## 范围与主要位置

本项目基于 Unity 2022.3.62f3c1 LTS 与 URP 14，是一款二维酒店经营游戏。主要代码与资源位于 `Assets/Scripts`、`Assets/Data`、`Assets/Scenes` 和 `Assets/Tests`。

## 项目与模块

```text
Assets/Scripts
├─ Core/Events
└─ Hotel
   ├─ Runtime
   │  ├─ State
   │  ├─ Kernel/Changes
   │  └─ Kernel/Reduction
   ├─ Authoring
   ├─ Data
   ├─ Managers
   ├─ UI
   ├─ Presentation/Avatars
   └─ Camera
Assets/Data
Assets/Scenes
Assets/Tests
```

## 程序集与依赖

- `Hotel.Runtime`：纯 C# 运行时程序集。
- `Hotel.Authoring`：依赖 `Hotel.Runtime`。
- `Hotel.Runtime.Tests`：依赖 `Hotel.Runtime` 与 `Hotel.Authoring`。
- 其他游戏脚本位于 `Assembly-CSharp`，并消费 `Hotel.Runtime`。

## 状态内核

`GameRunState` 按功能切片保存游戏运行状态。状态变更通过 `RunChange` 与 `AuthorizedChangeSet` 表达，`StateReducer` 负责校验、原子提交并推进 `StateVersion`。

```text
输入变更
  → AuthorizedChangeSet
  → StateReducer（校验与原子提交）
  → GameRunState 切片 + StateVersion
```

主要消费者包括阶段管理、事件管理、结算桥接、租客与房间分配、用户界面及表现层。

## 运行时流程

```text
NextPhaseButton
  → GamePhaseManager
  → PhaseEntered ScriptableObject 通道
  → EventManager / SettlementBridge / UI 等
```

`EventManager` 维护事件队列并处理租客评审。租客确认后，结果进入状态内核，再由分配表现层更新租客头像与相关界面。

## 事件通信

事件通信以通用和非通用 `GameEvent` ScriptableObject 通道为主。监听器在启用与禁用生命周期中完成注册和注销；分配变化另通过 C# assignment-changed 事件传播。

## 表现层与镜头

UI 接收运行时事件并刷新显示。头像表现层负责租客分配、拖放交互与 LOD。镜头模块独立处理相机逻辑与视差效果，和对象分配表现相隔离。

## 编辑与数据资源

Authoring 层定义并配置 `EventConfig`、候选人 ScriptableObject、阶段循环与资源定义。`Assets/Data` 保存事件通道资源及运行时配置。

## 阶段类型边界

项目存在两个独立的阶段概念：`GamePhase` 用于管理器与事件数据，`HotelPhase` 用于运行时与 Authoring。两者的枚举顺序不同，架构消费者必须将它们视为不同类型，并且只通过显式映射使用。

## 场景与测试

`MainScene` 是主要场景；`SampleScene` 当前位于构建设置中。运行时测试仅在编辑器环境执行。
