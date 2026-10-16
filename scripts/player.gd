class_name Player
extends CharacterBody3D
## First-person castaway: walk, look, jump, interact (E), switch bait (1-4), journal (Tab).

const WALK_SPEED := 4.5
const SPRINT_SPEED := 7.0
const JUMP_VELOCITY := 5.2
const MOUSE_SENS := 0.0025
const INTERACT_RANGE := 3.5
const EYE_HEIGHT := 1.6

var camera: Camera3D
var rod_tip: Marker3D
var fishing: Fishing
var hud: Hud
var spawn_point := Vector3.ZERO
var spawn_yaw := 0.0
var ui_open := false
var automation := false  # tests drive the player; ignore real keyboard/mouse
var sim_move := Vector2.ZERO  # movement input used when automation is on (x = strafe, y = forward/back)
var pitch := 0.0  # camera look up/down; yaw is the body's rotation.y

var _skip_motion_events := 0
var _step_distance := 0.0
var _ray: RayCast3D
var _rod_pivot: Node3D
var _gravity: float = ProjectSettings.get_setting("physics/3d/default_gravity")


func _ready() -> void:
	var col := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.35
	capsule.height = 1.75
	col.shape = capsule
	col.position.y = 0.875
	add_child(col)

	# The body moves on fixed 60 Hz physics ticks and Godot interpolates it between ticks.
	# The camera is detached (top_level) and placed every rendered frame at the interpolated
	# eye position, with mouse-look applied directly, so view and movement stay smooth at any
	# refresh rate (the same tick + smoothing split FishNet uses for owned objects).
	camera = Camera3D.new()
	camera.fov = 75.0
	camera.current = true
	camera.top_level = true
	camera.physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF
	add_child(camera)

	_ray = RayCast3D.new()
	_ray.target_position = Vector3(0, 0, -INTERACT_RANGE)
	_ray.add_exception(self)
	camera.add_child(_ray)

	_build_rod()

	fishing = Fishing.new()
	add_child(fishing)
	capture_mouse()


func capture_mouse() -> void:
	if automation:
		return
	Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
	_skip_motion_events = 2


func _build_rod() -> void:
	_rod_pivot = Node3D.new()
	_rod_pivot.position = Vector3(0.32, -0.32, -0.45)
	camera.add_child(_rod_pivot)
	var rod := MeshInstance3D.new()
	var m := CylinderMesh.new()
	m.top_radius = 0.008
	m.bottom_radius = 0.02
	m.height = 1.3
	rod.mesh = m
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.45, 0.3, 0.15)
	rod.material_override = mat
	rod.rotation_degrees = Vector3(-60, 0, 0)
	rod.position = Vector3(0, 0.3, -0.38)
	_rod_pivot.add_child(rod)
	var reel := MeshInstance3D.new()
	var rm := CylinderMesh.new()
	rm.top_radius = 0.05
	rm.bottom_radius = 0.05
	rm.height = 0.04
	reel.mesh = rm
	reel.rotation_degrees = Vector3(0, 0, 90)
	reel.position = Vector3(0.04, 0.0, 0.0)
	var rmat := StandardMaterial3D.new()
	rmat.albedo_color = Color(0.25, 0.25, 0.28)
	rmat.metallic = 0.7
	reel.material_override = rmat
	_rod_pivot.add_child(reel)
	rod_tip = Marker3D.new()
	rod_tip.position = Vector3(0, 0.62, -0.75)
	_rod_pivot.add_child(rod_tip)


func turn(yaw_delta: float, pitch_delta: float) -> void:
	rotate_y(yaw_delta)
	pitch = clampf(pitch + pitch_delta, -1.45, 1.45)
	_sync_camera()


func _process(_delta: float) -> void:
	_sync_camera()


func _sync_camera() -> void:
	if not is_inside_tree():
		return
	var origin := get_global_transform_interpolated().origin + Vector3(0, EYE_HEIGHT, 0)
	camera.global_transform = Transform3D(Basis.from_euler(Vector3(pitch, rotation.y, 0)), origin)


func respawn() -> void:
	spawn_point = World.spawn_point(Game.current_island)
	global_position = spawn_point
	rotation.y = spawn_yaw
	pitch = 0.0
	velocity = Vector3.ZERO
	reset_physics_interpolation()  # teleport: don't smear from the old position
	_sync_camera()
	_step_distance = 0.0


