# 玩家日志（Player Log）系统设计规格（2026-08-09）

> 状态：已批准设计。本文档只定义设计规格，**不包含任何实现**；不新增源代码、Unity 资产/场景、测试或代码注释。后续实现计划与实现以本文档为唯一规格依据。
>
> **架构边界（已确认）**：新 `LogManager` 只负责「记录与查询」。它不订阅任何事件通道，不依赖也不被 EventManager / EventEffectManager / SettlementBridge / TenantReviewCoordinator / TenantAssignmentCoordinator / GamePhaseManager 反向依赖；上述系统仅在**自身状态事务提交成功后**调用 `LogManager.Record`。UI 只读、只查询。

---

## 1. 设计原则（已确认）

1. **面向玩家（player-facing）**：记录内容全部为玩家可见语言（中文），不含内部调试细节。
2. **每局永久（permanent per-run）**：日志随存档持久化，贯穿整局累积；新一局（新 `GameRunState`）从空日志开始。
3. **按时间线混合排序（chronological mixed timeline）**：事件、结算、招募、阶段推进等全部类别汇入同一条时间线，按发生顺序排列。
4. **分类标签（category tags）**：每条记录带 `PlayerLogCategory` 标签，UI 可按类别筛选。
5. **摘要卡（summary-only cards）**：每条记录是一张「标题 + 摘要文本」的摘要卡，不展示内部变更明细。

---

## 2. 相关现有文件与精确钩子（已确认）

| 记录方 | 文件 | 精确钩子（提交成功后） | 记录类别 |
| --- | --- | --- | --- |
| 事件/选择结算 | `Assets/Scripts/Hotel/Managers/EventManager.cs` | `TrySettleProcessedEvent`（第 394–402 行）中 `_effectManager.TrySettle(...) == EventSettleResult.Settled` 返回 true 处。该函数是 `OnEventProcessed`（第 334 行）与 `Update` 重试（第 354 行）的**共同成功汇点**，保证每起事件恰好记录一次 | `EventChoice` / `SpecialStory` |
| 效果结算汇总 | `Assets/Scripts/Hotel/Managers/EventEffectManager.cs` | `TrySettle`（第 11–62 行）**完整变更集**提交成功（第 46 行）后，汇总 `effects`/`changes`（即 `LogEffects` 第 149–191 行的既有输入）生成效果汇总数据，经 DTO 交给 EventManager 在事件卡之后落卡。降级 resolve-only 路径（第 49–53 行）**不**产出效果汇总 | `EffectSettlement` |
| Buff 每日结算/到期 | `Assets/Scripts/Hotel/Managers/EventEffectManager.cs` | `TickBuffs`（第 64–124 行）提交成功（第 122–123 行）后，为每个发生 tick 的 buff 记录一条（含到期移除） | `BuffTick` |
| 食物/资源结算与短缺 | `Assets/Scripts/Hotel/Managers/SettlementBridge.cs` | `ExecuteFoodSettlement`（第 121–181 行）提交成功（第 152 行）后；短缺 > 0 时在同一卡内体现 | `ResourceFood` |
| 阶段推进 | `Assets/Scripts/Hotel/Managers/SettlementBridge.cs` | `OnPhaseEntered`（第 89–119 行）空引用检查之后（第 95 行后）、食物结算（第 97–106 行）与 `TickBuffs`（第 113–114 行）**之前**，以事件载荷 `data.day`/`data.phase` 为权威值记录；`GamePhaseManager.AdvancePhase → NotifyPhaseEntered → onPhaseEntered.Raise`（第 85/131–146 行）为驱动源 | `PhaseTransition` |
| 租客招募 | `Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs` | `OnConfirm`（第 255–297 行）提交成功（第 282–283 行）后 | `TenantRecruit` |
| 租客拒绝 | `Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs` | `OnReject`（第 299–332 行）提交成功（第 320–321 行）后 | `TenantReject` |
| 房间分配/移动 | `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs` | `TryAssign`（第 144–179 行）与 `TryMoveToEmptyRoom`（第 181–220 行）提交成功（第 170/211 行）后 | `RoomAssignment` |

- 特殊故事判定：`EventManager._currentConfig.trigger.kind ∈ {SpecialVisitor, Personal, ChainStep}` → `SpecialStory`；其余事件（当前管线仅 `Normal` 可选）→ `EventChoice`。分支判定在同一个成功汇点内完成。
- 存档通道（`SaveGameService.TrySave`、`Runtime/SaveAndQuitFlow.cs`、`SettlementBridge` 第 116–118 行 Dawn 自动存档）**无需改动**：`PlayerLogs` 随 `GameRunState` 由既有 `RunSaveData/RunSaveCodec` 一并保存。

---

## 3. 条目 Schema 与类别枚举（Hotel.Runtime，`RunModel.cs`）

