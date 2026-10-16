class_name Fishing
extends Node
## Rod state machine: charge -> cast -> wait -> bite -> tension fight -> catch.
## Hold the left mouse button to charge and release to cast. While waiting, hold to reel the
## bobber back. When something bites, click quickly, then reel in bursts: holding builds
## tension (snap at 100%), letting go lets the fish swim away.

enum State { IDLE, CHARGING, WAITING, BITE, FIGHT }

const BITE_WINDOW := 1.1
const RETRIEVE_SPEED := 9.0
const LAND_DISTANCE := 1.8
const ESCAPE_MARGIN := 8.0
# Fight tuning (simulated so each rod tier opens up the next bait tier):
const FISH_PULL := 2.8       # metres/s a fish swims away per unit strength when you're not reeling
const HELD_PULL := 0.6       # fraction of that pull that still drags line while you reel
const TENSION_GAIN := 1.5    # tension build-up per unit strength while reeling (divided by rod line)
const TENSION_RELAX := 0.9   # tension recovery per second when you let go

var state: State = State.IDLE
var player: Player
var bobber: Bobber
var charge := 0.0
var wait_timer := -1.0
var bite_timer := 0.0
var hooked_id := ""
var hooked_size := 1.0
var hooked_golden := false
var tension := 0.0
var distance := 0.0
var max_distance := 0.0
var clean := true
var fight_t := 0.0
var auto_reel := -1  # -1 = read the mouse; 0/1 = forced input (used by the smoke tests)
var rng := RandomNumberGenerator.new()

var _was_pressed := false
var _land_hint_shown := false
var _splashed_down := false
var _tick_timer := 0.0
var _leap_timer := 0.0
var _line: MeshInstance3D
var _line_mesh: ImmediateMesh
var _fight_fish: Node3D
var _trophy: Node3D
var _trophy_timer := 0.0


func _ready() -> void:
	player = get_parent() as Player
	rng.randomize()
	_line_mesh = ImmediateMesh.new()
	_line = MeshInstance3D.new()
	_line.mesh = _line_mesh
	_line.top_level = true
	_line.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.albedo_color = Color(0.95, 0.95, 0.9)
	_line.material_override = mat
	add_child(_line)


func reel_pressed() -> bool:
	if auto_reel >= 0:
		return auto_reel == 1
	if player.ui_open or Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		return false
	return Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT)


func _physics_process(delta: float) -> void:
	var pressed := reel_pressed()
	var just_pressed := pressed and not _was_pressed
	var just_released := not pressed and _was_pressed
	_was_pressed = pressed

	match state:
		State.IDLE:
			if just_pressed:
				state = State.CHARGING
				charge = 0.0
		State.CHARGING:
			charge = minf(charge + delta * 0.8, 1.0)
			if just_released:
				_cast()
		State.WAITING:
			_process_waiting(delta, pressed)
		State.BITE:
			bite_timer -= delta
			if just_pressed:
				_start_fight()
			elif bite_timer <= 0.0:
				Game.notify("It nibbled the bait and left...", Color(0.8, 0.8, 0.8))
				state = State.WAITING
				wait_timer = _new_wait_time()
		State.FIGHT:
			_process_fight(delta, pressed)

	_update_line()
	if _trophy:
		_trophy_timer -= delta
		_trophy.rotate_y(delta * 2.0)
		if _trophy_timer <= 0.0:
			_trophy.queue_free()
			_trophy = null


## Snapshot of what other players need to draw our rod (sent by main at ~20 Hz).
func net_state() -> Dictionary:
	var d := {"t": tension if state == State.FIGHT else 0.0}
	if is_instance_valid(bobber):
		d["b"] = bobber.global_position
	return d


func _new_wait_time() -> float:
	var b: Vector2 = Game.bait()["bite"]
	return rng.randf_range(b.x, b.y)


func _reel_ticks(delta: float, speed_factor: float) -> void:
	_tick_timer -= delta
	if _tick_timer <= 0.0:
		_tick_timer = 0.09 / maxf(speed_factor, 0.3)
		Sfx.play("tick", rng.randf_range(0.9, 1.1) * clampf(speed_factor, 0.8, 1.4), -8.0)


func _cast() -> void:
	if not Game.has_bait(Game.current_bait):
		Game.current_bait = 0
	bobber = Bobber.new()
	get_tree().current_scene.add_child(bobber)
	bobber.global_position = player.rod_tip.global_position
	var fwd: Vector3 = -player.camera.global_basis.z
	bobber.linear_velocity = fwd * (5.0 + 13.0 * charge) + Vector3.UP * (2.0 + 2.0 * charge)
	state = State.WAITING
	wait_timer = -1.0
	_land_hint_shown = false
	_splashed_down = false
	Sfx.play("whoosh", 0.8 + charge * 0.5)


