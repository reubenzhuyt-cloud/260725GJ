# Hotel Game — Architecture

Source-verified documentation of the implemented codebase as of 2026-08-02.

---

## 1. Project Overview

This is a Unity 2D hotel management game. The player assigns tenants to rooms, manages food/medicine resources, and progresses through a fixed day/night phase cycle. Random events present confirm or choice dialogs. The runtime state is mutated exclusively through an `AuthorizedChangeSet` → `StateReducer` pipeline.

---

## 2. Source Directory Tree

```
Assets/
├── Scripts/
│   ├── Core/
│   │   └── Events/
│   │       ├── GameEvent.cs              — Void SO event channel
│   │       ├── GameEventT.cs             — Generic SO event channel
│   │       ├── GameEventListener.cs      — MonoBehaviour listener (void)
│   │       └── GameEventListenerT.cs     — MonoBehaviour listener (generic)
│   └── Hotel/
│       ├── Authoring/
│       │   ├── DayCycle/
│       │   │   └── DayCycleDefinition.cs — Phase cycle SO (Dawn→Day→Dusk→Night)
│       │   └── Resources/
│       │       └── ResourceDefinition.cs — Resource SO (id, name, initialAmount, icon)
│       ├── Camera/
│       │   ├── CameraController.cs       — Pan/zoom with UI-scroll guard
│       │   └── ParallaxBackground.cs     — Phase-aware parallax background
│       ├── Data/
│       │   ├── EventConfig.cs            — Event definition SO (confirm/choice)
│       │   ├── EventProcessedEvent.cs    — SO channel: event dismissed
│       │   ├── EventQueueEmptyEvent.cs   — SO channel: queue drained
│       │   ├── FoodShortageEvent.cs      — SO channel: food shortage
│       │   ├── GamePopupEvent.cs         — SO channel: popup display
│       │   ├── PhaseEnteredEvent.cs      — SO channel: phase transition
│       │   └── ResourceAdjustedEvent.cs  — SO channel: resource change
│       ├── Managers/
│       │   ├── EventManager.cs           — Event queue + pre-generation
│       │   ├── GamePhaseManager.cs       — Phase advancement (singleton)
│       │   ├── SettlementBridge.cs       — GameRunState owner + food settlement
│       │   └── TenantAssignmentCoordinator.cs — Room assignment coordinator
│       ├── Presentation/
│       │   └── Avatars/
│       │       ├── AnchorDropTarget.cs       — Room drop target on map
│       │       ├── TenantAvatarDisplay.cs    — Colored circle renderer
│       │       ├── TenantAvatarLod.cs        — Per-avatar LOD (single)
│       │       └── TenantAvatarLodController.cs — Batch LOD controller
│       ├── Runtime/
│       │   ├── Kernel/
│       │   │   ├── Changes/
│       │   │   │   └── RunChanges.cs     — RunChange types + AuthorizedChangeSet
│       │   │   └── Reduction/
│       │   │       └── StateReducer.cs   — Validation + application reducer
│       │   └── State/
│       │       └── RunModel.cs           — GameRunState + all state slice types
│       ├── Services/
│       │   └── ResourceService.cs        — Static helpers for resource queries/adjust
│       └── UI/
│           ├── EventUI.cs                — Event popup (confirm/choice)
│           ├── InfoPanelResourceDisplay.cs — Food/Medicine display in info panel
│           ├── NextPhaseButton.cs        — Long-press to advance phase
│           ├── NextPhasePanel.cs         — Shown when event queue is empty
│           ├── PhaseUI.cs                — Day/phase text display
│           ├── TenantAssignmentItemView.cs — Readonly struct: tenant display data
│           ├── TenantAssignmentPanel.cs  — Unassigned tenant list UI
│           ├── TenantAssignmentPanelEventVisibility.cs — Hide panel during events
│           ├── TenantAssignmentPanelReveal.cs      — Slide-in on hover
│           ├── TenantAssignmentPanelRevealRelay.cs  — Hover relay for child elements
│           ├── TenantAvatarListItem.cs   — Draggable tenant avatar in list
│           ├── TenantAvatarDragTrigger.cs — Pointer-event relay to TenantAvatarListItem
│           ├── TenantDragOverlay.cs      — Drag cursor overlay
│           └── UIManager.cs              — Enables managed panels on start
└── Tests/
    └── Hotel.Runtime.Tests/
        └── Runtime/
            ├── DayCycleAndGameRunStateTests.cs
            ├── MainSceneNextPhasePanelWiringTests.cs
            └── StateReducerTests.cs
```

