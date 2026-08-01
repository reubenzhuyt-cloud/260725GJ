# HotelMap Child-Based Placeholder-Avatar Display System

**Date:** 2026-08-01  
**Status:** Approved  

---

## Overview

The HotelMap displays placeholder avatars for each tenant room using a fixed set of child anchor GameObjects. Each anchor holds a SpriteRenderer that renders a colored circle. The system is purely presentational — it reads occupant data from GameRunState but never writes to it.

---

## Anchor Hierarchy

Nine anchor GameObjects exist as direct children of the HotelMap:

```
HotelMap/
  TenantAvatarAnchors/
    Anchor01
    Anchor02
    Anchor03
    Anchor04
    Anchor05
    Anchor06
    Anchor07
    Anchor08
    Anchor09
```

Each anchor is positioned in **HotelMap local coordinates**. Because the anchors are children of HotelMap, Unity's Transform hierarchy provides automatic position, rotation, and scale inheritance — no manual sync is required at runtime.

---

## Placeholder Avatar Rendering

Each anchor GameObject carries a **SpriteRenderer** that draws a colored circle sprite. This circle serves as the placeholder avatar for the room associated with that anchor.

- SpriteRenderer sorting order for the hotel map layer: **-49**
- Avatar SpriteRenderers must render **above** the hotel map, so their sorting order must be greater than -49.

---

## Data Flow

- The presentation layer reads occupant data from `GameRunState` to determine which anchors should display active avatars and what color or sprite to assign.
- The presentation layer **must not mutate** `GameRunState`. All GameRunState changes happen through game-logic systems only.

---

## Future Milestone (M5)

M5 will introduce a projection layer that maps actual occupants from GameRunState to the nine anchor slots. The mapping logic is out of scope for this design note and will be specified separately when M5 work begins.

---

## Design Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | Anchors are children of HotelMap, not a separate root | Automatic transform inheritance eliminates manual position/rotation/scale sync. |
| 2 | Fixed 9 anchors (Anchor01–Anchor09) | Matches the fixed room count on the hotel map. No dynamic instantiation. |
| 3 | Colored-circle SpriteRenderer per anchor | Simple visual placeholder that is easy to replace with final art later. |
| 4 | Presentation must not mutate GameRunState | Clean separation: game logic owns state, presentation only reads it. |
| 5 | Avatar sorting order must be > -49 | Hotel map SpriteRenderer uses sorting order -49; avatars must layer above. |

---

## Acceptance Criteria

- [ ] `HotelMap/TenantAvatarAnchors/Anchor01` through `Anchor09` exist as child GameObjects of HotelMap.
- [ ] Each anchor has a SpriteRenderer component with a colored-circle sprite assigned.
- [ ] All anchor positions are expressed in HotelMap local space and render at the correct world positions via Transform parenting.
- [ ] The hotel map SpriteRenderer sorting order is -49.
- [ ] Avatar SpriteRenderers sort above the hotel map (sorting order > -49).
- [ ] No code path in the presentation layer writes to GameRunState.
- [ ] Rotating, scaling, or moving HotelMap in the scene causes all anchors and their avatars to follow automatically.
