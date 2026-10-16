class_name FishModel
extends RefCounted
## Builds low-poly creature meshes in code from the "model" entry of each creature in data.gd.
## Models are built along +X (head forward), 1 unit = 1 metre at size 1.0, centred on the origin.

const RINGS := 12
const SIDES := 10


static func build(id: String, size := 1.0, golden := false) -> Node3D:
	var c: Dictionary = Data.CREATURES[id]
	var m: Dictionary = c["model"]
	var length := float(c["length"])
	var col1: Color = c["color"]
	var col2: Color = m.get("color2", col1.darkened(0.3))
	if golden:
		col1 = Color(1.0, 0.78, 0.25)
		col2 = Color(1.0, 0.92, 0.55)
	var root := Node3D.new()
	root.name = "Fish_" + id
	match String(m.get("shape", "fish")):
		"eel":
			_eel(root, length, m, col1, col2)
		"ray":
			_ray(root, length, col1, col2)
		"crab":
			_crab(root, length, m, col1, col2)
		"squid":
			_squid(root, length, m, col1, col2)
		"boot":
			_boot(root, length, col1, col2)
		_:
			_fish(root, length, m, col1, col2)
	_apply_material(root, golden, String(m.get("extra", "")) == "glow")
	root.scale = Vector3.ONE * size
	return root


# ------------------------------------------------------------------ shapes

static func _fish(root: Node3D, length: float, m: Dictionary, col1: Color, col2: Color) -> void:
	var h := float(m.get("h", 0.28)) * length
	var w := float(m.get("w", 0.14)) * length
	var pattern := String(m.get("pattern", "belly"))
	var st := _begin()
	_lathe(st, length, h, w, pattern, col1, col2, 0.0)
	# tail fin
	var tx := -length * 0.5
	var fin_col := col1.darkened(0.15)
	if String(m.get("tail", "fork")) == "fork":
		_tri(st, Vector3(tx + length * 0.04, 0, 0), Vector3(tx - length * 0.26, h * 0.62, 0), Vector3(tx - length * 0.14, 0, 0), fin_col)
		_tri(st, Vector3(tx + length * 0.04, 0, 0), Vector3(tx - length * 0.14, 0, 0), Vector3(tx - length * 0.26, -h * 0.62, 0), fin_col)
	elif String(m.get("tail", "")) == "round":
		var prev := Vector3(tx - length * 0.05, h * 0.5, 0)
		for k in range(1, 7):
			var a := lerpf(PI * 0.5, -PI * 0.5, k / 6.0)
			var p := Vector3(tx - length * 0.05 - cos(a) * length * 0.16, sin(a) * h * 0.5, 0)
			_tri(st, Vector3(tx + length * 0.04, 0, 0), prev, p, fin_col)
			prev = p
	# dorsal fin and a pair of pectoral fins
	_tri(st, Vector3(-length * 0.12, h * 0.46, 0), Vector3(length * 0.14, h * 0.46, 0), Vector3(-length * 0.08, h * 0.85, 0), fin_col)
	for side in [-1.0, 1.0]:
		_tri(st, Vector3(length * 0.16, -h * 0.15, side * w * 0.45), Vector3(length * 0.02, -h * 0.25, side * w * 0.5),
			Vector3(length * 0.0, -h * 0.4, side * w * 1.1), fin_col)
	_finish(root, st)
	_eyes(root, Vector3(length * 0.34, h * 0.12, w * 0.36), length * 0.035)
	match String(m.get("extra", "")):
		"whiskers":
			for side in [-1.0, 1.0]:
				for k in range(2):
					_stick(root, Vector3(length * 0.46, -h * 0.05, side * w * 0.2),
						Vector3(length * 0.62, -h * (0.35 + 0.25 * k), side * w * (1.2 + k)), length * 0.008, col2.darkened(0.3))
		"crown":
			for k in range(5):
				var x := lerpf(-length * 0.08, length * 0.2, k / 4.0)
				_spike(root, Vector3(x, h * 0.48, 0), length * 0.12, length * 0.03, Color(1.0, 0.85, 0.2))
		"lure":
			var tip := Vector3(length * 0.7, h * 0.75, 0)
			_stick(root, Vector3(length * 0.3, h * 0.42, 0), tip, length * 0.01, col1)
			var bulb := _sphere(root, tip, length * 0.06, Color(0.6, 1.0, 0.9))
			var glow := StandardMaterial3D.new()
			glow.albedo_color = Color(0.6, 1.0, 0.9)
			glow.emission_enabled = true
			glow.emission = Color(0.4, 1.0, 0.85)
			glow.emission_energy_multiplier = 3.0
			bulb.material_override = glow
			bulb.set_meta("keep_material", true)
		"frown":
			_stick(root, Vector3(length * 0.42, -h * 0.02, w * 0.3), Vector3(length * 0.47, -h * 0.12, w * 0.12), length * 0.008, Color(0.1, 0.1, 0.1))


