class_name World
extends Node3D
## Builds every island from Data.ISLANDS: terrain, dock, Fish Market, General Store, Ferry,
## and theme props (palms / coral / storm rocks + lighthouse). Islands sit far apart on one ocean.

const ISLAND_RADIUS := 20.0
const DOCK_START := 18.0
const DOCK_END := 36.0

var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_build_sky()
	for i in range(Data.ISLANDS.size()):
		_build_island(i)


static func spawn_point(island_index: int) -> Vector3:
	return Data.ISLANDS[island_index]["origin"] + Vector3(0, 1.3, DOCK_END - 2.0)


static func island_at(pos: Vector3) -> int:
	var best := 0
	var best_d := INF
	for i in range(Data.ISLANDS.size()):
		var d := pos.distance_to(Data.ISLANDS[i]["origin"])
		if d < best_d:
			best_d = d
			best = i
	return best


# ------------------------------------------------------------------ primitives

func _mat(color: Color, roughness := 0.85) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	m.roughness = roughness
	return m


func _static(parent: Node3D, mesh: Mesh, shape: Shape3D, pos: Vector3, color: Color, rot := Vector3.ZERO) -> StaticBody3D:
	var body := StaticBody3D.new()
	body.position = pos
	body.rotation = rot
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = _mat(color)
	body.add_child(mi)
	if shape:
		var col := CollisionShape3D.new()
		col.shape = shape
		body.add_child(col)
	parent.add_child(body)
	return body


func _box(parent: Node3D, size: Vector3, pos: Vector3, color: Color, collide := true, rot := Vector3.ZERO) -> StaticBody3D:
	var m := BoxMesh.new()
	m.size = size
	var s: BoxShape3D = null
	if collide:
		s = BoxShape3D.new()
		s.size = size
	return _static(parent, m, s, pos, color, rot)


func _cylinder(parent: Node3D, r_top: float, r_bottom: float, height: float, pos: Vector3, color: Color, collide := true, segments := 32) -> StaticBody3D:
	var m := CylinderMesh.new()
	m.top_radius = r_top
	m.bottom_radius = r_bottom
	m.height = height
	m.radial_segments = segments
	var s: CylinderShape3D = null
	if collide:
		s = CylinderShape3D.new()
		s.radius = maxf(r_top, r_bottom)
		s.height = height
	return _static(parent, m, s, pos, color)


func _label(parent: Node3D, text: String, pos: Vector3, size := 96) -> void:
	var board := Label3D.new()
	board.text = text
	board.font_size = size
	board.pixel_size = 0.006
	board.outline_size = 18
	board.modulate = Color(1, 0.97, 0.85)
	board.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	board.position = pos
	parent.add_child(board)


# ------------------------------------------------------------------ world

func _build_sky() -> void:
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.25, 0.5, 0.85)
	sky_mat.sky_horizon_color = Color(0.75, 0.85, 0.95)
	sky_mat.ground_horizon_color = Color(0.6, 0.75, 0.85)
	sky_mat.ground_bottom_color = Color(0.1, 0.25, 0.4)
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	env.fog_enabled = true
	env.fog_light_color = Color(0.7, 0.82, 0.92)
	env.fog_density = 0.006
	env.ssao_enabled = true
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, -35, 0)
	sun.light_energy = 1.2
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 80.0
	add_child(sun)


