# 《Hotel · Day of Erosion》 (Working Title) — Game Design Proposal

> Version: v1.4 | Team: Nailong, Shark, Jazz Dog | Theme: Erosion

---

## 1. Project Overview

### 1.1 One-Sentence Description

**《Hotel · Day of Erosion》** is a post-apocalyptic hotel management + identity-inspection deduction game. The player takes on the role of a hotel manager and, during a 30-day countdown, must screen and recruit survivors, assign rooms and jobs, and deal with nightly Erosion events. Under the dual pressure of incomplete information and limited resources, the goal is to keep as many humans alive as possible until dawn.

### 1.2 Core Experience Positioning

| Dimension      | Content                                                                                       |
| -------------- | --------------------------------------------------------------------------------------------- |
| Target Players | Players who enjoy strategy management + narrative atmosphere + light deduction                |
| Core Loop      | Screen/recruit people → Assign jobs/rooms → Handle events → Observe changes → Adjust strategy |
| Emotional Tone | Oppressive, uneasy, but hopeful — “We can still hold on.”                                     |
| Session Length | Approximately 30–45 minutes (30 days)                                                         |

### 1.3 Design Principles

1. **Screening is strategy, not labor** — Screening happens infrequently, but each decision carries significant weight.
2. **Incomplete information is the core tension** — You can never be 100% certain who is truly human.
3. **Management is a means of survival, not the goal** — Every management decision ultimately serves the goal of surviving the 30 days.
4. **Consequences must be visible** — Players should feel both the positive and negative consequences of every decision.

---

## 2. Worldbuilding and Theme

### 2.1 Background Story

In the near future, an unknown phenomenon known as **“Erosion”** spreads across the world. Those affected by Erosion look completely normal on the outside, but their behavior gradually becomes strange. They may be gentle, efficient, and even “kind,” but their very existence contaminates the minds of those around them.

The **“Edge Hotel”** you manage is one of the last safe houses for normal humans. Located on the outskirts of an abandoned city, it was once an ordinary roadside hotel. Now, it has become a refuge for survivors.

**Your mission:** Keep the hotel running for 30 days until the day the “Purification Signal” arrives. No one knows whether the signal will actually come, but it is the only hope you have.

### 2.2 The Nature of “Erosion” — Design Definition

- Erosion is **not demonic possession or turning into a monster**.
- Erosion is the **gradual loss of cognitive and empathic abilities**.
- People affected by Erosion can still talk, work, and smile — but their “smiles” no longer represent happiness.
- They may still remember their name and their past, but they no longer care about them.
- **The horror comes from appearing normal.**

### 2.3 Art Style Positioning

See Section 5 for details. The core direction is:

**Anime-influenced realism + filter treatment + “something uncanny in everyday life.”**

---

## 3. Core Gameplay Loop

### 3.1 Overview

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│                         30-Day Countdown (Survival Goal)                    │
│  Every 1–3 days: New visitors arrive → Screen/recruit → Assign rooms       │
│  Every day: Four phases → Assign jobs → Handle events → Update statuses    │
│  End of each run: Survivors × Misjudgment Rate → Medals → New-game unlocks  │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Phase        │ →  │ Screening /  │ →  │ Events /     │ →  │ Status       │
│ Progression  │    │ Management   │    │ Feedback     │    │ Update       │
│ Dawn / Day / │    │ Recruit or   │    │ Evaluate    │    │ Color change │
│ Dusk / Night │    │ reject       │    │ decisions   │    │ Resource     │
│              │    │ Assign jobs  │    │ Log records │    │ settlement   │
└──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘
```

### 3.2 Detailed Steps

**Step 1: Phase Progression**

- Time is divided into four phases: Dawn → Day → Dusk → Night → next Dawn.
- When an event or actionable situation occurs, the game stops and waits for the player. When nothing requires action, the phase is automatically skipped and recorded in the log.
- See Section 4.1, Time System.

**Step 2: Screening / Management**\
*(During actionable phases)*

| Action Type      | Trigger                                                    | Content                                                                                     |
| ---------------- | ---------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| Screen / Recruit | Dawn/Dusk (new visitors arrive / interactive events occur) | View applicant information → decide whether to recruit → recruit/reject; event interactions |
| Assign Jobs      | Day/Night (start of phase)                                 | Select idle guests → assign them to jobs → generate resources                               |
| Room Management  | Any phase                                                  | Drag guests to different rooms → isolation/combination strategies                           |
| Evict Guest      | Any phase (manual action)                                  | Select guest to evict → confirm → trigger eviction reaction                                 |

**Step 3: Events / Feedback**

- At night: Random Erosion events, such as “Someone screams” or “Something has been damaged.”
- During the day: Everyday events, such as “A merchant arrives,” “Weather changes,” or “Guests have a conflict.”
- Event outcomes are linked to guests’ ability tags and color statuses.

**Step 4: Status Update**

- Erosion level changes based on events, room environment, and neighboring guests.
- Color labels automatically update (green/yellow/red).
- Resources are settled (food consumption/production).
- Everything that happened during the day is recorded in the log.

---

## 4. Detailed System Design

### 4.1 Time System

#### 4.1.1 Four-Phase Structure

| Phase | Time                | Characteristics                                  | Player Actions                                                         | Progression         |
| ----- | ------------------- | ------------------------------------------------ | ---------------------------------------------------------------------- | ------------------- |
| Dawn  | Approx. 5:00–7:00   | Sky gradually brightens; new visitors may arrive | Screening (if visitors arrive), event interaction (if an event occurs) | Automatic or manual |
| Day   | Approx. 7:00–17:00  | Main activity period                             | Assign jobs, trigger daytime events, manage guests                     | Manual              |
| Dusk  | Approx. 17:00–19:00 | Light fades; guests return from outside          | Screening (if visitors arrive), event interaction (if an event occurs) | Automatic or manual |
| Night | Approx. 19:00–5:00  | Rest period; danger is more frequent             | Assign night watch/duty, handle nighttime events                       | Manual              |

#### 4.1.2 Progression Rules

**Core principle: Stop when there is a decision; skip automatically when there is none.**

```text
Click “Advance Time”
    │
    ▼
Check whether the next phase contains actionable events
    │
    ├── Screening (visitor arrives) / interactive event
    │       → Stop and wait for player decision
    │
    ├── Idle guest available for work
    │       → Stop and wait for player assignment
    │
    ├── Sudden radio signal event
    │       → Stop and wait for player response
    │
    └── No actionable events
            │
            ▼
        Automatically skip quickly
            │
            ▼
        Log: “The day passes peacefully.”
            │
            ▼
        Enter next phase
