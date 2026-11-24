# Otsukai (おつかい) — project guide for Claude Code

2D top-down (3/4 view) pixel-art errand game set in a small Japanese town.
Solo dev: **Oput** (animation student in Japan, aiming for background art; Unity beginner).
Repo: https://github.com/ArgoVesta07/otsukai — Oput commits with GitHub Desktop.

---

## 1. How to work with Oput

- Explain in **Indonesian**, casual (lu/gw). Go **step by step** and wait for "lanjut" before the next step.
- Be concise. Unity instructions must be concrete (which object, which component, which field).
- **Code, comments and Debug.Log in English.**
- **Everything editable in the Inspector / prefabs / ScriptableObjects.** No hardcoded text, numbers or visuals in scripts.
- **All player-facing text in English + Japanese** (see Localization). Japanese = standard style, kanji, short (panels are small). Not childish hiragana-only.
- When replacing a script, give the whole file and say what changed. Keep old serialized fields alive with `[FormerlySerializedAs]` when renaming.
- Before touching a script, read the current version in the repo (it may differ from earlier drafts).

## 2. Tech setup

- Unity **6.6 (6000.6.4f1)**, **URP 2D** (Renderer2D).
- Packages in use: Localization, TextMeshPro, Addressables (via Localization), Input System + legacy keys.
- Font: **DotGothic16-Regular SDF** (dynamic, multi-atlas) is the TMP default; LiberationSans SDF as fallback. "Inconsistent result" import warning is harmless.
- Scenes: `MainMenu`, `MainScene` (town), `House`, `Supermarket` (interiors).
- Data: `Assets/Data/` → `Errands/`, `Incidents/`, `Item/`, `Story/`, `TimeOfDay.asset`.

## 3. Pixel / visual standard (decided)

- **16 px per tile**, Sprite import: PPU 16, Filter **Point**, Compression **None**.
- **Pixel Perfect Camera** (URP): Assets PPU 16, Reference 384×216, Upscale Render Texture on.
- Renderer2D: Transparency Sort Mode = **Custom Axis (0, 1, 0)** (Y-sort).
- Object structure: **root** (scale 1, Sorting Group, colliders, scripts) + child **Visual** (SpriteRenderer, Y offset = half height). Swapping final art = only replace the sprite in Visual.
- Buildings: root at **bottom-centre**; wall collider covers only the lower (wall) part, not the roof.
- Tilemap Grid: `Ground` (order -100), `Road` (order -90). Palette "Town" (placeholder tiles).
- Camera view: **3/4 top-down** (like Pokémon / Stardew / Mother). HD-2D was considered and rejected (would need a full 3D rebuild).

| Thing | Tiles | Pixels |
|---|---|---|
| Tile | 1×1 | 16×16 |
| Character | 1×2 | 16×32 |
| House | 5×5 | 80×80 |
| Medium building | 7×5 | 112×80 |
| Shop / supa | 8×6 | 128×96 |
| Shotengai shop | 4×5 | 64×80 |
| Door, mailbox | 1×1.5 | 16×24 |
| Bed 1×2 · Desk 2×1 · Shelf 2×2 · Tree 2×3 · Board 2×2 · Marker 1×1 | | |
| Road width | 2–3 | |

Prefabs: Player, NPC, House, Supermarket, OfferMarker (square "!"), OtsukaiMarker (pink star).

## 4. Game systems (Phase 1 — DONE)

### Modes
| Mode | Summary |
|---|---|
| **Free** | Walk around with the clock running, nothing saved. Default when entering the town. Must still sleep at night. |
| **Classic** (relaxed) | Day/phase cycle, autosave `classic.json` (day, phase, completed) at each phase change. 2–3 otsukai per day at random phases from Family NPCs. Townsperson requests are optional (Accept / Later panel). Unfinished errands **expire** at phase change (notice only). |
| **Challenge** | Score + strikes. Each day phase: 1 otsukai + request quota (2 → +1 every 2 days, cap 4) + 1 extra offer. Night: otsukai chance 0.3 + 0.1/day (max 0.7). Requests auto-accepted on talk. Strike = otsukai missed/failed, or each request missing from quota. Deadlines count from pickup and shrink per day. Best score/day in PlayerPrefs `Otsukai_ClassicBestScore` / `Otsukai_ClassicBestDay`. Game over → `WorldClock.Stop()`. |
| **Story** | TimeManager rounds + DayData/StoryData. Currently **"coming soon"** (`ModeSelectUI.storyAvailable = false`). |

Mode switching: `ModeSelectPoint` (board in town) → `ModeSelectUI`. Inside a mode it only offers "Go back to free mode?".

