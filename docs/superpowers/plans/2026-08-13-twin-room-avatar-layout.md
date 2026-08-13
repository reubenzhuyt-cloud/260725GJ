# 双人房头像布局实现计划（Twin Room Avatar Layout Implementation Plan）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 按任务逐步实现本计划。步骤使用复选框（`- [ ]`）语法跟踪进度。
>
> **本计划特殊约定：** 按既有计划约定与工作区策略，本计划**不包含任何 bash / git 命令**（用户未授权提交，每个任务以「Unity 编译验证 + Console」＋（Task 6/7 场景接线）「Play 模式人工验收」作为完成检查点，任务末尾设「评审门」）。**当前项目无自动化测试**：`Assets/Tests/` 目录不存在（glob 0 结果；ARCHITECTURE.md 中 `Assets/Tests/Hotel.Runtime.Tests` 尚未创建，仓库根残留 `Hotel.Runtime.Tests.csproj` 为旧产物，勿碰）。本计划涉及的脚本全部属于默认 Assembly-CSharp（Managers / UI / Presentation/Avatars），无法被 `Hotel.Runtime.Tests` 程序集引用，**不创建任何 asmdef / tests 目录 / 测试文件**；验收一律为「Unity 编译检查 + Console 0 错误 + Play 模式人工验收」。`Assets/Scenes/MainScene.unity` 的任何修改**仅由 unitymaster 子代理**执行，且只做「新增组件 + 层级重建 + 序列化引用接线」，禁止任何 UI 布局/美术/样式改动。

**Goal:** 依据已批准规格《双人房头像布局设计（2026-08-13）》实现「双人房头像布局」：每个 `TenantAvatarAnchors/Anchor01..Anchor10` 挂 `RoomAvatarProperty`（`roomId` + `allowDoubleOccupancy`，默认 true）；`RoomAvatarSlotsPanel` 下 10 个房间面板各横向排布 1~2 个头像视图（自动尺寸 1:1、MiddleCenter 对齐）；分配/移动按场景属性容量（1/2）做前置校验；渲染按 `OccupantIds` 索引显示全部入住者；保存/载入与 `OccupantIds` 语义完全不变。

**Architecture:** 新增纯场景配置组件 `RoomAvatarProperty`（静态注册表，按 `roomId` 查询容量，缺失/非法一律回退容量 1）；`TenantAssignmentCoordinator` 新增统一容量查询 `TryGetRoomCapacity`、`CanAssign`、`GetRoomOccupantIds`，`TryAssign`/`TryMoveToEmptyRoom` 的占用判定由「非空即拒绝」改为「容量校验」（Reducer/状态/存档零改动）；`RoomTenantAvatarSlot` 从「单张图」改为「按索引的多视图渲染」（`avatarViews` 绑定列表 + `GetOccupantIdAt(index)`/`GetOccupantCount()`）；新增 `RoomAvatarSlotLayoutController`（挂在 `RoomAvatarSlotsPanel`）负责按容量横向布局 1~2 个视图（MiddleCenter、自动尺寸）并隐藏超容量视图。全部视图为**场景作者化结构**，不做运行时 `Instantiate`、不建 Prefab（现有源码结构无任何运行时实例化需求，且 `RoomTenantAvatarSlot.AllSlots`/`GetSlotsForRoom` 静态注册模式足以支撑每房间单组件管理多视图）。

**Tech Stack:** Unity 2022.3.62f3c1 LTS（`ProjectSettings/ProjectVersion.txt` 实测）、C#（默认 Assembly-CSharp）、UGUI（`RectTransform`/`Image`/`Mask`/`HorizontalLayoutGroup` 不用，布局由控制器逐帧计算）、旧 Input Manager。不改动 `Hotel.Runtime`（`RunModel.cs`/`RunSaveData.cs`/`StateReducer.cs`/`RunChanges.cs`）任何序列化结构。

## Global Constraints

- **独立子项目范围（只允许下列文件）**：新建 `Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs`、`Assets/Scripts/Hotel/UI/RoomAvatarSlotLayoutController.cs`；修改 `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`、`Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs`、`Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs`、`Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs`、`Assets/Scenes/MainScene.unity`。**不触碰** `Hotel.Runtime` 全部文件（`RunModel.cs`/`RunSaveData.cs`/`StateReducer.cs`/`RunChanges.cs`）、`RunSaveData.CloneRoom`、`StateReducer.Apply` 的 `AssignRoomChange` 语义、`RoomWorldHitArea`、`RoomFloorRegistry`、`TenantAvatarListItem`、`TenantAvatarDragTrigger`、`TenantDragOverlay`、`CameraController`、LOD 相关。
- **容量推导（全文唯一规则，规格 §3.2/§6）**：`allowDoubleOccupancy=true` → 容量 2；`false` → 容量 1；锚点对象不存在 / 组件缺失 / `roomId` 为空或不匹配 / 组件 `enabled=false` 或 GameObject 禁用 / 注册表引用失效 → **一律回退容量 1**。回退路径不得抛异常、不得 `Debug.LogError` 刷屏；允许一次性 `Debug.LogWarning`（Task 2 实现，静态标志位去重）。
- **每房一个世界锚点（规格 §1.2 不变式 2）**：不增删 `Anchor01`~`Anchor10`；`RoomTenantAvatarSlot.positionAnchor` 继续指向对应 `AnchorXX`（`RoomFloorRegistry.TryGetFloorForSlot` 依赖该引用，规格 §2.1）。
- **分配校验只发生在协调器层（规格 §1.2 不变式 5/9、§3.3）**：`TryAssign`/`TryMoveToEmptyRoom` 入口处做容量校验（`CanAssign`，即 `OccupantIds.Count < capacity`）；`StateReducer`/`RoomRunState`/存档**不加**任何容量字段，`AssignRoomChange` 保持「移除旧房 + 追加新房」纯语义。
- **拖动/落点（规格 §4.4）**：`RoomTenantSlotDragTrigger.EndDrag` 目标房间判定改用 `coordinator.CanAssign(targetRoomId)`（同房跳过逻辑不变），命中仍走 `RoomWorldHitArea` 并调 `TryMoveToEmptyRoom`；`TenantAvatarListItem.FinishDrag` **不改**（仍调 `TryAssign`，容量校验在协调器内部完成）。
- **多视图渲染（规格 §1.2 不变式 6、§5.2）**：视图 i ↔ `OccupantIds[i]`；无住客的视图隐藏内容（透明色，Image 保持 `enabled` 与交互表面，沿用现有策略）；已存在的越限数据（`OccupantIds.Count > 2`，理论仅旧数据/手工编辑）只渲染前 2 个视图、不裁剪存档。
- **视图数量由组件配置决定（规格 §5.1）**：`allowDoubleOccupancy=false` 或回退（容量 1）→ 显示 1 个视图；`true`（容量 2）→ 显示 2 个视图；数量**不**随入住人数动态增减。所有 10 个锚点默认 `allowDoubleOccupancy=true`，因此场景统一作者化为「1 面板 + 2 视图」；容量 1 时由 `RoomAvatarSlotLayoutController` 在运行时隐藏第 2 个视图。
- **布局规则（规格 §5.1/§5.3）**：视图尺寸 1:1（`Width = Height`），沿用现有 `screenSize` 固定屏幕尺寸语义（各面板 `screenSize=120`）；双人房容器宽度 = `2 × screenSize + spacing`（`spacing` 默认 8）；视图在面板内**MiddleCenter 对齐**（水平居中组、垂直居中，`y=0`），不依赖固定像素偏移以外的硬编码（偏移由控制器按容量对称计算）。
- **无运行时 Instantiate / 无 Prefab**：两个视图子节点在场景中作者化（Task 6 由 unitymaster 复制现有单视图结构为视图 0/1）；控制器只做 `SetActive`/`sizeDelta`/`anchoredPosition`，绝不 `Instantiate`。
- **旧存档兼容（规格 §7）**：`OccupantIds` 任意长度深拷贝/还原保持不变；单人间存档载入后第 2 个视图为空视图；双人入住存档往返后两视图均恢复。
- **无 git 操作**：本计划不含任何 bash/git 命令，不做任何提交（用户未请求提交）；所有任务以「评审门」步骤收尾。

