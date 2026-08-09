# 租客工作系统设计规格（2026-08-09）

> 状态：已批准设计。本文档描述第一阶段（运行时基础）的目标设计，是后续实现计划与实现的唯一规格依据。
>
> **明确声明：第一阶段实施范围不包含「UI 场景布局修改」与「事件/候选资产迁移」；任何涉及场景布局（新预制体、场景接线、美术、样式）或事件资产（EventConfig 条目、TenantReviewCandidateSO 能力值、Carpenter 移除）的改动，必须另行单独批准后才可实施。**

---

## 1. 范围与非目标

### 范围

第一阶段（运行时基础）覆盖：

1. 内核数据模型与配置资产：`JobDefinition`、`TeamComboDefinition`、`WorkCatalog`。
2. `TenantRunState.JobId` 的语义落地：职业分配的运行时真相，可随时修改，仅影响下一次结算。
3. Day/Night 半日工作结算：结算账本（exactly-once）、完整授权变更集、经 `StateReducer` 原子提交。
4. 事件管线拦截：夜间个人损失 / 同楼层扩散 / 整楼扩散三类正面（恶化方向）侵蚀效果的缓解。
5. 资源与指标：现有 `food`/`currency` 之外新增 `ingredients`/`resources` 两类资源，以及单一全局设施耐久值。
6. 团队组合的动态推导与效果应用（不持久化）。
7. 确定性探索种子（含成功结算序号）。
8. 持久化与迁移（存档 SchemaVersion 1 → 2、缺失资源默认行为、载入后不重复生产）。
9. 只读展示与职业分配 UI 逻辑（挂载于既有 pinned 信息面板，不新建场景布局）。
10. 错误处理、状态不变量与 NUnit 测试矩阵。

### 非目标（第一阶段明确不做，除非另行批准）

- UI 场景布局：职业分配面板的预制体、场景接线、美术与样式；资源/设施/组合展示区域的场景布局。
- 事件资产迁移：EVENTS.md 中 N/D 事件目录条目接入新系统与拦截语义；`TenantAbility.Carpenter`（木工）的移除与相关候选/事件资产的迁移。
- `ApplyBuff`（BuffRunState 的逐 tick 侵蚀）的缓解拦截（阶段 2 再评估）。
- 资源损失缓解职业：阶段 1 没有任何职业缓解资源损失；缓解机制仅预留方向规则。
- 天气、探索线索、楼层阻断、完全体伪人、驱逐等尚未实现的系统。
- 平衡数值定稿：除标注「已确认」的数值外，本文所有数值均为可编写配置字段的初始默认值，可在资产中调整。

---

## 2. 已确认规则

1. **能力标签（9 个固有标签 + 无标签）**：`Doctor`（医生）、`Cook`（厨师）、`Engineer`（工程师）、`NightWatch`（守夜人）、`FormerEmployee`（前员工）、`Merchant`（商贩）、`Farmer`（农民）、`Driver`（司机）、`Teacher`（教师）、`None`（无标签）。
2. **能力是招募即得的固有属性**：由 `TenantReviewCandidateSO.ability` 按租客 `DefinitionId` 查得，不存入运行状态（`TenantRunState` 不新增能力字段）。事件选项资格（`ChoiceOption.requiredTags` / `GamePopupEvent.choiceRequiredTags`，见 `EventUI.GetOwnedAbilities`）继续按固有标签判定，与职业分配完全无关；改职业不改变事件资格。
3. **职业（10 个）**：`cooking`（烹饪）、`medical`（医疗）、`repair`（维修）、`watch`（守夜）、`patrol`（巡逻）、`trade`（交易）、`farming`（农耕）、`exploration`（探索）、`organizing`（整理）、`chores`（杂务）。
4. **无标签租客只能分配 `chores`**（杂务）；带标签租客按 `JobDefinition.allowedTags` 配置判定可分配职业（第一阶段初始配置为一一对应）。
5. **职业可随时修改**：不阻塞阶段推进（与房间分配的 `HasUnassignedTenants` 阻塞无关），同一阶段内修改次数不限；修改只影响**下一次结算**（见 §4 快照机制）。
6. **结算时段语义**：`DayActive` 仅在 Day 执行、`NightActive` 仅在 Night 执行、`AllDay` 两者均执行；`watch` 仅夜间。