### Time — `WorldClock` + `TimeOfDayData`
- Phases: Morning 6:00, Afternoon 11:00, Evening 15:00, Night 18:00 (`isNight`).
- Events: `PhaseChanged`, `NightStarted`, `DayEnded`, `DayStarted`.
- Night → must sleep in **Bed** (NavTarget "Bed"); HUD shows "Go to bed" + arrow. Sleep blocked while errands are active (`HasUnfinishedWork`).
- Debug: `DebugSpeed`, `DebugAddMinutes`, `DebugJumpToNight` (DebugPanel).

### Errands
- **Otsukai (おつかい / "Errand")** = main missions from **Family** NPCs (Mom, Grandmother…), around home. Marked with pink star, HUD prefix "★".
- **Request (頼まれごと)** = side missions from **Townsperson** NPCs, marked with "!".
- `ErrandData` (SO): `kind`, steps (`targetId`, `navTargetId`, `requiredItem(s)`, `shoppingList`), `allowAtNight`, `offerInClassic`, `classicWeight`, `allowInChallenge`, `challengeDeadlineMinutes`, `challengeScore`, offer lines. Target `"Giver"` = the NPC who gave it; `{giver}` in text = giver's name.
- Shopping steps: `shoppingList` with empty `targetId` auto-completes when all items are in the bag. Items come from `ShopItem` shelves. `ShoppingMemo` (Tab) shows the list as a paper memo.
- `QuestManager` tracks active errands; `NpcRegistry` (id, name, role, awayNavId); `NPC.TryErrandTalk` order: waiting step → classic otsukai → challenge → classic request panel → idle line.
- `IncidentManager` / `IncidentData` (e.g. RoadClosed) = random obstacles (Challenge).
- HUD (`GameHud`): task card + nav arrow priority: tracked errand → otsukai giver → challenge offer → bed.

### Other scripts
Inventory (`InventoryManager/UI/Row`, `ItemData`), `Door` + `SceneTransition` (spawn by NavTarget id), `SaveSystem/SaveData/SavePoint`, `DialogBox`, `SpeechBubble`, `Toast`, `PeriodBanner`, `PauseMenu`, `MainMenu`, `InteractionPrompt`, `ErrandSpot` (proximity label, no E needed for simple info), `CameraFollow` (file `CameraFoll.cs`, map bounds), `StaticReset`, `GameFlags`.

## 5. Localization workflow

- Table **"UI"**: main menu (LocalizeStringEvent / LocalizedString).
- Table **"Game"**: in-game text. **The English text is the key.**
  - In scripts: mark string fields with `[GameText]`; read with `Loc.T(text)` / `Loc.F(format, args)` (safe on bad formats).
  - Static TMP text in scenes/prefabs: add `LocalizeText` component.
- Editor menu **Otsukai > Localization**: **Collect Game Texts** (scans SOs, prefabs, scenes → fills table, reports missing ja / unused keys), **Export CSV**, **Import CSV** (columns Key,en,ja; UTF-8 BOM).
- To change displayed wording without breaking keys: edit the en/ja columns, not the key.
- After adding any new text: Collect → fill ja (CSV) → Import.

## 6. Roadmap

| Phase | Content | Status |
|---|---|---|
| 1 | Core systems (above) | **Done** |
| 1.5 | Tidy map, sizes, prefabs | **In progress** |
| 2 | Content: more errands, requests, incidents, NPCs | Next |
| 3 | Story mode flow | Later |
| 4 | Final visuals (art, layered art, time-of-day lighting) | Later |
| 5 | Polish: UI animation, sound | Later |

### Phase 1.5 checklist
- [ ] Resize town to **96×64 tiles** (centre 0,0) → paint ground/roads, CameraFollow `boundsSize` 96×64, edge walls at ±48.5 / ±32.5.
- [ ] Place buildings / NPCs per the town plan (section 7).
- [ ] Props as root + Visual: Mailbox, ModeBoard, Bed, LetterDesk, EggShelf.
- [ ] Interior tilemap floors + camera bounds per interior scene.
- [ ] Spawn NavTargets on every door.
- [ ] **StairZone** script (slow down + optional visual offset on stairs) — stairs to the school hill.
- [ ] Update How to Play text (Memo = Tab).
- [ ] Playtest + commit.

## 7. Town plan (96×64 tiles)

Reference image: `Docs/Denah_Kota_96x64.png` (+ `Denah_Kota_96x64.csv` with Unity root positions).
Unity coords: x = tileX − 48, y = 32 − tileY (root = bottom-centre).