```

#### 4.1.3 Day/Night Activity Types

Each guest has an active-period attribute:

| Type         | Characteristics                               | Best Working Period | Penalty for Forced Assignment                                                                               |
| ------------ | --------------------------------------------- | ------------------- | ----------------------------------------------------------------------------------------------------------- |
| 🌞 Diurnal   | Energetic during the day, needs rest at night | Day                 | Night work efficiency -50%, Erosion +5/day                                                                  |
| 🌙 Nocturnal | Focused at night, sluggish during the day     | Night               | Day work efficiency -50%, Erosion +5/day                                                                    |
| 🌗 All-Day   | Can work at any time, but mediocre efficiency | Day/Night           | No additional penalty, but lower base efficiency than diurnal/nocturnal guests during their optimal periods |

---

### 4.2 Screening System

#### 4.2.1 Visitors Arrive

- **Frequency:** 1–3 visitors arrive every 1–3 days.
- **Timing:** Any phase. Visitors arriving during the day generally have slightly lower Erosion levels.
- **Method:** A pop-up window displays applicant information.

#### 4.2.2 Applicant Information

Each visitor displays the following:

| Information       | Content                                      | Example                                                                   |
| ----------------- | -------------------------------------------- | ------------------------------------------------------------------------- |
| Name              | Character name                               | “Lin Xi”                                                                  |
| Portrait          | Avatar                                       | Anime-style half-body portrait                                            |
| Short Description | 20–30-character background description       | “Claims to be a former hospital nurse; her eyes seem a little unfocused.” |
| Ability Tag       | 1 tag                                        | 【Doctor】                                                                  |
| Active Period     | 🌞 Diurnal / 🌙 Nocturnal / 🌗 All-Day       | 🌞                                                                        |
| Initial Erosion   | **Hidden; player must judge for themselves** | Not displayed                                                             |

#### 4.2.3 Screening Interaction

- The player sees the information above and clicks **“Recruit”** or **“Reject.”**
- **There is no complicated document-verification process** — the decision is based on limited information, intuition, and current needs.
- A simple Q&A interaction may be added.
- After recruitment, the character’s true Erosion level gradually becomes visible through gameplay.

#### 4.2.4 Strategic Dimensions of Screening

| Consideration    | Question the Player Must Consider                                                   |
| ---------------- | ----------------------------------------------------------------------------------- |
| Ability Needs    | “Do I need a doctor or a cook right now?”                                           |
| Risk Tolerance   | “Something about this person sounds suspicious… but I desperately need more staff.” |
| Active Period    | “I need someone for the night shift, but this diurnal guest isn't suitable.”        |
| Current Capacity | “Do I still have an available room? If not, I need to expand first.”                |

---

### 4.3 Ability Tags and Job System

#### 4.3.1 Ability Tags — 10 Types

| Tag               | Corresponding Job         | Effect                                    |
| ----------------- | ------------------------- | ----------------------------------------- |
| 【Doctor】          | Medical                   | Treat injured guests; reduce Erosion      |
| 【Cook】            | Cooking                   | Food efficiency +20%                      |
| 【Engineer】        | Repair / Renovation       | Repair facilities and renovate rooms      |
| 【Night Watch】     | Watch / Guard             | Nighttime event losses -40%               |
| 【Former Employee】 | Patrol / Guide            | Erosion spread on the same floor -30%     |
| 【Merchant】        | Trading                   | 20% discount on transactions              |
| 【Farmer】          | Farming                   | Produces a small amount of food every day |
| 【Driver】          | Exploration               | Can be sent outside to collect supplies   |
| 【Teacher】         | Counseling / Organization | Organized activities increase resistance  |
| No Tag            | Chores                    | Basic work efficiency                     |

#### 4.3.2 Job Types and Output

| Job          | Suitable Tags                  | Output                                | Cost                     |
| ------------ | ------------------------------ | ------------------------------------- | ------------------------ |
| Cooking      | 【Cook】                         | Food                                  | Food ingredients         |
| Medical      | 【Doctor】                       | Reduce guest Erosion                  | None                     |
| Repair       | 【Engineer】                     | Restore facility durability           | Small amount of currency |
| Night Watch  | 【Night Watch】【Former Employee】 | Reduce nighttime event losses         | None (night only)        |
| Patrol       | 【Former Employee】              | Slow Erosion growth on the same floor | None                     |
| Trading      | 【Merchant】                     | Currency                              | None                     |
| Farming      | 【Farmer】                       | Food                                  | None                     |
| Exploration  | 【Driver】                       | Random supplies                       | None                     |
| Organization | 【Teacher】                      | Slow Erosion throughout the hotel     | None                     |
| Chores       | Any                            | Small amount of food or currency      | None                     |

#### 4.3.3 Job Combination Effects

| Combination    | Condition                     | Effect                                             |
| -------------- | ----------------------------- | -------------------------------------------------- |
| Medical Team   | Doctor + Cook                 | Food efficiency +10%, treatment effectiveness +20% |
| Security Team  | Night Watch + Former Employee | Nighttime event losses -60%                        |
| Logistics Team | Merchant + Farmer + Driver    | Supply acquisition efficiency +30%                 |

#### 4.3.4 Job Assignment

**Interaction:** In the side-view interface, click a guest → select “Today’s Assignment” → open the available job list → select one.

**Job Output Rules: (values to be adjusted)**

- Each job produces output **once every half-day (Day/Night)**.
- Output type depends on the job: food/currency/facility durability/guest mental state.
- The guest’s **ability tag** affects job efficiency:
  - Perfect match (e.g. 【Cook】 → Cooking): output +20%
  - Mismatched but with basic capability (e.g. 【Doctor】 → Chores): base output
  - Completely mismatched (e.g. 【Night Watch】 → Cooking): output -20%
- The guest’s **Erosion level** also affects efficiency:
  - Green: base efficiency
  - Yellow: efficiency -10%
  - Red: efficiency +20% (but contaminates neighboring guests)

#### 4.3.3 Job Types and Output

| Job         | Suitable Tags                  | Output                                    | Cost                     |
| ----------- | ------------------------------ | ----------------------------------------- | ------------------------ |
| Cooking     | 【Cook】                         | Food (output = consumed amount + surplus) | Food ingredients         |
| Medical     | 【Doctor】                       | Reduce/prevent increase in guest Erosion  | None                     |
| Repair      | 【Engineer】【Carpenter】          | Restore facility durability               | Small amount of supplies |
| Night Watch | 【Night Watch】【Former Employee】 | Reduce nighttime event losses             | None (night only)        |
| Patrol      | 【Former Employee】              | Slow Erosion growth on the same floor     | None                     |
| Trading     | 【Merchant】                     | Currency                                  | None                     |
| Farming     | 【Farmer】                       | Food                                      | Water/time               |
| Chores      | Any                            | Small amount of food or currency          | None                     |

---

### 4.4 Erosion Level and Faction System

#### 4.4.1 Erosion Values

- **Range:** 0–100 (hidden value; not visible to the player)
- **Initial value:** Randomly between 0–40 when a guest moves in.
- **Factors affecting it:**

| Factor                                  | Direction | Example Magnitude |
| --------------------------------------- | --------- | ----------------- |
| Sharing a room with a red guest         | ↑         | +3–5 per night    |
| Being on the same floor as a red guest  | ↑         | +1–2 per night    |
| Experiencing a negative nighttime event | ↑         | +5–15             |
| Positive event / medical treatment      | ↓         | -3–8              |
| Long-term isolation (no contact)        | ↑         | +1–2 per day      |
| Sharing a room with a green guest       | ↓         | -1–2 per night    |

#### 4.4.2 Color Labels — Automatically Determined

| Color     | Erosion Range | Behavioral Characteristics                                          | Effect on Hotel                                                                              |
| --------- | ------------- | ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| 🟢 Green  | 0–30          | Normal human; stable and strongly empathetic                        | 100% work efficiency; Erosion accelerates when contaminated by red guests                    |
| 🟡 Yellow | 31–60         | Mildly eroded; occasionally behaves strangely                       | 90% work efficiency; slightly affects roommates (+1/night)                                   |
| 🔴 Red    | 61–100        | Severely eroded / pseudo-human; appears normal but has lost empathy | 120% work efficiency; contaminates roommates (+3–5/night) and same-floor guests (+1–2/night) |

#### 4.4.3 Player-Assigned Labels — Flag System

**Purpose:** Help players record their own deductions and memories and support strategic decision-making.

- Players can **at any time** manually label a guest as “suspected green / suspected yellow / suspected red.”
- These labels **do not affect any system judgment** and serve purely as the player’s personal notes.
- Labels can be changed or removed at any time.
- This is similar to the flag system in Minesweeper — it helps players manage information without replacing their judgment.

**Interaction:** Click a guest portrait → click the color marker button → choose “Mark Green / Mark Yellow / Mark Red / Remove.”

#### 4.4.4 Revealing the True Color

The true color is revealed in the following situations:

1. **Nighttime event exposure:** A guest performs an obviously “non-human” behavior during a nighttime event, such as talking to a wall or attacking another person. The event description directly reveals their true color.
2. **Using an item:** Use the “Psychological Analyzer” purchased from the Medal Shop to reveal the true value of a single guest.
3. **Extreme behavior:** When a guest’s Erosion reaches 80 or higher, they have a chance to trigger an obvious abnormal behavior animation/event, automatically revealing their status.

#### 4.4.5 Relationship Between Erosion and Endings

- At the end of Day 30, calculate the **number of surviving guests** and their **average Erosion level**.
- Lower average Erosion → higher human purity → better ending evaluation.
- Even if every guest survives, if everyone has become red, the ending is still: **“The hotel remains, but the people inside no longer recognize you.”**

---

### 4.5 Rooms and Management System

#### 4.5.1 Side-View Layout

The following is a demonstration framework. The final layout may not be perfectly regular, and there should also be a basement.

```text
       ┌──────────────────────────────────────────────────────┐
 Third │ [301]    [302]    [303]    [Storage Room]          │ ← Unlocked later
 Floor │                                                    │
       ├──────────────────────────────────────────────────────┤