---

## File Structure

| 文件 | 操作 | 职责 |
| --- | --- | --- |
| `Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs` | **创建** | 锚点房间属性组件：`roomId` + `allowDoubleOccupancy`（默认 true）；静态注册表按 `roomId` 查询；`TryGetCapacity` 回退 1 |
| `Assets/Scripts/Hotel/UI/RoomAvatarSlotLayoutController.cs` | **创建** | 房间面板布局控制器（挂在 `RoomAvatarSlotsPanel`）：按容量计算容器宽度、视图横向 MiddleCenter 定位、隐藏超容量视图 |
| `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs` | **修改** | `TryGetRoomCapacity`/`CanAssign`/`GetRoomOccupantIds`；`TryAssign`/`TryMoveToEmptyRoom` 占用判定改容量校验 |
| `Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs` | **修改** | 单图→多视图：`avatarViews` 绑定列表、`GetOccupantIdAt(index)`/`GetOccupantCount()`、容量感知 `Refresh`；移除拖拽触发器自动添加 |
| `Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs` | **修改** | `GetComponentInParent` 取槽、`slotIndex` 字段、落点判定改 `CanAssign` |
| `Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs` | **修改** | `slotIndex` 字段，按索引取入住者 |
| `Assets/Scenes/MainScene.unity` | **修改**（仅 unitymaster Inspector 操作） | 10 个 `AnchorXX` 加 `RoomAvatarProperty`；10 个 `RoomAvatarSlot_XX` 重建为「1 面板 + 2 视图」；`RoomAvatarSlotsPanel` 挂 `RoomAvatarSlotLayoutController` 并接线 |

## 现状关键事实（供各任务对齐）

- `RoomAvatarSlot_XX`（如 `RoomAvatarSlot_01`，fileID 1060406946）当前组件：RectTransform、`TenantInfoHoverTrigger`、`RoomTenantAvatarSlot`（`roomId`/`avatarImage`/`hoverTrigger`/`positionAnchor`/`screenSize=120`）、Image（透明交互面）、CanvasRenderer；子节点：`FlagRing`（`RoomAvatarFlagRing` + Image，localScale 1.32）、`AvatarMask`（Mask + Image）→ 子 `Avatar`（Image，raycastTarget=0，占位 sprite guid `5e04d55da7d4f714b907318c2dba612f`）。
- `RoomTenantSlotDragTrigger` 目前**不在场景中**（由 `RoomTenantAvatarSlot.Awake` 运行时 `AddComponent` 自动添加）。
- `RoomTenantAvatarSlot.GetOccupantId()` 消费方：`RoomTenantSlotDragTrigger`（OnPointerDown）、`RoomAvatarFlagRing`（Refresh / OnTenantFlagChanged）——本计划全部改为按 `slotIndex` 索引。
- `RoomFloorRegistry.TryGetFloorForSlot` 用 `slot.PositionAnchor`（= AnchorXX）做楼层映射；`EventEffectExecutor`/`EventConditionEvaluator` 用 `RoomTenantAvatarSlot.GetSlotsForRoom(roomId)`——每房间保持**一个**根槽组件即可，二者不受影响。
- `TenantAvatarLodController` 的 `targets`（avatarLayer）在场景中为空引用，与本计划无关，不触碰。
- 旧场景序列化字段 `avatarImage`/`hoverTrigger` 在 Task 3 脚本变更后会被 Unity 静默丢弃（无编译错误、无 Missing Script）；Task 6 重新接线 `avatarViews` 前，运行时视图为空（透明），属预期中间态。

---

### Task 1: 创建 RoomAvatarProperty 房间属性组件

**Files:**
- Create: `Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs`

**Interfaces:**
- Consumes: 无（本任务为全部后续任务的前置）。
- Produces: 组件 `RoomAvatarProperty`，序列化字段 `roomId`（string）、`allowDoubleOccupancy`（bool，默认 true）；静态查询 `RoomAvatarProperty.TryGetCapacity(string roomId, out int capacity)`（命中且匹配 → `allowDoubleOccupancy ? 2 : 1`；任何缺失/非法 → 回退 1 并返回 false）。Task 2 的 `TenantAssignmentCoordinator.TryGetRoomCapacity` 委托它；Task 6 将组件挂到 10 个 `AnchorXX`。

- [ ] **Step 1: 创建组件脚本**

在 Unity Project 窗口右键 `Assets/Scripts/Hotel/Presentation/Avatars` → Create → C# Script，命名 `RoomAvatarProperty`，内容**整体替换**为：

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-only room avatar configuration attached to each world anchor
/// (TenantAvatarAnchors/Anchor01..Anchor10). Carries no runtime state.
/// </summary>
public class RoomAvatarProperty : MonoBehaviour
{
    [SerializeField] private string roomId;
    [SerializeField] private bool allowDoubleOccupancy = true;

    private static readonly Dictionary<string, RoomAvatarProperty> Registry =
        new Dictionary<string, RoomAvatarProperty>();

    public string RoomId => roomId;
    public bool AllowDoubleOccupancy => allowDoubleOccupancy;

    private void OnEnable()
    {
        if (string.IsNullOrEmpty(roomId))
            return;
        Registry[roomId] = this;
    }