func _unhandled_input(event: InputEvent) -> void:
	if ui_open or automation:
		return
	var motion := event as InputEventMouseMotion
	var button := event as InputEventMouseButton
	var key := event as InputEventKey
	if motion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		# capturing warps the cursor to the centre, producing one huge bogus delta; skip it
		if _skip_motion_events > 0:
			_skip_motion_events -= 1
			return
		if motion.relative.length() > 250.0:
			return
		turn(-motion.relative.x * MOUSE_SENS, -motion.relative.y * MOUSE_SENS)
	elif button and button.pressed and Input.mouse_mode != Input.MOUSE_MODE_CAPTURED:
		capture_mouse()
		get_viewport().set_input_as_handled()
	elif key and key.pressed and not key.echo:
		match key.physical_keycode:
			KEY_ESCAPE:
				hud.open_pause()
			KEY_E:
				_try_interact()
			KEY_TAB:
				hud.toggle_journal()
			KEY_1, KEY_2, KEY_3, KEY_4:
				if fishing.state == Fishing.State.IDLE:
					Game.select_bait(key.physical_keycode - KEY_1)
				else:
					Game.notify("Reel in before changing bait.", Color(0.9, 0.9, 0.9))


func _physics_process(delta: float) -> void:
	if not is_on_floor():
		velocity.y -= _gravity * delta
	var can_move := not ui_open and not automation and fishing.state != Fishing.State.FIGHT
	if can_move and Input.is_physical_key_pressed(KEY_SPACE) and is_on_floor():
		velocity.y = JUMP_VELOCITY
		Sfx.play("jump", 1.0, -10.0)
	var input := Vector2.ZERO
	if automation:
		input = sim_move
	elif can_move:
		input.x = float(Input.is_physical_key_pressed(KEY_D)) - float(Input.is_physical_key_pressed(KEY_A))
		input.y = float(Input.is_physical_key_pressed(KEY_S)) - float(Input.is_physical_key_pressed(KEY_W))
	var dir := (global_basis * Vector3(input.x, 0, input.y)).normalized()
	var speed := SPRINT_SPEED if Input.is_physical_key_pressed(KEY_SHIFT) else WALK_SPEED
	var accel := 12.0 if is_on_floor() else 3.0
	velocity.x = move_toward(velocity.x, dir.x * speed, accel * speed * delta)
	velocity.z = move_toward(velocity.z, dir.z * speed, accel * speed * delta)
	var was_airborne := not is_on_floor()
	move_and_slide()
	_footsteps(delta, was_airborne)

	if global_position.y < Water.LEVEL - 0.6:
		Sfx.play_at("splash_big", global_position)
		Net.send_event("splash", {"p": global_position})
		fishing.reset()
		respawn()
		Game.notify("Splash! You climb back onto the dock, dripping.", Color(0.6, 0.85, 1.0))

	# rod bends back while charging and dips while fighting
	var target_pitch := 0.0
	if fishing.state == Fishing.State.CHARGING:
		target_pitch = deg_to_rad(35.0) * fishing.charge
	elif fishing.state == Fishing.State.FIGHT:
		target_pitch = deg_to_rad(-12.0) - fishing.tension * deg_to_rad(10.0)
	_rod_pivot.rotation.x = lerpf(_rod_pivot.rotation.x, target_pitch, 10.0 * delta)

	hud.set_prompt(_current_prompt())


func _surface() -> String:
	for i in range(get_slide_collision_count()):
		var c := get_slide_collision(i).get_collider()
		if c and c.has_meta("surface"):
			return String(c.get_meta("surface"))
	return "sand"


func _footsteps(delta: float, was_airborne: bool) -> void:
	if not is_on_floor():
		return
	var flat := Vector2(velocity.x, velocity.z).length()
	if was_airborne:
		Sfx.play("step_wood" if _surface() == "wood" else "step_sand", 0.8, -4.0)
		_step_distance = 0.0
		return
	_step_distance += flat * delta
	var stride := 2.2 if flat > WALK_SPEED + 0.5 else 1.7
	if _step_distance >= stride:
		_step_distance = 0.0
		Sfx.play("step_wood" if _surface() == "wood" else "step_sand", randf_range(0.9, 1.1), -6.0)


## Snapshot of our pose for other players.
func net_state() -> Dictionary:
	var d := fishing.net_state()
	d["p"] = global_position
	d["y"] = rotation.y
	d["x"] = pitch
	d["i"] = Game.current_island
	return d


func _looked_at_interactable() -> Variant:
	if not _ray.is_colliding():
		return null
	var c: Object = _ray.get_collider()
	return c if c and c.has_method("interact") else null


func _current_prompt() -> String:
	if ui_open:
		return ""
	var target: Variant = _looked_at_interactable()
	return ("[E] " + target.get_prompt()) if target else ""


func _try_interact() -> void:
	var target: Variant = _looked_at_interactable()
	if target:
		target.interact(self)
