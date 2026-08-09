# 房间拖放命中区设计规格（2026-08-09）

> 状态：已批准设计。本文定义「房间拖放命中区」的目标设计，是后续实现计划与实现的规格依据。
>
> **明确声明：本设计仅调整组件/脚本职责与场景的组件字段/渲染配置；绝不修改、重置或移动 `MainScene` 中 9 个 `DetailBackground` 的 Transform position；不新增 Collider、PhysicsRaycaster、轮询或延时；不改变现有 UI 头像槽定位。**

---

## 1. 目标与职责变更

### 1.1 目标

把 `MainScene` 中 9 个 `DetailBackground` 从「LOD 控制的详情背景 Sprite」改造为「隐藏、手工可调大小的正方形世界空间房间命中区」，并把拖放落点判定从 UI 头像槽射线切换为世界坐标包含检测。

### 1.2 核心不变式（已确认）

1. 绝不修改、重置或移动 9 个 `DetailBackground` 的 Transform position；其世界位置关系由既有 HotelMap 层级自然保持。
2. 每个 `DetailBackground` 改为隐藏、手工可调大小的正方形世界空间房间命中区；尺寸可手工调整，大小跟随 HotelMap 层级缩放自然变化，保持世界关系。
3. 现有 LOD 仅控制 UI 头像层，不再修改 `DetailBackground` 的显隐或 localScale。
4. 拖放结束时用主相机把 `Input.mousePosition` 转为世界坐标，对每个隐藏方形命中区做包含检测；多区域意外重叠时选「鼠标点到区域中心距离最小」者。
5. 命中得到的 roomId 继续复用现有 `TryAssign` / `TryMoveToEmptyRoom` 及 Reducer 的空房/入住合法性校验；未命中则维持取消拖放。
6. 不使用 Collider、PhysicsRaycaster、轮询、延时，不改变现有 UI 头像槽定位。

---

## 2. 涉及文件与改动边界

### 2.1 TenantAvatarLodController.cs

- 现状：`TenantAvatarLodTarget.Apply` 对 `detailBackground` 执行 `SetActive(showBackground)` 与 `localScale = baseScale × multiplier`。
- 改动：移除 LOD 对 `detailBackground` 的显隐与 localScale 控制；LOD 只作用于 UI 头像层（如 `coloredCircle`）。脚本职责收敛为「纯 UI 头像层 LOD」。

### 2.2 RoomTenantAvatarSlot.cs

- 现状：房间槽是 UI 拖放目标、入住状态与拖拽数据来源；`TrackAnchorPosition`/`UpdateSizeForZoom` 负责头像槽定位与缩放。
- 改动：落点判定不再依赖头像槽的 UI 射线结果；房间槽保留入住状态与数据源职责，定位逻辑不变。命中区与房间通过既有房间标识（roomId，与槽内 `roomId` 同源）关联，供世界坐标查询使用。

### 2.3 TenantAvatarListItem.cs

- 现状：`FinishDrag` 用 `FindRoomSlotUnderPointer` 取目标槽，命中则 `TryAssign(_tenantId, slot.RoomId)`；无槽则取消。
- 改动：落点判定替换为「世界坐标命中区查询」；命中 → `TryAssign(_tenantId, 命中区 roomId)`；未命中 → 不调用，维持取消拖放。

### 2.4 RoomTenantSlotDragTrigger.cs

- 现状：`EndDrag` 用 `FindRoomSlotUnderPointer` 取目标槽，跳过同槽/已入住槽后调用 `TryMoveToEmptyRoom`。
- 改动：落点判定替换为世界坐标命中区查询；命中 → `TryMoveToEmptyRoom(tenantId, 命中区 roomId)`，空房/换房合法性继续由该方法与 Reducer 判定；未命中 → 维持取消。不再以头像槽射线为目标。

### 2.5 MainScene.unity

- 现状：9 个 `DetailBackground` 为世界空间 SpriteRenderer（正方形 size、sortingOrder −49、启用渲染），随 HotelMap 层级缩放。
- 改动（仅组件字段/渲染配置）：9 个对象改为隐藏渲染；尺寸保留可手工调整的正方形配置。**不移动 Transform position、不改变层级父子关系、不增删 GameObject。**

---

## 3. 拖放落点判定流程（拖放结束时一次性执行）

1. 用主相机（正交）把 `Input.mousePosition` 转为世界坐标（`ScreenToWorldPoint`）。
2. 遍历 9 个隐藏方形命中区做包含检测：以世界空间 Bounds（由 SpriteRenderer size × 层级变换推导）或等价 Transform 世界尺度判断点是否在区内；HotelMap 层级缩放自然反映在结果中。
3. 命中 0 个 → 取消拖放（走现有取消路径）。
4. 命中 1 个 → 采用该区对应 roomId。
5. 命中多个（意外重叠）→ 取「鼠标点到区域世界中心距离最小」者。
6. 用 roomId 走既有 `TryAssign`（列表项拖放）或 `TryMoveToEmptyRoom`（房间槽拖放）；空房、已入住、租客现状等合法性校验全部留在既有入口与 Reducer，命中区只提供 roomId。

---

## 4. 验证标准

1. 9 个区域均不渲染：运行时无任何 Sprite 渲染，画面不出现背景方块。
2. 手工调整任一区域面积后，拖入该区域能命中对应房间（roomId 正确）。
3. 拖放到区域外 → 取消拖放（不触发 `TryAssign`/`TryMoveToEmptyRoom`）。
4. UI 头像 LOD 照常：随缩放显隐/缩放仅作用于 UI 头像层，与命中区互不影响。
5. 意外重叠区域 → 按「最近中心」命中。
6. 读档与实时安排均不回归：落点判定只替换 roomId 来源，不触碰既有协调器/Reducer 校验与提交路径。

---

## 自审记录（Self-Review）

- **规格覆盖**：6 项已确认需求逐一成节（§1.2），5 个既有文件逐一说明改动边界（§2），场景修改限定组件字段/渲染配置并显式声明不移动 Transform。
- **占位符扫描**：无 TODO/TBD/「待定」。
- **与既有代码对齐**：`TryAssign` 与 `TryMoveToEmptyRoom` 均内置租客存在、房间存在、`IsRoomOccupied` 与换房条件校验（`TenantAssignmentCoordinator.cs`），命中区仅提供 roomId，合法性校验归属不变；现状引用基于 `TenantAvatarLodController.cs`、`TenantAvatarListItem.FinishDrag`、`RoomTenantSlotDragTrigger.EndDrag` 与 `MainScene.unity` 中 9 处 `DetailBackground`（正方形 SpriteRenderer、随 HotelMap 层级缩放）。