Layout concept:
- **North**: railway, **station** + ekimae plaza (bus stop), **koban** and **konbini** in front of the station, park (NW).
- **NE hill**: **SMP + SMA** reached by **stairs**.
- **Centre**: **shotengai** lane of small 4×5 shops (roti, kue, kopi, bunga, dagashiya | daging, yaoya, laundry, restoran). Below: **Daiso, drugstore, supa** (+ parking).
- **West**: civic — **shiyakusho, perpustakaan, yuubin (+ATM)**.
- **East**: **rumah sakit** + **apotek** next to it, parking.
- **South**: residential (player house near **SD** and **gomi station**), **Juntendo** (+ parking), park, **river** as the south edge (bridge = decoration).
- Mailboxes: yuubin, ekimae, shotengai, residential.

| No | Building | Tiles | Enter | Root (Unity x, y) |
|---|---|---|---|---|
| 1 | Supa | 8×6 | yes | 6, -5 |
| 2 | Player house | 5×5 | yes | -36.5, -20 |
| 2b–2k | Neighbor houses (2–3 color variants) | 5×5 | no | see CSV |
| 3 | Konbini | 7×5 | yes | -16.5, 19 |
| 4 | Station | 14×6 | no | 0, 22 |
| 5 | Daiso (100-yen) | 8×5 | yes | -18, -4 |
| 6 | Hospital | 12×8 | yes | 35, 4 |
| 8 | Yuubin + ATM | 7×5 | yes | -29.5, -2 |
| 10 | Apotek | 4×4 | yes | 44, 5 |
| 11 | Library | 10×6 | yes | -40, -2 |
| 12a | SD | 14×8 | no | -15, -17 |
| 12b / 12c | SMP / SMA (hill) | 11×7 | no | 25.5, 20 / 39.5, 20 |
| 13 | Flower shop | 4×5 | yes | -10, 4 |
| 14 | Restaurant | 6×5 | yes | 16, 4 |
| 15 | Laundry | 4×5 | yes | 11, 4 |
| 16 | Drugstore | 8×5 | yes | -8, -4 |
| 17 | Koban | 4×4 | no | 15, 19 |
| 18 | Juntendo (DIY) | 12×7 | yes | 10, -16 |
| 20 | Bakery | 4×5 | yes | -22, 4 |
| 21 | Butcher | 4×5 | yes | 3, 4 |
| 22 | Yaoya (greengrocer) | 4×5 | yes | 7, 4 |
| 23 | Shiyakusho | 12×7 | yes | -39, 6 |
| 24 | Cake shop | 4×5 | yes | -18, 4 |
| 25 | Coffee shop | 4×5 | yes | -14, 4 |
| 26 | Dagashiya | 5×5 | yes | -5.5, 4 |
| — | Gomi station, mailboxes, bus stop | 1–2 tiles | — | see image |

"Enter = no" buildings are decorative doors for now (can be changed). Map size may be tightened after playtest if walking feels too long.

## 8. Planned content (Phase 2) — errand ideas

| Errand | Where | Notes |
|---|---|---|
| Return borrowed book | Library | item: Book |
| Buy flowers | Flower shop | |
| Print something | Konbini | |
| Pay bills (electric/water/gas) | Konbini | |
| Wash clothes | Laundry | maybe wait time |
| Send a parcel | Yuubin | item: Parcel |
| Withdraw money | ATM (yuubin) | gives Money item |
| Accompany parent to hospital | Hospital | escort / follow NPC |
| Accompany dad to shiyakusho | Shiyakusho | escort |
| Take out the trash | Gomi station | near home |
| Buy fertilizer | Juntendo | |
| Buy mom's favorite bread | Bakery | |
| Buy meat | Butcher | |
| Shopping at 100-yen shop | Daiso | shoppingList |
| Take little sibling to dagashiya | Dagashiya | escort |
| Take little sibling to school | SD | escort, morning only |
| Walk the pet around town | Parks / river | undecided (needs follower system) |

Escort-type errands need a new **follower NPC** feature (Phase 2).

## 9. Art plan (Phase 4, can start early)

- Claude generates clean 16 px base sprites with code (Python/PIL) at the exact sizes above; Oput adds detail / shading / finishing.
- Order: house → supa → shotengai shops → civic buildings → props.
- Later ideas: layered art (more "alive"), Light 2D time-of-day + lit windows/street lamps at night, blob shadows, bloom/vignette, swaying grass.

## 10. Known gotchas

- Scene references must point to **scene objects**, not prefab assets (e.g. NPC OtsukaiMarker).
- Ground tilemap must stay below sprites (order -100) or it hides the player.
- Errand step `navTargetId` left over from copied assets sends the arrow to the wrong NPC — clear it.
- `LocalizeText` private fields are `[NonSerialized]` on purpose (domain reload was wiping them).
- Unity "GUIView" assertion and DotGothic "inconsistent result" warnings are harmless.
