# TenantReviewPanel 能力标签显示 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **本计划特殊约定：** 按用户指示，本计划**不包含任何 bash / git 命令**，任务结尾统一以「Unity 编译验证」作为完成检查点，代替常规的 git commit 步骤。

**Goal:** 让 TenantReviewPanel 用场景中已有的 TagPanel（fileID 465672024）与禁用的 TagPerfab（fileID 280117558）显示候选住客能力标签，None 时隐藏标签面板，为未来多标签显示预留扩展点。

**Architecture:** 在 `TenantReviewPanel.cs` 中新增与 `EventUI` 一致的 `tagPanel` / `tagPrefab` / `tagTextPath` 序列化字段，新增接受 `TenantAbility[]` 的私有方法 `RefreshTagPanel(...)`（数组签名即未来多标签扩展点），在 `Show(...)` 内用单一 `TenantAbility` 包装成数组调用；删除本类私有的 `GetAbilityLabel`，标签文案统一由 `AbilityDisplayName.Get` 输出。标签克隆体挂到 TagPanel 的 HorizontalLayoutGroup 下由其自动排布，绝不修改任何布局参数。场景接线只改 TenantReviewPanel 组件（fileID 423411129）的序列化块，其余场景对象、其余脚本一律不动。

**Tech Stack:** Unity 2022.3.62f3c1、C#、UGUI（`UnityEngine.UI`）、TextMeshPro（`TMPro`）、现有 ScriptableObject 配置（`TenantReviewCandidateSO`）。代码位于默认程序集 Assembly-CSharp（`Assets/Scripts/Hotel/UI/` 下无 asmdef）。

## Global Constraints

- 本计划**只允许修改两个文件**：`Assets/Scripts/Hotel/UI/TenantReviewPanel.cs` 与 `Assets/Scenes/MainScene.unity`（仅 MonoBehaviour 块 fileID 423411129，即 TenantReviewPanel 组件的序列化字段）。其余文件一律只读。
- **绝不改布局**：TagPanel（fileID 465672024）上的 HorizontalLayoutGroup（MonoBehaviour 465672026，guid `30649d3a9faa99c48a7b1166b86bf2a0`）及其 m_Padding / m_Spacing / m_ChildForceExpand 等所有参数、TagPanel 与 TagPerfab 的 RectTransform、Image、LayoutElement 值均不得改动；克隆体只作为子节点加入 TagPanel，不触碰任何布局组件。
- **不改动以下系统**：`TenantAssignmentPanel.cs`、`EventUI.cs`、事件系统（GamePopupEvent / EventProcessedEvent / PhaseEnteredEvent 等）、`TenantReviewCoordinator.cs`、`TenantReviewCandidateSO.cs`、`AbilityDisplayName.cs`、`TenantReviewFontPrewarmer.cs`。
- `Show(...)` 签名保持不变（当前唯一调用方 `TenantReviewCoordinator.ShowCurrentReview()` 仍传单一 `TenantAbility`，见 TenantReviewCoordinator.cs:223-234）。
- 能力标签文案**必须**来自 `Assets/Scripts/Hotel/UI/AbilityDisplayName.cs` 的 `AbilityDisplayName.Get(TenantAbility)`，禁止在 TenantReviewPanel 内保留第二套映射。
- `TenantAbility.None`（或空数组 / null）→ 不生成任何标签，且 `tagPanel.SetActive(false)`。
- 场景中 TagPerfab（fileID 280117558）保持禁用（m_IsActive: 0）作为模板，永不销毁、永不被 SetActive；只实例化它的克隆并激活克隆。
- TagPerfab 的子节点文本对象名为 `"Text (TMP)"`（场景 1684360846 块），`tagTextPath` 默认值必须为 `Text (TMP)`，与 `EventUI` 序列化字段一致。
- 复用场景既有对象，禁止新建 Prefab / GameObject：tagPanel → fileID 465672024，tagPrefab → fileID 280117558。
- 项目存在 EditMode 测试程序集 `Assets/Tests/Hotel.Runtime.Tests`，但其 `Hotel.Runtime.Tests.asmdef` 仅引用 `Hotel.Runtime` 与 `Hotel.Authoring`；本计划修改的 UI 代码位于 Assembly-CSharp（未被该测试程序集引用）。按用户指示：**不新增/不修改测试，验证采用 Unity 编译验证**（Console 零错误）＋编辑器内静态检查＋Play 模式人工验证，不捏造测试命令。
- `TenantReviewFontPrewarmer.FixedPanelStrings`（TenantReviewFontPrewarmer.cs:34-42）已包含全部 9 个能力标签字符串（"医生"…"无标签"），与 `AbilityDisplayName.Get` 输出完全一致，故无需改动该文件。
- 不执行 bash、不执行 git 操作、不对既有生产代码做评价性评论。