Second │ [201]    [202]    [203]    [Public Bathroom]       │ ← Mid-game unlock
 Floor │                                                    │
       ├──────────────────────────────────────────────────────┤
 First │ [101]    [102]    [Lobby/Cafeteria]   [Kitchen]   │ ← Available initially
 Floor │                                                    │
       └──────────────────────────────────────────────────────┘
        ◀── Each room displays: guest portrait + name + color border ──▶
```

#### 4.5.2 Room Capacity and Unlocks

| Unlock Stage    | Available Rooms                        | Maximum Occupancy | Unlock Condition                                 |
| --------------- | -------------------------------------- | ----------------- | ------------------------------------------------ |
| Initial (Day 1) | 2 rooms (101, 102)                     | 4 people          | Available by default                             |
| After Day 5–7   | 3 rooms (+103/lobby renovation)        | 6 people          | 5 food + 2 currency                              |
| After Day 10–12 | 5 rooms (second floor unlocked)        | 10 people         | 15 food + 5 currency + 【Engineer】                |
| After Day 18–20 | 7 rooms (part of third floor unlocked) | 14 people         | 30 food + 10 currency + 【Engineer】 + 【Carpenter】 |

> *Values to be refined. Intended direction: fast, low-cost expansion early on; specific ability tags required mid-game; high costs late-game, forcing the player to consider whether expanding to maximum capacity is worthwhile.*

#### 4.5.3 Room Management Strategies

| Strategy           | Method                                 | Effect                                                                                |
| ------------------ | -------------------------------------- | ------------------------------------------------------------------------------------- |
| Isolate Red Guests | Place red guests alone in remote rooms | Reduces contamination of others but wastes room space                                 |
| Mixed Arrangement  | Green + Yellow in the same room        | Green can slowly reduce Yellow’s Erosion, but risks contamination                     |
| Close Off a Floor  | Put all red guests on the same floor   | Limits contamination to that floor, but accelerates Erosion for guests there          |
| Single Rooms       | Give high-risk guests their own rooms  | Prevents contamination of others but reduces efficiency due to lack of mutual support |

#### 4.5.4 Resource System

**Food:**

- **Consumption:** Daily (every 24 hours), based on total number of guests; 1 unit per person per day.
- **Production:** Cooking by 【Cook】, farming by 【Farmer】, and certain events.
- **Shortage consequence:** When food reaches 0, all guests gain +5 Erosion per day (hunger causes mental instability).

**Currency:**

- **Acquisition:** Job output (【Merchant】 trading, chores), event rewards.
- **Uses:** Unlock rooms, purchase supplies, handle specific events (e.g. exchange currency for food/items during “Merchant Arrives”).
- **Shortage consequence:** Cannot unlock rooms or purchase supplies, but there is no direct penalty.

**Other Resources (to be refined later):**

- Facility durability (maintained by 【Engineer】/【Carpenter】)
- Medicine (used by 【Doctor】 to treat injured guests)

#### 4.5.5 Item System — Newly Added

In addition to the original two basic resources, **Food** and **Currency**, an item system is added. Items can be obtained through:

1. Purchasing them with currency during Merchant events
2. Certain personal guest events
3. Rewards from specific nighttime/daytime events

| Item          | Acquisition               | Effect                                            | Price (Currency) |
| ------------- | ------------------------- | ------------------------------------------------- | ---------------- |
| First Aid Kit | Merchant purchase         | Reduce one selected guest’s Erosion by 8          | 3                |
| Calming Tea   | Merchant purchase         | Reduce Erosion throughout the hotel by 1 (once)   | 5                |
| Flashlight    | Merchant purchase         | Additional -10% nighttime event losses            | 2                |
| Old Radio     | Merchant purchase         | Listen to additional information and unlock clues | 4                |
| Toolbox       | Merchant / Engineer event | Engineer work efficiency +30% (one-time)          | 3                |

> The item system does not replace screening decisions or management strategy and only serves as an auxiliary system. Priority: P1. The complete item system may be omitted from the mid-term playable demo, with only the interface reserved for later implementation.

---

### 4.6 Eviction System

#### 4.6.1 Voluntary Eviction

- The player can choose to evict a guest at any time (click guest → choose “Ask Them to Leave”).
- After eviction, the guest leaves the hotel and their room becomes available.

#### 4.6.2 Eviction Reactions — Based on Guest Status

| Reaction             | Trigger                                        | Example Text                                                                                                   | Consequence                                                                                                   |
| -------------------- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- |
| **Leave Quietly**    | Green guest / low Erosion                      | “He packed a small bag without saying a word and left before dawn.”                                            | No additional cost. The diary records “XXX left the hotel.”                                                   |
| **Beg to Stay**      | Yellow guest / guest has an important ability  | “She knelt at the door and repeatedly said, ‘I can work. I can do anything. Please don’t kick me out…’”        | Reject → morale of other guests decreases (Erosion +2–3); Keep → consumes an additional 5 food as reassurance |
| **Attack / Madness** | Red guest / high Erosion                       | “His expression suddenly twisted. He grabbed the desk lamp and smashed it against the wall — he had gone mad!” | One random guest is injured (Erosion +5–10), or a facility is damaged (requires supplies to repair)           |
| **Spread Rumors**    | Specific personality (“Talkative,” “Vengeful”) | “He shouted in the lobby: ‘Do you know how the manager treats the old guests?’”                                | Increases difficulty of screening future visitors (false information mixed into descriptions)                 |

#### 4.6.3 Design Purpose of Eviction

- **It is not a “remove guest” button**, but a management decision with consequences.
- Evicting a red guest may trigger an attack and cause even greater losses.
- Evicting a green guest has no direct penalty but wastes the resources and time previously invested in recruiting them.
- The player must weigh **“keeping this dangerous person”** against **“taking the risk of kicking them out.”**

---

## 4.7 Event System

### Event Count

| Type                        | Quantity                   |
| --------------------------- | -------------------------- |
| Nighttime Events            | 28                         |
| Daytime Events (Regular)    | 18                         |
| Special NPC Events (P2)     | 8                          |
| Personal Guest Events       | 13                         |
| Continuous Narrative Chains | 6 chains (28 stages total) |

### 4.7.0 Global Event Rules

- All event-related costs use the two basic resources: **Food** and **Currency**. There is no separate “Medicine” resource.
- “Investigation” does not introduce an additional system; results are directly provided through event choices.
- Mechanics such as “Trust” and “Respect” do not exist; they are all converted into Erosion changes or guest-status changes.
- Special NPC events (marked P2) do not occupy regular visitor slots and are handled separately.

### 4.7.1 Nighttime Events — 28 Total

#### A. Medical / Health Events — 5

| ID  | Event             | Trigger                    | Description                                                                                                                       | Relevant Tag      | With Tag                                                                   | Without Tag                                          |
| --- | ----------------- | -------------------------- | --------------------------------------------------------------------------------------------------------------------------------- | ----------------- | -------------------------------------------------------------------------- | ---------------------------------------------------- |
| N01 | High Fever        | A guest is injured or cold | “In the middle of the night, XXX develops a persistent high fever. Their body is burning hot and they are speaking incoherently.” | 【Doctor】          | Doctor treats them; Erosion -3; recovers the next day                      | Guest Erosion +10; next-day efficiency -50%          |
| N02 | Seizure           | A Yellow guest exists      | “XXX suddenly begins convulsing violently in the room, foaming at the mouth.”                                                     | 【Doctor】          | Doctor successfully controls it; Erosion -4; costs 3 currency for medicine | Guest Erosion +15; other roommates +3                |
| N03 | Mass Vomiting     | Food supply below 3 days   | “Late at night, waves of vomiting come from the cafeteria — several people have food poisoning.”                                  | 【Doctor】【Cook】    | Doctor + Cook cooperate; hotel-wide Erosion +1; costs 5 food + 5 currency  | Hotel-wide Erosion +5 for 2 days                     |
| N04 | Mental Breakdown  | Guest Erosion >70          | “XXX begins screaming and repeatedly bangs their head against the wall, as if they are seeing something.”                         | 【Doctor】【Teacher】 | Doctor or Teacher successfully calms them; Erosion -6; costs 3 currency    | Guest Erosion +20; same-floor guests +5              |
| N05 | Old Injury Recurs | Elderly or weak guest      | “XXX curls up on the bed clutching an old injury, groaning in pain.”                                                              | 【Doctor】          | Costs 3 currency for medicine; recovers the next day                       | Guest Erosion +8; efficiency becomes zero for 2 days |

#### B. Disturbance / Contamination Events — 6

| ID  | Event                    | Trigger            | Description                                                                                                           | Relevant Tag                   | With Tag                                                        | Without Tag                                                  |
| --- | ------------------------ | ------------------ | --------------------------------------------------------------------------------------------------------------------- | ------------------------------ | --------------------------------------------------------------- | ------------------------------------------------------------ |
| N06 | Whispering               | A Red guest exists | “Whispers come from the end of the corridor, but there is clearly no one there.”                                      | 【Former Employee】【Night Watch】 | Former Employee calms the guests; no Erosion change             | All guests Erosion +2                                        |
| N07 | Note Under the Door      | Random             | “Someone has slipped a note under a door. It reads: ‘They are already among you.’”                                    | 【Night Watch】                  | Night Watch investigates and discovers it is a prank; no effect | Hotel-wide Erosion +3; individual guest +8                   |
| N08 | Night Wandering          | A Red guest exists | “XXX opens the doors of other rooms in the middle of the night and stands there watching the sleeping people inside.” | 【Night Watch】                  | Warning and intervention; observed guest Erosion +2             | Observed guest Erosion +5; Red guest +3                      |
| N09 | Mass Sleepwalking        | ≥3 Yellow guests   | “Several guests simultaneously get up in the middle of the night and walk down the corridor in a line.”               | 【Night Watch】                  | Wake and escort each person back; each gains +2 Erosion         | Each gains +8; hotel-wide +2                                 |
| N10 | Reflection in the Mirror | Hotel has mirrors  | “A face appears in the corridor mirror — but it does not belong to anyone present.”                                   | 【Former Employee】              | Former Employee removes the mirror; no effect                   | Hotel-wide Erosion +3                                        |
| N11 | Voice in the Walls       | Random             | “Someone is knocking on the wall. Once, twice — but the neighboring room is empty.”                                   | 【Engineer】                     | Engineer checks by opening the wall — nothing is found          | Hotel-wide Erosion +4; room becomes uninhabitable for 2 days |

#### C. Abnormal Behavior Events — 5

| ID  | Event                   | Trigger             | Description                                                                                       | Tag               | With Tag                                                   | Without Tag                        |
| --- | ----------------------- | ------------------- | ------------------------------------------------------------------------------------------------- | ----------------- | ---------------------------------------------------------- | ---------------------------------- |
| N12 | Talking to Oneself      | Yellow guest exists | “XXX sits beside the bed and talks to the empty wall for two full hours.”                         | 【Doctor】          | Doctor intervenes and talks with them; Erosion -5          | Roommates +3; guest +5             |
| N13 | Compulsive Organization | Yellow guest exists | “XXX repeatedly rearranges objects in the room — placing the same pair of shoes dozens of times.” | 【Former Employee】 | Former Employee stays with them; Erosion -2                | Guest +5; roommates +2             |
| N14 | Refusing to Sleep       | Yellow guest exists | “XXX refuses to close their eyes, saying, ‘Whenever I close my eyes, I see them.’”                | 【Teacher】         | Teacher talks with them until they fall asleep; Erosion -4 | Guest +8; next-day efficiency -30% |
| N15 | Repetitive Writing      | Yellow guest exists | “XXX repeatedly writes the same word on paper, filling more than a dozen sheets.”                 | 【Teacher】         | Teacher interprets the writing and obtains a clue          | Guest +5; papers destroyed         |
| N16 | Strange Food            | Yellow guest exists | “XXX arranges their food into strange shapes before they are willing to eat it.”                  | 【Cook】            | Cook prepares the food again; Erosion -2                   | Guest +5                           |

#### D. Facility / Environment Events — 5

| ID  | Event               | Trigger        | Description                                                                               | Tag        | With Tag                                                   | Without Tag                                                                                        |
| --- | ------------------- | -------------- | ----------------------------------------------------------------------------------------- | ---------- | ---------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| N17 | Burst Pipe          | Random         | “Water is running in the bathroom — a pipe has burst and water is leaking everywhere.”    | 【Engineer】 | Engineer repairs it; costs 2 currency                      | Facility durability -30%; hotel-wide Erosion +2                                                    |
| N18 | Electrical Fire     | Random         | “A short circuit occurs. A spark flashes through the corridor.”                           | 【Engineer】 | Engineer cuts the circuit and repairs it; costs 1 currency | Facility durability -20%; hotel-wide Erosion +3                                                    |
| N19 | Broken Window       | Stormy weather | “The storm blows out a window, letting cold air and rain inside.”                         | 【Engineer】 | Engineer makes a temporary repair; costs 1 currency        | Guests in the room +8 Erosion; facility durability -15%                                            |
| N20 | Heating Failure     | Random         | “In the middle of the night, the heating pipes stop making noise — there is no heat.”     | 【Engineer】 | Engineer checks and discovers the valve has been closed    | Hotel-wide Erosion +5                                                                              |
| N21 | Collapsed Staircase | After Day 20   | “A tremendous crash — the staircase leading to the second floor has partially collapsed.” | 【Engineer】 | Costs 8 food + 5 currency + 1 day to repair                | Second and third floors become inaccessible; upper-floor guests gain +5 Erosion/day until repaired |

#### E. Invasion / External Threat Events — 4

| ID  | Event                | Trigger | Description                                                                 | Tag           | With Tag                                                                                      | Without Tag                                                 |
| --- | -------------------- | ------- | --------------------------------------------------------------------------- | ------------- | --------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| N22 | Stranger at the Door | Random  | “Someone knocks on the hotel door late at night, claiming to be a refugee.” | 【Night Watch】 | Night Watch interrogates them, discovers they are a pseudo-human, and refuses entry           | If not rejected, a new visitor with Erosion ≥50 enters      |
| N23 | Pack of Wild Dogs    | Random  | “A pack of wild dogs circles the hotel, growling.”                          | 【Night Watch】 | Night Watch drives them away; no effect                                                       | Exploration unavailable the next day; hotel-wide Erosion +2 |
| N24 | Radio Static         | Random  | “The radio suddenly comes alive with indistinct voices and screams.”        | 【Engineer】    | Engineer tunes it and hears half a sentence: “…they are waiting for the Purification Signal…” | Hotel-wide Erosion +5; Yellow guests +10                    |

#### F. Erosion Intensification Events — 3

| ID  | Event             | Trigger                    | Description                                                                                            | Tag                            | With Tag                                                       | Without Tag                                                                                 |
| --- | ----------------- | -------------------------- | ------------------------------------------------------------------------------------------------------ | ------------------------------ | -------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| N25 | Erosion Outbreak  | ≥3 Red guests              | “XXX’s Erosion suddenly worsens — their body twists in an unnatural way.”                              | 【Doctor】                       | Costs 5 currency; Erosion -10; temporarily stabilized          | Guest becomes a complete pseudo-human; hotel-wide Erosion +15; must be evicted the next day |
| N26 | Spreading Shadow  | ≥2 Red guests              | “Darkness gathers into a physical shape in the corridor and slowly moves.”                             | 【Night Watch】                  | Night Watch uses a torch to disperse it; hotel-wide Erosion +2 | Random 2 guests +15 Erosion                                                                 |
| N27 | Erosion Resonance | Red + Yellow on same floor | “All the lights in the hotel flicker several times — an invisible force spreads throughout the floor.” | 【Former Employee】【Night Watch】 | Former Employee guides an evacuation; floor Erosion +3         | All guests on the floor +8; Red guests +12                                                  |

---

### 4.7.2 Daytime Events — 18 Total

#### A. Regular Daytime Events — 9

| ID  | Event                     | Trigger                   | Description                                                                                                              | Option A                                                                | Option B                                                        | Option C                                         |
| --- | ------------------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------- | --------------------------------------------------------------- | ------------------------------------------------ |
| D07 | Guest Argument            | Green + Red on same floor | “XXX and XXX are loudly arguing in the corridor. One says, ‘He watches me every night.’ The other says, ‘You’re crazy.’” | A. Intervene and mediate (3 food)                                       | B. Move them to different floors                                | C. Ignore it (hotel-wide Erosion +1)             |
| D08 | Food Distribution Dispute | Low food supply           | “Someone in the cafeteria complains: ‘Why does he get more than us?’”                                                    | A. Equal rationing (everyone eats slightly less; hotel-wide Erosion +1) | B. Punish the troublemaker (Erosion +2)                         | C. Go without food yourself (Manager Erosion +2) |
| D09 | Rumor Spreads             | Yellow guest exists       | “‘They say XXX is actually a pseudo-human.’ The rumor spreads throughout the hotel.”                                     | A. Public investigation (half a day; hotel-wide Erosion -1)             | B. Suppress the rumor (requires 【Teacher】, 2 food)              | C. Ignore it (hotel-wide Erosion +2)             |
| D10 | Couple's Argument         | Random                    | “A couple is loudly arguing in a public area. The woman cries, ‘You’ve changed.’”                                        | A. Mediate (requires 【Teacher】, 2 food)                                 | B. Give them a private room (1 empty room)                      | C. Ignore it                                     |
| D11 | Bullying Incident         | Elderly/weak guest exists | “Several guests mock XXX because of their abnormal behavior last night.”                                                 | A. Stop the bullying (hotel-wide Erosion -2)                            | B. Comfort the victim privately (2 currency; victim Erosion -3) | C. Ignore it (hotel-wide Erosion +3)             |
| D12 | Merchant Arrives          | Random                    | “An old van stops outside the hotel — it is a wandering merchant.”                                                       | A. Trade (currency for supplies)                                        | B. Exchange information for supplies (requires 【Driver】)        | C. Send them away                                |
| D13 | Supply Donation           | Random                    | “A guest voluntarily offers to share the food they have saved.”                                                          | A. Accept (hotel-wide Erosion -1)                                       | B. Praise them but let them keep it                             | —                                                |
| D14 | Theft                     | High food/currency stock  | “Someone has stolen food from the warehouse.”                                                                            | A. Search the entire hotel (hotel-wide Erosion +2)                      | B. Set a trap (requires 【Night Watch】)                          | C. Ignore it (lose 5 food)                       |
| D15 | Anonymous Complaint       | Random                    | “A letter has been slipped under the manager’s office door, listing several complaints about hotel management.”          | A. Respond publicly (hotel-wide Erosion -2)                             | B. Investigate who wrote it                                     | —                                                |

#### B. Weather / Environment Events — 5

| ID  | Event              | Trigger      | Description                                                                                       | Option A                                                                                             | Option B                                                                       |
| --- | ------------------ | ------------ | ------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| D16 | Ash Snow           | Random       | “Gray-black snow begins falling from the sky — this is not ordinary snow.”                        | A. Seal doors and windows (requires 【Engineer】, 2 food)                                              | B. Warn guests not to go outside (hotel-wide Erosion -1)                       |
| D17 | Dense Fog          | Random       | “A dense fog surrounds the hotel, reducing visibility to less than five meters.”                  | A. No one is allowed outside                                                                         | B. Send someone to investigate (requires 【Night Watch】; that guest +3 Erosion) |
| D18 | Earthquake         | Random       | “The ground shakes for several seconds — an aftershock, or something else?”                       | A. Emergency evacuation (hotel-wide Erosion +2)                                                      | B. Inspect the building (requires 【Engineer】, 3 currency)                      |
| D19 | Strange Sound      | Random       | “A deep horn sounds from the sky and continues for a full minute.”                                | A. Send the Driver to investigate (requires 【Driver】; successful → clue, failed → Driver +5 Erosion) | B. Reassure guests (hotel-wide Erosion -2)                                     |
| D20 | Green Thunderstorm | After Day 15 | “Green lightning appears during the storm. Each flash illuminates strange shadows on the ground.” | A. Everyone enters the basement (hotel-wide Erosion +1)                                              | B. Strengthen the night watch (hotel-wide Erosion -1)                          |

#### C. Internal Management Events — 4

| ID  | Event               | Trigger                                    | Description                                                                                        | Option A                                                        | Option B                                                     |
| --- | ------------------- | ------------------------------------------ | -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- | ------------------------------------------------------------ |
| D21 | Guest Request       | Guest Erosion <30                          | “XXX comes to the office and says, ‘I want to help you.’”                                          | A. Give them additional responsibilities (Trust +1, Erosion -1) | B. Thank them but decline                                    |
| D22 | Birthday Discovery  | Specific guest                             | “You discover in XXX’s file that today is their birthday.”                                         | A. Hold a small gathering (5 food; hotel-wide Erosion -2)       | B. Give them a small private gift (2 food; guest Erosion -5) |
| D23 | Prayer Gathering    | Teacher exists                             | “Several guests organize a small prayer meeting in a public area.”                                 | A. Support it (hotel-wide Erosion -1)                           | B. Stop it (hotel-wide Erosion +3)                           |
| D24 | Old Diary Discovery | Random (Cleaner or Former Employee exists) | “While cleaning a room, an old diary is discovered — it belonged to someone who once stayed here.” | A. Read it (Erosion +1; obtain a clue)                          | B. Seal it away                                              |

---

### 4.7.3 Special NPC Events — P2 Priority

These events do not occupy regular visitor slots (the normal rhythm of 1–3 visitors every 1–3 days). If accepted, they occupy one empty room. If there is no empty room, an alternative option is automatically triggered.

| ID  | Event                  | Trigger      | Description                                                                                                         | Option A                                                                       | Option B                                                                                         | Option C                                                                      |
| --- | ---------------------- | ------------ | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------- |
| D01 | Family of Three        | After Day 3  | “A married couple and their silent daughter appear at the hotel. The wife says their car broke down.”               | A. Take them in (1 room; triggers follow-up chain)                             | B. Give them supplies and send them away (5 food + 3 currency)                                   | C. Reject (hotel-wide Erosion +2)                                             |
| D02 | Injured Police Officer | Random       | “A man wearing a police uniform, his arm bleeding, knocks on the door.”                                             | A. Take him in (1 room; costs 5 currency; has 【Night Watch】 at 70% efficiency) | B. Give him bandages (3 currency; leaves next day)                                               | C. Reject (hotel-wide Erosion +2)                                             |
| D03 | War Correspondent      | Random       | “A woman says she is a war correspondent and wants to interview the survivors of the apocalypse.”                   | A. Accept (1 room; +1 Medal at settlement)                                     | B. Reject (hotel-wide Erosion +1)                                                                | —                                                                             |
| D04 | Silent Boy             | Random       | “A boy stands at the door without speaking, staring directly at you. Around his neck is a sign that says ‘Please.’” | A. Take him in (1 room; triggers disappearance event chain)                    | B. Give him food and send him away (3 food)                                                      | C. Reject (hotel-wide Erosion +2)                                             |
| D05 | Lost Old Man           | Random       | “An old man stands by the road, saying he cannot find his family.”                                                  | A. Let him stay overnight (1 room; obtain old photo clue the next day)         | B. Give directions (no cost)                                                                     | C. Reject (hotel-wide Erosion +1)                                             |
| D06 | Man in a Suit          | After Day 10 | “A man in a suit and tie smiles at the door. His clothes are spotless — impossible in the apocalypse.”              | A. Reject (hotel-wide Erosion -1)                                              | B. Interrogate then reject (requires 【Night Watch】/【Former Employee】; obtain business-card clue) | C. Accept (high probability of triggering negative events; P2 implementation) |
| D29 | Rumor of the Signal    | After Day 10 | “Someone heard on the radio: ‘The Purification Signal may arrive after 30 days.’”                                   | A. Affirm it (hotel-wide Erosion -2)                                           | B. Respond cautiously (hotel-wide Erosion -1)                                                    | C. Deny it (hotel-wide Erosion +3)                                            |
| D30 | Escapee                | After Day 15 | “A survivor from a neighboring town arrives, saying the town has been ‘completely eroded.’”                         | A. Take them in + interrogate (1 room; obtain clue T03; hotel-wide Erosion +2) | B. Isolate and observe (1 room; release after 3 days; hotel-wide Erosion -1)                     | —                                                                             |

---

### 4.7.4 Personal Guest Events — 13 One-Time Events

| ID  | Trigger                                         | Description                                                                                                       | Effect                                                                                    |
| --- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| P01 | Guest is 【Teacher】 and Erosion <30              | “XXX begins telling stories to other guests in the common area — about how beautiful the outside world once was.” | Hotel-wide Erosion -2                                                                     |
| P02 | Guest is 【Cook】 and Erosion <40                 | “XXX uses limited ingredients to prepare a proper hot meal today.”                                                | Hotel-wide Erosion -2; food efficiency temporarily +10% for 1 day                         |
| P03 | Guest is 【Doctor】 and Erosion <50               | “XXX proactively asks about the physical condition of every guest.”                                               | Random 2 guests Erosion -3                                                                |
| P04 | Random guest Erosion >60                        | “XXX suddenly says: ‘I feel like I can’t remember who I am anymore.’”                                             | Guest Erosion -8 (regains clarity) or +15 (breakdown), randomly                           |
| P05 | Random guest Erosion <30 and has stayed 5 days  | “XXX says: ‘Thank you for taking me in. I will always remember.’”                                                 | After this guest leaves, gain bonus points at settlement                                  |
| P06 | A 【Former Employee】 is present                  | “XXX discovers strange scratches in the corner while cleaning — they look like writing.”                          | Obtain a clue about the origin of Erosion                                                 |
| P07 | Random guest Erosion <20                        | “XXX plants a small flower in the courtyard.”                                                                     | Hotel-wide Erosion -1 for 3 days                                                          |
| P08 | A 【Driver】 is present                           | “XXX says: ‘I saw something outside… but I’m not sure if I should tell you.’”                                     | Choice: Ask further (obtain information or +3 Erosion) / Do not ask                       |
| P09 | Random guest Erosion >50                        | “XXX sings a children’s song today — the melody is familiar, but the lyrics are wrong.”                           | Same-floor guests +2 Erosion                                                              |
| P10 | A 【Merchant】 is present                         | “XXX takes out some privately stored goods — ‘I got them through a special channel.’”                             | Spend currency to purchase rare items                                                     |
| P11 | Random guest Erosion <40 and has stayed 10 days | “XXX says: ‘I want to protect this place.’”                                                                       | Guest gains the “Guardian” status — prioritizes protecting others during nighttime events |
| P12 | 【Night Watch】 exists and a Red guest exists     | “XXX volunteers for night watch — ‘I don’t trust those newcomers.’”                                               | Nighttime event defense +1 level                                                          |
| P13 | Random guest Erosion >80                        | “XXX draws a door on the wall with their finger — and then tries to open it.”                                     | Guest +3 Erosion; roommates +3 Erosion                                                    |

---

### 4.7.5 Continuous Narrative Chains — 6 Chains

#### Chain 1: Diary of the Silent One — 5 Days

| Stage | Day                   | Description                                                                                              | Choices and Effects                                                                                                |
| ----- | --------------------- | -------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| 1     | Day 1 after moving in | “XXX has barely spoken since moving in and simply keeps writing something in their notebook.”            | No action                                                                                                          |
| 2     | Day 3                 | “XXX’s notebook seems to be getting thicker. Someone saw them still writing in the middle of the night.” | Option: Send 【Former Employee】 to observe (Erosion +1; observe symbols)                                            |
| 3     | Day 5                 | “XXX begins refusing meals, saying, ‘I don’t have time.’”                                                | A. Force them to eat (1 food; guest Erosion +2 but relationship improves) / B. Leave them alone (guest Erosion +5) |
| 4     | Day 7                 | “XXX goes to the basement in the early morning, carrying the notebook.”                                  | If symbol clue obtained: send 【Night Watch】 to follow (obtain clue T04) / Do not follow (hotel-wide Erosion +2)    |
| 5     | Day 8                 | “XXX stops talking and stops writing. They simply sit by the window, looking outside.”                   | Investigate the room → obtain Truth Item T01; XXX’s Erosion permanently fixed at 60                                |

#### Chain 2: Mysterious Voice in the Basement — 5 Days

| Stage | Day   | Description                                                                                         | Choices and Effects                                                                                                                                     |
| ----- | ----- | --------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1     | Day 1 | “A guest says: ‘There seems to be a voice coming from the basement.’”                               | No action                                                                                                                                               |
| 2     | Day 2 | “The voice becomes clearer — it sounds like someone asking for help, or calling a particular name.” | Hotel-wide Erosion +1                                                                                                                                   |
| 3     | Day 3 | “XXX volunteers to investigate the basement (they are a 【Former Employee】).”                        | A. Agree (bring back a box of old objects) / B. Refuse (hotel-wide Erosion +2)                                                                          |
| 4     | Day 4 | “The voice disappears — but the basement lights turn on by themselves every night.”                 | A. Seal the basement entrance (requires 【Engineer】, 3 food) / B. Continue investigating (hotel-wide Erosion +2)                                         |
| 5     | Day 5 | “A hidden passage leading underground is discovered beneath XXX’s bed.”                             | A. Discover an old shelter (obtain a large amount of supplies) / B. Discover records of “another group of guests” (hotel-wide Erosion +10; obtain clue) |

#### Chain 3: The Gradually Disappearing Guest — 4 Days

| Stage | Day   | Description                                                                                                | Choices and Effects                                                                                                 |
| ----- | ----- | ---------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| 1     | Day 1 | “XXX stops participating in group activities and always stays alone.”                                      | No action                                                                                                           |
| 2     | Day 2 | “XXX walks toward you in the corridor, but does not look at you — they are looking at the air beside you.” | Same-floor Erosion +1                                                                                               |
| 3     | Day 3 | “A person’s voice can be heard from XXX’s room — but XXX lives alone.”                                     | A. Eavesdrop (Erosion +2) / B. Knock and interrupt                                                                  |
| 4     | Day 4 | “XXX is gone. The only thing left in the room is a note: ‘They finally found me.’”                         | Permanently lose the guest; hotel-wide Erosion +3 — if there was no intervention within 3 days, the ending is fixed |

#### Chain 4: Contaminated Supplies — 3 Days

| Stage | Day   | Description                                                                                                       | Choices and Effects                                                                                       |
| ----- | ----- | ----------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| 1     | Day 1 | “Today’s food tastes strange — but you cannot say exactly what is wrong.”                                         | No action                                                                                                 |
| 2     | Day 2 | “Several guests develop mild diarrhea.”                                                                           | Hotel-wide Erosion +1                                                                                     |
| 3     | Day 3 | “XXX (【Cook】) discovers that something has been mixed into the food — it is not poison, but something like… ash.” | A. Investigate (requires 【Doctor】; obtain Truth Item T02) / B. Destroy the inventory (lose 15 food; safe) |

#### Chain 5: The Uninvited Child — 4 Days

| Stage | Day   | Description                                                                                                                   | Choices and Effects                                                                                                          |
| ----- | ----- | ----------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| 1     | Day 1 | “A child appears alone at the hotel entrance. He says his name is A-Yuan and that his parents have ‘disappeared.’”            | A. Take him in (1 room; if 【Teacher】/【Doctor】 exists, hotel-wide Erosion -2) / B. Send him elsewhere (hotel-wide Erosion +2) |
| 2     | Day 2 | “A-Yuan starts playing hide-and-seek with the other guests — but often hides in strange places, such as deep inside closets.” | If any guest has Erosion >40, A-Yuan avoids them (hotel-wide Erosion +1)                                                     |
| 3     | Day 3 | “A-Yuan says: ‘The walls here can talk. They told me… someone should not be here.’”                                           | A. Ask who he means → points to a Yellow guest (can be verified) / B. Reassure him (hotel-wide Erosion +1)                   |
| 4     | Day 4 | “A-Yuan disappears. A note is left on his bed: ‘Thank you. I’m going to find them.’”                                          | Hotel-wide Erosion +3 — if you asked who he meant on Day 3, that Yellow guest Erosion -5                                     |

#### Chain 6: Diary Inside the Wall — 5 Days

| Stage | Day   | Description                                                                                                                         | Choices and Effects                                                                                                           |
| ----- | ----- | ----------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| 1     | Day 1 | “XXX (【Former Employee】) discovers an old diary that falls out of a crack in the wall while cleaning.”                              | A. Read it (Erosion +2) / B. Destroy it                                                                                       |
| 2     | Day 2 | “The diary reads: ‘They disguise themselves as us; we disguise ourselves as ourselves.’”                                            | Hotel-wide Erosion +1                                                                                                         |
| 3     | Day 3 | “The diary mentions: ‘There is a door on the wall of Room 306 on the third floor that does not exist.’”                             | Investigate Room 306 (requires third floor unlocked) → discover a loose wall panel                                            |
| 4     | Day 4 | “Behind the wall panel is an old list containing the names of everyone who has ever stayed here. Some names have been crossed out.” | Compare against the current guest list (may discover an anomaly)                                                              |
| 5     | Day 5 | “One guest’s name appears on the old list — but that list was written five years ago.”                                              | Identify that guest as an anomalous presence. The guest subsequently disappears; obtain Truth Item T07. Hotel-wide Erosion +3 |

### 4.7.6 Event Trigger Logic

- **At least one nighttime event is guaranteed every day** (an event must occur every night).
- Daytime events trigger probabilistically (approximately 40–60% of daytime phases have an event).
- Events are correlated with the current guest color composition, current day, and previous event history rather than being completely random.
- For example, after Day 20, when there are many Red guests, the probability of “Nightmare Spread” appearing increases significantly.

### 4.7.7 Relationship Between Events and Ability Tags

- Events are the core vehicle for **ability-tag-driven survival responses**.
- **Having the corresponding tag ≠ the event automatically disappears**. Instead, the consequences are reduced.
- This ensures that even when the player recruits the right people, events still require resources to handle — they are simply less painful.
- Similar to *This War of Mine*: having a cook can save ingredients, but even without a cook, people still need to eat.

---

## 4.8 Save System

- **Save timing:** Automatically save every Dawn after events are settled.
- **Number of saves:** Three independent save slots. Each slot keeps only the most recent record, preventing players from using save-scumming to bypass the consequences of decisions.
- **Loading:** When starting the game, manually select a save slot and load it.
- **No manual save/load functionality.** Specific save slots can be manually deleted but cannot be copied.

---

# 5. Art Style Positioning

### 5.1 Core Direction

**A rough-line Cthulhu art style inspired by *****Darkest Dungeon***

**Core keywords:** Rough hand-drawn linework + thick painted texture + high-contrast dark tones + indescribable Cthulhu horror

> *Note: Most major art assets will be generated by the art team using AI. Some generated examples have already achieved good results.*

### 5.2 Style References

| Reference                 | Elements Borrowed                                                                                                                  |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| *Darkest Dungeon*         | Rough hand-drawn lines, thick-painted coloring, distorted/exaggerated character expressions, dark fantasy visual tone              |
| Cthulhu Mythos visuals    | Hints of indescribable forms, restrained use of tentacles/multiple limbs/multiple eyes, visual contrast between madness and sanity |
| Linework + Thick Painting | Preserve the roughness of hand-drawn lines while using thick painting to increase texture and atmospheric depth                    |

### 5.3 Specific Visual Elements

| Element             | Approach                                                                                                                                                                                                                                                                                           |
| ------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Overall Palette     | High-contrast dark tones: dark brown, dark red, dark green, pale white, gray-black. Lighting mainly comes from one-sided overhead lights/candlelight to create a theatrical feeling.                                                                                                               |
| Line Style          | Rough, unpolished hand-drawn lines with slight jitter and exaggeration. Avoid overly clean and smooth vector-art aesthetics.                                                                                                                                                                       |
| Character Portraits | Facial features outlined with rough lines and thick-painted coloring; expressions have the distortion and tension of *Darkest Dungeon*; solid or dark backgrounds strengthen the visual impact.                                                                                                    |
| Showing Erosion     | Avoid relying on gore. Instead show Erosion through uneven pupil sizes, distorted facial shadows, abnormal smile curves, unnatural jitter in outlines, and unreasonable shadows/dark patterns appearing on the body.                                                                               |
| Hotel Environment   | Rough-line interior cross-sections with strong light/dark contrast (candlelight alternating with shadows). Walls/floors use hand-drawn textures and stains similar to *Darkest Dungeon*. Local details can be “wrong,” such as nonexistent doors or distorted shadow outlines, hinting at Erosion. |
| UI Style            | Parchment/old-paper textured background, bold handwritten-style fonts, thick-line borders with aged textures, hand-drawn doodle-like buttons; overall “rough but unified.”                                                                                                                         |
| Color Labels        | Green/yellow/red borders integrated into the rough-line style, with an aged treatment and reduced saturation to harmonize with the overall palette.                                                                                                                                                |

### 5.4 Differences from *Darkest Dungeon*

| Dimension         | *Darkest Dungeon*                      | This Project                                                                      |
| ----------------- | -------------------------------------- | --------------------------------------------------------------------------------- |
| Theme             | Medieval fantasy dungeon exploration   | Near-future post-apocalyptic hotel survival                                       |
| Environment       | Dungeons, wilderness, castles          | Cross-section of an abandoned hotel (everyday space)                              |
| Source of Horror  | Monsters, combat, psychological stress | Uncanny everyday life; “people who appear normal”                                 |
| Visual Difference | European medieval clothing/armor       | Modern/near-future everyday clothing, using the same stylized rough-line approach |

> **Core difference:** Bring the visual language of *Darkest Dungeon* — rough lines, dark tones, thick painting — into an **everyday space (a hotel)**. Use the same art style to depict what should be an ordinary environment and ordinary people, while allowing subtle signs that something is wrong. This itself is the visual expression of the “Erosion” theme.

### 5.5 Art Production Scope

| Level                                | Environment                                                                              | Characters                                                                 | Estimated Effort |
| ------------------------------------ | ---------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ---------------- |
| Minimum Viable (available in Week 2) | Solid-color room grids with rough-line drawings + textured backgrounds                   | Portraits only (Darkest Dungeon-style thick painting) + color borders      | Low              |
| Ideal (completed by Week 4)          | Detailed rough-line hand-drawn interior cross-section with furniture and lighting layers | Portraits + small side-view standing silhouettes in rough-line style       | Medium           |
| Perfect (if time allows)             | Full Darkest Dungeon-level environment detail + dynamic lighting + environmental effects | Multi-frame character animations (walking/sitting/lying/abnormal behavior) | High             |

---

# 6. Game Flow and Pacing

### 6.1 Complete Run Flow

```text
Start New Game
    │
    ▼
