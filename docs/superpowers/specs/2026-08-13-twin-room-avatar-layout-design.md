# 双人房头像布局设计规格（2026-08-13）

> 状态：已批准设计。本文定义「双人房头像布局」（Twin Room Avatar Layout）的目标设计，是后续实现计划与实现的规格依据。
>
> **明确声明：本设计仅创建设计文档；不实现任何代码、不修改任何 Unity 场景/资产、不触碰 git 状态。核心机制：每个 `TenantAvatarAnchors/AnchorXX` 挂一个房间属性组件，含 `allowDoubleOccupancy` 复选框（默认 true）；每房一个世界锚点；`RoomAvatarSlotsPanel` 的房间 UI 横向排布 1~2 个头像视图（自动尺寸、MiddleCenter 对齐）；入住判定复用现有 `RoomRunState.OccupantIds`，容量 1/2，分配前做容量校验；所有入住者全部渲染；保存/载入保持兼容；缺失/非法锚点属性有明确回退行为。**

---

## 1. 目标与核心不变式

### 1.1 目标

让部分房间支持「双人入住」：玩家可以把两名住客拖入同一房间，房间 UI 与场景头像槽同时展示两名住客的头像。房间是否允许双人由场景中每个锚点上的房间属性组件单独配置，不进入存档数据、不改变现有运行时状态模型。

### 1.2 核心不变式（已确认）

1. 房间属性组件挂在 `TenantAvatarAnchors` 的每个 `AnchorXX` 子对象上（10 个锚点一一对应 10 个房间）；组件包含一个 `allowDoubleOccupancy` 复选框，**默认值为 true**。
2. 每个房间有且只有一个世界锚点（`Anchor01`~`Anchor10`，与 `room_01`~`room_10` 一一对应）；锚点同时是头像定位锚、LOD 目标与房间命中区的父子来源，本设计不增删锚点数量。
3. 房间 UI（`RoomAvatarSlotsPanel` 的 10 个子面板）为每个房间横向排布 1~2 个头像视图；单人间显示 1 个视图，双人房显示 2 个视图；视图自动适配尺寸（自动宽度，高度 1:1），面板内 MiddleCenter 对齐。
4. 入住数据继续使用现有 `RoomRunState.OccupantIds`（`List<string>`），容量为该属性决定：`allowDoubleOccupancy=false` → 容量 1，`true` → 容量 2；**容量由场景属性查询得出，不在 `RoomRunState` 新增字段。**
5. 分配/移动前做容量校验：`OccupantIds.Count >= 容量` 时拒绝；校验失败走现有失败返回路径（不抛异常、不修改状态）。
6. 房间内所有入住者全部渲染：每个住客一个头像视图；查询按 `OccupantIds` 顺序（索引 0、1）对应视图 1、2；无住客的视图隐藏（透明/不激活，保持可交互表面）。
7. 保存/载入兼容：`OccupantIds` 已完整序列化/深拷贝（`RunSaveData.CloneRoom`），旧存档单人间记录直接兼容；载入后按该房间当前属性决定是否展示第 2 个视图。
8. 锚点缺失/非法属性（锚点无组件、房间属性组件缺失、`roomId` 不匹配、属性组件引用失效）：一律按 `allowDoubleOccupancy=false`（容量 1）回退，绝不导致空引用或崩溃。
9. 分配流程中 `TryAssign`/`TryMoveToEmptyRoom` 的「已入住判定」由「非空即拒绝」改为「容量校验」；`AssignRoomChange` Reducer 不变（仍为追加 `OccupantIds`），容量校验只发生在协调器层。
10. 不在存档、运行状态、事件/日志结构中新增任何容量字段；属性只存在于场景组件上。

---

## 2. 场景结构现状与改动边界

### 2.1 现状（MainScene.unity）