```csharp
public enum PlayerLogCategory
{
    EventChoice,       // 事件/选择结算
    SpecialStory,      // 特殊故事结算
    EffectSettlement,  // 效果结算汇总（每次结算一条）
    BuffTick,          // Buff 每日 tick/到期
    TenantRecruit,     // 租客招募
    TenantReject,      // 租客拒绝
    RoomAssignment,    // 房间分配/移动
    ResourceFood,      // 资源/食物结算与短缺
    PhaseTransition    // 阶段推进
}

[Serializable]
public sealed class PlayerLogEntry
{
    public int Sequence;            // 每局单调递增，LogManager 赋值（1 起）
    public int Day;                 // 记录时权威值（调用方提供）
    public HotelPhase Phase;        // 记录时权威值（调用方提供）
    public PlayerLogCategory Category;
    public string Title;            // 卡片标题（玩家可见语言）
    public string Summary;          // 摘要文本（玩家可见语言，可含 \n）
    public string DetailKey;        // 可选引用：eventId / tenantId / buffId / roomId
}
```

- 摘要文本示例：事件卡「选择『安抚他』：侵蚀 −3」；食物结算卡「第 2 天食物结算：消耗 3、短缺 1」；招募卡「招募 老周（初始侵蚀 15）」；分配卡「老周 → room_03」；阶段卡「第 2 天 · Dawn 开始」。
- 新增字段为纯展示数据，不进入任何 `RunChange` 校验，不影响 `StateVersion` 与确定性。

---

## 4. 记录契约（DTO-based Record）

`PlayerLogManager` 为 Hotel.Runtime 纯 C# 静态服务（`Assets/Scripts/Hotel/Runtime/State/` 下，无需 MonoBehaviour、无场景实例）：

```csharp
public readonly struct PlayerLogWriteDto
{
    public PlayerLogCategory Category;
    public int Day;                 // 由调用方给出权威值
    public HotelPhase Phase;
    public string Title;
    public string Summary;
    public string DetailKey;
}

// 记录：成功返回 true；state 为空 / Summary 为空 / 内部异常 → 返回 false，绝不抛出
public static bool Record(GameRunState state, PlayerLogWriteDto dto);

// 查询：返回只读视图，UI 无法改写日志
public static IPlayerLogQuery Query(GameRunState state);
```

- `Record` 内部完成：校验 → 赋 `Sequence = state.PlayerLogs.Count + 1` → 追加到 `state.PlayerLogs`。
- 调用方纪律：**只有** `CommitResult.Succeeded` 分支内才调用 `Record`；一次玩法事务至多触发一次记录。

---

## 5. 查询契约

```csharp
public interface IPlayerLogQuery
{
    int Count { get; }
    IReadOnlyList<PlayerLogEntry> All();                          // Sequence 升序 = 时间线顺序
    IReadOnlyList<PlayerLogEntry> ByDay(int day);                 // 按天过滤（降序）
    IReadOnlyList<PlayerLogEntry> ByCategory(PlayerLogCategory category);
    IReadOnlyList<PlayerLogEntry> Since(int lastSeenSequence);    // 增量刷新（UI 轮询用）
    PlayerLogEntry Get(int sequence);
}
```

---

## 6. 排序与幂等（已确认）

- **排序**：`Sequence` 自 1 起、跨存档加载延续（持久化计数），单调递增；时间线顺序 = Sequence 升序 = 记录追加顺序。各记录方承诺「事务提交成功后才 Record」，且同一阶段进入过程（SettlementBridge → 食物结算 → TickBuffs → 评审 → 事件）为同步调用链，追加顺序即玩法发生顺序。UI 展示按 Sequence 降序（最新在上）、按 Day 分组。
- **幂等**：记录调用点全部位于 `Succeeded` 分支内；事件结算经 `OnEventProcessed`/`Update` 共用 `TrySettleProcessedEvent` 单一成功汇点，天然去重。`LogManager` 不做内容去重（不比较文本），只保证每次 `Record` 产生一条带唯一 Sequence 的记录。
- **防呆**：`Record` 拒绝空 `state` 与空 `Summary`（返回 false 并 `Debug.LogWarning`），但不以此阻塞玩法主流程。

---

## 7. 失败处理（已确认）

- **玩法提交失败 → 无玩家日志**：`TryCommit` 返回 false 时对应记录方不调用 `Record`，即该玩法动作不产生任何日志条目。
- **不设待补记队列**：`LogManager` 不订阅、不缓存、不重试。「pending」语义仅存在于记录方内部——它们在提交成功后才发出记录，提交失败则丢弃，不做延迟补写。
- **记录所需信息在提交成功时已全部可得**（如 `EventEffectManager` 的 `effects`/`changes`、`TenantReviewCoordinator` 的 `candidate`/`initialErosion`），不存在「先占位后补全」需求。
- **记录失败不影响玩法**：`Record` 全程 try/catch，异常只记录警告并返回 false；`PlayerLogs` 追加失败绝不回滚玩法状态。

---

## 8. 持久化与迁移（已确认）

