# Brine Time - fight & economy balance

All numbers here come from the offline simulation in `docs/gameplay/sim/` (Python 3, no packages):

| file | what it is |
|---|---|
| `fightmodel.py` | line-by-line mirror of `Assets/BrineTime/Scripts/FightModel.cs`. **Keep the two in sync.** |
| `sim.py` | creature/rod tables (mirror of `Data.cs`) + the player bots + win-rate tables (`python -I sim.py`) |
| `curve.py` | win rate vs. strength per style and rod (`python -I curve.py 150 attentive,human`) |
| `econ.py` | $/min per bait/island/rod and fresh-save progression (`python -I econ.py attentive 16`) |
| `econ_before.py` | the same economy run against the ORIGINAL fight model + data, for the "before" columns |

The port was checked: `Tests/GameplayTests.cs` runs the C# `FightModel` with the same bot and its win rates
match the Python sim within a few percent (for example Stripe Bass on Bamboo is 98% in C# and 99% in Python;
Old Whiskers on Storm is 36% / 40%).

## Bots ("reasonable human")

* **human**: hold until tension reaches 75%, let go until it drops to 30%. Every switch has a 0.15-0.30 s reaction delay.
* **attentive**: the same, but it also lets go (after the same reaction delay) when the lunge telegraph
  (`Fishing.LungeWarning`) starts, and waits for the lunge to end. This is how the game is meant to be played,
  so it is the main tuning target. The plain "human" bot shows how forgiving the game is to someone who ignores telegraphs.
* **bot** (CoreLoop smoke test): reel whenever tension < 0.7, with no delay. It catches 100% of bread fish on the Driftwood Rod.

Cast distance in the sims: uniform 10-22 m x rod cast multiplier x 0.85 + 1.5 m. Fixed step is 0.02 s.

## What changed in the fight

| | before | after |
|---|---|---|
| thrash | one sine for every fish, plus random 1-tick spikes (3%/tick) | 3 personalities (Steady / Thrasher / Runner), each with its own rhythm |
| lunges | random single-tick spikes with no warning (snaps felt random) | scheduled every few s, **telegraphed** for 0.55-0.8 s (`LungeWarning` 0 to 1), then 0.35-1.1 s of extra pull |
| slack line | fish swims at a constant pull | Runners speed up by up to +70% after 1.2 s of slack line |
| fatigue | none | holding tires the fish (up to -35% strength over 6+6*strength s); it recovers slowly when you let go |
| snap | possible from a random spike | only while **holding**; slack line can never snap (this is unit tested) |
| tension relax | 0.9/s | 1.0/s |
| distance pace | FishPull 2.8, escape +8 m, reel 2.2/3/4/5.2 | FishPull 3.8, escape +10.8 m, reel 2.95/4.05/5.4/7.0 (all x1.35: same tiers, ~30% shorter fights) |
| bite window | 1.1 s | 1.1-1.45 s (heavier fish take the bait more slowly) |

## Fight win rates per rod (tier targets)

Target: each rod makes the next bait tier's fish reliable (>80%), while the tier after it stays hard (<35%).
Legendaries need the Storm Rod. Values are win% for **after (attentive)** / after (human) / **before (human)**.

| creature (bait tier) | Driftwood | Bamboo | Carbon | Storm |
|---|---|---|---|---|
| Puddle Minnow (bread) | **100** / 92 / 76 | 100 / 100 / 100 | 100 | 100 |
| Grumpy Perch (bread) | **96** / 88 / 2 | 100 / 100 / 97 | 100 | 100 |
| Stripe Bass (worm) | 33 / 13 / 0 | **99** / 79 / 60 | 100 | 100 |
| Mud Gulper (worm) | 5 / 2 / 0 | **90** / 80 / 18 | 100 / 100 / 91 | 100 |
| Pebblefin (worm) | 23 / 9 / 0 | **91** / 75 / 88 | 100 | 100 |
| Snorkel Crab (glowberry) | 0 | 22 / 15 / 41 | **97** / 93 / 100 | 100 |
| Glimmer Eel (glowberry) | 0 | 36 / 11 / 1 | **91** / 81 / 71 | 100 |
| Moon Carp (glowberry) | 0 | 18 / 13 / 22 | **96** / 91 / 96 | 100 |
| Candy Wrasse (Coral) | 0 | 34 / 7 / 43 | **95** / 82 / 100 | 100 |
| Prism Squid (Coral, rare) | 0 | 20 / 5 / 0 | **89** / 68 / 61 | 100 / 98 / 99 |
| Crowned Sunfish (glowberry, rare) | 0 | 0 | 67 / 61 / 22 | 100 / 99 / 87 |
| Static Ray (spoon) | 0 | 0 | 31 / 8 / 46 | **99** / 87 / 97 |
| Tidal Tuna (spoon) | 0 | 0 | 2 / 1 / 3 | **92** / 70 / 70 |
| Abyss Lantern (spoon) | 0 | 0 | 14 / 7 / 35 | **93** / 91 / 96 |
| Thunderhead Grouper (Stormrock, rare) | 0 | 0 | 4 / 1 / 0 | 79 / 68 / 58 |
| Old Whiskers (legendary) | 0 | 0 | 0 | **40** / 29 / 8 |
| Kraken Pup (legendary) | 0 | 0 | 1 / 0 / 0 | **59** / 43 / 15 |

Before the change the starter rod could not land the starter Perch (2% for a human bot), and both legendaries
were almost impossible even with the Storm Rod (8-15%).

Mean fight length (attentive, winning fights), own tier: bread on Driftwood takes 9-15 s, worm fish on Bamboo
10-15 s, glowberry fish on Carbon 8-10 s, spoon fish on Storm 8-16 s, legendaries 10-32 s.