---

## File Structure

| 文件 | 操作 | 职责 |
| --- | --- | --- |
| `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs` | **修改** | 新增 `tagPanel` / `tagPrefab` / `tagTextPath` 字段；新增 `RefreshTagPanel(TenantAbility[])` 与私有 `FindTMP` 辅助方法；`Show` 内调用；删除私有 `GetAbilityLabel`；描述行改用 `AbilityDisplayName.Get` |
| `Assets/Scenes/MainScene.unity` | **修改**（仅 423411129 块） | 为 TenantReviewPanel 组件接线：`tagPanel: {fileID: 465672024}`、`tagPrefab: {fileID: 280117558}`、`tagTextPath: Text (TMP)` |
| `Assets/Scripts/Hotel/UI/AbilityDisplayName.cs` | 只读 | 唯一映射源，已有 `public static string Get(TenantAbility)` |
| `Assets/Scripts/Hotel/Managers/TenantReviewCoordinator.cs` | 只读 | 调用方，`Show` 签名不变，无需改动 |
| `Assets/Scripts/Hotel/UI/TenantReviewFontPrewarmer.cs` | 只读 | 已覆盖能力标签字形，无需改动 |
| `Assets/Scenes/MainScene.unity`（TagPanel 1750-1846 / 2737-2839 / 8972 行附近、EventUI 序列化 11202-11204 行附近） | 只读参考 | 场景接线任务的取值依据 |

场景层级参考（只读，不得改动结构）：

```
TenantReviewPanel (GameObject 423411128)
└─ InfoPanel (GameObject 131372142, RectTransform 131372143)
   ├─ ... (1670937118)
   ├─ TagPanel (GameObject 465672024, RectTransform 465672025, m_IsActive: 1)
   │  └─ TagPerfab (GameObject 280117558, RectTransform 280117559, m_IsActive: 0 ← 模板，保持禁用)
   │     └─ "Text (TMP)" (RectTransform 1684360846)
   └─ ... (897345779)
```

---

### Task 1: 统一能力标签映射（删除 GetAbilityLabel，改用 AbilityDisplayName.Get）

**Files:**
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs:67`（短描述行）
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs:91-106`（删除私有方法）
- Reference（只读）: `Assets/Scripts/Hotel/UI/AbilityDisplayName.cs:5`（`public static string Get(TenantAbility ability)`）

**Interfaces:**
- Consumes: `AbilityDisplayName.Get(TenantAbility ability) : string` — 已存在，全局命名空间，无需 using（AbilityDisplayName.cs 与 TenantReviewPanel.cs 同在全局命名空间与 Assembly-CSharp）。
- Produces: 本任务不产出新接口；后续任务依赖「TenantReviewPanel 内不再存在 GetAbilityLabel」这一状态。

- [ ] **Step 1: 替换短描述行中的映射调用**

打开 `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs`，定位第 67 行：

```csharp
            shortDescriptionLabel.text = $"能力：{GetAbilityLabel(ability)}　活跃：{GetActivityLabel(activityType)}\n{shortDescription ?? string.Empty}";
```

将其改为：

```csharp
            shortDescriptionLabel.text = $"能力：{AbilityDisplayName.Get(ability)}　活跃：{GetActivityLabel(activityType)}\n{shortDescription ?? string.Empty}";
```