    private void OnDisable()
    {
        if (string.IsNullOrEmpty(roomId))
            return;
        if (Registry.TryGetValue(roomId, out RoomAvatarProperty current) && current == this)
            Registry.Remove(roomId);
    }

    /// <summary>
    /// Capacity for a room id: 2 when allowDoubleOccupancy, otherwise 1.
    /// Missing/invalid registration (no anchor, no component, empty/mismatched
    /// roomId, disabled component/GameObject) always falls back to 1.
    /// </summary>
    public static bool TryGetCapacity(string roomId, out int capacity)
    {
        capacity = 1;
        if (string.IsNullOrEmpty(roomId))
            return false;
        if (!Registry.TryGetValue(roomId, out RoomAvatarProperty property) || property == null)
            return false;
        if (!property.isActiveAndEnabled)
            return false;
        capacity = property.allowDoubleOccupancy ? 2 : 1;
        return true;
    }
}
```

- [ ] **Step 2: Unity 编译验证**

1. 返回 Unity，等待自动重新编译。Expected：Console **0 错误、0 警告**。
2. 在 Project 窗口选中该脚本，Inspector 顶部显示类名 `RoomAvatarProperty`；字段预览含 `roomId` 与 `allowDoubleOccupancy`（勾选状态，默认 true）。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs`（新建）。
- 检查点：字段名/默认值与 Task 6 接线逐字一致（`roomId`、`allowDoubleOccupancy` 默认 true）；注册表按 `roomId` 键控，`OnEnable`/`OnDisable` 成对注册/注销；`TryGetCapacity` 五种回退情形（规格 §6）全覆盖且返回 false + capacity=1；无抛异常/无 LogError。
- 通过标准：评审者确认后进入 Task 2；不通过则在本任务内修复后重新复核。

---

### Task 2: TenantAssignmentCoordinator 容量 API 与分配容量校验

**Files:**
- Modify: `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`（第 203 行 `TryAssign` 内、第 268 行 `TryMoveToEmptyRoom` 内、文件末尾 `IsRoomOccupied`/`GetRoomOccupantId` 之后）

**Interfaces:**
- Consumes: Task 1 的 `RoomAvatarProperty.TryGetCapacity(string roomId, out int capacity)`；既有 `_runState.Rooms[roomId].OccupantIds`、`AuthorizedChangeSet.Domain`、`AssignRoomChange`、`CommitResult`。
- Produces: `public bool TryGetRoomCapacity(string roomId, out int capacity)`（委托 `RoomAvatarProperty.TryGetCapacity`，回退 1，一次性 `Debug.LogWarning`）；`public bool CanAssign(string roomId)`（`OccupantIds.Count < capacity`）；`public IReadOnlyList<string> GetRoomOccupantIds(string roomId)`（不存在/无状态返回空列表）。Task 3 的 `RoomTenantAvatarSlot` 消费三者；Task 4 的 `RoomTenantSlotDragTrigger.EndDrag` 消费 `CanAssign`；Task 5 的 `RoomAvatarSlotLayoutController` 消费 `TryGetRoomCapacity`。

- [ ] **Step 1: 修改分配入口的占用判定**

用文本编辑器打开 `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`，做两处精确替换：

替换 1（`TryAssign` 内，原第 203 行）：
```csharp
        if (IsRoomOccupied(roomId))
            return false;
```
→
```csharp
        if (!CanAssign(roomId))
            return false;
```

替换 2（`TryMoveToEmptyRoom` 内，原第 268 行）：
```csharp
        if (IsRoomOccupied(targetRoomId))
            return false;
```
→
```csharp
        if (!CanAssign(targetRoomId))
            return false;
```

- [ ] **Step 2: 在类末尾新增容量 API**

在 `GetRoomOccupantId(string roomId)` 方法（当前文件最后）之后、类闭合大括号之前，追加以下方法，并在类体顶部（`IsDragging` 属性之后）追加静态告警标志位：

```csharp
    private static bool _warnedMissingRoomProperty;
```

```csharp
    public bool TryGetRoomCapacity(string roomId, out int capacity)
    {
        if (RoomAvatarProperty.TryGetCapacity(roomId, out capacity))
            return true;

        capacity = 1;
        if (!_warnedMissingRoomProperty)
        {
            _warnedMissingRoomProperty = true;
            Debug.LogWarning($"[TenantAssignmentCoordinator] RoomAvatarProperty missing or invalid for room '{roomId}'; falling back to capacity 1 (single occupancy).", this);
        }
        return false;
    }

    public bool CanAssign(string roomId)
    {
        if (_runState == null)
            return false;
        if (!_runState.Rooms.ContainsKey(roomId))
            return false;
        TryGetRoomCapacity(roomId, out int capacity);
        return _runState.Rooms[roomId].OccupantIds.Count < capacity;
    }

    public IReadOnlyList<string> GetRoomOccupantIds(string roomId)
    {
        if (_runState == null)
            return Array.Empty<string>();
        if (!_runState.Rooms.TryGetValue(roomId, out RoomRunState room))
            return Array.Empty<string>();
        return room.OccupantIds;
    }
```

（`using System;`、`using System.Collections.Generic;`、`using UnityEngine;` 均已在文件顶部，无需新增。）

- [ ] **Step 3: Unity 编译验证**

1. 返回 Unity，等待自动重新编译。Expected：Console **0 错误、0 新增警告**。
2. 全局搜索 `IsRoomOccupied(`：仅剩 `TenantAssignmentCoordinator.cs` 内的定义（第 334 行附近）——它作为「房间是否有人」查询保留，不再用于分配判定；`GetRoomOccupantId(` 仍保留为主住客（索引 0）查询，调用方 `RoomTenantSlotDragTrigger`/`RoomTenantAvatarSlot` 将在 Task 3/4 改造后不再依赖它作为落点判定。