---

## 3. 数据驱动设计

### 3.1 配置资产（ScriptableObject，`Hotel.Authoring`）

**`JobDefinition`**（一个职业一项，字段含）：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `jobId` | string | 唯一标识（上表 10 个 id） |
| `displayName` | string | 显示名 |
| `activityWindow` | TenantActivityType | 复用既有枚举：`DayActive`/`NightActive`/`AllDay` |
| `allowedTags` | List\<TenantAbility\> | 可分配该职业的能力标签；空列表 = 仅无标签（`None`） |
| 产出/消耗数值字段 | 见 §3.2 | 各职业专属的可配置数值 |

**`TeamComboDefinition`**（一个团队一项）：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `comboId` | string | 唯一标识：`medical_team` / `security_team` / `logistics_team` |
| `displayName` | string | 显示名 |
| `roles` | List\<TeamRole\> | `TeamRole { TenantAbility tag; string jobId }`，即「某标签必须被分配到某职业」 |
| `effects` | List\<TeamEffect\> | `TeamEffect { TeamEffectKind kind; float value }`；`TeamEffectKind ∈ { NightLossMitigationOverride, HealPercentBonus, OutputMultiplier }` |

**`WorkCatalog`**（场景组合根的资产目录）：`List<JobDefinition> jobs`、`List<TeamComboDefinition> teams`；运行时唯一入口。目录中的数值在实现时按 §3.2 初始值创建资产，可在资产中调整。

### 3.2 可配置数值字段（初始默认值）

除标注「已确认」外均为可调初始值：

| 字段（所在资产） | 默认 | 说明 |
| --- | --- | --- |
| `watchNightLossMitigationPercent`（watch） | **40（已确认）** | 守夜对夜间个人侵蚀损失的缓解百分比 |
| `repairCostCurrency`（repair） | 2 | 维修每结算消耗货币 |
| `repairRestoreDurability`（repair） | 10 | 维修每结算恢复耐久（上限 100） |
| `ingredientCostPerSettlement`（cooking） | 1 | 烹饪每结算消耗食材 |
| `foodPerIngredient`（cooking） | 2 | 每消耗 1 食材产出食物 |
| `tradeCostCurrency`（trade） | 2 | 交易每结算消耗货币 |
| `tradeOutputResources`（trade） | 1 | 交易每结算产出物资 |
| `farmOutputIngredients`（farming） | 2 | 农耕每结算产出食材 |
| `explorationMin` / `explorationMax`（exploration） | 1 / 3 | 探索产出区间（含端点，种子决定） |
| `healPercentPerSettlement`（medical） | 2 | 医疗每结算对每位已分配租客的治疗百分比 |
| `floorSpreadReductionPerPatrol`（patrol） | 25 | 每个巡逻租客对同楼层扩散的削减百分比 |
| `floorSpreadReductionCap`（patrol） | 75 | 同楼层削减累加上限 |
| `buildingSpreadReductionPerOrganizer`（organizing） | 20 | 每个整理租客对整楼扩散的削减百分比 |
| `buildingSpreadReductionCap`（organizing） | 60 | 整楼削减累加上限 |
| `medical_team.effects[HealPercentBonus]` | 2 | 医疗队激活时每个医疗租客的治疗百分比加成 |
| `security_team.effects[NightLossMitigationOverride]` | **60（已确认）** | 安保队激活时守夜缓解**覆盖**为 60%，取代而非叠加 40% |
| `logistics_team.effects[OutputMultiplier]` | 1.5 | 物流队激活时 trade/farming/exploration 产出倍率 |

### 3.3 运行时真相与动态推导

- `TenantRunState.JobId`（string，已存在于 `RunModel.cs` 并已随存档克隆）是**唯一分配真相**：`null`/空串 = 未分配，不参与任何结算。
- 能力由候选资产查得（§2.2），不新增状态字段。
- **活跃团队动态推导、不保存**：每次进入 Day/Night 阶段时，由当前 `JobId` 快照对照 `WorkCatalog.teams` 计算激活团队；载入后重新推导结果一致。
- 配置资产不进入存档（与 `EventConfig` 同性质）。