static func _eel(root: Node3D, length: float, m: Dictionary, col1: Color, col2: Color) -> void:
	var st := _begin()
	_lathe(st, length, float(m.get("h", 0.07)) * length, float(m.get("w", 0.06)) * length, String(m.get("pattern", "plain")), col1, col2, 0.06)
	# a ribbon fin running along the back
	var h := float(m.get("h", 0.07)) * length
	for k in range(8):
		var x0 := lerpf(-length * 0.5, length * 0.25, k / 8.0)
		var x1 := lerpf(-length * 0.5, length * 0.25, (k + 1) / 8.0)
		_tri(st, Vector3(x0, h * 0.45, _wiggle(x0, length)), Vector3(x1, h * 0.45, _wiggle(x1, length)), Vector3((x0 + x1) * 0.5, h * 1.1, _wiggle((x0 + x1) * 0.5, length)), col2)
	_finish(root, st)
	_eyes(root, Vector3(length * 0.44, h * 0.15, float(m.get("w", 0.06)) * length * 0.4 + _wiggle(length * 0.44, length)), length * 0.015)


static func _ray(root: Node3D, length: float, col1: Color, col2: Color) -> void:
	var st := _begin()
	var span := length * 0.55
	var nose := Vector3(length * 0.35, 0.0, 0)
	var back := Vector3(-length * 0.2, 0.0, 0)
	var top := Vector3(0.05 * length, length * 0.07, 0)
	var bottom := Vector3(0.05 * length, -length * 0.03, 0)
	var left := Vector3(0.0, 0.01, -span)
	var right := Vector3(0.0, 0.01, span)
	for wing in [left, right]:
		for pair in [[nose, wing], [wing, back]]:
			_tri(st, top, pair[0], pair[1], col1)
			_tri(st, bottom, pair[1], pair[0], col2)
	# spots
	for k in range(6):
		var a := k * TAU / 6.0
		var p := top + Vector3(cos(a) * length * 0.12, -0.005, sin(a) * span * 0.45)
		_tri(st, p + Vector3(0.02, 0.002, 0), p + Vector3(-0.02, 0.002, 0.025), p + Vector3(-0.02, 0.002, -0.025), col2)
	_finish(root, st)
	_stick(root, back, back + Vector3(-length * 0.55, 0, 0), length * 0.012, col1.darkened(0.2))
	_eyes(root, Vector3(length * 0.16, length * 0.06, length * 0.06), length * 0.02)


static func _crab(root: Node3D, length: float, m: Dictionary, col1: Color, col2: Color) -> void:
	var body := _sphere(root, Vector3.ZERO, length * 0.5, col1)
	body.scale = Vector3(0.8, 0.45, 1.0)
	for side in [-1.0, 1.0]:
		for k in range(3):
			var base := Vector3(lerpf(-0.18, 0.12, k / 2.0) * length, 0.0, side * length * 0.38)
			var knee := base + Vector3(0, length * 0.12, side * length * 0.18)
			_stick(root, base, knee, length * 0.025, col1.darkened(0.15))
			_stick(root, knee, knee + Vector3(-0.04 * length, -length * 0.25, side * length * 0.1), length * 0.022, col1.darkened(0.15))
		var arm := Vector3(length * 0.3, 0.0, side * length * 0.3)
		var claw := arm + Vector3(length * 0.25, length * 0.08, side * length * 0.12)
		_stick(root, arm, claw, length * 0.035, col1)
		var pincer := _sphere(root, claw + Vector3(length * 0.06, 0, 0), length * 0.11, col2)
		pincer.scale = Vector3(1.4, 0.8, 0.8)
	for side in [-1.0, 1.0]:
		_stick(root, Vector3(length * 0.25, length * 0.1, side * length * 0.08), Vector3(length * 0.3, length * 0.24, side * length * 0.1), length * 0.015, col1)
		_sphere(root, Vector3(length * 0.3, length * 0.26, side * length * 0.1), length * 0.035, Color(0.05, 0.05, 0.05))
	if String(m.get("extra", "")) == "snorkel":
		_stick(root, Vector3(0, length * 0.2, 0), Vector3(0, length * 0.55, 0), length * 0.03, Color(0.2, 0.75, 0.95))
		_stick(root, Vector3(0, length * 0.55, 0), Vector3(length * 0.1, length * 0.6, 0), length * 0.03, Color(0.2, 0.75, 0.95))


