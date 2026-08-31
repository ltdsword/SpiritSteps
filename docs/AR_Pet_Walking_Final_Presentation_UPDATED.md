# Updated Final Presentation – 3D Location-Based Companion Game

## Why the final presentation must change

The old final presentation presents the project as a broad AR virtual pet app with care/grooming, turn-by-turn AR navigation, dynamic evolution, memory seeds, social features, room customization, and fitness quests.

The current feature source defines a more focused final build:

- 3D location-based companion collection;
- walking gives Growth EXP to all owned companions;
- walking gives Coins;
- Coins buy Food for bonus Growth EXP;
- companion growth is shown by model scale;
- GPS detects nearby Vietnamese landmarks;
- Vuforia Image Target opens AR cultural memory;
- 3D companion appears in AR;
- player collects Landmark Stamp;
- one Landmark can unlock a new companion;
- Journey stores visited landmarks, stamps, story, and optional AR photo.

Therefore, the final presentation should be updated to match the playable vertical slice and remove features that are cut from scope.

---

## Slide 1 – Title

# Pawprints of Vietnam

## A 3D Location-Based Companion Game

A Unity mobile game where walking grows 3D companions and real Vietnamese landmarks unlock AR cultural memories.

Team 06:

- Lê Thị Tuyết Trâm – 23125093
- Lê Đức Tùng Dương – 23125081
- Võ Đăng Quân – 23125090
- Lê Tiến Đạt – 23125028

---

## Slide 2 – Core Concept

# Core Concept

**Pawprints of Vietnam** is a 3D location-based companion game inspired by *Pikmin Bloom*.

Players walk in the real world to:

- grow their unlocked 3D companions;
- earn Coins;
- unlock new companions through milestones or Landmark rewards;
- visit Vietnamese landmarks;
- scan a Vuforia Image Target;
- view an AR cultural memory;
- collect Landmark Stamps;
- save discoveries in Journey.

## One-sentence pitch

Walking becomes companion growth, and landmarks become AR memories.

---

## Slide 3 – What Changed from the Initial Idea

# Focused Final Scope

The project was narrowed to a playable vertical slice that can be finished and demonstrated reliably.

## Kept

- 3D animated companions.
- Walking-based progression.
- GPS location gameplay.
- AR companion experience.
- Vietnamese Landmark memories.
- Stamp collection.
- Journey records.

## Cut

- Backend.
- Multiplayer / friend system.
- Expedition.
- Mushroom battle.
- Hunger or pet punishment.
- Complex navigation.
- Continuous outdoor AR walking.
- AI object recognition.

## Presentation point

The final build focuses on doing one complete loop well.

---

## Slide 4 – Core Gameplay Loop

# Core Gameplay Loop

```text
Start with Companion
 ↓
Walk in the real world
 ↓
All unlocked companions gain Growth EXP
+
Player earns Coins
 ↓
Buy Food and feed one companion
 ↓
Reach a Landmark
 ↓
GPS unlocks AR Memory
 ↓
Scan Vuforia Image Target
 ↓
3D Companion appears in AR
 ↓
View Cultural Memory
 ↓
Collect Stamp
 ↓
Optional Companion Unlock
 ↓
Save to Journey
```

## Main value

The player is rewarded for movement, exploration, and cultural discovery.

---

## Slide 5 – Companion Collection & Growth

# Companion Collection & Growth

The player owns a collection of 3D companions.

Prototype companions:

- Dog as starter companion.
- Cat as walking milestone companion.
- Rabbit as Landmark reward companion.

## Growth system

Walking gives Growth EXP to **all unlocked companions**.

Growth stage is represented by model scale:

| Stage | EXP Range | Model Scale |
|---|---:|---:|
| Baby | 0–500 | 0.70 |
| Young | 500–1,500 | 0.85 |
| Adult | 1,500+ | 1.00 |

The same 3D model can be reused for each stage.

---

## Slide 6 – Walking, Coins & Food

# Walking Rewards

Walking is the main progression source.

```text
Walk 1 km
→ Dog +100 Growth EXP
→ Cat +100 Growth EXP
→ Rabbit +100 Growth EXP
→ Player +30 Coins
```

Locked companions do not receive EXP.

## Food system

Coins are used to buy Food.