func _process_waiting(delta: float, pressed: bool) -> void:
	if not is_instance_valid(bobber):
		reset()
		return
	if bobber.in_water and not _splashed_down:
		_splashed_down = true
		Sfx.play_at("plop", bobber.global_position)
		Sfx.play_at("splash", bobber.global_position, 1.3, -10.0)
	if pressed:
		bobber.reel_toward(player.rod_tip.global_position, RETRIEVE_SPEED)
		_reel_ticks(delta, 1.6)
		if _flat_distance() < 2.5:
			reset()
		return
	if bobber.in_water:
		if wait_timer < 0.0:
			wait_timer = _new_wait_time()
		wait_timer -= delta
		if wait_timer <= 0.0:
			_bite()
	elif bobber.on_land and bobber.linear_velocity.length() < 0.3 and not _land_hint_shown:
		_land_hint_shown = true
		Game.notify("That's dry land. Hold click to reel in and cast into the water.", Color(1, 0.8, 0.5))


func _bite() -> void:
	hooked_id = Data.roll_creature(Game.current_bait, Game.current_island, rng)
	hooked_size = Data.roll_size(rng)
	hooked_golden = not Data.CREATURES[hooked_id].get("junk", false) and rng.randf() < Data.GOLDEN_CHANCE
	state = State.BITE
	bite_timer = BITE_WINDOW
	bobber.tug(0.5)
	Sfx.play_at("plop", bobber.global_position, 0.8)
	Sfx.play_at("splash", bobber.global_position, 1.1, -4.0)
	Game.notify("!  Something bit. CLICK!", Color(1, 0.95, 0.4))


func _start_fight() -> void:
	state = State.FIGHT
	distance = _flat_distance()
	max_distance = distance + ESCAPE_MARGIN
	tension = 0.15
	clean = true
	fight_t = rng.randf() * 10.0
	_leap_timer = rng.randf_range(1.5, 3.0)
	bobber.freeze = true
	_fight_fish = FishModel.build(hooked_id, hooked_size, hooked_golden)
	_fight_fish.top_level = true
	get_tree().current_scene.add_child(_fight_fish)
	Sfx.play_at("splash", bobber.global_position)


func _process_fight(delta: float, pressed: bool) -> void:
	if not is_instance_valid(bobber):
		reset()
		return
	var strength := Data.fight_strength(hooked_id, hooked_size, hooked_golden)
	fight_t += delta
	var thrash := 0.55 + 0.45 * sin(fight_t * (2.0 + strength * 2.0))
	var lunge := rng.randf() < 0.03
	if lunge:
		thrash += rng.randf() * 0.6  # sudden lunge
	var r: Dictionary = Game.rod()
	var pull := strength * thrash * FISH_PULL
	if pressed:
		# reeling: the rod wins line back, but strong fish still drag some out
		tension += (strength * thrash * TENSION_GAIN + 0.3) * delta / float(r["line"])
		distance += (pull * HELD_PULL - float(r["reel"])) * delta
		_reel_ticks(delta, float(r["reel"]) / 2.2)
	else:
		tension -= TENSION_RELAX * delta
		distance += pull * delta
	tension = clampf(tension, 0.0, 1.2)
	if tension > Data.CLEAN_TENSION_LIMIT:
		clean = false

	# drag the bobber along the line toward/away from the player
	var tip: Vector3 = player.rod_tip.global_position
	var dir := bobber.global_position - tip
	dir.y = 0.0
	dir = dir.normalized() if dir.length() > 0.01 else -player.global_basis.z
	var pos := Vector3(tip.x, 0.0, tip.z) + dir * distance
	var side := dir.cross(Vector3.UP) * sin(fight_t * 6.0) * 0.25 * strength
	pos += side
	pos.y = Water.height_at(pos.x, pos.z) - 0.05 - thrash * 0.06
	bobber.global_position = pos
	_update_fight_fish(delta, dir, pos, strength, lunge)

	if tension >= 1.0:
		Sfx.play("snap")
		Game.notify("SNAP! The line broke%s." % ("" if bool(Game.bait()["infinite"]) else " and took your bait"), Color(1, 0.4, 0.3))
		if not bool(Game.bait()["infinite"]):
			Game.consume_bait()
		reset()
	elif distance >= max_distance:
		Sfx.play_at("splash", bobber.global_position)
		Sfx.play("escape", 1.0, -6.0)
		Game.notify("It got away!", Color(1, 0.6, 0.4))
		reset()
	elif distance <= LAND_DISTANCE:
		_land_catch()