---

## 3. Static ScriptableObject Definitions

### 3.1 DayCycleDefinition

**File**: `Assets/Scripts/Hotel/Authoring/DayCycle/DayCycleDefinition.cs`  
**Menu**: `Hotel/Day Cycle`

A `ScriptableObject` implementing `IPhaseCycle`. Contains a fixed, validated array of four phases in the order `Dawn → Day → Dusk → Night`. The `GetNext(HotelPhase)` method returns the next phase cyclically. The `Validate()` method enforces exactly this order and rejects duplicates. `OrderedPhases` exposes a read-only view.

### 3.2 ResourceDefinition

**File**: `Assets/Scripts/Hotel/Authoring/Resources/ResourceDefinition.cs`  
**Menu**: `Hotel/Resource Definition`

Fields: `resourceId` (string), `displayName` (string), `initialAmount` (int), `icon` (Sprite). Used by `SettlementBridge` to seed `GameRunState.Resources` at startup.

### 3.3 EventConfig

**File**: `Assets/Scripts/Hotel/Data/EventConfig.cs`  
**Menu**: `Configs/EventConfig`

Defines a single event: `eventIndex`, `eventId`, `triggerPhase` (GamePhase enum), `triggerCondition` (string, reserved for future use — leave empty), `eventTitle`, `eventDescription`, `eventImage`, `eventType` (Confirm or Choice), `confirmEffects` (list of `EventEffect`), `choices` (list of `ChoiceOption` — each has `choiceId`, `choiceText`, `choiceResult`, and `choiceEffects`). `EffectType` enum currently has `None` and `ModifyTenantErosion`.

**Phase enum warning**: `EventConfig` uses the global `GamePhase` enum declared in `EventConfig.cs` with ordinal ordering `{ Day, Dawn, Night, Dusk }`. This is distinct from `HotelPhase` in `RunModel.cs` which has ordering `{ Dawn, Day, Dusk, Night }`. `GamePhase` is used by `EventConfig.triggerPhase`, `GamePhaseManager`, `PhaseEnterData`, and all UI code. `HotelPhase` is used by `GameRunState` and the `StateReducer` pipeline. Do not convert between these enums via integer cast — their ordinal values do not match.

---

## 4. GameRunState and State Slices

**File**: `Assets/Scripts/Hotel/Runtime/State/RunModel.cs`

`GameRunState` is the single source of truth. Key fields:

| Field | Type | Purpose |
|---|---|---|
| `RunId` | `RunId` (readonly struct wrapping string) | Identifies the run |
| `StateVersion` | `long` | Optimistic concurrency version |
| `Day` | `int` | Current day number (starts at 1) |
| `Seed` | `int` | RNG seed |
| `Phase` | `PhaseRunState` | Current phase, lifecycle state, occurrence count |
| `Decisions` | `List<DecisionRunState>` | Pending/completed decisions |
| `EventHistory` | `List<EventHistoryRecord>` | All planned/resolved events |
| `AuditLog` | `List<string>` | Append-only audit trail |
| `Tenants` | `Dictionary<string, TenantRunState>` | Per-tenant state (erosion, room, job, mark) |
| `Rooms` | `Dictionary<string, RoomRunState>` | Per-room state (occupant list) |
| `Resources` | `Dictionary<string, ResourceRunState>` | Per-resource amount |
| `Summary` | `RunSummaryState` | Run completion metadata |

Supporting enums: `HotelPhase` (Dawn, Day, Dusk, Night), `PhaseLifecycleState` (Entered, Settled, WaitingForDecisions, Exiting, Completed).

`GameRunState.New(RunId, int seed)` creates a fresh state at Day 1, Dawn, version 0.

---

## 5. Mutation Pathway: RunChange → AuthorizedChangeSet → StateReducer

**Files**: `RunChanges.cs`, `StateReducer.cs`

All state mutations go through this pipeline. No field on `GameRunState` is modified directly.

### 5.1 RunChange Types

Abstract base `RunChange`. Concrete types: `SetPhaseLifecycleChange`, `SetCurrentPhaseChange`, `CreateDecisionChange`, `CompleteDecisionChange`, `AppendAuditLogChange`, `SetRunSummaryChange`, `PlanEventHistoryChange`, `ResolveEventHistoryChange`, `SetTenantMarkChange`, `AdjustTenantErosionChange`, `AssignRoomChange`, `AssignJobChange`, `AdjustResourceChange`.

