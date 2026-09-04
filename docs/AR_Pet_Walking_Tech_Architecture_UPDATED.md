# Updated Tech Architecture – 3D Location-Based Companion Game

## Why the tech architecture must change

The old architecture describes a larger AR pet product with backend accounts, PostGIS, object storage, social QR sync, Google Routes API, Health Connect/HealthKit, ARCore Geospatial Anchors, and real-time/social features.

The current feature scope is smaller and more prototype-focused:

- no backend;
- no multiplayer/social system;
- no continuous outdoor AR walking;
- no turn-by-turn navigation;
- no complex GPS route planning;
- no pet hunger/punishment system;
- no expedition or mushroom battle;
- local save/load is enough;
- Vuforia Image Target is the core AR requirement;
- GPS is used mainly for walking distance and landmark radius detection.

Therefore, the updated architecture should present a **local-first Unity mobile prototype** centered on walking progression, companion growth, GPS landmark unlock, Vuforia AR memory, stamp collection, and Journey.

---

## Slide 1 – Title

# Tech Architecture

**3D Location-Based Companion Game**  
Final 3D Game Project  
Team 06

Core technical focus:

- Unity mobile app
- 3D animated companions
- GPS walking distance
- Vuforia Image Target AR
- Local save/load
- Landmark memory system

---

## Slide 2 – Updated Technical Scope

# MVP Technical Scope

The final build is a playable vertical slice, not a full commercial location-based game.

## Must have

- 2–3 low-poly animated 3D companion models.
- Companion collection and growth stages.
- GPS walking distance tracking.
- Growth EXP for all unlocked companions.
- Coins and feeding bonus EXP.
- Landmark radius detection.
- Vuforia Image Target scan.
- 3D companion appears in AR.
- Cultural Memory / Story Panel.
- Landmark Stamp reward.
- Journey tab.
- Local data persistence.

## Should have

- AR Photo.
- Show AR Photo inside Journey.

## Cut from architecture

- Backend server.
- PostgreSQL/PostGIS.
- Google Routes API.
- Real-time multiplayer.
- Social sync.
- Continuous outdoor AR walking.
- ARCore Geospatial Terrain Anchors.
- Complex pet care stats.

---

## Slide 3 – Development Stack

# Development Stack

## Unity Client

- **Engine:** Unity LTS / Unity 6 with C#.
- **Target platform:** Android mobile prototype.
- **3D:** Low-poly rigged animal models with available animations.
- **AR:** Vuforia Engine using Image Targets.
- **Location:** Unity LocationService / GPS.
- **Direction indicator:** GPS position + optional compass heading.
- **Data:** Local JSON save file or PlayerPrefs for simple prototype data.
- **UI:** Unity UI / Canvas-based mobile screens.

## Architecture style

- Local-first prototype.
- ScriptableObjects for static data such as companions, foods, landmarks, and stamps.
- Plain C# services/managers for gameplay systems.
- Mock location provider for Editor testing.

---

## Slide 4 – System Architecture

# System Architecture

```text
Unity Mobile App
│
├── UI Layer
│   ├── Home / Map
│   ├── Companion Collection
│   ├── Shop / Food
│   ├── Walking Result
│   ├── Landmark AR
│   ├── AR Photo
│   └── Journey
│
├── Gameplay Systems
│   ├── WalkingTracker
│   ├── CompanionGrowthSystem
│   ├── CoinFoodSystem
│   ├── LandmarkService
│   ├── RewardService
│   └── JourneyService
│
├── AR Systems
│   ├── VuforiaImageTargetController
│   ├── ARCompanionSpawner
│   └── CulturalMemoryPanel
│
├── Data Layer
│   ├── PlayerData
│   ├── CompanionData
│   ├── LandmarkData
│   ├── StampData
│   └── SaveLoadService
│
└── Device Services
    ├── GPS / LocationService
    ├── Camera
    └── Optional Compass
```

The app does not require network calls for the final prototype.

---

## Slide 5 – Data Model

# Core Data Model

## PlayerData

```text
PlayerData
├── totalDistance
├── coins
├── unlockedCompanions[]
├── companionGrowthEXP[]
├── collectedStamps[]
├── visitedLandmarks[]
└── journeyEntries[]
```

## CompanionData

```text
CompanionData
├── id
├── displayName
├── prefab
├── animatorController
├── growthEXP
├── growthStage
├── modelScaleByStage
└── unlocked
```

## LandmarkData

```text
LandmarkData
├── id
├── name
├── latitude
├── longitude
├── unlockRadius
├── vuforiaImageTargetName
├── culturalStorySections[]
├── stampId
└── optionalCompanionRewardId
```

## JourneyEntry

```text
JourneyEntry
├── landmarkId
├── visitedDate
├── stampId
├── shortStory
└── optionalPhotoPath
```