`AbilityDisplayName.Get` 的返回值与本类旧 `GetAbilityLabel` 完全一致（医生/厨师/工程师/守夜人/前员工/商贩/木工/农民/无标签），因此渲染文本逐字节不变，无任何可见文案变化。

- [ ] **Step 2: 删除私有方法 GetAbilityLabel**

删除 `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs` 第 91-106 行整个方法（含其上方空行），即：

```csharp
    private static string GetAbilityLabel(TenantAbility ability)
    {
        return ability switch
        {
            TenantAbility.Doctor => "医生",
            TenantAbility.Cook => "厨师",
            TenantAbility.Engineer => "工程师",
            TenantAbility.NightWatch => "守夜人",
            TenantAbility.FormerEmployee => "前员工",
            TenantAbility.Merchant => "商贩",
            TenantAbility.Carpenter => "木工",
            TenantAbility.Farmer => "农民",
            _ => "无标签",
        };

    }
```

删除后，`GetActivityLabel` 方法（原第 108-117 行）紧跟 `Show` 方法之后。文件头部 `using System;`、`using TMPro;`、`using UnityEngine;` 等仍全部被使用（`Action`、`TextMeshProUGUI`、`MonoBehaviour`），不得删除。

- [ ] **Step 3: 静态检查无残留引用**

在编辑器中全局搜索 `GetAbilityLabel`（覆盖 `Assets/`）。
Expected: 0 个匹配（生产代码中唯一引用即第 67 行，已在本任务替换；`TenantReviewFontPrewarmer.cs:36` 的注释文本不含标识符调用，属注释字符串，无需理会）。

再全局搜索 `AbilityDisplayName.Get`。
Expected: 至少 1 个匹配位于 `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs`（其余在 EventUI.cs:204）。

- [ ] **Step 4: Unity 编译验证**

打开 Unity（2022.3.62f3c1）项目或聚焦已打开的 Unity 编辑器窗口，等待脚本重新编译完成；打开 Window → General → Console。
Expected: 编译 0 错误、0 新增警告。Console 中不应出现 `CS0103`（The name 'GetAbilityLabel' does not exist in the current context）等编译错误。

---

### Task 2: 新增 TagPanel 字段与 RefreshTagPanel(TenantAbility[]) 显示逻辑

**Files:**
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs:16`（rejectButton 之后插入字段）
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs:74-75`（Show 内 confirmButton 块之后插入调用）
- Modify: `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs`（GetActivityLabel 之后、Hide 之前插入两个方法）

**Interfaces:**
- Consumes: `AbilityDisplayName.Get(TenantAbility) : string`（Task 1 已统一）；`TenantAbility`（`Hotel.Runtime` 命名空间，RunModel.cs:45 枚举：None/Doctor/Cook/Engineer/NightWatch/FormerEmployee/Merchant/Carpenter/Farmer，文件已有 `using Hotel.Runtime;`）。
- Produces: 私有方法 `RefreshTagPanel(TenantAbility[] abilities)`（null 或空数组 → 面板隐藏；数组内 `TenantAbility.None` 项跳过；生成克隆数为 0 时面板隐藏）；私有静态辅助 `FindTMP(GameObject root, string path) : TextMeshProUGUI`。`Show` 签名不变。此数组签名即「未来多 Tag」扩展点：将来多标签只需新增接收 `TenantAbility[]` 的 Show 重载并直接透传给 `RefreshTagPanel`，本方法无需再改。

- [ ] **Step 1: 新增序列化字段**

在 `Assets/Scripts/Hotel/UI/TenantReviewPanel.cs` 第 16 行 `public Button rejectButton;` 之后插入：

```csharp

    [Header("Tag Display")]
    public GameObject tagPanel;
    public GameObject tagPrefab;
    public string tagTextPath = "Text (TMP)";
```

字段名、类型与默认值必须与 `EventUI.cs:31-33`（`tagPanel` / `tagPrefab` / `tagTextPath = "Text (TMP)"`）保持一致，保证场景序列化字段顺序为字母序 `tagPanel` → `tagPrefab` → `tagTextPath`。