func _build_island(index: int) -> void:
	var cfg: Dictionary = Data.ISLANDS[index]
	var root := Node3D.new()
	root.name = String(cfg["id"]).capitalize()
	root.position = cfg["origin"]
	add_child(root)
	_rng.seed = 7 + index * 101
	var theme := String(cfg["theme"])
	var sand: Color = cfg["sand"]
	var grass: Color = cfg["grass"]
	var rock: Color = cfg["rock"]

	# terrain: sand body (top at y=1.0), grass cap, a hill
	_cylinder(root, ISLAND_RADIUS, ISLAND_RADIUS + 4.0, 7.0, Vector3(0, -2.5, 0), sand, true, 48)
	_cylinder(root, ISLAND_RADIUS - 4.5, ISLAND_RADIUS - 3.5, 0.3, Vector3(0, 1.1, 0), grass, true, 48)
	_cylinder(root, 5.0, 9.0, 2.2, Vector3(-9, 2.3, -4), grass, true, 32)
	for i in range(7):
		var a := _rng.randf() * TAU
		var d := _rng.randf_range(30.0, 55.0)
		var r := _rng.randf_range(0.8, 2.5) * (1.6 if theme == "storm" else 1.0)
		_cylinder(root, r * 0.6, r, r * 1.6, Vector3(cos(a) * d, r * 0.3 - 0.4, sin(a) * d), rock, true, 7)

	_build_dock(root)
	_build_stall(root, "market", Vector3(-6.5, 1.0, 10.0), Color(0.2, 0.55, 0.85), "FISH MARKET")
	_build_stall(root, "store", Vector3(6.5, 1.0, 10.0), Color(0.85, 0.45, 0.2), "GENERAL STORE")
	_build_stall(root, "ferry", Vector3(7.0, 1.0, -9.0), Color(0.9, 0.9, 0.95), "FERRY")
	_build_ferry_boat(root, Vector3(9.0, 0.0, -24.0))
	_label(root, String(cfg["name"]).to_upper(), Vector3(0, 7.5, 0), 180)

	match theme:
		"coral":
			_scatter(root, 7, func(p: Vector3) -> void: _palm(root, p, Color(0.3, 0.68, 0.35)))
			for i in range(14):
				var a := _rng.randf() * TAU
				var d := _rng.randf_range(ISLAND_RADIUS + 1.0, ISLAND_RADIUS + 6.0)
				_coral(root, Vector3(cos(a) * d, -0.3, sin(a) * d))
			_hut(root, Vector3(-12, 1.0, -12), Color(1.0, 0.75, 0.8))
		"storm":
			_scatter(root, 8, func(p: Vector3) -> void: _pine(root, p))
			_lighthouse(root, Vector3(10, 1.0, -2))
			for i in range(6):
				var a := _rng.randf() * TAU
				var d := _rng.randf_range(ISLAND_RADIUS - 6.0, ISLAND_RADIUS - 2.0)
				var r := _rng.randf_range(1.0, 2.2)
				_cylinder(root, r * 0.5, r, r * 1.5, Vector3(cos(a) * d, 1.0 + r * 0.6, sin(a) * d), rock, true, 6)
		_:
			_scatter(root, 9, func(p: Vector3) -> void: _palm(root, p, Color(0.2, 0.55, 0.22)))
	# crates and a barrel by the market
	_box(root, Vector3(0.8, 0.8, 0.8), Vector3(-9.0, 1.4, 11.0), Color(0.6, 0.45, 0.25), true, Vector3(0, 0.4, 0))
	_box(root, Vector3(0.6, 0.6, 0.6), Vector3(-9.1, 2.1, 11.1), Color(0.65, 0.5, 0.3), true, Vector3(0, -0.3, 0))
	_cylinder(root, 0.35, 0.35, 0.9, Vector3(9.2, 1.45, 11.0), Color(0.35, 0.25, 0.15), true, 12)


func _scatter(root: Node3D, count: int, place: Callable) -> void:
	var placed := 0
	var tries := 0
	while placed < count and tries < 300:
		tries += 1
		var a := _rng.randf() * TAU
		var d := _rng.randf_range(6.0, ISLAND_RADIUS - 3.0)
		var p := Vector3(cos(a) * d, 1.0, sin(a) * d)
		if absf(p.x) < 3.0 and p.z > 4.0:
			continue  # dock path
		var blocked := false
		for spot in [Vector3(-6.5, 1, 10), Vector3(6.5, 1, 10), Vector3(7, 1, -9), Vector3(-9, 1, -4), Vector3(10, 1, -2), Vector3(-12, 1, -12)]:
			if p.distance_to(spot) < 4.5:
				blocked = true
		if blocked:
			continue
		place.call(p)
		placed += 1


func _build_dock(root: Node3D) -> void:
	var length := DOCK_END - DOCK_START
	var mid := (DOCK_START + DOCK_END) * 0.5
	var deck := _box(root, Vector3(3.0, 0.25, length), Vector3(0, 1.0, mid), Color(0.55, 0.38, 0.22))
	deck.set_meta("surface", "wood")
	var z := DOCK_START + 1.0
	while z <= DOCK_END:
		for x in [-1.35, 1.35]:
			_cylinder(root, 0.12, 0.12, 3.0, Vector3(x, -0.4, z), Color(0.4, 0.27, 0.15), false, 8)
		z += 3.0
	_box(root, Vector3(1.6, 0.12, 0.5), Vector3(-0.8, 1.5, DOCK_END - 0.8), Color(0.45, 0.3, 0.18), false)
	var lamp_pos := Vector3(1.35, 2.8, DOCK_START + 1.0)
	var lamp := OmniLight3D.new()
	lamp.position = lamp_pos
	lamp.light_color = Color(1.0, 0.85, 0.6)
	lamp.omni_range = 8.0
	root.add_child(lamp)
	_cylinder(root, 0.06, 0.06, 1.8, Vector3(1.35, 2.0, DOCK_START + 1.0), Color(0.2, 0.2, 0.22), false, 8)
	var bulb := MeshInstance3D.new()
	var bm := SphereMesh.new()
	bm.radius = 0.14
	bm.height = 0.28
	bulb.mesh = bm
	var bmat := StandardMaterial3D.new()
	bmat.emission_enabled = true
	bmat.emission = Color(1.0, 0.85, 0.6)
	bmat.emission_energy_multiplier = 2.0
	bulb.material_override = bmat
	bulb.position = lamp_pos
	root.add_child(bulb)