---

## Slide 6 – Walking & Reward Pipeline

# Walking and Reward Pipeline

```text
Start Walk
 ↓
GPS samples collected
 ↓
Filter weak or impossible movement points
 ↓
Calculate distance
 ↓
End Walk
 ↓
RewardService calculates:
  - Growth EXP for all unlocked companions
  - Coins for player
 ↓
Save updated PlayerData
 ↓
Show Walking Result screen
```

## Important implementation choices

- Use GPS distance as the main prototype metric.
- Do not require step sensor.
- Do not track walking when the app is closed.
- Do not store the full GPS route unless needed for debugging.
- Use mocked GPS points in Editor for faster testing.

---

## Slide 7 – Companion Growth System

# Companion Growth System

Companion progression is based on Growth EXP.

```text
Distance walked
→ Growth EXP for every unlocked companion
→ Growth Stage update
→ Model scale update
```

## Growth stages

| Stage | EXP Range | Scale |
|---|---:|---:|
| Baby | 0–500 | 0.70 |
| Young | 500–1,500 | 0.85 |
| Adult | 1,500+ | 1.00 |

## Feeding

```text
Coins
→ Buy Food
→ Choose one companion
→ Play Eat animation
→ Add bonus Growth EXP
→ Save data
```

No separate model is required for each growth stage. The same model can be scaled.

---

## Slide 8 – Landmark Detection

# GPS Landmark Detection

Each Landmark has a GPS coordinate and unlock radius.

```text
Current player GPS
+
Landmark GPS
 ↓
Distance check
 ↓
If distance <= unlockRadius:
  Landmark Discovered
  Show [Explore Memory]
```

## Landmark UI requirement

The map/home screen only needs:

- landmark marker;
- distance to landmark;
- simple direction indicator.

## Not required

- Turn-by-turn navigation.
- Route planning.
- Google Maps-like navigation.
- Outdoor AR path following.

---

## Slide 9 – Vuforia AR Memory Flow

# Vuforia AR Memory Flow

Vuforia is used after GPS unlock.

```text
GPS Landmark unlocked
 ↓
Open Landmark AR screen
 ↓
Camera starts
 ↓
Scan prepared Image Target
 ↓
Vuforia recognizes target
 ↓
Spawn 3D companion on target
 ↓
Show Cultural Memory / Story Panel
 ↓
Collect Stamp
 ↓
Optional companion unlock
 ↓
Save to Journey
```

## Image Target options

- printed landmark image;
- old postcard;
- landmark poster;
- cultural memory card.

The prototype does not need to recognize the full real building outdoors.

---

## Slide 10 – AR Scene Design

# AR Scene Design

## Must have

- 3D companion appears after Image Target detection.
- Companion plays Idle animation.
- Tap companion triggers simple animation such as Jump or Sit.
- Cultural Memory panel appears with short content.
- Player can collect Landmark Stamp.

## Cultural Memory panel

Recommended structure:

```text
Landmark Name
[Historical Image]
History
[Next]
Architecture
[Next]
Did You Know?
[Collect Stamp]
```

World Space Canvas is preferred for AR feeling. Screen Space popup is acceptable if World Space UI is hard to read.

---

## Slide 11 – Local Save / Load

# Local Save / Load

The game stores player progress locally.

## Save data includes

- total walking distance;
- coins;
- unlocked companions;
- companion EXP and growth stages;
- collected stamps;
- visited landmarks;
- optional AR photo path.

## Implementation options

- JSON file in `Application.persistentDataPath` for structured data.
- PlayerPrefs for very small data.

Recommended: JSON file because the project has multiple systems and collections.

```text
Gameplay Event
 ↓
Update PlayerData
 ↓
SaveLoadService.Save()
 ↓
Load on next app start
```

---

## Slide 12 – Testing Strategy

# Testing Strategy

## Editor testing

- Mock GPS coordinates.
- Simulate walking distance.
- Simulate entering Landmark radius.
- Test reward calculation without going outside.

## Mobile testing

- Check GPS permission.
- Check camera permission.
- Test walking distance on a short route.
- Test Vuforia Image Target scan.
- Test local save after closing and reopening the app.

## Demo fallback

If real GPS is unstable during demo, use a debug button or mock location mode to trigger Landmark unlock and show the AR memory flow.

---

## Slide 13 – Final Technical Message

# Final Technical Message

This architecture proves the required technical abilities within realistic scope:

- animated 3D companion system;
- mobile GPS/location-based gameplay;
- walking progression and reward calculation;
- Vuforia Image Target recognition;
- AR 3D companion experience;
- Vietnamese Landmark memory content;
- local persistent player data;
- Journey / Stamp collection.

The prototype focuses on a complete vertical slice rather than many unfinished advanced systems.