---

## 4. 半日执行与结算账本

### 4.1 阶段进入时结算

`WorkSettlementCoordinator`（MonoBehaviour，`Managers`，监听 `PhaseEnteredEvent`）在进入 **Day** 与 **Night** 时执行工作结算；`Dawn`/`Dusk` 不执行。

### 4.2 半日快照（冻结）

进入 Day/Night 的瞬间生成 `WorkSnapshot`：

- 参与对象：`RoomId` 非空（已分配房间）且 `JobId` 非空的所有租客。
- 内容：每名参与租客的 `tenantId`、`jobId`、`ability`（由候选 SO 按 `DefinitionId` 查得）。
- 快照同时作为本半日内**事件效果拦截**的依据（§10）。
- 快照在下一阶段进入前保持不变；因此本半日内修改职业，只影响**下一个** Day/Night 结算——与「职业可随时改、只影响下一次结算」一致。

### 4.3 结算账本（exactly-once）

- 状态新增（`GameRunState`）：`WorkSettlements: Dictionary<string, WorkSettlementRecord>`，key = `"{day}|{phase}"`（例：`"3|Night"`）；`WorkSettlementSequence: int`（最近一次成功结算序号，0 起）。
- `WorkSettlementRecord { int Day; HotelPhase Phase; int Sequence; }`，其中 `Phase` 仅允许 `Day` 或 `Night`。
- 执行前检查：key 已存在 → 跳过（幂等）。该检查**持久化生效**：载入中途阶段不会重算已结算半日，故不会重复生产。
- 执行时生成**完整授权变更集**（一次 `TryCommit` 原子提交）：

```
AuthorizedChangeSet.Domain(runId, version, "WorkSettlementCoordinator", $"WorkSettlement|{day}|{phase}")
  + 各职业产出/消耗：AdjustResourceChange、AdjustFacilityDurabilityChange（维修）
  + 医疗治疗：AdjustTenantErosionChange ×n
  + AddWorkSettlementChange(record)          // 账本写入 + 序号递增，同一变更集
  + AppendAuditLogChange("[WorkSettlement] Day {day} {phase}: produced…, consumed…")
```

- 提交失败 → 全部变更（含账本写入）不生效，序号不递增；协调器记 `AuditLog` 并保留重试能力。

### 4.4 与既有食物结算的关系

现有 `SettlementBridge.ExecuteFoodSettlement`（Night→Dawn 跨界）保持不动；工作结算（Day/Night 进入时）与之时点不同、变更集不同，互不冲突。烹饪在 Day 结算产出的食物，于下一次 Dawn 跨界食物结算中被消耗。

### 4.5 各职业结算效果

| 职业 | 时段 | 结算效果（每名该职业的参与租客） |
| --- | --- | --- |
| cooking | Day | 消耗 `min(1, 食材存量)` 食材，产出食物 `= 消耗量 × 2`（存量不足则部分产出） |
| medical | Day+Night | 对全楼已分配租客逐个治疗（§6.3） |
| repair | Day | 若 `FacilityDurability < 100`：消耗 2 货币，恢复 10 耐久（上限 100）；货币不足则跳过 |
| watch | Night | 无产出；效果通过事件拦截体现（§6/§10） |
| patrol | Night | 无产出；效果通过事件拦截体现 |
| trade | Day | 消耗 2 货币，产出 `floor(1 × OutputMultiplier)` 物资；货币不足则跳过 |
| farming | Day | 产出 `floor(2 × OutputMultiplier)` 食材 |
| exploration | Day | 产出 `floor(探索产出 × OutputMultiplier)` 物资，探索产出由确定性种子决定（§8） |
| organizing | Day+Night | 无产出；效果通过事件拦截体现 |
| chores | Day+Night | 无产出、无消耗 |

---

## 5. 资源与指标