- [ ] **Step 2: 在 Show 中调用 RefreshTagPanel**

定位 `Show` 方法内（当前文件第 74-75 行）：

```csharp
        if (confirmButton != null)
            confirmButton.interactable = canRecruit;
```

在其后插入一行：

```csharp
        RefreshTagPanel(ability != TenantAbility.None ? new[] { ability } : null);
```

即 `Show` 方法从第 75 行起变为：

```csharp
        if (confirmButton != null)
            confirmButton.interactable = canRecruit;

        RefreshTagPanel(ability != TenantAbility.None ? new[] { ability } : null);

        // NOTE: Activation (SetActive) is handled by the external controller
```

（仅插入 `RefreshTagPanel` 一行与一个空行，`// NOTE:` 注释及后续代码原样保留。）

- [ ] **Step 3: 新增 RefreshTagPanel 与 FindTMP 方法**

在 `GetActivityLabel` 方法结束（当前第 117 行的 `    }`）之后、`public void Hide()`（第 119 行）之前插入：

```csharp

    private void RefreshTagPanel(TenantAbility[] abilities)
    {
        if (tagPanel == null || tagPrefab == null)
        {
            if (tagPanel != null) tagPanel.SetActive(false);
            return;
        }

        for (int i = tagPanel.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = tagPanel.transform.GetChild(i);
            if (child.gameObject == tagPrefab)
                continue;
            Destroy(child.gameObject);
        }

        int generated = 0;
        if (abilities != null)
        {
            foreach (TenantAbility ability in abilities)
            {
                if (ability == TenantAbility.None) continue;

                GameObject clone = Instantiate(tagPrefab, tagPanel.transform);
                clone.gameObject.SetActive(true);

                TextMeshProUGUI label = FindTMP(clone, tagTextPath);
                if (label != null)
                    label.text = AbilityDisplayName.Get(ability);
                generated++;
            }
        }

        tagPanel.SetActive(generated > 0);
    }

    private static TextMeshProUGUI FindTMP(GameObject root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;
        Transform target = root.transform.Find(path);
        return target != null ? target.GetComponent<TextMeshProUGUI>() : null;
    }
```

行为说明（与 `EventUI.RefreshTagPanel`，EventUI.cs:172-211 同构，但不带 `$"{o + 1}·"` 前缀、不做 choice 索引）：
- 模板 TagPerfab（场景 m_IsActive: 0）保留在子节点中，清理循环跳过它、永不销毁；
- 克隆体 SetActive(true) 后由 TagPanel 的 HorizontalLayoutGroup 自动排布，代码不触碰任何布局参数；
- `abilities == null`、空数组、或全部为 `TenantAbility.None` 时 `generated == 0`，面板 `SetActive(false)`；
- 数组签名供未来多标签直接复用。

- [ ] **Step 4: Unity 编译验证**

聚焦 Unity 编辑器，等待编译完成；打开 Window → General → Console。
Expected: 编译 0 错误、0 新增警告。不应出现 `CS0103`（RefreshTagPanel/FindTMP/AbilityDisplayName 未找到）、`CS0246`（TextMeshProUGUI 类型缺失）等错误。

---

### Task 3: 场景接线（MainScene.unity 中 TenantReviewPanel 组件）

**Files:**
- Modify: `Assets/Scenes/MainScene.unity` — MonoBehaviour 块 `--- !u!114 &423411129`（当前第 2529-2547 行）
- Reference（只读）: TagPanel GameObject `&465672024`（第 2737 行起）、TagPerfab GameObject `&280117558`（第 1750 行起）、EventUI 组件 `&2017123646` 序列化字段格式（第 11202-11204 行：`tagPanel` / `tagPrefab` / `tagTextPath`）

**Interfaces:**
- Consumes: Task 2 新增的三个序列化字段（`tagPanel`、`tagPrefab`、`tagTextPath`）。
- Produces: 运行时 `TenantReviewPanel.tagPanel != null`、`tagPrefab != null`、`tagTextPath == "Text (TMP)"`，使 `RefreshTagPanel` 走正常分支。

