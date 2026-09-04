# Animal Companion UI Strategy

## Product boundary

This prototype is a local-only animal companion experience. It has no authentication, backend, friends, multiplayer, or synchronization. The player's display name and all progress are stored in `player-save.json` under Unity's `Application.persistentDataPath`.

Map positioning and walking measurement are integration boundaries. The checked-in deterministic providers keep the entire prototype demonstrable until the real implementations are merged.

## Four permanent tabs

The bottom navigation has exactly four roots:

1. **Map** — discover Landmarks and start or finish a walk.
2. **Companions** — inspect Dog, Cat, and Rabbit, including locks, Growth EXP, and stage.
3. **Shop** — buy food and choose an unlocked companion to feed.
4. **Journey** — review Landmark Stamps, saved photo paths, and Journey records.

AR is contextual rather than a fifth root. It opens from an eligible Landmark and returns to that Landmark or the Map flow.

## Twelve screens

| # | Route | Root | Purpose |
|---|---|---|---|
| 1 | Onboarding Setup | Map | Welcome, validate a 1–20 character display name, and confirm Dog as starter. |
| 2 | Home Map | Map | Show local profile summary, mock player/Landmark markers, and walk entry. |
| 3 | Active Walk | Map | Show live distance, duration, and optional steps from `IWalkMetricsProvider`. |
| 4 | Walk Result | Map | Apply completed-kilometre rewards and explain new companion unlocks. |
| 5 | Companion Collection | Companions | Show Dog, Cat, and Rabbit with stage or unlock requirement. |
| 6 | Companion Detail | Companions | Show Growth EXP, stage thresholds, placeholder scale, and food entry. |
| 7 | Shop / Food | Shop | Show two food tiers, then open an unlocked-companion picker. |
| 8 | Landmark Detail | Map | Show short History, Architecture, and Did You Know content plus proximity. |
| 9 | Landmark AR Memory | Map | Simulate Image Target recognition and reveal the three short content pages. |
| 10 | AR Photo | Map | Preview and persist a mock local photo path; real capture remains an integration. |
| 11 | Journey List | Journey | Show totals and locally created Landmark Journey records. |
| 12 | Journey Detail | Journey | Show the factual record and link back to the associated Landmark. |

## First launch and settings

On a missing save, the app presents welcome, name, and starter confirmation in sequence. It does not request location or camera permission during setup. Location is introduced from Map; camera is introduced only when Landmark AR opens.

If a save is invalid or corrupt, the original is renamed to a timestamped `.bak` file and the setup screen explains that a new local profile is needed.

Settings states plainly that data is phone-only and provides a two-step **Reset Local Progress** action. Confirmation deletes profile progress and returns to setup.

## Progression rules

- Dog starts unlocked with 450 Growth EXP.
- Cat unlocks when total distance reaches 1 km. A Cat unlocked by the current walk does not receive that walk's EXP.
- Rabbit unlocks when the Central Post Office Stamp is collected.
- Every whole completed kilometre in one walk grants each previously unlocked companion 100 Growth EXP and grants the player 30 Coins.
- Baby is below 500 EXP, Young is 500–1499 EXP, and Adult is 1500 EXP or more.
- Temporary UI art uses scale 0.70, 0.85, and 1.00 for Baby, Young, and Adult.
- Basic Food costs 20 Coins and grants 20 EXP. Better Food costs 40 Coins and grants 40 EXP.

Fractional distance still contributes to total distance, but discrete per-kilometre rewards use whole kilometres completed in that walk.

## Landmark demonstration

The Map contains Independence Palace, Central Post Office, and Notre-Dame Basilica. Central Post Office begins within the deterministic mock provider's unlock radius and is flagged AR-ready.

The AR demonstration is deliberately explicit: the player taps **Simulate recognition**, reads the three short pages, then collects the Stamp. The first completion unlocks Rabbit and creates one Journey entry. Repeating it shows the already-collected state and never duplicates the Stamp, unlock, or Journey.

The historical text is prototype copy and requires cultural review before release.

## Artwork policy

The existing plant artwork remains in the project archive and is intentionally reused as temporary Dog, Cat, and Rabbit imagery. Every companion-facing screen labels it as placeholder art. Do not delete those source images or modify the archived Next.js mockup. Replace the bindings only when licensed animal models or approved animal artwork are available.

## Presentation priorities

- Always explain why an action matters to walking, companion growth, or a remembered place.
- Keep distance primary and steps supplemental.
- Keep camera screens visually dominant; avoid large overlays over the simulated camera view.
- Keep cultural copy short enough to read safely after stopping.
- Use clear lock requirements and reward feedback instead of hidden progression.
- Make offline/local-only behavior reassuring and unambiguous rather than presenting fake sync states.
