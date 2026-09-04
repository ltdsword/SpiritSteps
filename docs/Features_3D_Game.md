# Proposal – 3D Location-Based Companion Game

## 1. Ý tưởng tổng quan

Game là một **3D location-based companion game** lấy cảm hứng từ cách *Pikmin Bloom* biến việc đi bộ thành progression và exploration.

Người chơi sở hữu một collection các companion 3D. Khi người chơi đi bộ ngoài đời thật:

- tất cả companion đã sở hữu nhận **Growth EXP**;
- người chơi nhận **Coins**;
- người chơi có thể tiến tới các **Landmark** để mở **AR cultural experiences**.

Companion không có evolution phức tạp như mầm → nụ → hoa. Thay vào đó, companion phát triển qua các **Growth Stage** bằng cách thay đổi kích thước model.

Các Landmark văn hóa/lịch sử được dùng như destination của walking gameplay. Khi tới gần Landmark, người chơi có thể dùng **Vuforia** để scan một **Image Target**, xem một companion 3D xuất hiện trong AR, khám phá cultural memory, nhận Stamp và trong một số trường hợp unlock companion mới.

**AR Photo** và **Journey** giúp người chơi lưu lại trải nghiệm với companion và các Landmark đã khám phá.

---

## 2. Core Gameplay Loop

```text
WALK
 ↓
Growth EXP cho tất cả owned companions
+
Coins
 ↓
Companions grow
+
Coins → Food → Bonus Growth cho một companion
```

Song song:

```text
WALK
 ↓
Reach Landmark
 ↓
GPS unlock
 ↓
Scan Vuforia Image Target
 ↓
3D Companion appears in AR
 ↓
Cultural Memory / Story
 ↓
Collect Stamp
 ↓
Optional Companion Unlock
 ↓
Save to Journey
```

Game tập trung vào ba mục tiêu:

1. Companion collection phát triển nhờ quá trình đi bộ.
2. Walking có progression và mục tiêu khám phá rõ ràng.
3. Các Landmark đã ghé trở thành Stamp và Journey Memory để người chơi xem lại.

---

## 3. Companion System

### 3.1 Companion Collection

Prototype dự kiến có khoảng **2–3 companion 3D** sử dụng model miễn phí có animation sẵn.

Ví dụ:

- Dog
- Cat
- Rabbit

Mỗi companion có:

- tên;
- model 3D;
- animation có sẵn;
- Growth EXP;
- Growth Stage;
- trạng thái locked/unlocked.

Game **không sử dụng Active Companion cho progression**.

Khi người chơi đi bộ, tất cả companion đã sở hữu đều nhận Growth EXP như nhau.

Người chơi chỉ cần chọn companion trong các hoạt động cụ thể như:

- AR Photo;
- AR companion display;
- các scene cần chọn con vật xuất hiện.

### 3.2 Unlock Companion

Companion mới không được mua trực tiếp bằng Coins và không được xem là pet “cấp cao hơn” thay thế pet cũ.

Prototype sử dụng ba kiểu unlock đơn giản:

```text
Starter Companion
Start Game
→ Dog unlocked
```

```text
Walking Companion
Reach total walking milestone
→ Cat unlocked
```

```text
Landmark Companion
Complete one selected Landmark AR Memory
→ Collect Stamp
→ Rabbit unlocked
```

Đối với prototype, một Landmark có thể unlock companion ngay để người chơi và giảng viên có thể thấy toàn bộ reward loop trong một lần demo.

Các Landmark khác có thể chỉ cho Stamp mà không cần unlock thêm companion.

Companion mới luôn bắt đầu với:

```text
Growth EXP = 0
```

Companion mới không nhận Growth EXP từ quãng đường người chơi đã đi trước khi unlock.

### 3.3 Growth System

Growth là progression chính của companion.

Ví dụ:

