# Against the Storm — Feature & Design Inspiration Reference

> Research compiled from the [Steam store page](https://store.steampowered.com/app/1336490/Against_the_Storm/), the game's trailers, and 10 official screenshots (all downloaded locally — see [Media Index](#media-index)).
> Purpose: an exhaustive catalogue of features, UI, style, and mechanics to mine as inspiration.

**Developer:** Eremite Games · **Publisher:** Hooded Horse
**Released:** Dec 8, 2023 (1.0) · Early Access Nov 1, 2022
**Reception:** Metacritic 91 · Steam "Overwhelmingly Positive" (95%)
**Genre:** Roguelite city-builder / survival colony sim
**Tagline:** *A dark fantasy city builder where you rebuild civilization in the face of apocalyptic rains.*

---

## Media Index

All assets downloaded to this folder.

### Screenshots (`screenshots/`)
| File | What it shows |
|------|---------------|
| `screenshot_01.jpg` | Core forest settlement, building under construction, full HUD |
| `screenshot_02.jpg` | Glade event — "Drainage Mole" investigate panel, rainy night |
| `screenshot_03.jpg` | Dense town + Clan Hall service building info panel |
| `screenshot_04.jpg` | Blightrot infestation (cyan tentacles + lightning) mid-storm |
| `screenshot_05.jpg` | Glade event — "Giant Stormbird's Nest" boss-like threat panel |
| `screenshot_06.jpg` | World map — hex overworld, biomes, Smoldering City |
| `screenshot_07.jpg` | Forbidden glade at night — ruins, reliquary, blight path |
| `screenshot_08.jpg` | Settlement built around the glowing Hearth idol |
| `screenshot_09.jpg` | Carpenter production building — recipe/worker panel |
| `screenshot_10.jpg` | Overhead "hero shot" of a mature, grid-planned town |

### Videos (`videos/`)
| File | Source |
|------|--------|
| `launch_trailer.mp4` | Official 1.0 Launch Trailer (~102 MB) |
| `release_date_trailer.mp4` | 1.0 Release Date Trailer (~80 MB) |

---

## 1. Premise & Narrative Framing

- **You are the Viceroy**, charged by the **Scorched Queen** to reclaim the wilderness for the **Smoldering City** — civilization's last bastion against the **Blightstorm** that destroyed the old world.
- Not one city but a **network of settlements** across a large world map.
- **Dark fantasy fairy-tale tone**: endless rains, ancient forest, mysterious ruins, forbidden glades, fantasy races living in the mud and firelight.
- **Roguelite loop framing**: settlements *fall*, the expedition ends — but the game continues. Failure is a run reset, not a game over.
- **Constant external pressure**: an expectant monarch (Impatience) + inevitable recurring storms.

**Takeaway:** A single strong framing device (the Queen + the eternal storm) justifies both the meta-progression loop *and* the moment-to-moment time pressure. One fiction does double duty.

---

## 2. Structural / Meta Pillars

- **Roguelite city builder** — each settlement is a self-contained "run" (typically ~1–2 hours), then dissolved.
- **Meta-progression carries forward**: resources, upgrades, experience, and unlocked blueprints/perks persist between expeditions.
- **The Smoldering City** = the persistent hub you upgrade with meta-resources between runs.
- **World map is a roguelike map**: pick your next settlement location; each tile advertises its biome, difficulty, modifiers, and rewards *before* you commit ("draft your challenge").
- **Hundreds of gameplay modifiers** + **6 distinct biomes** → every location is a different puzzle.
- **Adjustable difficulty tiers** (e.g. "Pioneer" minimum difficulty shown per map node) — difficulty is a first-class, surfaced choice.
- **Daily Expedition** and **Training Expedition** modes (seeded/challenge + practice) alongside the main campaign — visible as top-bar tabs on the world map.
- **Deeds / achievements** (80 Steam achievements) act as long-horizon goals.

**Takeaway:** The "draft before you commit" pattern — showing modifiers, rewards, and difficulty on the map node before entry — turns location selection into a strategic, replayable decision.

---

## 3. Core Gameplay Mechanics

### 3.1 The Reputation / Impatience / Hostility triangle (the win/lose engine)
- **Win condition:** fill the **Reputation** meter (blue).
- **Lose condition:** fill the **Impatience** meter (red) — the Queen loses patience.
- Both are shown as a **single opposed segmented bar** at bottom-center (blue filling left, red filling right).
- **Hostility** is the environmental pressure that scales as your settlement grows: it *lowers villager Resolve* and makes storms harder.
- These three are **interlinked**: gaining Reputation lowers Impatience but raises Hostility; carrying Impatience *reduces* Hostility (each Impatience point ≈ −15 Hostility). This creates a deliberate risk/tempo tension — rushing rep makes the world angrier.

### 3.2 Resolve (the villager morale currency)
- Each species has a **base Resolve**; net Resolve = base + all positive/negative modifiers (needs met, housing, hostility, weather, etc.).
- **Resolve < 1 → villagers leave** (population bleed = death spiral).
- Push a species' Resolve above a **Reputation Threshold** → their portrait turns **blue** and they **generate Reputation over time**. More species over threshold = faster Reputation.
- So the whole game is: **raise Resolve → earn Reputation → win, before Impatience/Storm breaks you.**

### 3.3 Seasons / Weather cycle (the master clock)
- Three seasons cycle each **year**: **Drizzle → Clearance → Storm**.
- **Drizzle:** mild, planting season, generally safe.
- **Clearance:** dry, harvest season, best working window.
- **Storm:** dangerous — Resolve craters, villagers get depressed & leave, Impatience/Hostility spike. You *survive* the storm rather than expand.
- Farming is tied to season phase (plough in storm, plant in drizzle, harvest in clearance) — lets you scale workers up/down by season.
- Central **season dial/clock** shows current season icon + progress; year shown in Roman numerals.

### 3.4 Glade Events (the exploration/risk layer)
- The map is **shrouded in fog**; villagers clear **glades** to expand.
- **Small / Dangerous / Forbidden glades** escalate in risk and reward.
- Opening a glade can trigger a **Glade Event**: a timed threat *and/or* opportunity with a description, a **countdown timer**, **requirements** (deliver goods / apply "working effects"), and **rewards**.
- Examples seen in screenshots: *Drainage Mole* (deliver 30 goods within timer → keep goods or send to Citadel), *Giant Stormbird's Nest* (a boss-tier threat darkening the skies, deliver 30 goods, choose Keep vs Convert reward).
- Events force **triage decisions** under a clock: pay the cost, ignore-and-suffer, or race the timer.
- Reward choice framing: **"Keep goods"** (local benefit) vs **"Send to the Citadel" / "Convert"** (meta / alternate benefit).

### 3.5 Cornerstones (run-defining perks / draft)
- Periodically you **draft a Cornerstone** — a powerful passive perk (e.g. "extra resources every time you open a glade").
- Build-defining, stackable, roguelite-style — this is the "deckbuilding" flavor layered onto the city builder.

### 3.6 Blightrot / Blight (the corruption hazard)
- **Blightrot** — cyan/teal fungal-tentacle corruption that erupts on buildings, especially during storms (see `screenshot_04`).
- Must be cleared (with "Blight Post" / Purging Fire mechanics) or it spreads and disrupts production.
- Tied to **Rainwater/Storm Water** — a corrupting but usable resource.

### 3.7 The Hearth & Firekeeper (the heart of the settlement)
- The **Hearth** is a central glowing brazier/idol structure (dragon-carved) — literally and mechanically the town's heart (`screenshot_08`, `screenshot_10`).
- Consumes fuel (wood, oil, etc.) to project **warmth/light radius** that boosts Resolve and holds back the dark.
- A **Firekeeper** villager staffs it; **Lizard Firekeeper grants +1 Global Resolve** — the only bonus that helps *every* species at once.
- Smaller **secondary hearths/campfires** extend the warm zone across the map.

### 3.8 Species / "Clans" (asymmetric population)
Five playable species, each with distinct **needs**, **Resolve profiles**, and **workplace specializations**:

| Species | Personality | Specializations | Signature needs |
|---------|-------------|-----------------|-----------------|
| **Humans** | Adaptable, former kingdoms | Farming, brewing | Pies, Religion, Ale |
| **Beavers** | Hardworking, demanding | Woodworking, engineering | Biscuits, Engineering, high comfort |
| **Lizards** | Resilient, distrustful, cold-blooded | Meat/animals, prefer warmth; best Firekeepers; highest hunger tolerance | Jerky, Warmth, Brawling |
| **Harpies** | Noble, fragile, primal | Alchemy, cloth/fabric | Crystalized Dew, Open space |
| **Foxes** | Mysterious, forest-bonded, blight-symbiotic | Gathering, forest work, blight resistance | Treats, Education |

- You **embark with a mix** of species; balancing their competing needs is the central juggling act.
- Each species reacts differently to hostility, hunger, and housing → asymmetry drives replay.

### 3.9 Production chains & the economy
- **Deep branching crafting trees**: raw materials → intermediate goods → complex goods & luxury/service goods.
- Buildings often produce **multiple recipes** and can be told which to prioritize (see Carpenter: Planks / Tools / Pack of Luxury Goods).
- **Recipe efficiency stars (★/★★)** show how well the assigned species/building performs a recipe (specialization bonus).
- **Ingredient substitution**: recipes accept alternative inputs (e.g. "8 wood + 3 storm water" *or* other combos), giving flexibility with whatever the biome provides.
- **Production limits / stock caps** per recipe (set a target stockpile).
- **Worker slots** per building (e.g. 2/2 filled), assign specific species per slot.
- **Needs vs Services:** raw survival needs (food, shelter, warmth) + higher-order services (Religion, Brawling, Leisure, Education) delivered by service buildings like the **Clan Hall** — each service satisfies needs and grants **passive effects** ("Ancient Ways").

### 3.10 Trade & the Trader
- **Trade Routes** with a wandering Trader — buy/sell goods for **Amber** (the premium currency).
- **Amber** is the main money; also spent on trade orders, recovering from shortages.
- **Trade Orders / Royal Orders** — objectives from the Queen that grant Reputation/rewards on fulfillment.

### 3.11 Time controls & pacing
- **Pause + 1× / 1.5× / 2× / 3× speed** controls front-and-center — the game expects you to pause-plan constantly and fast-forward the boring stretches.

**Takeaway cluster:** Almost every system is a *timed pressure valve* (season clock, glade timers, Impatience, hostility ramp, Resolve bleed). The tension is systemic, not scripted.

---

## 4. UI / UX Breakdown (observed from screenshots)

A dense, ornate, "living dashboard" HUD. Elements by region:

### Top-left — Species Roster panel
- Stacked cards, one per species present.
- Circular **portrait** with a colored ring (green = fine, blue = above Reputation threshold).
- Population count, workers assigned / idle / needed (little-figure icons).
- **Resolve readout with a trend arrow**: e.g. `30 ▶ 40` — current value animating toward target, color-coded. Excellent at-a-glance "is this species happy and where's it heading."

### Top-center — Resource bar
- Row of ~6 key resource icons (food & fuel types) with live counts.
- **Red flame / down-arrow badges** flag resources that are draining or critically low.

### Top-center — Season Dial & speed controls
- Ornate **circular clock** with the current season's icon (tree / cloud-rain / lightning), a filling arc, and the **year in Roman numerals**.
- A blue-vs-red mini indicator (reputation/impatience tempo).
- Directly below: **pause ▮▮**, **play ▶**, and **×1.5 / ×2 / ×3** speed chips.

### Top-center-right — Cornerstones / active effects
- A row of small square **perk/effect icons** with stack counts — your run's active modifiers, always visible.

### Top-right — Currencies & global menus
- Round **currency tokens** (Amber, trade goods).
- A cluster of round **menu buttons**: Orders/Reputation, Biome/map, Codex/tech, options.

### Bottom-center — Build menu + master meters
- Left: glowing circular **Hearth** shortcut.
- Center: a row of **build category buttons** (housing, storage, gathering, farming, production, services, decorations…), each opening a radial/list of blueprints.
- Right: a **skull icon** (storm/hostility status).
- Spanning the middle: the **segmented Reputation (blue) ↔ Impatience (red) bar** with a central numeric readout — the single most important "am I winning" glance.

### Bottom-left — Status / modifier tray
- Small icons with live values: rates (`3/min`), flat bonuses (`+2`), percentages (`20%`, `500%`), difficulty/mystery tokens, a **blight/dragon** warning icon. A compact "current global modifiers" strip.

### Right-side — Contextual detail panels (on selection)
Rich, tabbed inspector panels. Observed variants:
- **Production building (Carpenter):** title + type, "Can produce / Can use" summary, specialization bonus, **Construction progress bar + material tally + priority up/down**, then a **recipe list** — each recipe row shows inputs `=` output, craft time, star rating, a **stock-limit stepper**, lock toggle, and per-recipe worker counts.
- **Service building (Clan Hall):** which needs it serves, **Recipes** (need → fulfillment), and **Passive Effects** blocks with % values and a small skill-tree-like node.
- **Glade Event panels:** flavor description, **Threats** row (icons + countdown timer), **Requirements** (goods needed, "working effects" applied, a Time bar + % progress), **Rewards** as two selectable cards (**Keep goods** vs **Send to Citadel / Convert**), and a big **INVESTIGATE / CANCEL** action button.
- Panels use **tabs** (book / chest / droplet icons) to switch between description, storage, and resource views.

### World Map screen (`screenshot_06`)
- **Hex-tiled overworld**, painterly biome art, rain falling even on the map.
- Left panel for the highlighted tile: biome name (**Royal Woodlands**), **Min. difficulty (Pioneer)**, **Effects** (icons), and **Rewards** (icons + counts).
- Named settlement nodes (Blightwatch, Moledale), dotted **travel routes**, `?` fog markers for unexplored tiles.
- Central **Smoldering City** rendered as a volcanic citadel.
- Top tabs: **Smoldering City / Daily Expedition / Training Expedition**.

**UX Takeaways:**
- **Trend arrows** (`current ▶ target`) beat static numbers — they teach the player the *direction* of a system without a tooltip.
- **One master meter** for win/lose (the rep↔impatience bar) keeps a very complex sim legible.
- **Every panel answers "what, why, and what can I do about it"** in one view (description + requirement + reward + action button).
- Color-coded **portrait rings** and **red warning badges** create a fast triage read across a busy screen.
- **Always-visible active modifiers** (cornerstones + status tray) mean the player never forgets their build's rules.

---

## 5. Art & Visual Style

- **Painterly, semi-stylized 3D** on a fixed **isometric / high-angle top-down** camera with free rotate & zoom.
- **Storybook dark-fantasy** look: hand-painted textures, chunky readable silhouettes, exaggerated cozy-medieval architecture (curved pagoda-ish roofs, red/blue tile, timber frames).
- **Signature atmosphere: it is always raining.** Streaks of rain, wet-glistening cobble, puddles — the "eternal storm" is felt every frame.
- **Warm vs cold color language:** golden **firelight** (Hearth, campfires, windows) punching through **cold blue-green** rainy gloom — the core mood contrast, and a gameplay signal (warmth = safety).
- **Biome-driven color grading:** lush emerald forest by day, deep blue/teal by night & storm, sickly cyan **blight**, orange-red **volcanic** citadel.
- **Lush overgrowth** everywhere — ferns, mushrooms, flowering crops, vines reclaiming ruins; the wilderness feels alive and encroaching.
- **Fantastical set-pieces:** giant carved **dragon-idol Hearth**, stained-glass **reliquaries/chests**, ancient stone **arches & ruins**, glowing magical **blight tendrils**, oversized **creature threats** (Stormbird, Drainage Mole) that dwarf the villagers.
- **Ornate carved-wood & metal UI frames** with gold filigree, parchment textures, wax-seal buttons — the chrome matches the fiction (a royal expedition ledger).
- **Tiny expressive villagers** in colored garb, readable at distance, milling along footpaths between buildings.
- **Readability-first VFX:** colored auras/beams telegraph status (green resolve/effect beams, cyan blight lightning, golden hearth glow).

**Takeaways:**
- Commit to **one atmospheric constant** (here, rain) — it becomes free identity in every screenshot and trailer frame.
- **Light temperature as gameplay UI**: warm = safe/happy, cold = danger. The player reads the sim through the art before touching the HUD.
- Let the **environment encroach** (overgrowth, ruins) to sell "reclaiming a hostile world."

---

## 6. Audio / Feel (from trailers & store copy)

- Full voiced narration framing (Queen / lore), moody orchestral-folk score that swells for the storm.
- Constant **ambient rain + fire crackle** bed; storm brings wind/thunder stingers.
- Satisfying **production/UI click & stamp** feedback (ledger/seal metaphor).

---

## 7. Accessibility / Comfort Features (listed on store)

- Adjustable **text size**, **camera comfort**, **custom volume controls**, **adjustable difficulty**, **save anytime**, Steam Cloud. Full audio localization in 18 languages.
- **Moddable** (community-tagged) — designed for extension.

**Takeaway:** Adjustable difficulty + save-anytime + camera comfort are cheap to list and broaden the audience for a punishing systems game.

---

## 8. Distilled Inspiration Checklist (for our game)

Ideas worth stealing / adapting:

1. **One fiction that powers the loop** — a framing device (impatient patron + world threat) that justifies both meta-progression *and* time pressure.
2. **Draft-your-challenge map nodes** — surface biome, modifiers, difficulty, and rewards *before* the player commits.
3. **Single opposed master meter** for win vs lose to keep a deep sim legible.
4. **Trend arrows (`now ▶ target`)** on every important stat.
5. **Interlinked pressure triangle** (progress ↔ patience ↔ world-hostility) so pushing to win actively raises the stakes.
6. **Timed dilemma events** with description + requirement + dual reward choice + one action button.
7. **Cornerstone drafts** — periodic run-defining perk picks layered on the core sim (deckbuilder flavor).
8. **A master clock** (seasons) that rhythmically shifts safe → dangerous phases.
9. **Asymmetric population units** with distinct needs & specializations to force juggling and drive replay.
10. **A literal "heart" building** (Hearth) that projects a safety radius — spatial, visual, and mechanical anchor.
11. **Warm-vs-cold light as diegetic UI.**
12. **One committed atmospheric constant** (rain) for instant visual identity.
13. **Contextual inspector panels** that always show what/why/action together.
14. **Always-visible active modifiers** so the player never loses track of their build's rules.
15. **Roguelite failure = reset, not punishment** — losing a settlement advances the meta.

---

## Sources
- [Against the Storm — Steam store page](https://store.steampowered.com/app/1336490/Against_the_Storm/)
- [Resolve — Hooded Horse Official Wiki](https://wiki.hoodedhorse.com/Against_the_Storm/Resolve)
- [Beginner's Guide — Hooded Horse Official Wiki](https://wiki.hoodedhorse.com/Against_the_Storm/Beginner's_Guide)
- [Species — Against the Storm Wiki (Fandom)](https://against-the-storm.fandom.com/wiki/Species)
- [All Species in Against the Storm — gamepressure.com](https://www.gamepressure.com/newsroom/all-species-in-against-the-storm-description-and-characteristics/zc54ab)
- [Against the Storm — Have You Played? (Adrian Hon)](https://adrianhon.substack.com/p/against-the-storm)
- [Against the Storm (Video Game) — TV Tropes](https://tvtropes.org/pmwiki/pmwiki.php/VideoGame/AgainstTheStorm)