- 资源 id：`food`（食物）、`currency`（货币）、`ingredients`（食材，新增）、`resources`（物资，新增；为避免与通用术语混淆，UI 显示为「物资」）。
- 资源仍为 `ResourceRunState`（`ResourceId`/`DefinitionId`/`Amount`），继续由 `ResourceDefinition` 定义初始值；`AdjustResourceChange` 既有 clamp（≥0）不变。
- **全局设施耐久**：`GameRunState.FacilityDurability`（float，刻度 [0,100]，初始 100；100 为内核常量刻度，与事件文本「设施耐久-30%」的百分比刻度一致）。新增变更 `AdjustFacilityDurabilityChange(float delta)`，应用时 clamp [0,100]。第一阶段仅由 `repair` 职业恢复；事件对耐久的破坏效果属阶段 2（当前 `EventEffect` 无耐久效果类型）。

---

## 6. 百分比效果与缓解

### 6.1 方向规则（已确认）

**只有「恶化方向」的效果会被缓解**：侵蚀增加（delta > 0）与资源减少（delta < 0）才可被缓解；反向效果（治疗、资源增加）一律不缓解。阶段 1 无职业缓解资源损失（机制预留，遵循同一方向规则）。

### 6.2 缓解对照表（侵蚀）

| 效果目标（EffectTarget） | 缓解来源 | 生效时段 | 叠加规则 |
| --- | --- | --- | --- |
| `OwnerTenant`（夜间个人损失） | watch | Night | **取档位**：任一 watch 租客 → 40%（已确认）；安保队激活 → 60% 覆盖（已确认，**取代而非叠加**，多守夜不叠加） |
| `SameFloorTenants`（同楼层扩散） | patrol | Night | 每激活 patrol 租客 −25%，累加，上限 75% |
| `AllAssignedTenants`（整楼扩散） | organizing | Day+Night | 每激活 organizing 租客 −20%，累加，上限 60% |
| `SameRoomOtherTenants` / `ByPlayerFlag` / `RandomAssignedTenants` | 无 | — | 不缓解 |

- 缓解计算：`delta' = delta × (1 − 缓解百分比/100)`，保留浮点，最终由 `StateReducer` clamp 到 [0,100]。
- 缓解仅当来源职业在「当前事件阶段」激活（`activityWindow` 匹配）时生效，与快照一致。

### 6.3 医疗百分比治疗

每个激活的 medical 租客对每位已分配房间租客：

```
healPercent = healPercentPerSettlement(2) + (医疗队激活 ? HealPercentBonus(2) : 0)
heal = floor(当前侵蚀 × healPercent / 100)   // 向下取整，保守
侵蚀 = clamp(侵蚀 − 各医疗租客 heal 之和, 0, 100)
```

多个 medical 租客的治疗相加；结果被侵蚀下限 0 约束，无需额外上限。

---

## 7. 团队组合

### 7.1 定义

| comboId | 显示名 | 角色（tag → job） | 效果（初始默认） |
| --- | --- | --- | --- |
| `medical_team` | 医疗队 | `Doctor→medical`、`Cook→cooking` | `HealPercentBonus = 2` |
| `security_team` | 安保队 | `NightWatch→watch`、`FormerEmployee→patrol` | `NightLossMitigationOverride = 60`（已确认） |
| `logistics_team` | 物流队 | `Merchant→trade`、`Farmer→farming`、`Driver→exploration` | `OutputMultiplier = 1.5` |

### 7.2 激活规则

团队激活 ⇔ 其**每个** role 都存在至少一名租客「能力 == role.tag 且 `JobId == role.jobId`」。缺任一 role 即不激活。激活结果在阶段进入时随快照动态推导，**不写入状态、不保存**。

### 7.3 效果语义

- 医疗队：把每个 medical 租客的治疗百分比加上 `HealPercentBonus`。
- 安保队：把守夜缓解档位从 40% 覆盖为 60%（取代，不叠加；安保队激活必然含 watch 租客，故档位始终存在）。
- 物流队：trade/farming/exploration 产出乘 `OutputMultiplier`，资源产出向下取整。

---

## 8. 确定性探索种子

探索产出在 Day 结算时为每名 exploration 租客计算，公式：