| Growth Stage | EXP Range | Model Scale |
|---|---:|---:|
| Baby | 0–500 EXP | ~0.70 |
| Young | 500–1,500 EXP | ~0.85 |
| Adult | 1,500+ EXP | 1.00 |

Không cần model khác nhau cho từng Growth Stage.

Các giá trị EXP và scale có thể điều chỉnh sau khi test gameplay.

### 3.4 Animation

Game sử dụng animation thực sự có trong free assets.

Ví dụ:

- Idle
- Walk
- Run
- Jump
- Eat
- Sit
- Sleep

Không yêu cầu:

- Happy animation;
- Give Paw;
- Roll Over;
- animation riêng cho từng Growth Stage.

Ví dụ sử dụng:

```text
Normal state → Idle
Movement → Walk / Run
Feeding → Eat
Simple reaction → Jump / Sit
```

Animation dùng để hỗ trợ interaction, không phải reward progression bắt buộc.

### 3.5 Bond System

**CUT khỏi scope hiện tại.**

Lý do:

- chưa có gameplay role đủ rõ;
- dễ chồng chéo với Growth EXP;
- tăng thêm state và UI không cần thiết trong thời gian 12 ngày.

---

## 4. Walking System

Walking là nguồn progression chính.

Người chơi không cần chọn companion trước khi đi bộ.

```text
Start Walk
 ↓
Track GPS Distance
 ↓
End Walk
 ↓
Calculate rewards
```

Reward:

```text
Distance
→ Growth EXP cho tất cả owned companions
→ Coins cho player
```

Ví dụ:

```text
Walk 1 km
Dog +100 Growth EXP
Cat +100 Growth EXP
Rabbit +100 Growth EXP
Player +30 Coins
```

Companion chưa unlock không nhận EXP.

### 4.1 Vai trò của Walking

Walking có ba vai trò:

1. Làm companion phát triển.
2. Tạo Coins để mua Food.
3. Đưa người chơi tới Landmark để khám phá.

### 4.2 Walking Session và dữ liệu lưu

Prototype ưu tiên dùng **GPS distance** thay vì bắt buộc dùng step sensor.

Dữ liệu tối thiểu:

```text
PlayerData
├── totalDistance
├── coins
├── unlockedCompanions[]
├── companionGrowthEXP[]
└── collectedStamps[]
```

Có thể lưu thêm:

- lastWalkDistance
- lastCoinsEarned
- lastGrowthEarned

để hiển thị Walking Result.

Prototype không cần:

- lưu toàn bộ GPS route;
- background walking khi app đóng;
- tracking khi màn hình đã khóa.

Chi tiết implementation cuối cùng có thể điều chỉnh tùy quá trình làm mobile/location tracking.

---

## 5. Coin & Food System

### 5.1 Coins

Coins là secondary reward từ walking.

```text
Walk
→ Growth EXP
+ Coins
```

Coins không dùng để mua trực tiếp companion mới.

Trong prototype, mục đích chính của Coins là mua Food.

### 5.2 Food

Food cho phép người chơi ưu tiên phát triển một companion cụ thể.

```text
Walking
→ ALL owned companions get Growth EXP

Food
→ Choose ONE companion
→ Eat animation
→ Bonus Growth EXP
```

Ví dụ:

| Food | Cost | Effect |
|---|---:|---:|
| Basic Food | 20 Coins | +20 Growth EXP |
| Better Food | 40 Coins | +40 Growth EXP |

Food không có:

- hunger bar;
- pet death;
- giảm chỉ số theo thời gian;
- punishment khi người chơi không đăng nhập.

---

## 6. Landmark Navigation

Prototype hiện dự kiến có khoảng **3 Landmark**, nhưng con số này chưa khóa cứng và có thể thay đổi tùy tiến độ.

Map hiển thị:

- Landmark marker;
- khoảng cách tới Landmark.

Khi người chơi tới gần, game có thể hiển thị một direction indicator đơn giản:

```text
Central Post Office
180 m
↑
```

Không triển khai:

- turn-by-turn navigation;
- route planning;
- hệ thống navigation phức tạp như Google Maps.

Mục tiêu chỉ là giúp người chơi biết Landmark nằm ở hướng nào và còn cách bao xa.

---

## 7. Landmark Gameplay

Landmark gameplay kết hợp:

- walking;
- GPS;
- Vuforia;
- AR;
- 3D companion;
- cultural information;
- Stamp;
- optional companion reward.

Core flow:

```text
Walk toward Landmark
 ↓
Enter GPS Radius
 ↓
Landmark Discovered
 ↓
Open AR Memory Experience
 ↓
Scan Vuforia Image Target
 ↓
3D Companion appears in AR
 ↓
Cultural Memory / Story appears
 ↓
Collect Landmark Stamp
 ↓
Optional Companion Unlock
 ↓
Save to Journey
```

Một Landmark hoàn chỉnh phải đủ để demo toàn bộ flow trên.

### 7.1 GPS Landmark Detection

Mỗi Landmark cần dữ liệu tối thiểu:

- Name;
- Latitude;
- Longitude;
- Unlock Radius;
- Vuforia Image Target;
- Stamp;
- optional companion reward.

Cultural content cụ thể sẽ bổ sung sau khi mechanic đã hoạt động ổn định.

Ví dụ:

```text
Central Post Office
Unlock Radius: 100 m

Khi player đi vào vùng:
NEW LANDMARK DISCOVERED
Saigon Central Post Office
[Explore Memory]
```

### 7.2 Vuforia Image Target

Người chơi mở camera và scan một Image Target được chuẩn bị trước.

Target có thể là:

- old postcard;
- landmark poster;
- cultural memory card;
- printed landmark image.

Game không yêu cầu Vuforia phải nhận diện nguyên một công trình ngoài trời.

```text
GPS Landmark unlocked
 ↓
Open AR
 ↓
Scan correct Image Target
 ↓
Vuforia recognizes target
 ↓
AR Memory Experience begins
```

### 7.3 Core AR Experience – MUST HAVE

Sau khi Vuforia nhận diện đúng target:

```text
3D Companion appears
 ↓
Companion plays Idle animation
 ↓
Cultural Memory / Story Panel appears
```

Companion là nhân vật 3D xuất hiện cùng người chơi trong AR cultural experience.

Interaction tối thiểu có thể là:

```text
Tap companion
→ Jump / simple reaction
```

Không yêu cầu companion phải:

- tìm collectible;
- dẫn đường đến object;
- dùng AI/pathfinding;
- có animation đặc biệt ngoài asset có sẵn.

Mục tiêu là tạo một 3D animated companion experience trong AR với scope vừa sức.

### 7.4 Cultural Memory / Story Panel

Sau khi Image Target được nhận diện, cultural information của Landmark được hiển thị.

Recommended implementation:

```text
World Space Panel
Panel nằm trong AR world cùng companion.
```

Ví dụ:

```text
CENTRAL POST OFFICE
[Historical Image]
HISTORY
Short cultural information
[Next]
```

Có thể chia nội dung thành 2–3 phần ngắn:

```text
History
→ Next
Architecture
→ Next
Did You Know?
→ Collect Stamp
```

Không hiển thị đoạn văn dài.

Nếu World Space Canvas gây khó đọc hoặc tracking không ổn, có thể fallback sang Screen Space popup.

Không ưu tiên:

- mở website ngoài game;
- video dài;
- external browser.

Nội dung lịch sử/văn hóa cụ thể sẽ được viết sau khi feature hoạt động ổn định.

### 7.5 Landmark Stamp

Sau khi người chơi hoàn thành Cultural Memory:

```text
MEMORY DISCOVERED
Central Post Office
Stamp Collected
```

Stamp dùng để:

- ghi nhận Landmark đã khám phá;
- tạo collection;
- lưu vào Journey để xem lại sau.

Stamp không bắt buộc phải là model 3D.

### 7.6 Companion Reward từ Landmark

Trong prototype, một Landmark được chọn có thể unlock companion ngay sau khi hoàn thành AR Memory.

Ví dụ:

```text
Central Post Office
 ↓
Scan Image Target
 ↓
AR Memory
 ↓
View Cultural Story
 ↓
Collect Stamp
 ↓
NEW COMPANION UNLOCKED
Rabbit
```

Điều này giúp một lần demo Landmark thể hiện đầy đủ reward loop.

Các Landmark khác có thể chỉ cho Stamp.

### 7.7 Optional – AR Memory Fragment Hunt

**OPTIONAL, không phải feature bắt buộc.**

Nếu còn thời gian:

```text
Scan Target
 ↓
Memory Fragments appear in AR
 ↓
Player looks around
 ↓
Find and tap fragments
 ↓
Memory reconstructed
 ↓
Cultural Story
 ↓
Stamp
```

Nếu không hoàn thành đúng hạn thì cắt hoàn toàn mà không ảnh hưởng core Landmark flow.

---

## 8. AR Photo

**SHOULD HAVE.**

Người chơi có thể đưa companion vào môi trường thật và chụp ảnh.

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

AR Photo có thể hoạt động độc lập với Landmark.

Sau khi hoàn thành Landmark, game có thể gợi ý:

```text
Stamp Collected
 ↓
Take a photo with your companion?
```

Nếu ảnh được chụp tại Landmark, ảnh có thể được hiển thị lại trong Journey.

---

## 9. Journey

Journey có một tab riêng trong ứng dụng.

Mục đích chính:

- xem lại Landmark đã khám phá;
- xem Stamp đã thu thập;
- xem ngày ghé;
- xem cultural information;
- xem AR Photo nếu có.

Ví dụ:

```text
MY JOURNEY
✓ Central Post Office
  Postal Stamp
  Visited: ...
✓ Independence Palace
  Palace Stamp
  Visited: ...
□ Landmark 3
```

Khi mở một entry:

```text
Landmark Name
[Stamp]
Visited Date
Short Cultural Information
[AR Photo - nếu có]
```

Journey không cần:

- diary phức tạp;
- nhập text thủ công;
- lưu toàn bộ GPS route;
- thống kê chi tiết.

---

## 10. Accessories

**OPTIONAL.**

Ví dụ:

- collar;
- hat;
- bow;
- glasses.

Chỉ triển khai nếu:

- core gameplay đã hoàn thành;
- có asset phù hợp sẵn;
- không cần chỉnh model/rig phức tạp.

Accessories không phải requirement của final build.

---

## 11. Expedition

**CUT khỏi scope hiện tại.**

Không triển khai:

- gửi companion đi expedition;
- expedition timer;
- treasure mission;
- expedition reward;
- expedition UI.

---

## 12. Main Functional Screens

UI/UX chi tiết sẽ thiết kế sau khi các feature chính hoạt động ổn định.

Prototype cần các màn hình chức năng cơ bản:

### Home / Map

- player location;
- Landmark markers;
- distance / simple direction indicator;
- Start Walk / End Walk.

### Companion Collection

- owned companions;
- locked companions;
- Growth Stage;
- Growth EXP;
- Feed action.

Không có Active Companion bắt buộc cho progression.

### Shop / Food

- Coins;
- 1–2 loại Food;
- mua Food.

### Walking Result

- distance;
- Coins earned;
- Growth EXP earned.

### Landmark AR

- Vuforia scan;
- 3D companion;
- Cultural Memory Panel;
- Stamp reward;
- optional companion unlock.

### AR Photo

- choose companion;
- place companion;
- take photo.

### Journey

- visited Landmark list;
- Stamps;
- visited date;
- cultural information;
- optional AR Photo.