- `TenantAvatarAnchors`（GameObject）下挂 10 个世界空间子对象：`Anchor01`~`Anchor10`（Transform），与 `room_01`~`room_10` 一一对应。
- 每个 `AnchorXX` 之下是房间命中区子对象（如 `Anchor01` 的子 Transform → `SpriteRenderer`），`RoomWorldHitArea` 的 `areas` 列表把 `roomId` 映射到这些命中区（`hitAreaSprite`）。
- `RoomAvatarSlotsPanel`（RectTransform）下挂 10 个子面板：`RoomAvatarSlot_01`~`RoomAvatarSlot_10`。
- 每个 `RoomAvatarSlot_XX` 目前挂：`TenantInfoHoverTrigger`、`RoomTenantAvatarSlot`（字段 `roomId`、`avatarImage`、`hoverTrigger`、`positionAnchor`、`screenSize`）、Image、CanvasRenderer；其下已有两个子节点：一个「光环/环」装饰节点（ring）与一个「蒙版 + 头像 Image」节点（avatarImage）。
- `RoomTenantAvatarSlot`：`GetOccupantId()` 目前只返回 `OccupantIds[0]`（首个入住者）；`Refresh()` 只填充一张头像图；`TrackAnchorPosition`/`UpdateFixedScreenSize` 负责跟随 `positionAnchor` 世界坐标定位与固定屏幕尺寸。
- `TenantAssignmentCoordinator`：`TryAssign` 用 `IsRoomOccupied(roomId)`（`OccupantIds.Count > 0` 即拒绝）；`GetRoomOccupantId` 只返回索引 0；`TryMoveToEmptyRoom` 同样用 `IsRoomOccupied` 拒绝非空房间。
- `RoomRunState.OccupantIds` 已是 `List<string>`；`StateReducer.AssignRoomChange` 把 `tenantId` 追加进目标房间列表（并先从旧房间移除）；`RunSaveData.CloneRoom` 深拷贝 `OccupantIds`。

### 2.2 改动边界（实现阶段范围，本设计不实施）

- 新增一个房间属性组件脚本（如 `RoomAvatarProperty`），挂到 10 个 `AnchorXX` 上；字段：`roomId`（串，默认填 `room_01`~`room_10`）、`allowDoubleOccupancy`（bool，默认 true）。组件只承载场景配置，不持有任何运行时状态。
- 新增/改造一个房间槽面板控制器（在 `RoomAvatarSlotsPanel` 层级），负责：按房间属性生成/布局 1~2 个头像视图（横向排列、自动尺寸、MiddleCenter 对齐），并把视图与 `OccupantIds` 索引对应。
- `RoomTenantAvatarSlot` 的渲染/查询逻辑从「单张图」改为「按索引取入住者」：提供 `GetOccupantIdAt(index)` 与 `GetOccupantCount()`；`Refresh` 时对每个可见视图分别填充。
- `TenantAssignmentCoordinator`：新增容量查询（读取锚点属性，回退容量 1）；`TryAssign`/`TryMoveToEmptyRoom` 的占用判定改为容量校验。
- `RoomTenantSlotDragTrigger` 与 `TenantAvatarListItem` 的拖放落点判定保持调用 `TryAssign`/`TryMoveToEmptyRoom`，不自行判满；落点房间命中仍走 `RoomWorldHitArea`。
- `MainScene.unity`：仅给 10 个 `AnchorXX` 添加房间属性组件并配置字段；`RoomAvatarSlotsPanel` 下 10 个子面板按双视图结构重建/接线（保留现有 `RoomTenantAvatarSlot`、hover、drag、Image 交互职责）。

---

## 3. 房间属性组件与容量规则

### 3.1 组件定义（设计约定）

- 组件名（实现时可调整，以下为准）：`RoomAvatarProperty`（房间头像属性）。
- 序列化字段：
  - `roomId`（string）：本锚点对应的房间号（`room_01`~`room_10`）。
  - `allowDoubleOccupancy`（bool）：是否允许双人入住，**默认 true**。
