extends Node
## Player progress: money, baits, gear, cooler contents, journal. Saved to user://.

signal changed
signal message(text: String, color: Color)

const SAVE_PATH := "user://brinetime_save.json"
const STARTING_MONEY := 20

var money := STARTING_MONEY
var bait_counts: Array = [0, 0, 0, 0]
var current_bait := 0
var rod_level := 0
var cooler_level := 0
var cooler: Array = []       # [{id, size, golden, clean, value}]
var journal: Dictionary = {} # id -> {"count": int, "best": float}
var lifetime_earnings := 0
var current_island := 0
var unlocked_islands: Array = [0]
var persist := true          # false in automated tests so the real save isn't touched


func _ready() -> void:
	load_game()


func notify(text: String, color: Color = Color.WHITE) -> void:
	message.emit(text, color)


func rod() -> Dictionary:
	return Data.RODS[rod_level]


func island() -> Dictionary:
	return Data.ISLANDS[current_island]


func species_count() -> int:
	return journal.size()


## Empty string if the island can be unlocked right now, otherwise the reason it can't.
func unlock_block_reason(i: int) -> String:
	var isl: Dictionary = Data.ISLANDS[i]
	if species_count() < int(isl["species"]):
		return "Discover %d species first (%d / %d)" % [isl["species"], species_count(), isl["species"]]
	if money < int(isl["cost"]):
		return "Ticket costs $%d" % isl["cost"]
	return ""


func unlock_island(i: int) -> bool:
	if i in unlocked_islands or unlock_block_reason(i) != "":
		return false
	money -= int(Data.ISLANDS[i]["cost"])
	unlocked_islands.append(i)
	changed.emit()
	save_game()
	return true


func travel(i: int) -> bool:
	if not (i in unlocked_islands):
		return false
	current_island = i
	changed.emit()
	save_game()
	return true


func cooler_slots() -> int:
	return int(Data.COOLERS[cooler_level]["slots"])


func cooler_full() -> bool:
	return cooler.size() >= cooler_slots()


func bait() -> Dictionary:
	return Data.BAITS[current_bait]


func has_bait(i: int) -> bool:
	return bool(Data.BAITS[i]["infinite"]) or int(bait_counts[i]) > 0


func select_bait(i: int) -> void:
	if i < 0 or i >= Data.BAITS.size():
		return
	if not has_bait(i):
		notify("You're out of %s. Buy some at the General Store." % Data.BAITS[i]["name"], Color(1, 0.6, 0.4))
		return
	current_bait = i
	changed.emit()


func consume_bait() -> void:
	if bool(bait()["infinite"]):
		return
	bait_counts[current_bait] = int(bait_counts[current_bait]) - 1
	if int(bait_counts[current_bait]) <= 0:
		notify("Last %s used. Back to Bread Crust." % bait()["name"], Color(1, 0.8, 0.4))
		current_bait = 0
	changed.emit()


func add_catch(id: String, size: float, golden: bool, clean: bool) -> bool:
	if cooler_full():
		return false
	var value := Data.catch_value(id, size, golden, clean)
	cooler.append({"id": id, "size": size, "golden": golden, "clean": clean, "value": value})
	var entry: Dictionary = journal.get(id, {"count": 0, "best": 0.0})
	entry["count"] = int(entry["count"]) + 1
	entry["best"] = maxf(float(entry["best"]), size)
	journal[id] = entry
	changed.emit()
	save_game()
	return true


func sell_all() -> int:
	var total := 0
	for c in cooler:
		total += int(c["value"])
	if total > 0:
		money += total
		lifetime_earnings += total
		cooler.clear()
		changed.emit()
		save_game()
	return total


func spend(amount: int) -> bool:
	if money < amount:
		return false
	money -= amount
	changed.emit()
	return true


func buy_bait(i: int) -> bool:
	var b: Dictionary = Data.BAITS[i]
	if bool(b["infinite"]) or not (i in island()["baits"]) or not spend(int(b["cost"])):
		return false
	bait_counts[i] = int(bait_counts[i]) + int(b["pack"])
	changed.emit()
	save_game()
	return true


func buy_rod() -> bool:
	if rod_level + 1 > int(island()["max_rod"]) or not spend(int(Data.RODS[rod_level + 1]["cost"])):
		return false
	rod_level += 1
	changed.emit()
	save_game()
	return true


func buy_cooler() -> bool:
	if cooler_level + 1 > int(island()["max_cooler"]) or not spend(int(Data.COOLERS[cooler_level + 1]["cost"])):
		return false
	cooler_level += 1
	changed.emit()
	save_game()
	return true


func reset_state() -> void:
	money = STARTING_MONEY
	bait_counts = [0, 0, 0, 0]
	current_bait = 0
	rod_level = 0
	cooler_level = 0
	cooler = []
	journal = {}
	lifetime_earnings = 0
	current_island = 0
	unlocked_islands = [0]
	changed.emit()


func save_game() -> void:
	if not persist:
		return
	var data := {
		"money": money, "bait_counts": bait_counts, "current_bait": current_bait,
		"rod_level": rod_level, "cooler_level": cooler_level, "cooler": cooler,
		"journal": journal, "lifetime_earnings": lifetime_earnings,
		"current_island": current_island, "unlocked_islands": unlocked_islands,
	}
	var f := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if f:
		f.store_string(JSON.stringify(data, "\t"))


func load_game() -> void:
	if not FileAccess.file_exists(SAVE_PATH):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(SAVE_PATH))
	if typeof(parsed) != TYPE_DICTIONARY:
		push_warning("Save file unreadable, starting fresh.")
		return
	var d: Dictionary = parsed
	money = int(d.get("money", 0))
	var counts: Array = d.get("bait_counts", [0, 0, 0, 0])
	for i in range(mini(counts.size(), bait_counts.size())):
		bait_counts[i] = int(counts[i])
	current_bait = clampi(int(d.get("current_bait", 0)), 0, Data.BAITS.size() - 1)
	rod_level = clampi(int(d.get("rod_level", 0)), 0, Data.RODS.size() - 1)
	cooler_level = clampi(int(d.get("cooler_level", 0)), 0, Data.COOLERS.size() - 1)
	cooler = []
	for c in d.get("cooler", []):
		if typeof(c) == TYPE_DICTIONARY and Data.CREATURES.has(c.get("id", "")):
			cooler.append(c)
	journal = {}
	var j: Dictionary = d.get("journal", {})
	for id in j.keys():
		if Data.CREATURES.has(id):
			journal[id] = {"count": int(j[id].get("count", 0)), "best": float(j[id].get("best", 0.0))}
	lifetime_earnings = int(d.get("lifetime_earnings", 0))
	unlocked_islands = [0]
	for i in d.get("unlocked_islands", [0]):
		var n := int(i)
		if n > 0 and n < Data.ISLANDS.size() and not (n in unlocked_islands):
			unlocked_islands.append(n)
	current_island = int(d.get("current_island", 0))
	if not (current_island in unlocked_islands):
		current_island = 0
	if not has_bait(current_bait):
		current_bait = 0