- [ ] **Step 4: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`（修改）。
- 检查点：`TryAssign`/`TryMoveToEmptyRoom` 的占用判定已替换为 `!CanAssign(...)`，其余基础校验（状态/参数/存在性/未入住/非同房）逐行保留；`CanAssign` 语义 = `OccupantIds.Count < capacity`；`GetRoomOccupantIds` 返回读接口 `IReadOnlyList<string>`；回退告警一次性（静态标志位）；无任何 `Hotel.Runtime` 文件被触碰。
- 通过标准：评审者确认后进入 Task 3；不通过则在本任务内修复后重新复核。

---

### Task 3: RoomTenantAvatarSlot 多视图渲染（按索引取入住者）

**Files:**
- Modify: `Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs`（整文件替换为下述内容）

**Interfaces:**
- Consumes: Task 2 的 `TryGetRoomCapacity(string, out int)`、`GetRoomOccupantIds(string)`；既有 `TryGetTenantAvatar`/`TryGetTenantColor`/`AssignmentChanged`。
- Produces: 序列化 `List<AvatarView> avatarViews`（每个 `AvatarView` 含 `Image avatarImage` + `TenantInfoHoverTrigger hoverTrigger`）；`public string GetOccupantIdAt(int index)`（越界返回 null）、`public int GetOccupantCount()`、`GetOccupantId()`（= `GetOccupantIdAt(0)` 兼容别名）；`Refresh()` 对每个可见视图按索引填充、超容量视图透明隐藏；**移除** `Awake` 中的 `RoomTenantSlotDragTrigger` 自动添加（拖拽触发器改由 Task 6 场景接线到各视图）。Task 4 的 `RoomTenantSlotDragTrigger`/`RoomAvatarFlagRing` 消费 `GetOccupantIdAt`；Task 6 场景接线 `avatarViews`。

- [ ] **Step 1: 替换脚本文件**

用文本编辑器将 `Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs` 内容**整体替换**为：

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(20)]
public class RoomTenantAvatarSlot : MonoBehaviour
{
    [System.Serializable]
    public sealed class AvatarView
    {
        public Image avatarImage;
        public TenantInfoHoverTrigger hoverTrigger;
    }

    [SerializeField] private string roomId;
    [SerializeField] private List<AvatarView> avatarViews = new List<AvatarView>();
    [SerializeField] private Transform positionAnchor;
    [SerializeField, Min(1f)] private float screenSize = 120f;

    private static readonly List<RoomTenantAvatarSlot> AllSlots = new List<RoomTenantAvatarSlot>();

    private bool _isDragVisual;
    private Sprite _placeholderSprite;

    public string RoomId => roomId;

    public Transform PositionAnchor => positionAnchor;

    private void Awake()
    {
        if (avatarViews.Count > 0 && avatarViews[0] != null && avatarViews[0].avatarImage != null)
            _placeholderSprite = avatarViews[0].avatarImage.sprite;

        for (int i = 0; i < avatarViews.Count; i++)
        {
            AvatarView view = avatarViews[i];
            if (view == null || view.hoverTrigger == null)
                continue;

            int index = i;
            view.hoverTrigger.tenantIdProvider = () => GetOccupantIdAt(index);
            view.hoverTrigger.enableUiRightClick = true;
            view.hoverTrigger.source = TenantInfoPanel.DisplaySource.RoomSlot;
        }
    }

    private void OnEnable()
    {
        if (!AllSlots.Contains(this))
            AllSlots.Add(this);
        Subscribe();
        Refresh();
    }

    private void Start()
    {
        // If OnEnable ran before TenantAssignmentCoordinator.Awake,
        // re-subscribe so AssignmentChanged is still received.
        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        AllSlots.Remove(this);
        Unsubscribe();
    }

    private bool _subscribed;

    private void Subscribe()
    {
        if (_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
        {
            TenantAssignmentCoordinator.Instance.AssignmentChanged += Refresh;
            _subscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.AssignmentChanged -= Refresh;
        _subscribed = false;
    }

    private void LateUpdate()
    {
        TrackAnchorPosition();
        UpdateFixedScreenSize();
    }

    public string GetOccupantId()
    {
        return GetOccupantIdAt(0);
    }

    public string GetOccupantIdAt(int index)
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return null;
        IReadOnlyList<string> occupants = TenantAssignmentCoordinator.Instance.GetRoomOccupantIds(roomId);
        if (index < 0 || index >= occupants.Count)
            return null;
        return occupants[index];
    }

    public int GetOccupantCount()
    {
        if (TenantAssignmentCoordinator.Instance == null)
            return 0;
        return TenantAssignmentCoordinator.Instance.GetRoomOccupantIds(roomId).Count;
    }

    public void Refresh()
    {
        int capacity = 1;
        if (TenantAssignmentCoordinator.Instance != null)
            TenantAssignmentCoordinator.Instance.TryGetRoomCapacity(roomId, out capacity);

        for (int i = 0; i < avatarViews.Count; i++)
        {
            AvatarView view = avatarViews[i];
            if (view == null || view.avatarImage == null)
                continue;

            Image avatarImage = view.avatarImage;
            string occupantId = i < capacity ? GetOccupantIdAt(i) : null;
            bool occupied = !string.IsNullOrEmpty(occupantId);

            // The Image stays enabled at all times so the view remains a valid
            // UI drop target and pointer surface even when the room is empty.
            // "Hidden" is expressed via a transparent color, not SetActive(false).
            if (occupied && TenantAssignmentCoordinator.Instance.TryGetTenantAvatar(occupantId, out Sprite avatar))
            {
                avatarImage.sprite = avatar;
                avatarImage.color = Color.white;
                avatarImage.enabled = true;
            }
            else if (occupied && TenantAssignmentCoordinator.Instance.TryGetTenantColor(occupantId, out Color color))
            {
                avatarImage.sprite = _placeholderSprite;
                color.a = 1f;
                avatarImage.color = color;
                avatarImage.enabled = true;
            }
            else
            {
                avatarImage.sprite = _placeholderSprite;
                Color c = avatarImage.color;
                c.a = 0f;
                avatarImage.color = c;
                avatarImage.enabled = true;
            }

            if (_isDragVisual && occupied)
            {
                Color dragColor = avatarImage.color;
                dragColor.a *= 0.4f;
                avatarImage.color = dragColor;
            }
        }
    }

    public void SetDragVisual(bool active)
    {
        if (_isDragVisual == active)
            return;
        _isDragVisual = active;
        Refresh();
    }

    private void TrackAnchorPosition()
    {
        if (positionAnchor == null)
            return;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransform self = transform as RectTransform;
        if (canvasRect == null || self == null)
            return;

        Vector2 screenPoint = cam.WorldToScreenPoint(positionAnchor.position);
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, eventCamera, out Vector2 local))
        {
            self.anchoredPosition = local;
        }
    }

    private void UpdateFixedScreenSize()
    {
        RectTransform self = transform as RectTransform;
        if (self == null)
            return;

        float size = Mathf.Max(screenSize, 1f);
        self.sizeDelta = new Vector2(size, size);
    }

    public static IReadOnlyList<RoomTenantAvatarSlot> GetSlotsForRoom(string roomId)
    {
        List<RoomTenantAvatarSlot> result = new List<RoomTenantAvatarSlot>();
        for (int i = 0; i < AllSlots.Count; i++)
        {
            if (AllSlots[i] != null && AllSlots[i].roomId == roomId)
                result.Add(AllSlots[i]);
        }
        return result;
    }

    public static void RefreshAll()
    {
        for (int i = 0; i < AllSlots.Count; i++)
        {
            if (AllSlots[i] != null)
                AllSlots[i].Refresh();
        }
    }
}
```

- [ ] **Step 2: Unity 编译验证**