---

## 13. Scope cho khoảng 12 ngày

### MUST HAVE

- 2–3 companion 3D models có animation sẵn.
- Companion collection.
- Starter companion.
- Walking milestone companion.
- Landmark reward companion.
- Baby → Young → Adult bằng scale.
- Walking distance tracking.
- Growth EXP cho tất cả owned companions.
- Coins.
- Feeding.
- Local save/load.
- Khoảng 3 Landmark nếu tiến độ cho phép.
- GPS Landmark detection.
- Simple Landmark direction indicator.
- Vuforia Image Target.
- 3D companion xuất hiện trong AR.
- Cultural Memory / Story Panel.
- Landmark Stamp.
- Journey tab.

### SHOULD HAVE

- AR Photo.
- Hiển thị AR Photo trong Journey.

### OPTIONAL

- Accessories.
- AR Memory Fragment Hunt.
- Extra AR reactions / polish.

### CUT

- Bond System.
- Expedition.
- Mushroom Battle.
- Multiplayer.
- Backend.
- Friend System.
- Background Walking.
- Hand Tracking.
- AI Object Recognition.
- Continuous Outdoor AR Walking.
- Complex GPS Navigation.
- Complex Pet Stats.
- Multiple Minigames.
- Multiple Puzzle Engines.
- Hunger / pet punishment system.

---

## 14. Điểm học hỏi và khác biệt so với Pikmin Bloom

Game học từ Pikmin Bloom ở các nguyên tắc:

- walking-based progression;
- companion collection;
- location-based exploration;
- AR companion experience;
- memory / collection motivation.

Game điều chỉnh cho concept riêng bằng:

### Companion Growth

Động vật phát triển bằng Growth EXP và Growth Stage thay vì seedling → Pikmin.

### Shared Walking Progression

Walking cho Growth EXP cho toàn bộ companion đã sở hữu, thay vì bắt buộc chọn một Active Companion.

### Vietnamese Landmark AR Memories

Landmark văn hóa/lịch sử trở thành real-world destinations kết hợp GPS + Vuforia + AR.

### Landmark Stamp

Stamp ghi lại những Landmark đã khám phá và được lưu trong Journey.

### Companion Unlock

Companion mới được dùng như gameplay reward cho walking milestone hoặc Landmark discovery.

### Journey

Journey giúp người chơi xem lại các Landmark, Stamp và AR Photo đã thu thập trong quá trình chơi.

---

## 15. Mục tiêu Final Build

Final build không cần nhiều content. Mục tiêu là chứng minh một complete playable vertical slice:

```text
Start with Companion
 ↓
Walk
 ↓
All owned companions gain Growth EXP
+
Player earns Coins
 ↓
Feed one companion for bonus Growth
 ↓
Reach Landmark
 ↓
GPS unlock
 ↓
Scan Vuforia Image Target
 ↓
3D Companion appears in AR
 ↓
View Cultural Memory
 ↓
Collect Stamp
 ↓
Unlock Landmark Companion
 ↓
Save Landmark to Journey
 ↓
Optional AR Photo
```

Nếu loop này chạy ổn định, project có thể thể hiện:

- 3D animated character system;
- mobile GPS/location-based gameplay;
- companion progression;
- real-world exploration;
- Vuforia image recognition;
- AR 3D experience;
- cultural Landmark integration;
- collection / reward progression;
- persistent local player data.

---

## 16. Nội dung để xử lý sau

Chưa cần khóa ngay:

- danh sách Landmark cuối cùng;
- cultural content cụ thể của từng Landmark;
- Image Target cụ thể;
- UI/UX và visual polish;
- accessory assets;
- balancing chính xác của EXP / Coins / Food.

Ưu tiên hiện tại:

> Làm các gameplay system và AR/location flow chạy ổn định trước, sau đó mới bổ sung content và polish UI/UX.