```
StableHash(string s):  h = 17; 逐字符 h = h*31 + (int)c; 返回 h
DeriveExplorationSeed(runSeed, day, phase, tenantId, jobId, sequence):
  h = 17
  h = h*31 + runSeed
  h = h*31 + day
  h = h*31 + ((int)phase + 1)          // 与 EventSelectionService.ComputeSelectionSeed 的相位偏移约定一致
  h = h*31 + StableHash(tenantId)
  h = h*31 + StableHash(jobId)
  h = h*31 + sequence                  // 成功结算序号（§4.3）
  // fmix（与 EventSelectionService.DeriveSeed 同款）
  uint z = (uint)h ^ 0x9E3779B9u
  z = (z ^ (z >> 16)) * 0x85EBCA6Bu
  z = (z ^ (z >> 13)) * 0xC2B2AE35u
  z ^= z >> 16
  return (int)z

产出 = explorationMin + new System.Random(seed).Next(0, explorationMax - explorationMin + 1)
```

参数即 `runSeed`（`GameRunState.Seed`）、day、phase、`tenantId`、`jobId` 与**成功结算序号**。因此：同一输入必得同一产出（载入后重算一致）；账本 exactly-once 保证不会二次结算，即使重算也不产生「额外」产出。不使用 `UnityEngine.Random`。

---

## 9. 持久化与加载

- **不保存**：`JobDefinition`/`TeamComboDefinition`/`WorkCatalog` 配置资产（内容资产，同 `EventConfig`）。
- **保存**：既有全部状态 + `TenantRunState.JobId`（已保存）+ 新增 `FacilityDurability`、`WorkSettlementSequence`、`WorkSettlements`（账本）。
- `RunSaveData.CurrentSchemaVersion`：1 → **2**。新增字段 `FacilityDurability`（默认 100）、`WorkSettlementSequence`（默认 0）、`WorkSettlements: List<WorkSettlementRecord>`（沿用现有 Dictionary↔List 序列化模式：`CreateSnapshot` 转 List、`RestoreSnapshot` 还原 Dictionary）。
- **旧存档迁移（v1 → v2）**：
  - 设施耐久缺失 → 100。
  - `WorkSettlementSequence` 缺失 → 0；`WorkSettlements` 缺失 → 空。
  - 新资源 `ingredients`/`resources` 缺失 → 沿用 `SettlementBridge.Awake` 既有「缺失资源默认行为」：按 `ResourceDefinition.initialAmount` 初始化；若定义中也不存在 → 以 Amount=0 创建。
  - `JobId` 缺失/空 → 空串（未分配），不参与结算。
  - 能力不持久化（§2.2），无需迁移；`Carpenter` 相关处理属阶段 2。
- **载入后不重复生产**：账本随存档保存；载入中途阶段不会重新结算已入账的 (day, phase)，且探索产出确定性可重算一致。

---

## 10. 事件管线拦截

- 拦截点：`EventEffectExecutor.BuildChanges` 的 `AddErosionChanges` 在生成 `AdjustTenantErosionChange` 前调用 `WorkMitigationResolver.Compute(delta, effect.target, context)`，将返回的调整后 delta 用于变更。
- `context`（半日快照 + 当前阶段 + 激活团队 + 配置值）由 `WorkSettlementCoordinator` 在阶段进入时冻结并对外提供；context 缺失（如未就绪/测试环境）→ 返回原 delta，不缓解。
- 仅 `ModifyTenantErosion` 且方向为「恶化」且目标属于 §6.2 表格三类时缓解；`ModifyResource`、`ApplyBuff`、`SameRoomOtherTenants`/`ByPlayerFlag`/`RandomAssignedTenants` 目标、治疗方向效果一律不拦截。
- 拦截不改变事件资格判定（`requiredTags` 仍按固有标签），只改变侵蚀数值。

---

## 11. UI 流程