func _build_stall(root: Node3D, kind: String, pos: Vector3, roof_color: Color, title: String) -> void:
	var counter := Stall.new()
	counter.kind = kind
	counter.position = pos + Vector3(0, 0.55, 0)
	var mesh := BoxMesh.new()
	mesh.size = Vector3(3.2, 1.1, 1.2)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = _mat(Color(0.62, 0.45, 0.28))
	counter.add_child(mi)
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = mesh.size
	col.shape = shape
	counter.add_child(col)
	root.add_child(counter)

	for x in [-1.5, 1.5]:
		_box(root, Vector3(0.15, 2.8, 0.15), pos + Vector3(x, 1.4, -0.9), Color(0.45, 0.32, 0.2), false)
	_box(root, Vector3(3.8, 0.15, 2.4), pos + Vector3(0, 2.85, -0.4), roof_color, false, Vector3(deg_to_rad(-12), 0, 0))
	match kind:
		"market":
			_box(root, Vector3(1.0, 0.5, 0.7), pos + Vector3(0.8, 1.35, -0.1), Color(0.8, 0.92, 1.0), false)
		"store":
			for i in range(4):
				var jar := MeshInstance3D.new()
				var jm := CylinderMesh.new()
				jm.top_radius = 0.12
				jm.bottom_radius = 0.12
				jm.height = 0.3
				jar.mesh = jm
				jar.material_override = _mat(Color.from_hsv(i * 0.2, 0.6, 0.9), 0.3)
				jar.position = pos + Vector3(-1.1 + i * 0.6, 1.25, 0.0)
				root.add_child(jar)
		"ferry":
			# a life ring hanging on the stall
			var ring := MeshInstance3D.new()
			var tm := TorusMesh.new()
			tm.inner_radius = 0.22
			tm.outer_radius = 0.34
			ring.mesh = tm
			ring.material_override = _mat(Color(1.0, 0.35, 0.25))
			ring.rotation_degrees = Vector3(90, 0, 0)
			ring.position = pos + Vector3(0, 1.9, -0.82)
			root.add_child(ring)
	_label(root, title, pos + Vector3(0, 3.4, 0))


func _build_ferry_boat(root: Node3D, pos: Vector3) -> void:
	var boat: Node3D = preload("res://scripts/bob.gd").new()
	boat.position = pos
	root.add_child(boat)
	var hull := MeshInstance3D.new()
	var hm := BoxMesh.new()
	hm.size = Vector3(3.2, 1.0, 7.0)
	hull.mesh = hm
	hull.material_override = _mat(Color(0.9, 0.9, 0.92))
	hull.position.y = 0.3
	boat.add_child(hull)
	var stripe := MeshInstance3D.new()
	var sm := BoxMesh.new()
	sm.size = Vector3(3.25, 0.2, 7.05)
	stripe.mesh = sm
	stripe.material_override = _mat(Color(0.2, 0.4, 0.75))
	stripe.position.y = 0.55
	boat.add_child(stripe)
	var cabin := MeshInstance3D.new()
	var cm := BoxMesh.new()
	cm.size = Vector3(2.2, 1.4, 2.4)
	cabin.mesh = cm
	cabin.material_override = _mat(Color(0.95, 0.95, 0.85))
	cabin.position = Vector3(0, 1.5, -0.8)
	boat.add_child(cabin)
	var stack := MeshInstance3D.new()
	var stm := CylinderMesh.new()
	stm.top_radius = 0.25
	stm.bottom_radius = 0.3
	stm.height = 1.2
	stack.mesh = stm
	stack.material_override = _mat(Color(0.85, 0.25, 0.2))
	stack.position = Vector3(0, 2.6, -1.2)
	boat.add_child(stack)