### 5.2 AuthorizedChangeSet

Groups changes with authorization metadata: `RunId`, `ExpectedStateVersion`, `AuthorizerId`, `CommandId`. Two factory methods:
- `Coordinator(runId, version, command)` — authorizer = `"GamePhaseCoordinator"`, required for phase/lifecycle/summary changes.
- `Domain(runId, version, authorizer, command)` — for domain-level changes (rooms, resources, erosion, etc.).

### 5.3 StateReducer.TryCommit

`StateReducer` is a sealed class implementing `IStateReducer` (interface defined in `RunChanges.cs` with the single method `CommitResult TryCommit(GameRunState, AuthorizedChangeSet)`). `TryCommit` contract:

1. Returns `CommitResult(false)` if state or set is null, RunId mismatches, or StateVersion mismatches.
2. Runs full validation; any failure returns `CommitResult(false)` with no state side-effects.
3. On validation success, applies each `RunChange` in order, then increments `StateVersion` by 1 and returns `CommitResult(true)`.

**Validation** (all must pass, all-or-nothing):
1. RunId must match.
2. StateVersion must match expected version.
3. Phase lifecycle changes (`SetPhaseLifecycleChange`, `SetCurrentPhaseChange`, `SetRunSummaryChange`) require authorizer `"GamePhaseCoordinator"`.
4. `CompleteDecisionChange` requires the decision to exist and not already be completed.
5. `PlanEventHistoryChange` rejects duplicate event IDs.
6. `ResolveEventHistoryChange` requires the event to exist and be unresolved.
7. `SetTenantMarkChange` / `AdjustTenantErosionChange` require the tenant to exist.
8. `AssignRoomChange` requires both tenant and room to exist; duplicate tenant assignments within one changeset are rejected.
9. `AssignJobChange` requires the tenant to exist.
10. `AdjustResourceChange` requires the resource to exist.

**Application**: Applies each change in order, then increments `StateVersion` by 1. Erosion is clamped to [0, 100]. Room reassignment removes the tenant from the old room's occupant list.

---

## 6. Fixed Phase Cycle and GamePhaseManager

**File**: `Assets/Scripts/Hotel/Managers/GamePhaseManager.cs`

Singleton MonoBehaviour. Transition table: `Dawn→Day`, `Day→Dusk`, `Dusk→Night`, `Night→Dawn`. Initializes to Day 1, Day phase in `Awake()` (overriding any scene-serialized value). The `AdvancePhase()` method:

1. Gets the next phase via a switch (Dawn→Day, Day→Dusk, Dusk→Night, Night→Dawn).
2. If the next phase is "hidden" (Dawn or Dusk):
   - If `EventManager` has pre-generated events for that phase → enter it.
   - Otherwise → skip to the phase after the hidden one (Dawn skips to Day, Dusk skips to Night). Day counter increments when Dawn is skipped.
3. Fires `PhaseEnteredEvent` SO channel.

On `Start()`, a one-frame delay coroutine initializes at Day 1, Day phase, and fires the initial phase notification. `EventManager.PreGenerateDayEvents` is called when Day phase begins.

`EventManager` exposes inspector probability fields: `normalPhaseChance` (default 70, for Day/Night) and `hiddenPhaseChance` (default 50, for Dawn/Dusk). These control the probability of events appearing in each phase during `PreGenerateDayEvents`.

---

## 7. SettlementBridge — Ownership and Food Settlement

**File**: `Assets/Scripts/Hotel/Managers/SettlementBridge.cs`

Singleton MonoBehaviour with `[DefaultExecutionOrder(-100)]`. **Owns** the `GameRunState` instance and the `StateReducer` instance. Created in `Awake()` with `RunId("main_run")` and seed hardcoded to `1`. Seeds `Resources` from `ResourceDefinition` list. Resets singleton `Instance` to `null` in `OnDestroy`.

**Food settlement** triggers in `OnPhaseEntered` when: previous phase was `Night` AND the new day is greater than the last settlement day. Logic:
1. Counts tenants with a non-empty `RoomId` (assigned tenants).
2. If zero assigned tenants → skip (returns true).
3. Consumes `min(assignedCount, availableFood)` food.
4. Creates a `Coordinator`-authorized changeset with `AdjustResourceChange("food", -consumed)` and an audit log entry.
5. Commits via `StateReducer.TryCommit`.
6. On success: raises `ResourceAdjustedEvent` for food, and if `shortage > 0`, raises `FoodShortageEvent`.