- **入口（复用既有）**：租客列表项/房间头像 右键 → `TenantInfoHoverTrigger.OpenPinned` → `TenantInfoPanel.ShowPinned`。职业分配 UI 挂载于 pinned 面板内容区。
- **冻结租客 ID**：`ShowPinned` 固定 `CurrentTenantId`；职业列表所有操作绑定该 ID，面板打开期间不随悬停切换（既有 Pinned 行为，不新增）。
- **职业列表**：为当前租客列出全部 10 个职业，每项显示：
  - 兼容性（`allowedTags` 判定；无标签租客仅 `chores` 可用，其余显示「不兼容（仅限杂务）」并禁用）；
  - 当前职业标记（`JobId` 匹配项）；
  - 下次效果文本，例如：烹饪「下一结算：消耗食材 1 → 产出食物 2」、守夜「夜间：个人侵蚀损失 −40%」、未分配「未分配职业」。
- **分配提交**：点击职业项 → `WorkAssignmentCoordinator.TryAssignJob(tenantId, jobId)`（唯一允许提交 `AssignJobChange` 的入口）→ 校验（租客存在、职业存在、`allowedTags` 兼容）→ 提交后刷新列表与组合显示。
- **资源/设施/组合显示**：pinned 面板只读区显示 `food`/`currency`/`ingredients`/`resources` 数量、设施耐久（0–100）、当前激活团队列表（如「安保队：守夜 −60%」「物流队：产出 ×1.5」，由 `WorkCatalog` 动态推导）。数值来自 `SettlementBridge.GetResourceAmount` 与 `WorkSettlementCoordinator`。
- **无左键拖拽冲突**：职业分配全程点击完成；不触碰 `TenantAvatarDragTrigger`/`TenantDragOverlay`/`AnchorDropTarget` 的左键拖拽房间流程。pinned 面板既有行为保持：面板外左键点击关闭、面板内点击不关闭（`IsInternalHit`）、按住左键不触发悬停（`TryOpenHover` 的 `Input.GetMouseButton(0)` 检查）。
- **范围**：阶段 1 实现上述 UI 逻辑与数据绑定接口（在既有 pinned 面板代码内）；场景布局（新控件预制、场景接线、美术）属阶段 2。

---

## 12. 错误处理与不变量

1. **幂等**：`WorkSettlements` 已含 key → 跳过结算；`AddWorkSettlementChange` 校验 key 重复或 `Sequence != WorkSettlementSequence + 1` 即拒绝 → 不可能重复生产。
2. **原子性**：结算的全部变更（产出/消耗/治疗/耐久/账本/审计）在同一 `AuthorizedChangeSet` 内一次 `TryCommit`；失败则全部不生效、账本不写、序号不递增，协调器记审计并可重试。
3. **JobId 引用**：`AssignJobChange` 校验租客存在（既有）且 `jobId` 为空串或 ∈ `StateReducer` 构造时注入的已注册 JobId 集合；非法 id 拒绝。
4. **能力-职业兼容**：`AssignJobChange` 的唯一提交方是 `WorkAssignmentCoordinator`；提交前校验 `allowedTags`（含「无标签仅 `chores`」）。内核不复制该规则，但以协调器为唯一入口保证。
5. **团队一致性**：激活团队只由快照动态推导，不持久化；载入后重推一致。
6. **数值边界**：侵蚀 clamp [0,100]（既有）；设施耐久 clamp [0,100]；资源 ≥0（既有 clamp）。
7. **取整规则**：侵蚀缓解保留浮点；医疗治疗向下取整（≥0）；资源产出向下取整（物流队倍率同）；消耗取 `min(需求, 存量)`。
8. **阶段/时段约束**：账本记录 `Phase` 仅 `Day`/`Night`；`AddWorkSettlementChange` 仅可由授权者 `WorkSettlementCoordinator` 提交（沿用现有授权者校验模式）。

---

## 13. 分阶段推出与测试矩阵

### 13.1 分阶段推出