## The hooked creature swims just under the bobber, tail-wagging away from you, and now and
## then leaps clear of the water.
func _update_fight_fish(delta: float, dir: Vector3, bob_pos: Vector3, strength: float, lunge: bool) -> void:
	if not is_instance_valid(_fight_fish):
		return
	_leap_timer -= delta
	var p := bob_pos + dir * 0.4
	var depth := -0.35
	if _leap_timer < 0.0:
		var t := -_leap_timer / 0.6  # leap lasts 0.6 s
		depth = sin(t * PI) * (0.8 + strength * 0.5)
		if t >= 1.0:
			_leap_timer = rng.randf_range(2.0, 4.0) / maxf(strength, 0.5)
			Sfx.play_at("splash", p, rng.randf_range(0.9, 1.2), -2.0)
	elif lunge:
		Sfx.play_at("splash", p, 1.4, -10.0)
	p.y = Water.height_at(p.x, p.z) + depth
	_fight_fish.global_position = p
	var heading := atan2(-dir.z, dir.x)  # model faces +X; rotate so +X points along dir (away from player)
	_fight_fish.rotation = Vector3(0, heading + sin(fight_t * 14.0) * 0.35, 0)


func _land_catch() -> void:
	var c: Dictionary = Data.CREATURES[hooked_id]
	var value := Data.catch_value(hooked_id, hooked_size, hooked_golden, clean)
	var cm := int(round(float(c["length"]) * hooked_size * 100.0))
	var title := Data.catch_title(hooked_id, hooked_golden)
	var splash_pos := bobber.global_position if is_instance_valid(bobber) else player.global_position
	Sfx.play_at("splash_big", splash_pos)
	if Game.add_catch(hooked_id, hooked_size, hooked_golden, clean):
		var extra := "  Clean catch!" if clean else ""
		var col: Color = Color(1, 0.85, 0.2) if hooked_golden else Data.RARITY_COLORS[int(c["rarity"])]
		Game.notify("Caught %s  (%d cm, %s)  worth $%d%s" % [title, cm, Data.RARITY_NAMES[int(c["rarity"])], value, extra], col)
		Sfx.play("golden" if hooked_golden or int(c["rarity"]) >= 2 else "jingle")
		_show_trophy()
		Net.send_event("catch", {"id": hooked_id, "size": hooked_size, "golden": hooked_golden, "value": value})
	else:
		Sfx.play("deny")
		Game.notify("Your %s is full! You let the %s go. Sell at the Market." % [Data.COOLERS[Game.cooler_level]["name"], title], Color(1, 0.6, 0.4))
	if rng.randf() < float(Game.bait()["consume"]):
		Game.consume_bait()
	reset()


func _show_trophy() -> void:
	if _trophy:
		_trophy.queue_free()
	_trophy = FishModel.build(hooked_id, 1.0, hooked_golden)
	# show it at a readable size in front of the camera no matter how big it really is
	var length := float(Data.CREATURES[hooked_id]["length"])
	_trophy.scale = Vector3.ONE * clampf(0.55 / length, 0.3, 3.0)
	player.camera.add_child(_trophy)
	_trophy.position = Vector3(0.0, -0.1, -1.1)
	_trophy.rotation = Vector3(0.2, 0.0, 0.0)
	_trophy_timer = 2.8


func _flat_distance() -> float:
	var a: Vector3 = player.rod_tip.global_position
	var b := bobber.global_position
	return Vector2(a.x - b.x, a.z - b.z).length()


func reset() -> void:
	if is_instance_valid(bobber):
		bobber.queue_free()
	bobber = null
	if is_instance_valid(_fight_fish):
		_fight_fish.queue_free()
	_fight_fish = null
	state = State.IDLE
	charge = 0.0
	tension = 0.0
	wait_timer = -1.0


func _update_line() -> void:
	_line_mesh.clear_surfaces()
	if not is_instance_valid(bobber):
		return
	var a: Vector3 = player.rod_tip.global_position
	var b := bobber.global_position
	var sag := 0.0 if state == State.FIGHT else minf(a.distance_to(b) * 0.06, 1.2)
	_line_mesh.surface_begin(Mesh.PRIMITIVE_LINE_STRIP)
	for i in range(17):
		var t := i / 16.0
		var p := a.lerp(b, t)
		p.y -= sag * 4.0 * t * (1.0 - t)
		_line_mesh.surface_add_vertex(p)
	_line_mesh.surface_end()
