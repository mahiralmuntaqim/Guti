class_name Avatar
extends Node3D
## How other players appear: a chunky angler with a name tag, rod, bobber and line.
## Driven entirely by network state snapshots, smoothed between updates.

var peer_id := 0
var display_name := ""
var body_color := Color.WHITE

var _target_pos := Vector3.ZERO
var _target_yaw := 0.0
var _target_pitch := 0.0
var _bobber_target := Vector3.ZERO
var _has_bobber := false
var _tension := 0.0
var _has_state := false
var _last_step_pos := Vector3.ZERO

var _head_pivot: Node3D
var _rod_tip: Marker3D
var _bobber: MeshInstance3D
var _line_mesh: ImmediateMesh
var _label: Label3D
var _trophy: Node3D
var _trophy_timer := 0.0


func _ready() -> void:
	physics_interpolation_mode = Node.PHYSICS_INTERPOLATION_MODE_OFF  # smoothed per frame in _process
	var body := MeshInstance3D.new()
	var cap := CapsuleMesh.new()
	cap.radius = 0.35
	cap.height = 1.4
	body.mesh = cap
	body.position.y = 0.75
	body.material_override = _mat(body_color)
	add_child(body)

	_head_pivot = Node3D.new()
	_head_pivot.position.y = 1.6
	add_child(_head_pivot)
	var head := MeshInstance3D.new()
	var hm := SphereMesh.new()
	hm.radius = 0.26
	hm.height = 0.52
	head.mesh = hm
	head.material_override = _mat(Color(0.96, 0.8, 0.66))
	_head_pivot.add_child(head)
	var hat := MeshInstance3D.new()
	var hatm := CylinderMesh.new()
	hatm.top_radius = 0.2
	hatm.bottom_radius = 0.34
	hatm.height = 0.14
	hat.mesh = hatm
	hat.position.y = 0.24
	hat.material_override = _mat(body_color.darkened(0.35))
	_head_pivot.add_child(hat)
	for side in [-1.0, 1.0]:
		var eye := MeshInstance3D.new()
		var em := SphereMesh.new()
		em.radius = 0.04
		em.height = 0.08
		eye.mesh = em
		eye.position = Vector3(side * 0.1, 0.04, -0.23)
		eye.material_override = _mat(Color(0.05, 0.05, 0.05))
		_head_pivot.add_child(eye)

	var rod := MeshInstance3D.new()
	var rm := CylinderMesh.new()
	rm.top_radius = 0.008
	rm.bottom_radius = 0.02
	rm.height = 1.4
	rod.mesh = rm
	rod.material_override = _mat(Color(0.45, 0.3, 0.15))
	rod.rotation_degrees = Vector3(-55, 0, 0)
	rod.position = Vector3(0.35, -0.2, -0.55)
	_head_pivot.add_child(rod)
	_rod_tip = Marker3D.new()
	_rod_tip.position = Vector3(0.35, 0.37, -0.97)
	_head_pivot.add_child(_rod_tip)

	_label = Label3D.new()
	_label.text = display_name
	_label.font_size = 48
	_label.pixel_size = 0.006
	_label.outline_size = 12
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.modulate = body_color.lightened(0.4)
	_label.position.y = 2.25
	_label.no_depth_test = true
	add_child(_label)

	_bobber = MeshInstance3D.new()
	var bm := SphereMesh.new()
	bm.radius = 0.11
	bm.height = 0.22
	_bobber.mesh = bm
	_bobber.material_override = _mat(Color(0.9, 0.15, 0.1))
	_bobber.top_level = true
	_bobber.visible = false
	add_child(_bobber)

	_line_mesh = ImmediateMesh.new()
	var line := MeshInstance3D.new()
	line.mesh = _line_mesh
	line.top_level = true
	var lm := StandardMaterial3D.new()
	lm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	lm.albedo_color = Color(0.95, 0.95, 0.9)
	line.material_override = lm
	add_child(line)


func _mat(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.roughness = 0.7
	return m


func apply_state(s: Dictionary) -> void:
	_target_pos = s.get("p", _target_pos)
	_target_yaw = float(s.get("y", _target_yaw))
	_target_pitch = float(s.get("x", _target_pitch))
	_tension = float(s.get("t", 0.0))
	var b: Variant = s.get("b", null)
	_has_bobber = b is Vector3
	if _has_bobber:
		_bobber_target = b
	if not _has_state:
		_has_state = true
		global_position = _target_pos
		rotation.y = _target_yaw
		_bobber.global_position = _bobber_target
		_last_step_pos = _target_pos


func show_catch(id: String, size: float, golden: bool) -> void:
	if _trophy:
		_trophy.queue_free()
	_trophy = FishModel.build(id, 1.0, golden)
	var length := float(Data.CREATURES[id]["length"]) * size
	_trophy.scale = Vector3.ONE * clampf(0.7 / maxf(length, 0.01), 0.4, 3.0) * size
	_trophy.position = Vector3(0, 2.9, 0)
	add_child(_trophy)
	_trophy_timer = 4.0


func _process(delta: float) -> void:
	if not _has_state:
		visible = false
		return
	visible = true
	var k := clampf(delta * 12.0, 0.0, 1.0)
	var before := global_position
	global_position = global_position.lerp(_target_pos, k)
	rotation.y = lerp_angle(rotation.y, _target_yaw, k)
	_head_pivot.rotation.x = lerpf(_head_pivot.rotation.x, _target_pitch, k)
	# footsteps for remote players, so you hear friends walking by
	var flat := Vector2(global_position.x - _last_step_pos.x, global_position.z - _last_step_pos.z)
	if flat.length() > 1.9 and before.distance_to(global_position) < 1.0:
		_last_step_pos = global_position
		Sfx.play_at("step_wood", global_position, randf_range(0.9, 1.1), -6.0)
	elif flat.length() > 1.9:
		_last_step_pos = global_position

	_bobber.visible = _has_bobber
	_line_mesh.clear_surfaces()
	if _has_bobber:
		_bobber.global_position = _bobber.global_position.lerp(_bobber_target, k)
		var a := _rod_tip.global_position
		var b := _bobber.global_position
		var sag := minf(a.distance_to(b) * 0.06, 1.2) * (1.0 - clampf(_tension * 2.0, 0.0, 1.0))
		_line_mesh.surface_begin(Mesh.PRIMITIVE_LINE_STRIP)
		for i in range(13):
			var t := i / 12.0
			var p := a.lerp(b, t)
			p.y -= sag * 4.0 * t * (1.0 - t)
			_line_mesh.surface_add_vertex(p)
		_line_mesh.surface_end()

	if _trophy:
		_trophy_timer -= delta
		_trophy.rotate_y(delta * 2.0)
		if _trophy_timer <= 0.0:
			_trophy.queue_free()
			_trophy = null