func _palm(root: Node3D, base: Vector3, leaf_color: Color) -> void:
	var h := _rng.randf_range(4.5, 6.5)
	var lean := Vector3(_rng.randf_range(-0.15, 0.15), 0, _rng.randf_range(-0.15, 0.15))
	var trunk := _cylinder(root, 0.18, 0.28, h, base + Vector3(0, h * 0.5, 0), Color(0.5, 0.36, 0.22), true, 8)
	trunk.rotation = lean
	var top := base + Vector3(0, h, 0) + Vector3(lean.z, 0, -lean.x) * h * 0.5
	for i in range(6):
		var leaf := MeshInstance3D.new()
		var lm := BoxMesh.new()
		lm.size = Vector3(0.5, 0.05, 2.6)
		leaf.mesh = lm
		leaf.material_override = _mat(leaf_color)
		leaf.position = top
		leaf.rotation = Vector3(deg_to_rad(25), i * TAU / 6.0, 0)
		leaf.translate_object_local(Vector3(0, 0, 1.1))
		root.add_child(leaf)


func _pine(root: Node3D, base: Vector3) -> void:
	var h := _rng.randf_range(4.0, 6.0)
	_cylinder(root, 0.15, 0.22, h * 0.4, base + Vector3(0, h * 0.2, 0), Color(0.35, 0.25, 0.18), true, 8)
	for k in range(3):
		var cone := MeshInstance3D.new()
		var cm := CylinderMesh.new()
		cm.top_radius = 0.0
		cm.bottom_radius = 1.6 - k * 0.4
		cm.height = h * 0.45
		cm.radial_segments = 8
		cone.mesh = cm
		cone.material_override = _mat(Color(0.18, 0.32, 0.25))
		cone.position = base + Vector3(0, h * (0.45 + k * 0.2), 0)
		root.add_child(cone)


func _coral(root: Node3D, base: Vector3) -> void:
	var hue := _rng.randf_range(0.85, 1.05)
	var col := Color.from_hsv(fmod(hue, 1.0), 0.55, 0.95)
	for k in range(_rng.randi_range(3, 5)):
		var branch := MeshInstance3D.new()
		var cm := CylinderMesh.new()
		cm.top_radius = 0.08
		cm.bottom_radius = 0.18
		cm.height = _rng.randf_range(0.8, 1.8)
		cm.radial_segments = 6
		branch.mesh = cm
		branch.material_override = _mat(col, 0.6)
		branch.position = base + Vector3(_rng.randf_range(-0.4, 0.4), cm.height * 0.4, _rng.randf_range(-0.4, 0.4))
		branch.rotation = Vector3(_rng.randf_range(-0.5, 0.5), 0, _rng.randf_range(-0.5, 0.5))
		root.add_child(branch)


func _hut(root: Node3D, base: Vector3, wall: Color) -> void:
	_box(root, Vector3(4, 2.6, 4), base + Vector3(0, 1.3, 0), wall)
	var roof := MeshInstance3D.new()
	var rm := CylinderMesh.new()
	rm.top_radius = 0.0
	rm.bottom_radius = 3.4
	rm.height = 2.0
	rm.radial_segments = 4
	roof.mesh = rm
	roof.material_override = _mat(Color(0.75, 0.6, 0.35))
	roof.position = base + Vector3(0, 3.6, 0)
	roof.rotation.y = PI / 4.0
	root.add_child(roof)


func _lighthouse(root: Node3D, base: Vector3) -> void:
	for k in range(4):
		var col := Color(0.95, 0.95, 0.95) if k % 2 == 0 else Color(0.85, 0.2, 0.2)
		_cylinder(root, 1.3 - k * 0.15, 1.45 - k * 0.15, 2.5, base + Vector3(0, 1.25 + k * 2.5, 0), col, true, 16)
	var lamp := OmniLight3D.new()
	lamp.position = base + Vector3(0, 11.0, 0)
	lamp.light_color = Color(1.0, 0.9, 0.6)
	lamp.light_energy = 3.0
	lamp.omni_range = 25.0
	root.add_child(lamp)
	var glass := MeshInstance3D.new()
	var gm := SphereMesh.new()
	gm.radius = 0.8
	gm.height = 1.6
	glass.mesh = gm
	var gmat := StandardMaterial3D.new()
	gmat.emission_enabled = true
	gmat.emission = Color(1.0, 0.9, 0.6)
	gmat.emission_energy_multiplier = 3.0
	glass.material_override = gmat
	glass.position = lamp.position
	root.add_child(glass)