- [ ] **Step 1: 在 Unity Inspector 中接线（推荐，勿手改 YAML）**

1. Unity 中打开 `Assets/Scenes/MainScene.unity`。
2. 在 Hierarchy 选中 `TenantReviewPanel`（GameObject fileID 423411128）。
3. 在其 Inspector 中确认出现新增的 `Tag Display` 分组与 `Tag Panel`、`Tag Prefab`、`Tag Text Path` 三个字段。
4. 将 Hierarchy 中 `TenantReviewPanel → InfoPanel → TagPanel`（即 fileID 465672024）拖入 `Tag Panel`。
5. 将 `InfoPanel → TagPanel → TagPerfab`（即 fileID 280117558，场景中为禁用状态）拖入 `Tag Prefab`。
6. `Tag Text Path` 填 `Text (TMP)`。
7. 保存场景（Ctrl+S）。

- [ ] **Step 2: 核对序列化结果**

在任意文本编辑器打开 `Assets/Scenes/MainScene.unity`，定位 `--- !u!114 &423411129` 块。其结尾必须恰好为（第 2541-2547 行原字段 + 新增三行，顺序按字母序）：

```yaml
  avatarImage: {fileID: 1670937120}
  nameLabel: {fileID: 897345781}
  shortDescriptionLabel: {fileID: 912549301}
  detailedDescriptionLabel: {fileID: 912549301}
  detailedDescriptionScroll: {fileID: 182270235}
  confirmButton: {fileID: 236687290}
  rejectButton: {fileID: 1585518183}
  tagPanel: {fileID: 465672024}
  tagPrefab: {fileID: 280117558}
  tagTextPath: Text (TMP)
```

Expected: 该块之外无任何场景改动。

- [ ] **Step 3: 确认布局与模板未被触碰**

Expected（用文本编辑器核对）：
- TagPanel 组件 `&465672026`（HorizontalLayoutGroup）的 `m_Padding` / `m_Spacing` / `m_ChildForceExpand*` / `m_ChildControl*` / `m_ReverseArrangement` 值保持原样（第 2788-2801 行）；
- TagPerfab 的 `m_IsActive: 0` 保持（第 1768 行）与 LayoutElement / Image 值不变（第 1789-1838 行）；
- `&465672027`（Image）颜色、`&465672028`（CanvasRenderer）等其余场景对象、`&965651171`（TenantAssignmentPanel 区域的另一个 TagPanel）与 `&1311344544`（另一个 TagPerfab）均无改动。

- [ ] **Step 4: Unity 编译验证**

聚焦 Unity 编辑器，等待场景保存后的编译与场景加载完成；打开 Console。
Expected: 0 错误。Console 不出现 `MissingReferenceException` 或序列化丢失警告（如 `tagPanel` 指向缺失对象）。Hierarchy 中选中 TenantReviewPanel，Inspector 中三个新字段显示已赋值且不显示 None。

---

### Task 4: 端到端 Play 模式验证

**Files:**
- 只读验证，不修改任何文件。

**Interfaces:**
- Consumes: Task 2 的 `RefreshTagPanel` 逻辑 + Task 3 的场景接线；运行时数据来自 `TenantReviewCoordinator.candidates`（`List<TenantReviewCandidateSO>`，每项含 `ability`）经 `ShowCurrentReview()` → `Show(...)` 传入。

- [ ] **Step 1: 确认验证样本**

在 Project 窗口确认 `TenantReviewCoordinator.candidates` 引用的 `TenantReviewCandidateSO` 资产中至少存在：
- 一个 `ability` 非 None 的候选（例如 `Doctor`）——用于验证标签显示；
- 一个 `ability = None` 的候选——用于验证面板隐藏。
Expected: 样本存在（如不存在，仅需在验证前于 Inspector 中临时将某个候选的 `ability` 改为 `Doctor` 或 `None` 再改回；验证完成后恢复原值，该调整不属于代码改动）。

