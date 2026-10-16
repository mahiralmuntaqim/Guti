extends Node
## Content tables for Brine Time: creatures (with model shapes), baits, rods, coolers, islands.
## Tweak numbers here to rebalance the game.

const GOLDEN_CHANCE := 0.04      # chance a non-junk catch is a golden variant
const GOLDEN_MULT := 3.0         # golden catches sell for this much more
const CLEAN_CATCH_MULT := 1.2    # bonus for landing a fish without ever nearing a snap
const CLEAN_TENSION_LIMIT := 0.85

const RARITY_NAMES := ["Common", "Uncommon", "Rare", "Legendary"]
const RARITY_COLORS := [Color(0.85, 0.85, 0.85), Color(0.45, 0.9, 0.45), Color(0.4, 0.65, 1.0), Color(1.0, 0.7, 0.2)]

# worth: base sell price at size 1.0 | strength: how hard it fights (0.2 .. 1.6) | length: metres at size 1.0
# model: shape (fish/eel/ray/crab/squid/boot), h/w = body height/width as a fraction of length,
#        tail (fork/round/none), pattern (plain/belly/stripes/spots), color2, extra (special parts)
const CREATURES := {
	"soggy_boot": {"name": "Soggy Boot", "worth": 1, "rarity": 0, "strength": 0.25, "length": 0.30, "junk": true,
		"color": Color(0.35, 0.25, 0.18), "model": {"shape": "boot", "color2": Color(0.2, 0.15, 0.1)}},
	"puddle_minnow": {"name": "Puddle Minnow", "worth": 3, "rarity": 0, "strength": 0.35, "length": 0.15,
		"color": Color(0.72, 0.8, 0.84), "model": {"shape": "fish", "h": 0.22, "w": 0.12, "tail": "fork", "pattern": "belly", "color2": Color(0.95, 0.95, 0.95)}},
	"grumpy_perch": {"name": "Grumpy Perch", "worth": 7, "rarity": 0, "strength": 0.5, "length": 0.28,
		"color": Color(0.55, 0.62, 0.3), "model": {"shape": "fish", "h": 0.32, "w": 0.14, "tail": "round", "pattern": "stripes", "color2": Color(0.25, 0.3, 0.15), "extra": "frown"}},
	"stripe_bass": {"name": "Stripe Bass", "worth": 14, "rarity": 0, "strength": 0.65, "length": 0.42,
		"color": Color(0.5, 0.58, 0.45), "model": {"shape": "fish", "h": 0.27, "w": 0.13, "tail": "fork", "pattern": "stripes", "color2": Color(0.18, 0.22, 0.18)}},
	"mud_gulper": {"name": "Mud Gulper", "worth": 18, "rarity": 0, "strength": 0.8, "length": 0.5,
		"color": Color(0.45, 0.36, 0.25), "model": {"shape": "fish", "h": 0.26, "w": 0.2, "tail": "round", "pattern": "spots", "color2": Color(0.3, 0.24, 0.16), "extra": "whiskers"}},
	"pebblefin": {"name": "Pebblefin", "worth": 26, "rarity": 1, "strength": 0.55, "length": 0.3,
		"color": Color(0.6, 0.58, 0.55), "model": {"shape": "fish", "h": 0.38, "w": 0.18, "tail": "round", "pattern": "spots", "color2": Color(0.4, 0.38, 0.36)}},
	"snorkel_crab": {"name": "Snorkel Crab", "worth": 48, "rarity": 1, "strength": 0.7, "length": 0.35,
		"color": Color(0.9, 0.4, 0.25), "model": {"shape": "crab", "color2": Color(1.0, 0.85, 0.3), "extra": "snorkel"}},
	"glimmer_eel": {"name": "Glimmer Eel", "worth": 55, "rarity": 1, "strength": 0.9, "length": 0.9,
		"color": Color(0.3, 0.85, 0.8), "model": {"shape": "eel", "h": 0.07, "w": 0.06, "pattern": "spots", "color2": Color(0.9, 1.0, 0.6), "extra": "glow"}},
	"moon_carp": {"name": "Moon Carp", "worth": 70, "rarity": 1, "strength": 0.75, "length": 0.6,
		"color": Color(0.9, 0.9, 0.75), "model": {"shape": "fish", "h": 0.34, "w": 0.16, "tail": "fork", "pattern": "spots", "color2": Color(0.85, 0.5, 0.2), "extra": "whiskers"}},
	"candy_wrasse": {"name": "Candy Wrasse", "worth": 85, "rarity": 1, "strength": 0.7, "length": 0.35,
		"color": Color(1.0, 0.55, 0.75), "model": {"shape": "fish", "h": 0.3, "w": 0.12, "tail": "round", "pattern": "stripes", "color2": Color(1.0, 0.95, 0.5)}},
	"static_ray": {"name": "Static Ray", "worth": 190, "rarity": 1, "strength": 1.0, "length": 1.1,
		"color": Color(0.35, 0.4, 0.75), "model": {"shape": "ray", "pattern": "spots", "color2": Color(0.8, 0.9, 1.0), "extra": "glow"}},
	"tidal_tuna": {"name": "Tidal Tuna", "worth": 240, "rarity": 1, "strength": 1.25, "length": 1.3,
		"color": Color(0.25, 0.35, 0.55), "model": {"shape": "fish", "h": 0.26, "w": 0.18, "tail": "fork", "pattern": "belly", "color2": Color(0.85, 0.88, 0.9)}},
	"crowned_sunfish": {"name": "Crowned Sunfish", "worth": 260, "rarity": 2, "strength": 1.1, "length": 0.8,
		"color": Color(1.0, 0.8, 0.3), "model": {"shape": "fish", "h": 0.62, "w": 0.14, "tail": "round", "pattern": "belly", "color2": Color(1.0, 0.95, 0.75), "extra": "crown"}},
	"prism_squid": {"name": "Prism Squid", "worth": 320, "rarity": 2, "strength": 0.95, "length": 0.6,
		"color": Color(0.7, 0.5, 1.0), "model": {"shape": "squid", "color2": Color(0.5, 1.0, 0.95), "extra": "glow"}},
	"abyss_lantern": {"name": "Abyss Lantern", "worth": 420, "rarity": 2, "strength": 1.05, "length": 0.7,
		"color": Color(0.15, 0.12, 0.25), "model": {"shape": "fish", "h": 0.4, "w": 0.22, "tail": "round", "pattern": "spots", "color2": Color(0.3, 0.25, 0.45), "extra": "lure"}},
	"thunderhead_grouper": {"name": "Thunderhead Grouper", "worth": 650, "rarity": 2, "strength": 1.3, "length": 1.2,
		"color": Color(0.32, 0.34, 0.4), "model": {"shape": "fish", "h": 0.36, "w": 0.24, "tail": "round", "pattern": "stripes", "color2": Color(0.95, 0.9, 0.3)}},
	"old_whiskers": {"name": "Old Whiskers", "worth": 1800, "rarity": 3, "strength": 1.6, "length": 2.0,
		"color": Color(0.5, 0.42, 0.32), "model": {"shape": "fish", "h": 0.24, "w": 0.2, "tail": "round", "pattern": "spots", "color2": Color(0.35, 0.28, 0.2), "extra": "whiskers"}},
	"kraken_pup": {"name": "Kraken Pup", "worth": 2600, "rarity": 3, "strength": 1.55, "length": 1.0,
		"color": Color(0.55, 0.15, 0.35), "model": {"shape": "squid", "color2": Color(1.0, 0.6, 0.7)}},
}

