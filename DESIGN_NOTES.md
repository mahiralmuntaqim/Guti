# Brine Time: design notes and roadmap

## Pillars
1. **One more cast.** Bites come quickly and every fight is short and tense.
2. **Visible progress.** Each bait, rod and island opens up new fish to discover.
3. **Better together.** Friends fishing side by side, cheering each other's catches.
4. **Cozy-comedic tone.** Tiny islands, odd fish, bright low-poly art.

## Core loop
Cast, then wait (time set by the bait), then a bite with a ~1.1 s reaction window, then a tension fight, then the catch goes into storage. Sell at the Market, spend at the Store, and unlock the next island at the Ferry.

## Systems
| System | Where | Notes |
|---|---|---|
| Catch tables | `data.gd` BAITS + ISLANDS.exclusive | bait picks the pool; the current island adds its exclusives |
| Size roll | `data.gd roll_size` | Gaussian (mean 1.0, σ 0.18), clamped 0.45–1.9; value scales linearly |
| Value | `data.gd catch_value` | worth × size × golden(3×) × clean(1.2×) |
| Fight | `fishing.gd _process_fight` | thrash wave + random lunges; reeling builds tension; strong fish pull line even while you reel. Tuned by simulation so each rod tier opens the next bait tier |
| Islands | `data.gd ISLANDS`, `world.gd` | far apart on one ocean; the ferry teleports with a fade; stores cap rod/storage tiers per island |
| Models | `fish_model.gd` | lathe bodies with vertex-colour patterns + fins/eyes; special parts per creature |
| Sound | `sfx.gd` | synthesized at boot: splashes, plops, reel ticks, snap, jingles, cash, horn, steps, gulls, wave ambience |
| Co-op | `net.gd`, `avatar.gd`, `main.gd` | ENet, host relays. Presence at 20 Hz (unreliable), events reliable. Economy is per-player and local |
| Save | `game_state.gd` | JSON in `user://` |

## Island progression
| Island | Gate | Purpose |
|---|---|---|
| Brine Island | start | learn the loop; Bread → Worm → Glowberry; Bamboo Rod |
| Coral Key | 5 species + $250 | reef fish (Candy Wrasse, Prism Squid); Carbon Rod; Ice Chest |
| Stormrock | 10 species + $1500 | Thunder Spoon bait, Storm Rod, the legendaries |

## Roadmap ideas
- **Cooking:** a campfire where perfectly grilled fish sell for more.
- **Day/night and weather:** night-only and storm-only fish.
- **A drivable boat:** fish open water between islands.
- **Islander quests:** "bring me 3 Moon Carp" style requests for money and cosmetics.
- **Co-op extras:** text chat, emotes, a shared "catch of the day" leaderboard, and optionally a host-authoritative economy for competitive modes.
- **Cosmetics:** hats and colours for avatars, rod skins.
- **Settings menu:** mouse sensitivity, volume, invert Y, FOV.
- **Gamepad support.**