static func _squid(root: Node3D, length: float, m: Dictionary, col1: Color, col2: Color) -> void:
	var mantle := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.0
	cm.bottom_radius = length * 0.16
	cm.height = length * 0.55
	cm.radial_segments = 10
	mantle.mesh = cm
	mantle.rotation = Vector3(0, 0, -PI / 2.0)
	mantle.position = Vector3(-length * 0.12, 0, 0)
	_color_mesh(mantle, col1)
	root.add_child(mantle)
	var head := _sphere(root, Vector3(length * 0.2, 0, 0), length * 0.15, col1.lightened(0.1))
	head.scale = Vector3(0.9, 0.9, 0.9)
	_eyes(root, Vector3(length * 0.26, length * 0.05, length * 0.1), length * 0.04)
	for k in range(8):
		var a := k * TAU / 8.0
		var start := Vector3(length * 0.3, cos(a) * length * 0.07, sin(a) * length * 0.07)
		var mid := start + Vector3(length * 0.22, cos(a) * length * 0.07, sin(a) * length * 0.07)
		var tip := mid + Vector3(length * 0.2, cos(a + 0.6) * length * 0.05, sin(a + 0.6) * length * 0.05)
		_stick(root, start, mid, length * 0.022, col2)
		_stick(root, mid, tip, length * 0.014, col2)


static func _boot(root: Node3D, length: float, col1: Color, col2: Color) -> void:
	var shaft := _box(root, Vector3(-length * 0.15, length * 0.25, 0), Vector3(length * 0.3, length * 0.55, length * 0.3), col1)
	shaft.rotation.z = 0.08
	_box(root, Vector3(length * 0.12, 0.0, 0), Vector3(length * 0.75, length * 0.18, length * 0.32), col1)
	_box(root, Vector3(length * 0.1, -length * 0.1, 0), Vector3(length * 0.8, length * 0.05, length * 0.34), col2)
	# a sad strand of seaweed
	_stick(root, Vector3(-length * 0.15, length * 0.5, 0), Vector3(-length * 0.05, length * 0.75, length * 0.05), length * 0.02, Color(0.2, 0.55, 0.2))


# ------------------------------------------------------------------ mesh helpers

static func _begin() -> SurfaceTool:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	return st


static func _finish(root: Node3D, st: SurfaceTool) -> void:
	st.generate_normals()
	var mi := MeshInstance3D.new()
	mi.mesh = st.commit()
	root.add_child(mi)


static func _wiggle(x: float, length: float) -> float:
	return sin(x / length * TAU * 1.2) * length * 0.05


## Body of revolution along X with an elliptical cross-section. curl > 0 bends it sideways (eels).
static func _lathe(st: SurfaceTool, length: float, h: float, w: float, pattern: String, col1: Color, col2: Color, curl: float) -> void:
	var rings: Array = []
	for r in range(RINGS + 1):
		var u := float(r) / RINGS  # 0 = tail, 1 = nose
		var x := lerpf(-length * 0.5, length * 0.5, u)
		var prof := pow(sin(PI * lerpf(0.04, 0.96, u)), 0.7) * (0.55 + 0.45 * smoothstep(0.0, 0.7, u))
		var ring: Array = []
		for s in range(SIDES):
			var a := TAU * s / SIDES
			var y := cos(a) * h * 0.5 * prof
			var z := sin(a) * w * 0.5 * prof + (_wiggle(x, length) if curl > 0.0 else 0.0)
			var col := col1
			match pattern:
				"stripes":
					if sin(u * 5.0 * TAU) > 0.35:
						col = col2
				"spots":
					if fmod(absf(sin(r * 12.9898 + s * 78.233) * 43758.5453), 1.0) > 0.72:
						col = col2
				"belly":
					pass
			if cos(a) < -0.35 and pattern != "plain":
				col = col.lerp(Color(0.95, 0.95, 0.9), 0.55)
			ring.append([Vector3(x, y, z), col])
		rings.append(ring)
	for r in range(RINGS):
		for s in range(SIDES):
			var s2 := (s + 1) % SIDES
			var a: Array = rings[r][s]
			var b: Array = rings[r][s2]
			var cc: Array = rings[r + 1][s]
			var d: Array = rings[r + 1][s2]
			_tri_c(st, a, cc, b)
			_tri_c(st, b, cc, d)