```text
Coins
→ Buy Food
→ Choose one companion
→ Eat animation
→ Bonus Growth EXP
```

Food gives priority growth without adding hunger, punishment, or pet death mechanics.

---

## Slide 7 – Landmark Discovery

# Landmark Discovery

Vietnamese landmarks are real-world destinations.

The map/home screen shows:

- player location;
- Landmark markers;
- distance to each Landmark;
- simple direction indicator;
- Start Walk / End Walk.

Example:

```text
Central Post Office
180 m
↑
```

## Landmark data

Each Landmark has:

- name;
- latitude and longitude;
- unlock radius;
- Vuforia Image Target;
- Stamp;
- optional companion reward.

No turn-by-turn navigation is required.

---

## Slide 8 – AR Cultural Memory

# AR Cultural Memory

After the player enters a Landmark radius, the AR memory can be opened.

```text
GPS Landmark unlocked
 ↓
Open AR Memory
 ↓
Scan Vuforia Image Target
 ↓
3D companion appears
 ↓
Cultural Story Panel appears
 ↓
Collect Stamp
```

## AR interaction

- Companion plays Idle animation.
- Player can tap companion for a simple reaction such as Jump or Sit.
- Cultural content is split into short parts: History, Architecture, Did You Know.

The Image Target can be a printed landmark image, postcard, poster, or memory card.

---

## Slide 9 – Landmark Stamp & Journey

# Stamp and Journey

After completing an AR Cultural Memory, the player receives a Stamp.

```text
MEMORY DISCOVERED
Central Post Office
Stamp Collected
```

Journey stores:

- visited Landmark list;
- collected Stamps;
- visited date;
- short cultural information;
- optional AR Photo.

## Purpose

Journey turns walking history into a personal travel diary of Vietnamese cultural discoveries.

---

## Slide 10 – AR Photo

# AR Photo

AR Photo is a should-have feature.

Flow:

```text
Open AR Photo
 ↓
Choose Companion
 ↓
Place Companion in AR
 ↓
Companion plays available animation
 ↓
Take Photo
 ↓
Preview / Save
```

AR Photo can work independently or after a Landmark memory.

After collecting a Stamp, the game may ask:

```text
Take a photo with your companion?
```

Photos can be shown again in Journey.

---

## Slide 11 – Main Screens

# Main Functional Screens

## Home / Map

Player location, Landmark markers, distance indicator, Start Walk / End Walk.

## Companion Collection

Owned companions, locked companions, Growth EXP, Growth Stage, Feed action.

## Shop / Food

Coins, 1–2 food types, buy food.

## Walking Result

Distance walked, Coins earned, Growth EXP earned.

## Landmark AR

Vuforia scan, 3D companion, Cultural Memory Panel, Stamp reward, optional companion unlock.

## AR Photo

Choose companion, place companion, take photo.

## Journey

Visited Landmarks, Stamps, date, cultural information, optional photo.

---

## Slide 12 – Final Build Scope

# Final Build Scope

## Must have

- 2–3 animated 3D companions.
- Companion collection.
- Starter, walking milestone, and Landmark reward companion.
- Growth stages by model scale.
- GPS walking distance tracking.
- Growth EXP for all unlocked companions.
- Coins and feeding.
- Local save/load.
- Landmark GPS detection.
- Simple direction indicator.
- Vuforia Image Target.
- AR 3D companion.
- Cultural Memory Panel.
- Landmark Stamp.
- Journey tab.

## Should have

- AR Photo.
- Show AR Photo in Journey.

---

## Slide 13 – Project Value

# Project Value

The game combines:

- **3D character interaction** through animated companions;
- **real-world movement** through GPS walking;
- **progression** through Growth EXP, Coins, and Food;
- **AR interaction** through Vuforia Image Targets;
- **Vietnamese culture** through Landmark Memory stories;
- **collection motivation** through Stamps, companions, and Journey.

## Final message

The project does not need many features. It needs one complete and stable loop that shows walking, growth, AR, culture, and memory collection.

---

## Slide 14 – Closing

# Ready to Walk?

Thank you for listening.

We are ready to demonstrate:

```text
Walk → Grow Companion → Reach Landmark → Scan Target → AR Memory → Stamp → Journey
```

AR Pet Walking Team / Pawprints of Vietnam