### Creature changes (Data.cs)

| creature | worth before -> after | strength before -> after | style |
|---|---|---|---|
| Soggy Boot | 1 -> 2 | 0.25 | Steady |
| Puddle Minnow | 3 -> 6 | 0.35 -> 0.32 | Thrasher |
| Grumpy Perch | 7 -> 12 | 0.5 -> 0.40 | Steady |
| Stripe Bass | 14 -> 20 | 0.65 -> 0.58 | Runner |
| Mud Gulper | 18 -> 26 | 0.8 -> 0.62 | Steady |
| Pebblefin | 26 -> 32 | 0.55 -> 0.60 | Thrasher |
| Snorkel Crab | 48 | 0.7 -> 0.80 | Steady |
| Glimmer Eel | 55 | 0.9 -> 0.85 | Thrasher |
| Moon Carp | 70 | 0.75 -> 0.82 | Steady |
| Candy Wrasse | 85 | 0.7 -> 0.85 | Thrasher |
| Static Ray | 190 | 1.0 -> 1.12 | Runner |
| Tidal Tuna | 240 | 1.25 | Runner |
| Crowned Sunfish | 260 | 1.1 -> 0.95 | Steady |
| Prism Squid | 320 | 0.95 -> 0.90 | Thrasher |
| Abyss Lantern | 420 | 1.05 -> 1.15 | Steady |
| Thunderhead Grouper | 650 | 1.3 -> 1.25 | Steady |
| Old Whiskers | 1800 | 1.6 -> 1.45 | Runner |
| Kraken Pup | 2600 | 1.55 -> 1.50 | Thrasher |

### Gear and price changes

| item | before | after |
|---|---|---|
| Bamboo Rod | $150, reel 3.0 | **$75**, reel 4.05, cast x1.08 |
| Carbon Rod | $900, reel 4.0 | **$750**, reel 5.4, cast x1.16 |
| Storm Rod | $4000, reel 5.2 | **$7000**, reel 7.0, cast x1.25 |
| Driftwood Rod | reel 2.2 | reel 2.95, cast x1.0 |
| Bucket | 4 slots | **5 slots** |
| Coral Key ticket | $250 | **$350** (still needs 5 species) |
| Stormrock ticket | $1500 | **$2000** (still needs 10 species) |
| Bread Crust bite | 4-7 s | 3.5-6 s (nibbles happen inside this wait, so they add no time) |

## Economy

Model: each cast costs 0.9 s charge + 1.2 s flight + the bite wait (6% of bites are missed and re-rolled)
+ 0.35 s to hook + the fight + 1.2 s after. A selling trip costs 16 s and a ferry ride 8 s. Bait cost counts
as cost/pack per bait used (catch: `Consume` chance; snap: always). Clean catches (x1.2) and golden (4%, x3)
are included. Catch of the Day is NOT included (it adds about +5% on average).

Net $/min after the change (attentive; Brine / Coral / Stormrock):

| bait | Driftwood | Bamboo | Carbon | Storm |
|---|---|---|---|---|
| bread | 20 | 26 | 33 | 39 |
| worm | 12 | **52** / 53 / 52 | 81 / 103 / 81 | 107 / 137 / 107 |
| glowberry | -104 | 2 / 18 / -7 | **180 / 241** / 180 | 281 / 398 / 366 |
| spoon (Stormrock) | -1676 | -1254 | -471 | **762** |

Before (human bot, old model): bread on Driftwood earned 4 $/min (40% of bites lost), worm on Bamboo 22-32,
glowberry on Carbon 135-211, spoon on Storm 486.

The policy bot buys the next rod as soon as it can, buys coolers when it has $40 spare, buys tickets as soon as
it is eligible, fishes the best-paying bait (with a 15% bonus for baits that can still give new species),
and stocks 2 packs of bait.

### Time from a fresh save (median minutes over 16 runs, [min..max])

| milestone | target | before (human) | after (attentive) | after (human) |
|---|---|---|---|---|
| first sale | | 2.2 | 2.0 | 1.8 |
| first upgrade (Bamboo Rod) | 3-5 | 34.6 | **4.0** [2.1..6.2] | 4.0 [1.8..5.7] |
| 5 species | | 39.1 | 5.9 | 5.0 |
| Cooler | | 60.2 | 10.3 | 9.9 |
| Coral Key | 15-20 | 50.0 | **16.2** [12.2..19.3] | 16.7 [12.1..19.4] |
| Carbon Rod | | 89.0 | 29.6 | 30.7 |
| 10 species | | 92.7 | 31.9 | 33.5 |
| Ice Chest | | 107.0 | 37.2 | 37.5 |
| Stormrock | 45-60 | 100.0 | **44.8** [40.5..50.4] | 47.6 [44.1..56.8] |
| Storm Rod | ~90 | 132.7 | **83.8** [74.4..91.0] | 88.9 [79.0..95.4] |

Real players walk around, read menus and miss more bites than the bot, so expect these times to run about
10-20% longer, which puts them closer to the middle of each target range.

## Retuning

1. Edit `Data.cs`, and the same numbers in `sim.py` (`CRE`, `RODS`) / `econ.py` (`BAITS`, `COOLERS`, `ISLANDS`).
2. `python -I sim.py` for the tier table; `python -I econ.py attentive 16` for the milestones.
3. If you change `FightModel.cs`, change `fightmodel.py` the same way. Then run the PlayMode tests:
   `GameplayTests.FightTiers_EachRodOpensTheNextBait` guards the tier promises, and its log prints the C# win rates.