- 组件挂在 `AnchorXX` 上；一个锚点一个组件，一个房间一个锚点，因此「每房一个属性」。

### 3.2 容量推导（全项目统一）

- 查询函数：`TryGetRoomCapacity(string roomId, out int capacity)`。
- 规则：
  - 找到对应锚点的 `RoomAvatarProperty` 且 `roomId` 与查询值一致 → `capacity = allowDoubleOccupancy ? 2 : 1`。
  - 锚点对象不存在、组件缺失、`roomId` 不匹配、组件被禁用或引用失效 → 回退 `capacity = 1`（见 §6）。
- 查询结果用于：分配前置校验、UI 视图数量、拖放可用性提示（可选）。

### 3.3 容量不变式

- 单人间：`OccupantIds.Count <= 1` 恒成立。
- 双人房：`OccupantIds.Count <= 2` 恒成立。
- 校验在协调器层执行（`TryAssign`/`TryMoveToEmptyRoom` 入口）；Reducer/状态模型不做容量约束（保持现有纯追加语义），避免向存档结构引入容量字段。

---

## 4. 入住数据与分配流程

### 4.1 数据源（不变）

- 入住数据唯一来源：`RoomRunState.OccupantIds`（`List<string>`，顺序即入住顺序）。
- `StateReducer.AssignRoomChange` 语义不变：把 `tenantId` 从旧房间列表移除并追加到新房间列表。
- `RunSaveData.CloneRoom` 已深拷贝 `OccupantIds`，无需改动；旧存档（单人间）天然兼容。

### 4.2 分配校验（改造后）

`TryAssign(tenantId, roomId)` 流程：

1. 现有基础校验不变（状态非空、参数非空、tenant/room 存在、tenant 未入住）。
2. `TryGetRoomCapacity(roomId, out capacity)`（回退 1）。
3. `if (_runState.Rooms[roomId].OccupantIds.Count >= capacity) return false;`
4. 通过后构造 `AssignRoomChange` 提交（同现状）。

`TryMoveToEmptyRoom(tenantId, targetRoomId)` 流程：

1. 现有基础校验不变（状态非空、参数非空、tenant 已入住、目标房间存在、非同房）。
2. 同 §4.2 第 2~3 步做目标房间容量校验。
3. 通过后构造 `AssignRoomChange` 提交（同现状）。

### 4.3 已占用语义的兼容

- 现有 `IsRoomOccupied(roomId)`（`Count > 0`）保留，供「房间是否有人」类查询继续使用；新增 `CanAssign(roomId)`（`Count < capacity`）供分配判定使用。
- `GetRoomOccupantId(roomId)` 保留为「主住客（索引 0）」查询；新增 `GetRoomOccupantIds(roomId)` 返回整个列表供多视图渲染。

### 4.4 拖动/落点行为

- `RoomTenantSlotDragTrigger.EndDrag`：目标房间判定改为 `CanAssign(targetRoomId)`（不再是「无入住者」），同房跳过逻辑不变；命中 → `TryMoveToEmptyRoom`。
- `TenantAvatarListItem.FinishDrag`：不变，仍调 `TryAssign`；容量校验在协调器内部完成，未命中容量则维持取消。
- 双人房内点击任一视图拖动，仍以该视图对应的入住者 id 为数据源；两个视图各自独立可拖。

---

## 5. RoomAvatarSlotsPanel 房间 UI 布局

### 5.1 布局规则（已确认）

- 每个房间面板（`RoomAvatarSlot_XX`）内横向排布 **1~2 个头像视图**：
  - 房间属性 `allowDoubleOccupancy=false`（或回退，见 §6）→ 1 个视图。
  - 房间属性 `allowDoubleOccupancy=true` → 2 个视图。