1. 返回 Unity，等待自动重新编译。Expected：Console **0 错误、0 新增警告**（旧场景中该组件的 `avatarImage`/`hoverTrigger` 序列化值被静默丢弃属预期，无 Missing Script）。
2. Inspector 中 `RoomTenantAvatarSlot` 组件字段变为：`roomId`、`avatarViews`（List，当前空）、`positionAnchor`、`screenSize`；`Avatar View` 折叠面板含 `avatarImage` 与 `hoverTrigger` 两个子字段。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs`（修改）。
- 检查点：`GetOccupantIdAt`/`GetOccupantCount`/`GetOccupantId`(别名) 语义正确（越界 null）；`Refresh` 按索引填充、超容量（`i >= capacity`）透明隐藏且 Image 保持 enabled；`positionAnchor`/`screenSize`/`TrackAnchorPosition`/`UpdateFixedScreenSize`/`GetSlotsForRoom`/`RefreshAll`/订阅逻辑逐行保留；拖拽触发器自动添加已移除；`_isDragVisual` 语义保持（房间级淡化）。
- 通过标准：评审者确认后进入 Task 4；不通过则在本任务内修复后重新复核。

---

### Task 4: RoomTenantSlotDragTrigger 与 RoomAvatarFlagRing 索引化 + CanAssign 落点

**Files:**
- Modify: `Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs`
- Modify: `Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs`

**Interfaces:**
- Consumes: Task 2 的 `CanAssign(string)`；Task 3 的 `RoomTenantAvatarSlot.GetOccupantIdAt(int)`/`GetOccupantId()`。
- Produces: `RoomTenantSlotDragTrigger` 新增序列化字段 `slotIndex`（int，默认 0），`_slot` 改为 `GetComponentInParent<RoomTenantAvatarSlot>()` 获取，`OnPointerDown` 用 `GetOccupantIdAt(slotIndex)`，`EndDrag` 落点判定用 `CanAssign`；`RoomAvatarFlagRing` 新增序列化字段 `slotIndex`（int，默认 0），`Refresh`/`OnTenantFlagChanged` 用 `GetOccupantIdAt(slotIndex)`。Task 6 场景接线为各视图设置 `slotIndex`（视图 0 → 0、视图 1 → 1）。

- [ ] **Step 1: 修改 RoomTenantSlotDragTrigger.cs**

用文本编辑器对 `Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs` 做以下 4 处精确修改：

修改 1（类字段区，`holdDuration` 之后新增）：
```csharp
    [SerializeField] private float holdDuration = 0.4f;
```
→
```csharp
    [SerializeField] private float holdDuration = 0.4f;
    [SerializeField] private int slotIndex;
```

修改 2（`Awake` 内，原第 20 行）：
```csharp
        _slot = GetComponent<RoomTenantAvatarSlot>();
```
→
```csharp
        _slot = GetComponentInParent<RoomTenantAvatarSlot>();
```

修改 3（`OnPointerDown` 内，原第 51 行）：
```csharp
        string occupantId = _slot.GetOccupantId();
```
→
```csharp
        string occupantId = _slot.GetOccupantIdAt(slotIndex);
```

修改 4（`EndDrag` 内，原第 134 行）：
```csharp
        if (!string.IsNullOrEmpty(coordinator.GetRoomOccupantId(targetRoomId)))
            return;
```
→
```csharp
        if (!coordinator.CanAssign(targetRoomId))
            return;
```

（同房跳过 `if (targetRoomId == _slot.RoomId) return;` 与 `coordinator.TryMoveToEmptyRoom(tenantId, targetRoomId);` 保持原样。）

- [ ] **Step 2: 修改 RoomAvatarFlagRing.cs**

用文本编辑器对 `Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs` 做以下 3 处精确修改：

修改 1（类字段区，`ringImage` 之后新增）：
```csharp
    [SerializeField] private Image ringImage;
```
→
```csharp
    [SerializeField] private Image ringImage;
    [SerializeField] private int slotIndex;
```

修改 2（`OnTenantFlagChanged` 内，原第 87 行）：
```csharp
        if (_slot != null && tenantId == _slot.GetOccupantId())
```
→
```csharp
        if (_slot != null && tenantId == _slot.GetOccupantIdAt(slotIndex))
```

修改 3（`Refresh` 内，原第 96 行）：
```csharp
        string occupantId = _slot != null ? _slot.GetOccupantId() : null;
```
→
```csharp
        string occupantId = _slot != null ? _slot.GetOccupantIdAt(slotIndex) : null;
```

- [ ] **Step 3: Unity 编译验证**

1. 返回 Unity，等待自动重新编译。Expected：Console **0 错误、0 新增警告**。
2. 全局搜索 `GetRoomOccupantId(`：仅剩 `TenantAssignmentCoordinator.cs` 定义（保留为「主住客（索引 0）」查询，规格 §4.3）；搜索 `.GetOccupantId()` 仅剩 `RoomTenantAvatarSlot.cs` 内的别名定义本身。

- [ ] **Step 4: 评审门**

将改动清单提交评审者复核：
- 改动文件：`Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs`、`Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs`（修改）。
- 检查点：拖拽触发器按视图 `slotIndex` 取入住者、目标房间判定已改 `CanAssign`、同房跳过与 `TryMoveToEmptyRoom` 不变；旗环按 `slotIndex` 刷新与响应标记变更；两文件其余逻辑（`Update` 长按、`StartDrag`/`ReleaseDrag`/`CleanupDrag`、`TenantInfoPanel.TenantFlagChanged` 订阅）逐行保留。
- 通过标准：评审者确认后进入 Task 5；不通过则在本任务内修复后重新复核。

---

### Task 5: 创建 RoomAvatarSlotLayoutController 布局控制器

**Files:**
- Create: `Assets/Scripts/Hotel/UI/RoomAvatarSlotLayoutController.cs`

**Interfaces:**
- Consumes: Task 2 的 `TryGetRoomCapacity(string, out int)`。
- Produces: 组件 `RoomAvatarSlotLayoutController`（`[DefaultExecutionOrder(21)]`，晚于 `RoomTenantAvatarSlot` 的 20，每帧在槽跟踪锚点后覆盖容器宽度与视图布局），序列化 `List<RoomSlotLayout> rooms`（每项：`roomId`、`panel` RectTransform、`views` List<RectTransform>、`screenSize`（默认 120）、`spacing`（默认 8））。Task 6 挂到 `RoomAvatarSlotsPanel` 并填充 10 项。

- [ ] **Step 1: 创建布局控制器脚本**

在 Unity Project 窗口右键 `Assets/Scripts/Hotel/UI` → Create → C# Script，命名 `RoomAvatarSlotLayoutController`，内容**整体替换**为：

```csharp
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(21)]
public class RoomAvatarSlotLayoutController : MonoBehaviour
{
    [System.Serializable]
    public sealed class RoomSlotLayout
    {
        public string roomId;
        public RectTransform panel;
        public List<RectTransform> views = new List<RectTransform>();
        [Min(1f)] public float screenSize = 120f;
        [Min(0f)] public float spacing = 8f;
    }