Exposes `GetResourceAmount(string)` which delegates to `ResourceService.GetAmount`.

---

## 8. SO Event Channels

All channels use the `GameEvent<T>` pattern: ScriptableObject assets that maintain two registration lists — `GameEventListener<T>` MonoBehaviour components (via `Register(GameEventListener<T>)`) and direct `Action<T>` callbacks (via `Register(Action<T>)`). Listeners register/unregister in `OnEnable`/`OnDisable`. `Raise(T)` iterates both lists in reverse order. The void `GameEvent` variant follows the same dual-list pattern with `GameEventListener` and `Action`.

| Channel | Data Type | Raised By | Listened By |
|---|---|---|---|
| `PhaseEnteredEvent` | `PhaseEnterData { day, phase }` | `GamePhaseManager` | `SettlementBridge`, `EventManager`, `PhaseUI`, `ParallaxBackground`, `InfoPanelResourceDisplay` |
| `GamePopupEvent` | `PopupData { eventIndex, eventId, title, description, image, eventType, confirmEffects, choiceTexts, choiceResults, choiceEffects }` | `EventManager` | `EventUI`, `NextPhasePanel`, `TenantAssignmentPanelEventVisibility` |
| `EventProcessedEvent` | `string` (eventId) | `EventUI` | `EventManager` |
| `EventQueueEmptyEvent` | `int` | `EventManager` | `NextPhasePanel`, `TenantAssignmentPanelEventVisibility` |
| `FoodShortageEvent` | `FoodShortageData { day, shortageAmount }` | `SettlementBridge` | (listeners assigned in scene) |
| `ResourceAdjustedEvent` | `ResourceAdjustedData { resourceId, delta, newAmount }` | `SettlementBridge`, `ResourceService` | `InfoPanelResourceDisplay` |

---

## 9. Resource System

### 9.1 Resources

Two resources are configured via `ResourceDefinition` SO assets: **food** and **medicine**. Their IDs (`"food"`, `"medicine"`) are used as dictionary keys in `GameRunState.Resources`.

### 9.2 ResourceService

**File**: `Assets/Scripts/Hotel/Services/ResourceService.cs`

Static class with two methods:
- `GetAmount(GameRunState, resourceId)` → returns the amount or 0 if missing.
- `TryAdjust(GameRunState, StateReducer, resourceId, delta, authorizer, channel)` → creates a `Domain`-authorized changeset, commits it, and raises `ResourceAdjustedEvent` on success.

---

## 10. Tenant and Room Assignment

### 10.1 TenantAssignmentCoordinator

**File**: `Assets/Scripts/Hotel/Managers/TenantAssignmentCoordinator.cs`

Singleton MonoBehaviour. In `Start()`, obtains `GameRunState` and `StateReducer` from `SettlementBridge.Instance`. Creates **9 rooms** (`room_01` through `room_09`) and **9 tenants** (`tenant_alpha` through `tenant_iota`) with display names and colors. Each tenant is added to `GameRunState.Tenants` and each room to `GameRunState.Rooms`. Resets singleton `Instance` to `null` in `OnDestroy`.

`TryAssign(tenantId, roomId)` validates both exist, room is unoccupied, tenant is unassigned, then commits an `AssignRoomChange` via `AuthorizedChangeSet.Domain`. On success, refreshes the unassigned list, fires `AssignmentChanged` event, and refreshes all `AnchorDropTarget` and `TenantAssignmentPanel` instances.

`IsRoomOccupied(roomId)` and `GetRoomOccupantId(roomId)` query `GameRunState.Rooms` directly.

### 10.2 Avatar Presentation

- `AnchorDropTarget` (per room on the map): Shows a colored `TenantAvatarDisplay` circle when occupied; listens to `AssignmentChanged` to refresh.
- `TenantAvatarDisplay`: `SpriteRenderer`-based colored circle with `SetColor`/`SetVisible`.
- `TenantAvatarLod` / `TenantAvatarLodController`: Camera-zoom-based LOD that scales avatars and toggles detail backgrounds at configurable thresholds.
- `TenantAssignmentItemView`: Readonly struct holding `TenantId`, `DisplayName`, `Color`.

---

## 11. UI

### 11.1 Resource Panel

`InfoPanelResourceDisplay` shows food and medicine amounts as `TextMeshProUGUI` text. Listens to `ResourceAdjustedEvent` (updates on any resource change) and `PhaseEnteredEvent` (refreshes on Dawn). Reads amounts from `SettlementBridge.Instance.GetResourceAmount`.