Day 1 · Dawn: First visitors arrive (2–3 people)
    │
    ▼ Player screens, recruits, and assigns rooms
Day 1 · Day: Assign jobs (tutorial)
    │
    ▼
Day 1 · Night: First nighttime event (gentle)
    │
    ▼
Day 2 · Dawn: Save, update log, begin a new day
    │
    ▼
  ... (repeat for 30 days)
    │
    ▼
End of Day 30 · Night: 30 days reached, immediately settle
    │
    ▼
Settlement Screen: Survivors × Average Erosion × Misjudgment Rate = Medal Count
    │
    ▼
Ending Text (Good / Normal / Bad)
    │
    ▼
Medal Shop / New Game+
```

### 6.2 Pacing Goals

- **Days 1–3:** Player builds the initial team and learns the basic controls.
- **Days 4–10:** Pressure gradually increases. The first Yellow guests appear, and the player begins paying more attention to room management.
- **Days 11–20:** Chaos period. Red guests appear, and eviction/isolation decisions become more frequent.
- **Days 21–30:** Final challenge. Resources become scarce, and the objective becomes simply **“survive.”**

### 6.3 Estimated Run Length

| Stage     | Days        | Estimated Real Time             |
| --------- | ----------- | ------------------------------- |
| Early     | Days 1–5    | 5–8 minutes                     |
| Early-Mid | Days 6–10   | 6–10 minutes                    |
| Mid       | Days 11–20  | 12–18 minutes                   |
| Late      | Days 21–30  | 8–12 minutes                    |
| **Total** | **30 days** | **Approximately 30–45 minutes** |

---

# 7. Settlement and Replayability

### 7.1 Settlement Metrics

| Metric                | Description                                                                     |
| --------------------- | ------------------------------------------------------------------------------- |
| Number of Survivors   | Number of guests still in the hotel at settlement                               |
| Average Erosion       | Average Erosion level of all surviving guests                                   |
| Misjudgment Rate      | Percentage of player-assigned labels that do not match the system’s true colors |
| Highest-Erosion Guest | Name of the surviving guest with the highest Erosion                            |
| Lowest-Erosion Guest  | Name of the surviving guest with the lowest Erosion                             |

### 7.2 Ending Determination

| Ending        | Condition                                                                                 | Tone                                                                                                                                                                                                                                                                          |
| ------------- | ----------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Good Ending   | Survivors ≥5 and average Erosion <40                                                      | “The Purification Signal has arrived. You open the hotel doors, and sunlight floods the corridor — you made it.”                                                                                                                                                              |
| Normal Ending | Survivors ≥3 and average Erosion <60                                                      | “The signal has arrived. Some people still recognize you; some no longer do. But the hotel is still here.”                                                                                                                                                                    |
| Bad Ending    | Survivors <3 or average Erosion ≥60                                                       | “When the signal arrived, there was no one left in the hotel who recognized you.”                                                                                                                                                                                             |
| True Ending   | Good-ending conditions + ≥5 Truth Items collected + at least one complete narrative chain | Additional text: “You finally pieced together the fragments — Erosion never came from outside. It has always been here. In the walls, in the cracks, in the silence of every person. The Purification Signal will never come. But you survived, carrying the truth with you.” |

### 7.3 Medal Shop — Tentative, Low Priority

After settlement, the player receives a number of medals based on their performance and can purchase items from the shop:

| Item          | Price (Medals) | Effect                                                                            |
| ------------- | -------------- | --------------------------------------------------------------------------------- |
| First Aid Kit | 3              | In the next game, reduce one selected guest’s Erosion by 8 (one-time)             |
| Calming Tea   | 5              | In the next game, reduce hotel-wide Erosion by 1 when used (one-time)             |
| Flashlight    | 2              | In the next game, reduce nighttime event losses by an additional 10% (entire run) |
| Old Radio     | 4              | In the next game, listen to information to unlock clues (entire run)              |
| Toolbox       | 3              | In the next game, Engineer work efficiency +30% (one-time)                        |

> Items can be exchanged for medals in the Medal Shop. Normal currency can also be used to purchase the same items during Merchant events at different prices (approximately 1.5–2× the medal price), providing multiple acquisition paths.

### 7.4 High-Difficulty Events for Multiple Playthroughs — If Time Allows

Optional restrictions unlocked after completing the game:

| Restriction                       | Effect                                                                                                      |
| --------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| “Every Night Must Cost Something” | Every nighttime event must include at least one negative effect that cannot be completely avoided           |
| “Double Eviction Cost”            | All numerical consequences of evicting guests are ×2                                                        |
| “Half Supplies”                   | All starting supplies and production are halved                                                             |
| “Windowless Rooms”                | Guests gain an additional +1 Erosion every day due to the psychological pressure of an enclosed environment |

---

# 8. Development Schedule Recommendations

### 8.1 Four-Week Sprint Plan

| Phase      | Time      | Core Goal             | Deliverables                                                                                          |
| ---------- | --------- | --------------------- | ----------------------------------------------------------------------------------------------------- |
| **Week 1** | Day 1–7   | Core Prototype        | Paper prototype validation + core gameplay loop demo (screening → accommodation → events → feedback)  |
| **Week 2** | Day 8–14  | Playable Demo         | **Mid-stage playable version:** 15 days of playable content + basic side-view UI + basic event system |
| **Week 3** | Day 15–21 | Content Filling       | Full 30-day content + all event text + expanded guest profiles                                        |
| **Week 4** | Day 22–30 | Polish & Optimization | Art refinement + sound effects + bug fixes + settlement/Medal Shop systems                            |

### 8.2 Team Responsibilities

| Role              | Weeks 1–2                                                                                                                        | Weeks 3–4                                                              |
| ----------------- | -------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- |
| **Game Designer** | Write event library, generate NPC profiles, initial numerical design                                                             | Balance numbers, polish text, organize playtest feedback               |
| **Programmer**    | Core loop (time/screening/rooms/events/saves)                                                                                    | Shop system, optimization, bug fixing                                  |
| **Artist**        | Finalize AI-generated style (rough-line Darkest Dungeon style), generate and post-process AI portraits/UI/side-view environments | Integrate final art assets + animation frames + unify lighting effects |
| **Music / Sound** | Atmosphere sound design and style confirmation                                                                                   | Implement sound effects, mixing, final export                          |

---

# 9. Future Refinement Directions / To-Do List

The following areas already have a general direction but do not yet have finalized numerical values and will be gradually refined during development:

- Detailed numerical design: food consumption speed, currency production multiplier, Erosion change rate
- Complete event library (30+ nighttime events, 20+ daytime events, 10+ personal events)
- NPC profile library (at least 20 different guests)
- Specific costs for room unlocks and the exact size of the hotel
- Complete output table for all jobs
- Complete item-system design (acquisition methods, prices, numerical effects)
- Complete Truth collection system design (conditions for obtaining Truth Items and trigger verification)
- Specific products and pricing for the Medal Shop
- Specific restrictions for high-difficulty modes