    [SerializeField] private List<RoomSlotLayout> rooms = new List<RoomSlotLayout>();

    private void LateUpdate()
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            RoomSlotLayout layout = rooms[i];
            if (layout == null || layout.panel == null || string.IsNullOrEmpty(layout.roomId))
                continue;

            int capacity = 1;
            if (TenantAssignmentCoordinator.Instance != null)
                TenantAssignmentCoordinator.Instance.TryGetRoomCapacity(layout.roomId, out capacity);

            float size = Mathf.Max(layout.screenSize, 1f);
            float step = size + layout.spacing;
            float width = capacity >= 2 ? size * 2f + layout.spacing : size;
            layout.panel.sizeDelta = new Vector2(width, size);

            for (int v = 0; v < layout.views.Count; v++)
            {
                RectTransform view = layout.views[v];
                if (view == null)
                    continue;

                bool visible = v < capacity;
                if (view.gameObject.activeSelf != visible)
                    view.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                view.sizeDelta = new Vector2(size, size);
                float offset = (v - (capacity - 1) * 0.5f) * step;
                view.anchoredPosition = new Vector2(offset, 0f);
            }
        }
    }
}
```

- [ ] **Step 2: Unity 编译验证**

1. 返回 Unity，等待自动重新编译。Expected：Console **0 错误、0 警告**。
2. 在 Project 窗口选中该脚本，Inspector 顶部显示类名 `RoomAvatarSlotLayoutController`；`Room Slot Layout` 折叠面板含 `roomId`/`panel`/`views`/`screenSize`（120）/`spacing`（8）字段。

- [ ] **Step 3: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scripts/Hotel/UI/RoomAvatarSlotLayoutController.cs`（新建）。
- 检查点：`DefaultExecutionOrder(21)` 晚于槽组件的 20（保证先跟踪锚点后布局）；容量 2 → 容器宽 `2×size+spacing`、视图偏移 `±(size+spacing)/2`（MiddleCenter）；容量 1 → 容器宽 `size`、视图偏移 0；超容量视图 `SetActive(false)` 且布局跳过；无 `Instantiate`/无 Prefab 引用。
- 通过标准：评审者确认后进入 Task 6；不通过则在本任务内修复后重新复核。

---

### Task 6: MainScene 场景接线（10 锚点 + 10 面板重建 + 布局控制器）

> **本任务仅由 unitymaster 子代理在 Unity 编辑器中执行**，只做「新增组件 + 层级重建 + 序列化引用接线」，禁止 UI 布局/美术/样式改动，完成后 Ctrl+S 保存场景。

**Files:**
- Modify: `Assets/Scenes/MainScene.unity`（`TenantAvatarAnchors/Anchor01`~`Anchor10`、`RoomAvatarSlotsPanel/RoomAvatarSlot_01`~`RoomAvatarSlot_10`、`RoomAvatarSlotsPanel`）

**Interfaces:**
- Consumes: Task 1 `RoomAvatarProperty`、Task 2 `TenantAssignmentCoordinator` 容量 API、Task 3 `RoomTenantAvatarSlot.avatarViews`/`GetOccupantIdAt`、Task 4 `slotIndex` 字段、Task 5 `RoomAvatarSlotLayoutController`。
- Produces: 场景接线后的完整功能：每房 2 个视图（默认容量 2）、锚点属性组件、布局控制器 10 项；供 Task 7 全量人工验收。

- [ ] **Step 1: 给 10 个 AnchorXX 添加 RoomAvatarProperty**

对 `TenantAvatarAnchors` 下的 `Anchor01`~`Anchor10`（共 10 个 Transform 子对象）逐个执行：

1. 在 Hierarchy 选中 `Anchor01` → Add Component → 搜索并选择 `RoomAvatarProperty`。
2. Inspector 设置 `Room Id` = `room_01`；`Allow Double Occupancy` **保持勾选（默认 true）**。
3. 依此类推：`Anchor02` → `room_02`、…、`Anchor10` → `room_10`。

- [ ] **Step 2: 重建 10 个 RoomAvatarSlot_XX 为「1 面板 + 2 视图」**

对 `RoomAvatarSlotsPanel` 下的 `RoomAvatarSlot_01`~`RoomAvatarSlot_10` 逐个执行（以 `RoomAvatarSlot_01` 为例；`RoomTenantAvatarSlot` 组件**保留在面板根**上）：

1. 选中面板根 `RoomAvatarSlot_01`，删除其 `TenantInfoHoverTrigger` 与 `Image` 组件（交互职责下移到视图节点；根保留 `RectTransform` + `RoomTenantAvatarSlot` + `CanvasRenderer`）。
2. 创建视图 0：右键 `RoomAvatarSlot_01` → UI → Image，命名 `AvatarView_01_0`。设置：
   - RectTransform：Anchor Min/Max = (0.5, 0.5)，Anchored Position = (0, 0)，Size Delta = (120, 120)。
   - Image：Color alpha = 0（透明交互面），Raycast Target = **勾选**。
   - Add Component → `TenantInfoHoverTrigger`：`hoverInfoPanel` 与 `pinnedInfoPanel` **在删除旧根组件前先记下其引用值**（旧根组件序列化值为 `hoverInfoPanel: {fileID: 1629988879}`、`pinnedInfoPanel: {fileID: 1677878090}`，即场景中 `InfoHoverPanel` 与 `InfoPinnedPanel` 对应对象），新组件沿用这两个引用；`hoverDelay` = 0.5、`hideDelay` = 0.15、`preferLeftPlacement` = 1、`enableUiRightClick` = 1、`source` = 0（RoomSlot）。
   - Add Component → `RoomTenantSlotDragTrigger`：`slotIndex` = **0**、`holdDuration` = 0.4。
   - 子节点 `FlagRing`：复制旧 `FlagRing`（Hierarchy 中拖动旧子节点到 `AvatarView_01_0` 下），其 `RoomAvatarFlagRing.slotIndex` = **0**（localScale 1.32、Image 引用保持）。
   - 子节点 `AvatarMask`：复制旧 `AvatarMask`（连同其子 `Avatar`）到 `AvatarView_01_0` 下；`AvatarMask` 的 Mask 组件与 `Avatar` 的 Image（占位 sprite、raycastTarget=0）保持原样。
3. 创建视图 1：选中 `AvatarView_01_0` → Ctrl+D 复制 → 改名 `AvatarView_01_1`。设置：
   - `RoomTenantSlotDragTrigger.slotIndex` = **1**。
   - `FlagRing` 上 `RoomAvatarFlagRing.slotIndex` = **1**。
