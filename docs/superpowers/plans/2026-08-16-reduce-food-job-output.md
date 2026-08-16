# 降低食物相关岗位基础产出 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 食物相关岗位基础产出统一改为 2：Cooking 5→2、Farming 4→2、Exploration 选 food 时基础值 3→2（选 currency 仍为 3）。

**Architecture:** 仅修改 `JobSettlementService.BuildJobChanges` 中三处 `AddResourceOutput` 的 baseAmount 实参。效率公式 `CalculateEfficiency`、取整下限 `CalculateOutput`（`Mathf.Max(1, Mathf.RoundToInt(...))`）、其他岗位（Trading/Chores/Medical/Patrol/Organization/NightWatch/Repair）及事件资源效果均保持不变。

**Tech Stack:** Unity (C#) — `Assets/Scripts/Hotel/Services/JobSettlementService.cs`

## Global Constraints

- 项目已按用户要求删除自动化测试：禁止创建持久测试文件；验证仅限代码审查 + Unity AssetDatabase 刷新与编译（0 errors / 0 warnings），不进入 Play Mode。
- 不执行 git commit（用户未授权）。
- 仅改动食物相关岗位基础值，其余逻辑一律不动。

---

### Task 1: 调整食物相关岗位基础产出

**Files:**
- Modify: `Assets/Scripts/Hotel/Services/JobSettlementService.cs:213`（Cooking）
- Modify: `Assets/Scripts/Hotel/Services/JobSettlementService.cs:215`（Farming）
- Modify: `Assets/Scripts/Hotel/Services/JobSettlementService.cs:220-224`（Exploration）

- [ ] **Step 1: Cooking 基础产出 5 → 2**

将第 213 行改为：

```csharp
case JobCatalog.Cooking:
    return AddResourceOutput(state, "food", 2, efficiency, job.DisplayName, changes, resourceDeltas);
```

- [ ] **Step 2: Farming 基础产出 4 → 2**

将第 215 行改为：

```csharp
case JobCatalog.Farming:
    return AddResourceOutput(state, "food", 2, efficiency, job.DisplayName, changes, resourceDeltas);
```

- [ ] **Step 3: Exploration 按资源类型区分基础值**

将第 220-224 行替换为：

```csharp
case JobCatalog.Exploration:
{
    bool picksFood = StableChoice(tenantId, day, phase);
    string resourceId = picksFood ? "food" : "currency";
    int baseAmount = picksFood ? 2 : 3;
    return AddResourceOutput(state, resourceId, baseAmount, efficiency, job.DisplayName, changes, resourceDeltas);
}
```

- [ ] **Step 4: 代码审查**

核对仅上述三处改动；`CalculateEfficiency`、`CalculateOutput`、Trading(currency 4)、Chores(currency 2)、Medical、Patrol、Organization、NightWatch、Repair 分支及事件资源效果均未改动。`StableChoice` 调用保持不变，food/currency 选择逻辑与改动前一致。

- [ ] **Step 5: Unity AssetDatabase 刷新与编译**

在 Unity 编辑器中触发 AssetDatabase 刷新（聚焦编辑器自动导入或执行 Refresh）并等待编译完成。
Expected: Console 输出 0 errors / 0 warnings。不进入 Play Mode。