- 视图数量由组件配置决定（固定），不随入住人数动态增减；空视图在无入住者时隐藏内容但保留交互表面（沿用现有「透明色隐藏」策略）。
- 自动尺寸：视图宽度与高度按 1:1 自适应（`Width = Height`），面板内可用高度决定视图尺寸；两视图时各占一半横向空间并保留间距。
- 对齐：面板内所有视图 **MiddleCenter 对齐**（水平居中对齐、垂直居中）；视图不依赖固定像素偏移。
- 每视图复用现有视觉结构：`avatarImage`（Image，显示头像/占位色）+ 光环（ring）+ 蒙版 + `TenantInfoHoverTrigger` + `RoomTenantSlotDragTrigger`。

### 5.2 视图与入住者索引映射

- 视图 0 ↔ `OccupantIds[0]`，视图 1 ↔ `OccupantIds[1]`。
- 面板控制器 `Refresh`：对每个视图取对应索引的入住者；有则填头像（优先 `AvatarKey` 解析，失败回退占位色），无则透明隐藏。
- `TenantInfoHoverTrigger.tenantIdProvider` 按视图注入：视图 i 返回 `OccupantIds[i]`（越界返回 null）。

### 5.3 场景接线范围（实现阶段）

- `RoomAvatarSlotsPanel` 下 10 个子面板重建为「1 个面板 + 2 个视图子节点」的统一结构；每个视图的 `positionAnchor` 仍指向对应 `AnchorXX`。
- 保留现有组件职责：`RoomTenantAvatarSlot` 定位/跟随/尺寸逻辑、`RoomTenantSlotDragTrigger` 拖拽、`TenantInfoHoverTrigger` 悬停信息、Image 交互表面。
- 双人房面板的固定屏幕尺寸沿用现有 `screenSize`（头像视图自身仍保持固定屏幕尺寸语义，两个视图并排后整体宽度为 2×尺寸 + 间距）。

---

## 6. 无效/缺失锚点属性行为

- 查询 `TryGetRoomCapacity` 时出现以下任一情形 → 回退 `capacity = 1`（单人间语义）：
  1. 目标 `roomId` 找不到对应 `AnchorXX`。
  2. `AnchorXX` 上无 `RoomAvatarProperty` 组件。
  3. 组件存在但 `roomId` 字段为空/与查询值不匹配。
  4. 组件被 `enabled=false` 或所在 GameObject 被禁用。
  5. 组件引用字段（如锚点 Transform 引用）已失效（`null`/`Destroyed`）。
- 回退策略统一：**不允许双人**；UI 显示 1 个视图；分配按容量 1 拒绝第二名住客。
- 回退路径不得抛异常、不得 `Debug.LogError` 刷屏；可用一次性 `Debug.LogWarning` 提示场景配置缺失（实现阶段可加）。
- 已存在的越限数据（理论仅旧数据/手工编辑导致 `OccupantIds.Count > 2`）：渲染时仅显示前 2 个视图对应住客，容量校验仍按当前属性；不主动裁剪存档。

---

## 7. 保存/载入兼容

- 不修改 `RoomRunState`、`RunSaveData`、`StateReducer` 的序列化结构；`OccupantIds` 已支持任意长度列表的深拷贝与还原。
- 旧存档兼容：单人间记录（`OccupantIds` 长度 ≤1）载入后行为与现在完全一致；若该房间场景属性为双人房，第 2 个视图保持空视图。
- 新存档：双人入住后 `OccupantIds` 长度为 2，保存/载入往返后两名住客均还原，UI 两个视图均恢复。
- 房间属性只存在于场景组件，不进入存档：同一存档在不同场景配置下，双人能力随场景属性变化（符合「配置在场景、数据在状态」的现有分层）。

---

## 8. 测试范围（实现阶段验收指引）