- [ ] **Step 2: Play 模式验证标签显示**

1. 打开 `MainScene`，进入 Play 模式。
2. 按正常流程推进到下一次入住评审批次（触发 `TenantReviewCoordinator` 的 PhaseEntered 批次，评审卡片弹出）。
3. 当 `ability = Doctor` 的候选出现时，在 Hierarchy 中展开 `TenantReviewPanel → InfoPanel → TagPanel`。

Expected:
- TagPanel 处于 active；
- TagPanel 下出现一个激活状态的克隆标签（名字为 `TagPerfab(Clone)`），且原模板 TagPerfab 仍为禁用；
- 克隆标签内 `Text (TMP)` 的文本为 `医生`（即 `AbilityDisplayName.Get(TenantAbility.Doctor)`）；
- 克隆标签位置由 HorizontalLayoutGroup 自动排布，无需也不应手动调整其 RectTransform。

- [ ] **Step 3: 验证 None 隐藏与多候选不累积**

1. 继续推进评审批次，直到 `ability = None` 的候选出现。

Expected:
- TagPanel 被 `SetActive(false)` 隐藏，TagPanel 下无任何激活的克隆标签；
- 若之后又出现非 None 候选，TagPanel 重新变为 active 且仅含该候选对应的单个标签（先前 None 批次未残留克隆）。

2. 连续确认/拒绝多个候选，让评审批次多次刷新。

Expected: TagPanel 子节点中始终只有「模板 TagPerfab + 当前批次的克隆标签」，克隆数等于非 None 能力数，无旧克隆累积（清理循环 `Destroy` 生效）。

- [ ] **Step 4: 回归确认其它系统无影响**

Expected（Play 模式全程）：
- `TenantReviewPanel.Show` 其它行为不变：头像/名字/描述/滚动条归顶/按钮交互态与改动前一致；
- 短描述行文本与改动前逐字一致（`能力：医生　活跃：…`，因 Task 1 的 `AbilityDisplayName.Get` 与原 `GetAbilityLabel` 输出相同）；
- `TenantAssignmentPanel`、`EventUI`、事件通道日志无异常，招募/拒绝/批次推进流程正常；
- Console 无报错、无新增警告。

- [ ] **Step 5: 完成检查点**

退出 Play 模式，保存场景（Ctrl+S）与所有脚本。最终状态核对：
- `git status` 显示的改动仅限：`Assets/Scripts/Hotel/UI/TenantReviewPanel.cs` 与 `Assets/Scenes/MainScene.unity`（核对用，不做任何提交）。
- Unity Console 0 错误。

---

## Self-Review

- **Spec coverage:** 目标（TagPanel+TagPerfab 显示能力标签）→ Task 2/3；未来多 Tag → `RefreshTagPanel(TenantAbility[])` 数组签名 + 说明；调用方仍传单一 ability → `Show` 签名不变（Task 2 Step 2）；None 隐藏面板 → `RefreshTagPanel` 中 `generated > 0` 判空与 Show 传 null（Task 2 Step 3）；映射统一 `AbilityDisplayName.cs` → Task 1；绝不改布局 / 不改 TenantAssignmentPanel、EventUI、事件系统 → Global Constraints + Task 3 Step 3；场景既有对象复用 → Task 3 Step 1；无测试框架捏造 → Global Constraints + 各任务「Unity 编译验证」步骤。无缺口。
- **Placeholder scan:** 无 TBD/TODO/「类似 Task N」等占位；每个步骤含精确行号、完整代码或精确 YAML 期望值。
- **Type consistency:** `tagPanel`/`tagPrefab` 为 `GameObject`、`tagTextPath` 为 `string`、`RefreshTagPanel(TenantAbility[])`、`FindTMP(GameObject, string) : TextMeshProUGUI`、`AbilityDisplayName.Get(TenantAbility) : string` 在全部任务中签名一致；场景 fileID（423411129 / 465672024 / 280117558 / 1684360846）与 `Text (TMP)` 路径名全部与 MainScene.unity 实测一致。