- **阶段 1（运行时基础，本规格范围）**：内核模型与资产定义、`JobId` 语义、半日结算与账本、事件拦截、确定性种子、资源/设施耐久、持久化与 v2 迁移、`StateReducer` 规则、UI 逻辑（既有面板内）、测试矩阵。另含最小源码增量：`TenantAbility` 枚举新增 `Driver`、`Teacher`，`AbilityDisplayName` 增加对应映射（保留 `Carpenter`，其移除属阶段 2）。
- **阶段 2（另行批准后）**：UI 场景布局（职业面板预制/接线/美术）、事件与候选资产迁移（N/D 条目接入、`Carpenter` 移除）、数值打磨。
- 再次明确：阶段 1 **不含** UI 场景布局与事件资产迁移。

### 13.2 NUnit 测试矩阵

EditMode 测试，置于 `Assets/Tests/Hotel.Runtime.Tests`（沿用 ARCHITECTURE.md 约定：引用 `Hotel.Runtime` 与 `Hotel.Authoring`）。涉及 Assembly-CSharp 的协调器/UI 逻辑不在该程序集覆盖范围，以 Unity 编译验证 + Play 模式人工验证为准（与既有计划约定一致）。

| 分组 | 覆盖点 |
| --- | --- |
| T1 数据定义与兼容性 | 10 职业的 `activityWindow`/产出字段映射；9 标签→职业 `allowedTags` 一一对应；无标签仅 `chores`；`WorkCatalog` 查表 |
| T2 结算账本 exactly-once | 同一 (day,phase) 二次结算跳过；账本记录与序号严格递增；提交失败不写账本、不递增序号 |
| T3 生产/消耗 | cooking 消耗食材产食物；存量不足部分产出；repair 消耗货币恢复耐久（上限 100）；trade/farming/exploration 产出；未分配租客与 `chores` 无产出 |
| T4 百分比缓解 | watch 档位 40%；安保队覆盖 60% 非叠加；patrol 同层累加与上限 75%；organizing 整楼累加与上限 60%；医疗向下取整；恶化方向才缓解、反向不缓解；`SameRoomOtherTenants` 等目标不缓解 |
| T5 团队激活 | 满角色激活；缺任一角色不激活；效果来自配置字段；动态推导不持久化（载入后重推一致） |
| T6 确定性 | 相同 `(seed,day,phase,tenantId,jobId,sequence)` → 相同产出；任一分量变化 → 结果变化；载入后重算一致 |
| T7 持久化/迁移 | v1→v2 迁移（缺失耐久/序号/账本/新资源默认）；序列化往返；载入后无重复生产 |
| T8 内核校验 | `AssignJobChange` 非法 jobId 拒绝；`AddWorkSettlementChange` 重复 key/序号错误/越权授权者拒绝；耐久 clamp |
| T9 UI/协调器（编译+人工） | 冻结租客 ID；兼容性/当前职业/下次效果显示；提交走唯一协调器；无左键拖拽冲突回归 |

---

## 自审记录（Self-Review）

- **规格覆盖**：13 项要求逐一成节（§1–§13）；§1 与 §13.1 显式声明「UI 场景布局与事件资产迁移不在第一阶段范围，除非另行批准」。
- **占位符扫描**：全文无 TODO/TBD/「待定」；所有配置字段均给出初始默认值；未实现系统以「阶段 2 / 不缓解」的明确范围语句处理，而非占位。
- **内部一致性**：
  - 时段（`DayActive`/`NightActive`/`AllDay`）在三处一致：§2.6、§3.1、§4.5；`watch` 仅 Night 三处一致（§2.6、§4.5、§6.2）。
  - 团队角色与 §3.1 `roles` 定义一致；安保队覆盖语义（取代非叠加）在 §2.6 之外于 §6.2、§7.3 重复确认。
  - 序号/账本约定（key = `"{day}|{phase}"`、`Sequence == WorkSettlementSequence + 1`）在 §4.3、§8（种子入参）、§12 一致。
  - 9 标签清单、10 职业 id、资源 id、`comboId` 在全文逐字一致。
  - 与既有代码对齐：`TenantRunState.JobId` 已存在（RunModel.cs）、`AssignJobChange` 已存在（RunChanges.cs/StateReducer.cs）、资源缺失默认行为沿用 SettlementBridge.Awake、确定性混合沿用 EventSelectionService.DeriveSeed、EditMode 测试程序集沿用 ARCHITECTURE.md 约定。