4. 接线根组件的 `RoomTenantAvatarSlot`：
   - `avatarViews`（List Size = 2）：
     - Element 0：`avatarImage` = `AvatarView_01_0/AvatarMask/Avatar` 的 Image；`hoverTrigger` = `AvatarView_01_0` 的 `TenantInfoHoverTrigger`。
     - Element 1：`avatarImage` = `AvatarView_01_1/AvatarMask/Avatar` 的 Image；`hoverTrigger` = `AvatarView_01_1` 的 `TenantInfoHoverTrigger`。
   - `positionAnchor` = `TenantAvatarAnchors/Anchor01`（保持原引用不变）。
   - `screenSize` = 120（保持原值）。
5. 对 `RoomAvatarSlot_02`~`RoomAvatarSlot_10` 重复步骤 1–4（视图命名 `AvatarView_0X_0`/`AvatarView_0X_1`，`slotIndex` 分别 0/1，`positionAnchor` 指向对应 `Anchor0X`，`roomId` 已在场景中保持 `room_0X`）。

- [ ] **Step 3: 挂载并接线 RoomAvatarSlotLayoutController**

1. 选中 `RoomAvatarSlotsPanel` → Add Component → `RoomAvatarSlotLayoutController`。
2. `Rooms`（List Size = 10），逐项设置（`i` 从 1 到 10）：
   - `roomId` = `room_0X`（`room_01`…`room_10`）。
   - `panel` = `RoomAvatarSlot_0X` 的 RectTransform。
   - `views`（List Size = 2）：Element 0 = `AvatarView_0X_0` 的 RectTransform；Element 1 = `AvatarView_0X_1` 的 RectTransform。
   - `screenSize` = 120、`spacing` = 8（全部 10 项一致）。
3. Ctrl+S 保存场景。

- [ ] **Step 4: 编辑期验证**

1. 打开 MainScene。Expected：Console **0 错误、0 警告**；Hierarchy 无「Missing Script」标记；`TenantAvatarAnchors` 下 10 个锚点各含 `RoomAvatarProperty`；10 个面板根各含 `RoomTenantAvatarSlot`（`avatarViews` 各 2 项已接线）且各有 `AvatarView_0X_0`/`AvatarView_0X_1` 子节点（含 `RoomTenantSlotDragTrigger` 与 `RoomAvatarFlagRing`）。
2. 编辑模式 Game 视图：面板仍按原逻辑跟随锚点显示（此时 Play 前 `_runState` 为空，视图内容透明，交互表面存在）。

- [ ] **Step 5: 评审门**

将改动清单提交评审者复核：
- 改动文件：仅 `Assets/Scenes/MainScene.unity`（10 锚点加组件、10 面板重建、1 个布局控制器接线）。
- 检查点：未触碰任何其它对象（`CameraController`/`GamePhaseManager`/`SettlementBridge`/`RoomWorldHitArea`/LOD 等）；10 个锚点数量未增删；`RoomAvatarProperty` 的 `roomId` 与锚点一一对应且 `allowDoubleOccupancy` 默认勾选；面板根保留 `RoomTenantAvatarSlot`（`positionAnchor` 指向对应 `AnchorXX`）；每个视图 `slotIndex` 0/1 正确；布局控制器 10 项 `roomId`/`panel`/`views`/`screenSize`/`spacing` 全接线；无 Prefab、无运行时实例化脚本写入。
- 通过标准：评审者确认后进入 Task 7；不通过则在本任务内修复后重新复核。

---

### Task 7: Play 模式全量人工验收（规格 §8 十条）

**Files:**
- 只读验证，不修改任何文件。

**Interfaces:**
- Consumes: Task 1–6 全部成果。

- [ ] **Step 1: 环境与编译检查**

1. 打开 MainScene 进入 Play。Expected：Console **0 错误、0 警告**，无 `MissingReferenceException`、无空引用异常。
2. 通过正常评审流程招募 4 名以上住客（确保有足够候选人可分配），进入分配阶段。

- [ ] **Step 2: 验收 2+3 —— 双人房成功双住 / 第三人拒绝**

1. 任意双人房（默认全部为双人房）拖入住客 A：成功，`OccupantIds` 长度 1，视图 0 显示 A 头像/颜色、视图 1 透明。
2. 再拖入住客 B：成功，`OccupantIds` 长度 2，视图 0 显示 A、视图 1 显示 B（两视图并排、MiddleCenter 对齐、1:1 尺寸）。
3. 拖入第三名住客 C 到该房：**被拒**（`TryAssign` 返回 false，拖拽取消，`OccupantIds` 长度保持 2，无报错）。

- [ ] **Step 3: 验收 1 —— 单人间拒绝第二人（临时把一间房设为单人间）**

1. 退出 Play，编辑模式下把 `Anchor03` 的 `RoomAvatarProperty.allowDoubleOccupancy` **取消勾选**，保存；重新进入 Play。
2. `RoomAvatarSlot_03` 面板只显示 1 个视图（`AvatarView_03_1` 被隐藏）。
3. 拖入住客 D：成功，长度 1；再拖入住客 E：**被拒**，长度保持 1，`IsRoomOccupied("room_03")` 仍为 true。
4. 验收后退出 Play，把 `Anchor03` 的勾选**恢复**并保存。

- [ ] **Step 4: 验收 4 —— 跨房移动容量**

1. 双人房（1 人占用）中：把住客拖到单人间（占用中）→ **被拒**；拖到另一双人房（1 人占用）→ **成功并入**，目标房 `OccupantIds` 长度 2，两视图分别显示。
2. 把已占用单人间（1 人）的住客拖到双人房（已 2 人）→ **被拒**。

- [ ] **Step 5: 验收 5+9 —— 双人房拖动单住客 / hover 与拖动回归**

1. 双人房（A、B 入住）：长按视图 1（B）拖动到空房 → 成功后 `OccupantIds` 剩 A，视图 1 透明隐藏、视图 0 不变。
2. 双人房两视图各自 hover 显示对应住客信息（视图 0 hover 显示 A、视图 1 hover 显示 B）；右键各自可打开 pinned 面板；两视图各自可独立拖拽。

- [ ] **Step 6: 验收 6 —— 布局**

1. 单人间面板：1 个视图；双人房面板：2 个视图；两视图横向并排、MiddleCenter 对齐、尺寸 1:1（`Width = Height`）。
2. 移动/缩放相机：两视图位置实时跟随 `AnchorXX` 世界坐标（`TrackAnchorPosition` 生效），无漂移、无重叠。

- [ ] **Step 7: 验收 7 —— 保存/载入往返**

1. 双人入住（长度 2）→ 保存 → 载入存档：两名住客仍在对应房间，两视图均正常显示（视图索引与 `OccupantIds` 顺序一致）。