### 11.2 Panel Visibility

- `TenantAssignmentPanelEventVisibility`: Hides the tenant assignment panel when an event popup is shown (`GamePopupEvent`), re-shows it when the event queue empties (`EventQueueEmptyEvent`).
- `NextPhasePanel`: Shown (via `CanvasGroup`) when the event queue is empty; hidden when an event popup fires.
- `TenantAssignmentPanelReveal` / `TenantAssignmentPanelRevealRelay`: Slide-in animation on pointer hover with retraction delay.
- `UIManager`: Activates all managed panel GameObjects and enables their MonoBehaviour components on `Start()`.

### 11.3 Phase Display

`PhaseUI` displays current day number and phase name (localized: 白天/黎明/黑夜/黄昏). Listens to `PhaseEnteredEvent`.

### 11.4 Event Popup

`EventUI` displays event content (image, title, description) in an overlay. Supports Confirm mode (single button) and Choice mode (dynamic button instantiation from prefab). On confirm/choice selection, applies `EventEffect` array and raises `EventProcessedEvent` to signal `EventManager` to process the next queued event.

### 11.5 Next Phase Button

`NextPhaseButton` implements a long-press interaction (configurable `holdDuration`). On completion, calls `GamePhaseManager.Instance.AdvancePhase()`.

### 11.6 Drag-and-Drop Tenant Assignment

`TenantAvatarDragTrigger` implements `IPointerDownHandler`, `IPointerUpHandler`, `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`. It acts as a pointer-event relay, forwarding `OnPointerDown` → `owner.BeginAvatarHold()` and `OnPointerUp`/`OnEndDrag` → `owner.EndAvatarHold()` to its owning `TenantAvatarListItem`. This decouples the EventSystem pointer interface from the drag logic.

`TenantAvatarListItem` supports long-press to initiate drag. On drag, shows `TenantDragOverlay` at cursor position. On release, raycasts to find an `AnchorDropTarget` under the cursor and calls `TenantAssignmentCoordinator.TryAssign`.

---

## 12. Camera Input Rule

**File**: `Assets/Scripts/Hotel/Camera/CameraController.cs`

`CameraController` handles pan (mouse drag) and zoom (scroll wheel). **UI scroll does not zoom**: both `HandleZoomInput` and `HandleDragInput` check `EventSystem.current.IsPointerOverGameObject()` and return early if the pointer is over UI. Drag is also suppressed when `TenantAssignmentCoordinator.Instance.IsDragging` is true.

Camera is clamped to the `HotelMap` sprite bounds (auto-detected from `SpriteRenderer`). Zoom is clamped between `minZoom` (3) and an effective max that prevents showing beyond the map.

---

## 13. Test Project

**Location**: `Assets/Tests/Hotel.Runtime.Tests/Runtime/`

Three test files:
- `DayCycleAndGameRunStateTests.cs` — Validates `DayCycleDefinition` ordering, `GameRunState.New` initialization, serialization round-trip, `IPhaseCycle` implementation.
- `StateReducerTests.cs` — Validates authorization rules (coordinator-only changes), version/run mismatch rejection, all change types, atomicity, clamping, duplicate detection.
- `MainSceneNextPhasePanelWiringTests.cs` — Scene-level test verifying `NextPhasePanel` component wiring in `MainScene.unity`.

---

## 14. Legacy Systems Removed

The following systems have been removed from the codebase and are not present in the source:

- **TimeManager / TimeState / TimePhase / TimeUI / TimeControlUI** — Legacy time system replaced by `GamePhaseManager` + `PhaseEnteredEvent`.
- **ErosionManager / ErosionState / ErosionConfig / ErosionChangedEvent / ErosionUI** — Global erosion system removed; erosion now lives per-tenant in `TenantRunState.TrueErosion` and is adjusted via `AdjustTenantErosionChange`.
- **GamePhaseCoordinator** — Referenced only as an authorization string (`"GamePhaseCoordinator"`) in `StateReducer` validation; no coordinator MonoBehaviour exists.
- **CommandGateway** — Not implemented. The `AuthorizedChangeSet` factory methods are the entry point.
- **EventPlanningService** — Not implemented. Event pre-generation happens directly in `EventManager.PreGenerateDayEvents`.
- **ErosionService** — Not implemented. Erosion adjustment is a direct `RunChange` type.