- `GameRunState.PlayerLogs`（`List<PlayerLogEntry>`）新增于 `RunModel.cs`，与 `AuditLog` 同模式。
- `RunSaveData.PlayerLogs`（`List<PlayerLogEntry>`）新增于 `RunSaveData.cs`；`RunSaveCodec.CreateSnapshot` 逐条克隆追加、`RestoreSnapshot` 还原（缺失 → 空列表）。
- **SchemaVersion 保持 1**：新增字段为可选、旧存档加载得到空日志（不回溯补写），与 `ReviewHistory`/`ResolvedReviewCandidateIds` 的演进方式一致。
- 日志**不**通过 `StateReducer`/`RunChange` 提交：不参与校验、不递增 `StateVersion`、不影响重放与确定性。

---

## 9. UI 集成范围（已确认）

- **只读**：UI 仅经 `IPlayerLogQuery` 查询，从不写入。
- 展示形态（后续 UI 布局阶段实施）：摘要卡 = 标题 + 摘要 + 分类标签 + 日/阶段；时间线按 Sequence 倒序、按 Day 分组；支持分类筛选（查询接口已具备）。
- 本规格**不含** UI 场景布局（面板预制体、接线、美术、样式），另行批准。
- 既有 UI（`PhaseUI`、`EventUI`、`NextPhasePanel`、`TenantReviewPanel`、`TenantAssignmentPanel`、`UIManager`）不改动。

---

## 10. 测试计划

EditMode NUnit 测试置于 `Assets/Tests/Hotel.Runtime.Tests`（引用 `Hotel.Runtime`，沿用 ARCHITECTURE.md 约定）；协调器/UI 钩子属 Assembly-CSharp，以 Unity 编译验证 + Play 模式人工验证为准（与既有计划约定一致）。

| 分组 | 覆盖点 |
| --- | --- |
| T1 序列化往返 | `PlayerLogEntry` 经 `RunSaveCodec` 往返字段一致；旧存档（无 `PlayerLogs` 字段）加载 → 空列表且不报错 |
| T2 记录/查询 | `Record` 后 Sequence 单调递增且自 1 起、跨加载延续；`All`/`ByDay`/`ByCategory`/`Since`/`Get` 过滤正确；查询返回只读视图，无法改写日志 |
| T3 失败与防呆 | 空 `state` → false 不抛出；空 `Summary` → 拒绝；单次成功事务对应单条记录（调用方纪律的可测部分） |
| T4 无侵入 | `Record` 不改变 `StateVersion`、`Tenants`/`Resources`/`Buffs` 数值；日志数量不影响任何 `StateReducer` 校验 |
| T5 钩子位置（编译+人工） | 事件/选择、特殊故事、效果汇总、Buff tick/到期、招募/拒绝、分配/移动、食物结算、阶段推进：提交成功后各产生且仅产生一条对应分类记录；失败路径（含降级 resolve-only）不产生记录 |

---

## 11. 明确非目标（Out of Scope）

- **本规格不实施任何源代码、Unity 资产/场景、测试**；实现需另行批准。
- UI 场景布局：时间线面板的预制体、接线、美术、样式。
- 修改既有系统行为与事件通道（只新增记录调用点）。
- 日志经 `StateReducer`/`RunChange` 提交，或改变 `StateVersion`/确定性。
- 替换既有 `Debug.Log` 与 `AuditLog` 系统（并行保留）。
- 日志参与玩法规则（不阻塞阶段推进、不影响结算、不参与事件资格）。
- 旧存档/已过去玩法的回溯补写。
- 多语言/本地化管线、聚合统计报表、撤回/撤销。

---

## 自审记录（Self-Review）

- **覆盖**：§1–§11 覆盖全部批准要素——player-facing（§1.1）、per-run 永久（§1.2/§8）、混合时间线（§1.3/§6）、分类标签（§1.4/§3）、摘要卡（§1.5）、全部玩家可见类别（§3 枚举覆盖：事件/选择、特殊故事、效果汇总、Buff tick/到期、招募/拒绝、分配/移动、资源/食物与短缺、阶段推进）、架构边界（顶部声明 + §2 钩子表 + §7）、DTO Record 与查询接口（§4/§5）、排序与幂等（§6）、失败处理（§7）、持久化 `GameRunState.PlayerLogs` + `RunSaveData/RunSaveCodec`（§8）、UI 只读范围（§9）、测试计划（§10）、显式非目标（§11）。
- **占位符扫描**：全文无 TODO/TBD/「待定」；所有未实施项均以「另行批准 / 后续 UI 布局阶段」的明确范围语句表达。
- **与既有代码对齐**：钩子位置逐一对齐已读源码行号（`EventManager.TrySettleProcessedEvent`、`EventEffectManager.TrySettle/TickBuffs`、`SettlementBridge.OnPhaseEntered/ExecuteFoodSettlement`、`TenantReviewCoordinator.OnConfirm/OnReject`、`TenantAssignmentCoordinator.TryAssign/TryMoveToEmptyRoom`）；`GameEvent<T>.Raise` 为同步调用链（`Core/Events/GameEventT.cs`），保证 §6 排序成立；存档演进与 `ReviewHistory` 加字段模式一致，SchemaVersion 不变。