- [ ] **Step 8: 验收 8 —— 缺失属性回退**

1. 退出 Play，编辑模式下删除 `Anchor06` 的 `RoomAvatarProperty` 组件（或把其 `roomId` 改错为 `room_99`），保存；重新进入 Play。
2. `RoomAvatarSlot_06` 按容量 1 工作：仅 1 个视图，第二名住客拖入被拒；Console **无异常、无刷屏错误**（最多一次性 `Debug.LogWarning`）。
3. 验收后退出 Play，恢复 `Anchor06` 的 `RoomAvatarProperty`（`roomId` = `room_06`、`allowDoubleOccupancy` 勾选），保存。

- [ ] **Step 9: 验收 10 —— 旧存档回归**

1. 载入改动前保存的旧单人间存档：各房间行为与改动前一致（单人间显示 1 视图、入住/移动/保存正常），Console 无错误。

- [ ] **Step 10: 最终评审门（完整验证收尾）**

将最终验证结果提交评审者全量复核：
- 改动文件仅限：`Assets/Scripts/Hotel/Presentation/Avatars/RoomAvatarProperty.cs`（新建）、`Assets/Scripts/Hotel/UI/RoomAvatarSlotLayoutController.cs`（新建）、`Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`、`Assets/Scripts/Hotel/Presentation/Avatars/RoomTenantAvatarSlot.cs`、`Assets/Scripts/Hotel/UI/RoomTenantSlotDragTrigger.cs`、`Assets/Scripts/Hotel/UI/RoomAvatarFlagRing.cs`、`Assets/Scenes/MainScene.unity`（核对用，不做任何提交）。
- 检查点：规格 §8 十条验收全部通过；Console 0 错误；`Hotel.Runtime`（`RunModel.cs`/`RunSaveData.cs`/`StateReducer.cs`/`RunChanges.cs`）零改动、`OccupantIds` 与保存格式零改动；10 个锚点数量未增删、默认 `allowDoubleOccupancy=true`；无运行时 `Instantiate`、无 Prefab、无 asmdef/tests；未执行任何 git 操作。
- 通过标准：评审者确认后，本计划完成。

---

## Self-Review

- **规格覆盖**：§1.2 不变式 1（每锚点 `RoomAvatarProperty` + `allowDoubleOccupancy` 默认 true）→ Task 1 + Task 6 Step 1；不变式 2（每房一个世界锚点，不增删）→ Global Constraints + Task 6（锚点仅加组件）；不变式 3（1~2 视图横向、自动尺寸 1:1、MiddleCenter）→ Task 5 + Task 6 Step 2/3；不变式 4（容量由场景属性推导、`RoomRunState` 不加字段）→ Task 1 `TryGetCapacity` + Task 2；不变式 5（分配/移动前容量校验、失败走现有返回路径）→ Task 2 `CanAssign`/`TryAssign`/`TryMoveToEmptyRoom`；不变式 6（全部入住者渲染、按索引对应、空视图透明保留交互表面）→ Task 3 `Refresh` + 既有透明策略；不变式 7（保存/载入兼容）→ Global Constraints + Task 7 Step 7/9（`Hotel.Runtime` 零改动）；不变式 8（缺失/非法属性回退 1，无空引用）→ Task 1 `TryGetCapacity` 五种回退 + Task 2 一次性告警；不变式 9（`TryAssign`/`TryMoveToEmptyRoom` 判定改容量校验、Reducer 不变）→ Task 2 替换两处 `IsRoomOccupied` 判定；不变式 10（存档/状态/事件日志不加容量字段）→ Global Constraints + Task 2（容量仅查询场景注册表）。§2.2 各文件改动边界 → File Structure 表逐项对应。§3.2 `TryGetRoomCapacity` 统一查询 → Task 2；§3.3 容量不变式（单人间 ≤1、双人房 ≤2）→ Task 2 `CanAssign`。§4.1 数据源不变、`AssignRoomChange` 语义不变 → Task 2 未改 Reducer；§4.2 分配校验流程 → Task 2 Step 1/2；§4.3 `IsRoomOccupied` 保留、`CanAssign` 新增、`GetRoomOccupantId` 保留、`GetRoomOccupantIds` 新增 → Task 2；§4.4 拖放落点判定改 `CanAssign`、列表项不改 → Task 4 + Global Constraints。§5.1/§5.3 布局规则 → Task 5 + Task 6；§5.2 视图↔索引映射与 hover provider 注入 → Task 3（闭包 `() => GetOccupantIdAt(index)`）。§6 回退策略 → Task 1 + Task 2；§7 保存/载入兼容 → Global Constraints + Task 7；§8 十条验收 → Task 7 Step 2–9 逐条对应；§9 涉及文件表 → File Structure 表。无缺口。
- **占位符扫描**：全文无 TBD/TODO/「待定」/「类似 Task N」；每个代码步骤给出完整文件内容或精确替换对；所有数值（`screenSize=120`、`spacing=8`、`DefaultExecutionOrder(20)/(21)`、`holdDuration=0.4`、hoverDelay 0.5/hideDelay 0.15）均为具体值；场景操作为精确 Inspector 步骤。
- **接口与类型一致性**：`RoomAvatarProperty.TryGetCapacity(string, out int)` 在 Task 1 定义、Task 2 委托调用一致；`TryGetRoomCapacity`/`CanAssign`/`GetRoomOccupantIds`（返回 `IReadOnlyList<string>`）在 Task 2 定义、Task 3 `GetOccupantIdAt`/`Refresh`/`GetOccupantCount` 与 Task 4 `EndDrag`、Task 5 布局控制器消费一致；`GetOccupantIdAt(int index)` 在 Task 3 定义、Task 4 两文件消费一致；`slotIndex` 字段在 Task 4 两文件定义、Task 6 场景接线 0/1 一致；`avatarViews`/`AvatarView(avatarImage/hoverTrigger)` 在 Task 3 定义、Task 6 场景接线一致；`RoomAvatarSlotLayoutController` 字段（`rooms`/`roomId`/`panel`/`views`/`screenSize`/`spacing`）Task 5 定义、Task 6 接线一致；执行顺序 20 < 21 保证「先跟踪锚点后布局」。
- **范围声明复核**：本计划仅产出实现计划文档（本轮指令只要求写计划）；实施阶段执行方不得触碰 `Hotel.Runtime`、`RoomWorldHitArea`、`RoomFloorRegistry`、`TenantAvatarListItem`、`TenantAvatarDragTrigger`、`TenantDragOverlay`、`CameraController`、LOD；不得创建 asmdef/tests；`MainScene.unity` 仅由 unitymaster 按 Task 6 操作；不得执行 git 操作（用户未授权提交）。