# bite: min/max seconds in water before something bites | consume: chance a catch uses up one bait
# table: creature id -> relative weight (islands can add exclusive entries on top, see ISLANDS)
const BAITS := [
	{"id": "bread", "name": "Bread Crust", "cost": 0, "pack": 0, "infinite": true, "consume": 0.0,
		"bite": Vector2(4.0, 7.0), "table": {"puddle_minnow": 50, "grumpy_perch": 30, "soggy_boot": 20}},
	{"id": "worm", "name": "Wriggle Worm", "cost": 12, "pack": 5, "infinite": false, "consume": 0.6,
		"bite": Vector2(2.5, 5.0), "table": {"grumpy_perch": 25, "stripe_bass": 30, "mud_gulper": 25, "pebblefin": 15, "soggy_boot": 5}},
	{"id": "glowberry", "name": "Glowberry", "cost": 90, "pack": 5, "infinite": false, "consume": 0.5,
		"bite": Vector2(2.0, 4.0), "table": {"pebblefin": 15, "glimmer_eel": 28, "moon_carp": 24, "snorkel_crab": 28, "crowned_sunfish": 5}},
	{"id": "spoon", "name": "Thunder Spoon", "cost": 600, "pack": 3, "infinite": false, "consume": 0.35,
		"bite": Vector2(1.5, 3.0), "table": {"glimmer_eel": 20, "static_ray": 30, "tidal_tuna": 30, "abyss_lantern": 16, "old_whiskers": 4}},
]