1. **单人间拒绝第二人**：`allowDoubleOccupancy=false` 房间，第二名住客拖入被拒（`TryAssign` 返回 false，`OccupantIds` 长度保持 1）。
2. **双人房成功双住**：`allowDoubleOccupancy=true` 房间，两名住客先后拖入均成功；`OccupantIds` 长度 2；两个视图分别显示两名住客头像/颜色。
3. **第三人拒绝**：双人房已满 2 人时第三名住客拖入被拒；`IsRoomOccupied` 仍为 true。
4. **跨房移动容量**：已入住者拖到单人间（占用中）被拒；拖到双人房（1 人）成功并入。
5. **双人房拖动单住客**：拖走视图 1 的住客后 `OccupantIds` 剩 1 人，视图 1 隐藏内容、视图 0 不变。
6. **布局**：单人间面板 1 个视图，双人房面板 2 个视图；均 MiddleCenter 对齐、自动尺寸 1:1；运行时视图位置跟随锚点。
7. **保存/载入往返**：双人入住 → 保存 → 载入 → 两名住客仍在且两个视图正常显示。
8. **缺失属性回退**：删除某锚点的 `RoomAvatarProperty`（或改错 `roomId`）→ 该房间按容量 1 工作，无异常；UI 显示 1 个视图。
9. **hover/拖动回归**：双人房两视图各自 hover 显示对应住客信息、各自可拖动、落点判定正确。
10. **旧存档回归**：载入旧单人间存档，各房间行为与改动前一致。

---

## 9. 涉及文件与改动边界（实现阶段参考，非本文档交付物）

| 文件 | 类型 | 改动 |
| --- | --- | --- |
| 新增房间属性组件脚本 | 新建 | `RoomAvatarProperty`：`roomId` + `allowDoubleOccupancy`（默认 true） |
| 房间槽面板控制器（含布局） | 新建/改造 | 1~2 视图横向布局、自动尺寸、MiddleCenter、按索引渲染 |
| `RoomTenantAvatarSlot.cs` | 修改 | 按索引查询入住者、多视图渲染、`positionAnchor` 沿用 |
| `TenantAssignmentCoordinator.cs` | 修改 | `TryGetRoomCapacity`、`CanAssign`、`TryAssign`/`TryMoveToEmptyRoom` 容量校验、`GetRoomOccupantIds` |
| `RoomTenantSlotDragTrigger.cs` | 修改 | 目标房间判定改用 `CanAssign` |
| `RunModel.cs` / `RunSaveData.cs` / `StateReducer.cs` | 不动 | 结构兼容，`OccupantIds` 语义不变 |
| `MainScene.unity` | 修改 | 10 个 `AnchorXX` 加属性组件；`RoomAvatarSlotsPanel` 子面板重建为双视图结构并接线 |

---

## 自审记录（Self-Review）

- **规格覆盖**：锚点房间属性组件与 `allowDoubleOccupancy` 默认 true（§1.2/§3）、每房一个世界锚点（§1.2/§2.1）、`RoomAvatarSlotsPanel` 1~2 视图横向 + 自动尺寸 + MiddleCenter（§5）、`OccupantIds` 与容量 1/2（§1.2/§3.2）、分配容量校验（§4.2）、所有入住者全部渲染（§1.2/§5.2）、保存/载入兼容（§7）、无效/缺失属性回退（§6）、测试与场景接线范围（§8/§9）均已成节。
- **一致性核对**：容量推导统一为「属性 true→2、false→1、缺失回退 1」，全文无第二套规则；视图索引与 `OccupantIds` 索引 0/1 对应在 §5.2 唯一；`IsRoomOccupied`（有人）与 `CanAssign`（有位）语义区分清晰，`TryAssign`/`TryMoveToEmptyRoom`/`RoomTenantSlotDragTrigger` 引用一致；明确 `RoomRunState`/存档/Reducer 不改。
- **占位符扫描**：无 TODO/TBD/「待定」；实现阶段的组件名与文件路径均标注「可调整/参考」。
- **范围声明**：本文仅产出设计文档；未修改任何源码、场景、资产或 git 状态。