static func _tri_c(st: SurfaceTool, a: Array, b: Array, c: Array) -> void:
	for v in [a, b, c]:
		st.set_color(v[1])
		st.add_vertex(v[0])


static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, col: Color) -> void:
	st.set_color(col)
	st.add_vertex(a)
	st.add_vertex(b)
	st.add_vertex(c)


static func _color_mesh(mi: MeshInstance3D, col: Color) -> void:
	mi.set_meta("tint", col)


static func _sphere(root: Node3D, pos: Vector3, radius: float, col: Color) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = radius
	sm.height = radius * 2.0
	sm.radial_segments = 10
	sm.rings = 6
	mi.mesh = sm
	mi.position = pos
	_color_mesh(mi, col)
	root.add_child(mi)
	return mi


static func _box(root: Node3D, pos: Vector3, size: Vector3, col: Color) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var bm := BoxMesh.new()
	bm.size = size
	mi.mesh = bm
	mi.position = pos
	_color_mesh(mi, col)
	root.add_child(mi)
	return mi


static func _stick(root: Node3D, from: Vector3, to: Vector3, radius: float, col: Color) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = radius
	cm.bottom_radius = radius
	cm.height = maxf(from.distance_to(to), 0.001)
	cm.radial_segments = 6
	mi.mesh = cm
	mi.position = (from + to) * 0.5
	var dir := (to - from).normalized()
	if absf(dir.dot(Vector3.UP)) < 0.999:
		mi.basis = Basis(Quaternion(Vector3.UP, dir))
	elif dir.y < 0.0:
		mi.basis = Basis(Vector3.RIGHT, PI)
	_color_mesh(mi, col)
	root.add_child(mi)
	return mi


static func _spike(root: Node3D, base: Vector3, height: float, radius: float, col: Color) -> void:
	var mi := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = 0.0
	cm.bottom_radius = radius
	cm.height = height
	cm.radial_segments = 6
	mi.mesh = cm
	mi.position = base + Vector3(0, height * 0.5, 0)
	_color_mesh(mi, col)
	root.add_child(mi)


static func _eyes(root: Node3D, pos: Vector3, radius: float) -> void:
	for side in [-1.0, 1.0]:
		var p := Vector3(pos.x, pos.y, pos.z * side)
		_sphere(root, p, radius, Color(0.97, 0.97, 0.95))
		_sphere(root, p + Vector3(radius * 0.35, 0, side * radius * 0.6), radius * 0.6, Color(0.03, 0.03, 0.05))


static func _apply_material(root: Node3D, golden: bool, glow: bool) -> void:
	for child in root.get_children():
		var mi := child as MeshInstance3D
		if mi == null or mi.has_meta("keep_material"):
			continue
		var mat := StandardMaterial3D.new()
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		mat.roughness = 0.35 if golden else 0.55
		mat.metallic = 0.75 if golden else 0.05
		if mi.has_meta("tint"):
			mat.albedo_color = mi.get_meta("tint")
		else:
			mat.vertex_color_use_as_albedo = true
		if glow:
			mat.emission_enabled = true
			mat.emission = (mat.albedo_color if mi.has_meta("tint") else Color(0.3, 0.9, 0.85))
			mat.emission_energy_multiplier = 0.35
		if golden:
			mat.emission_enabled = true
			mat.emission = Color(1.0, 0.7, 0.2)
			mat.emission_energy_multiplier = 0.25
		mi.material_override = mat