# reel: metres per second while reeling | line: higher = tension builds slower
const RODS := [
	{"name": "Driftwood Rod", "cost": 0, "reel": 2.2, "line": 1.0},
	{"name": "Bamboo Rod", "cost": 150, "reel": 3.0, "line": 1.3},
	{"name": "Carbon Rod", "cost": 900, "reel": 4.0, "line": 1.7},
	{"name": "Storm Rod", "cost": 4000, "reel": 5.2, "line": 2.2},
]

const COOLERS := [
	{"name": "Bucket", "cost": 0, "slots": 4},
	{"name": "Cooler", "cost": 200, "slots": 8},
	{"name": "Ice Chest", "cost": 1200, "slots": 14},
]

# Each island: where it sits, how it looks, what its store sells (bait indices, best rod/cooler level),
# what the ferry ticket costs, how many journal species you need first, and island-only catches
# (bait id -> {creature: weight}) mixed into that bait's table while fishing there.
const ISLANDS := [
	{"id": "brine", "name": "Brine Island", "origin": Vector3(0, 0, 0), "theme": "palm",
		"sand": Color(0.86, 0.78, 0.55), "grass": Color(0.36, 0.62, 0.3), "rock": Color(0.45, 0.45, 0.48),
		"baits": [0, 1, 2], "max_rod": 1, "max_cooler": 1, "cost": 0, "species": 0, "exclusive": {}},
	{"id": "coral", "name": "Coral Key", "origin": Vector3(420, 0, -160), "theme": "coral",
		"sand": Color(0.96, 0.88, 0.82), "grass": Color(0.5, 0.74, 0.42), "rock": Color(0.95, 0.5, 0.55),
		"baits": [0, 1, 2], "max_rod": 2, "max_cooler": 2, "cost": 250, "species": 5,
		"exclusive": {"worm": {"candy_wrasse": 10}, "glowberry": {"candy_wrasse": 18, "prism_squid": 9}, "spoon": {"prism_squid": 10}}},
	{"id": "storm", "name": "Stormrock", "origin": Vector3(-360, 0, -520), "theme": "storm",
		"sand": Color(0.58, 0.57, 0.55), "grass": Color(0.3, 0.42, 0.32), "rock": Color(0.3, 0.3, 0.34),
		"baits": [0, 1, 2, 3], "max_rod": 3, "max_cooler": 2, "cost": 1500, "species": 10,
		"exclusive": {"glowberry": {"thunderhead_grouper": 5}, "spoon": {"thunderhead_grouper": 18, "kraken_pup": 4, "old_whiskers": 3}}},
]


func catch_table(bait_index: int, island_index: int) -> Dictionary:
	var table: Dictionary = (BAITS[bait_index]["table"] as Dictionary).duplicate()
	var extra: Dictionary = ISLANDS[island_index]["exclusive"].get(BAITS[bait_index]["id"], {})
	for id in extra.keys():
		table[id] = int(table.get(id, 0)) + int(extra[id])
	return table


func roll_creature(bait_index: int, island_index: int, rng: RandomNumberGenerator) -> String:
	var table := catch_table(bait_index, island_index)
	var total := 0.0
	for w in table.values():
		total += float(w)
	var r := rng.randf() * total
	for id in table.keys():
		r -= float(table[id])
		if r <= 0.0:
			return String(id)
	return String(table.keys()[-1])


func roll_size(rng: RandomNumberGenerator) -> float:
	return clampf(rng.randfn(1.0, 0.18), 0.45, 1.9)


func catch_value(id: String, size: float, golden: bool, clean: bool) -> int:
	var c: Dictionary = CREATURES[id]
	var v := float(c["worth"]) * size
	if golden:
		v *= GOLDEN_MULT
	if clean:
		v *= CLEAN_CATCH_MULT
	return maxi(1, int(round(v)))


func fight_strength(id: String, size: float, golden: bool) -> float:
	var s := float(CREATURES[id]["strength"])
	s *= lerpf(0.85, 1.2, inverse_lerp(0.45, 1.9, size))
	if golden:
		s *= 1.25
	return s


func catch_title(id: String, golden: bool) -> String:
	return ("Golden " if golden else "") + String(CREATURES[id]["name"])
